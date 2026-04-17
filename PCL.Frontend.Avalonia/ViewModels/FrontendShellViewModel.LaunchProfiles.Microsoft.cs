using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private MinecraftLaunchMicrosoftDeviceCodePromptPlan? _launchMicrosoftPromptPlan;

    public string LaunchMicrosoftStatusText
    {
        get => _launchMicrosoftStatusText;
        private set => SetProperty(ref _launchMicrosoftStatusText, value);
    }

    public string LaunchMicrosoftPrimaryButtonText => _launchMicrosoftPromptPlan is null
        ? T("launch.profile.microsoft.actions.start")
        : T("launch.profile.microsoft.actions.continue_after_auth");

    public string LaunchMicrosoftDeviceCode
    {
        get => _launchMicrosoftDeviceCode;
        private set => SetProperty(ref _launchMicrosoftDeviceCode, value);
    }

    public bool HasLaunchMicrosoftDeviceCode => !string.IsNullOrWhiteSpace(LaunchMicrosoftDeviceCode);

    public string LaunchMicrosoftVerificationUrl
    {
        get => _launchMicrosoftVerificationUrl;
        private set => SetProperty(ref _launchMicrosoftVerificationUrl, value);
    }

    public bool HasLaunchMicrosoftVerificationUrl => !string.IsNullOrWhiteSpace(LaunchMicrosoftVerificationUrl);

    private Task LoginMicrosoftLaunchProfileAsync()
    {
        ResetMicrosoftDeviceFlow();
        LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.initial");
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.MicrosoftEditor);
        return Task.CompletedTask;
    }

    private async Task SubmitMicrosoftLaunchProfileAsync()
    {
        var microsoftClientId = ResolveMicrosoftClientId();
        if (_launchMicrosoftPromptPlan is null)
        {
            await BeginMicrosoftDeviceLoginAsync(microsoftClientId);
            return;
        }

        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.microsoft_login")))
        {
            return;
        }

        try
        {
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.waiting_for_auth");
            var oauthTokens = await PollMicrosoftOAuthTokensAsync(_launchMicrosoftPromptPlan, microsoftClientId);
            var xboxLiveResponseJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(oauthTokens.AccessToken));

            string xstsResponseJson;
            try
            {
                xstsResponseJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(
                        MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(xboxLiveResponseJson)));
            }
            catch (LaunchProfileRequestException ex)
            {
                var decisionPrompt = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(ex.ResponseBody);
                if (decisionPrompt is not null)
                {
                    LaunchMicrosoftStatusText = T(decisionPrompt.MessageText);
                    if (decisionPrompt.Options.FirstOrDefault()?.Decision == MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort &&
                        !string.IsNullOrWhiteSpace(decisionPrompt.Options[0].Url))
                    {
                        OpenExternalTarget(decisionPrompt.Options[0].Url, T("launch.profile.microsoft.messages.opened_related_web"));
                    }

                    ResetMicrosoftDeviceFlow(keepStatus: true);
                    return;
                }

                throw;
            }

            var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(xstsResponseJson);
            var minecraftAccessTokenJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(
                    xstsResponse.UserHash,
                    xstsResponse.Token));
            var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(minecraftAccessTokenJson);
            var ownershipJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(minecraftAccessToken));
            if (!MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(ownershipJson))
            {
                LaunchMicrosoftStatusText = T(MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt().MessageText);
                ResetMicrosoftDeviceFlow(keepStatus: true);
                return;
            }

            string profileJson;
            try
            {
                profileJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(minecraftAccessToken));
            }
            catch (LaunchProfileRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
            {
                var prompt = MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt();
                LaunchMicrosoftStatusText = T(prompt.MessageText);
                if (prompt.Options.FirstOrDefault()?.Decision == MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort &&
                    !string.IsNullOrWhiteSpace(prompt.Options[0].Url))
                {
                    OpenExternalTarget(prompt.Options[0].Url, T("launch.profile.microsoft.messages.opened_profile_create_page"));
                }

                ResetMicrosoftDeviceFlow(keepStatus: true);
                return;
            }

            var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(profileJson);
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
                new MinecraftLaunchMicrosoftProfileMutationRequest(
                    IsCreatingProfile: true,
                    SelectedProfileIndex: GetSelectedProfileIndexOrNull(profileDocument),
                    profileDocument.Profiles.Select(ToStoredProfile).ToArray(),
                    profileResponse.Uuid,
                    profileResponse.UserName,
                    minecraftAccessToken,
                    oauthTokens.RefreshToken,
                    profileResponse.ProfileJson));

            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));
            ResetMicrosoftDeviceFlow();
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.microsoft_login"), T("launch.profile.microsoft.completed", ("user_name", profileResponse.UserName)));
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            LaunchMicrosoftStatusText = message;
            AddFailureActivity(T("launch.profile.activities.microsoft_login_failed"), message);
            if (ex is TimeoutException || message.Contains(T("launch.profile.refresh.errors.expired_keyword"), StringComparison.Ordinal))
            {
                ResetMicrosoftDeviceFlow(keepStatus: true);
            }
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task BeginMicrosoftDeviceLoginAsync(string microsoftClientId)
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.microsoft_login")))
        {
            return;
        }

        try
        {
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.requesting_device_code");
            var deviceCodeJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest(microsoftClientId));
            var promptPlan = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(deviceCodeJson);
            _launchMicrosoftPromptPlan = promptPlan;
            LaunchMicrosoftDeviceCode = promptPlan.UserCode;
            LaunchMicrosoftVerificationUrl = promptPlan.OpenBrowserUrl;
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.browser_opened");
            TryCopyLaunchProfileText(promptPlan.UserCode);
            OpenExternalTarget(promptPlan.OpenBrowserUrl, T("launch.profile.microsoft.messages.opened_sign_in_page"));
            RaiseLaunchProfileSurfaceProperties();
        }
        catch (Exception ex)
        {
            LaunchMicrosoftStatusText = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity(T("launch.profile.activities.microsoft_login_failed"), LaunchMicrosoftStatusText);
            ResetMicrosoftDeviceFlow(keepStatus: true);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private void OpenMicrosoftDeviceLink()
    {
        if (!string.IsNullOrWhiteSpace(LaunchMicrosoftVerificationUrl))
        {
            OpenExternalTarget(LaunchMicrosoftVerificationUrl, T("launch.profile.microsoft.messages.opened_sign_in_page"));
        }
    }

    private async Task<LaunchProfileRefreshResult> RefreshMicrosoftLaunchProfileAsync(
        MinecraftLaunchProfileDocument profileDocument,
        int selectedProfileIndex,
        MinecraftLaunchPersistedProfile selectedProfile,
        CancellationToken cancellationToken,
        bool forceRefresh,
        Action<string>? onStatusChanged)
    {
        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken))
        {
            try
            {
                onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.validate_session"));
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(selectedProfile.AccessToken),
                    cancellationToken);
                return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.microsoft.valid", ("profile_name", selectedProfile.Username ?? T("launch.profile.entry.unnamed"))));
            }
            catch (LaunchProfileRequestException)
            {
                // Follow HMCL's flow: validate first, then refresh when the current token is no longer accepted.
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.RefreshToken))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.microsoft.errors.missing_refresh_token"));
        }

        var clientId = ResolveMicrosoftClientId();
        onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.refresh_account"));

        string oauthRefreshJson;
        try
        {
            oauthRefreshJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildOAuthRefreshRequest(selectedProfile.RefreshToken, clientId),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex)
        {
            var resolution = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveOAuthRefreshFailure(GetLaunchProfileFriendlyError(ex));
            if (resolution.Kind == MinecraftLaunchMicrosoftFailureResolutionKind.RequireRelogin)
            {
                throw new InvalidOperationException(T("launch.profile.refresh.microsoft.errors.relogin_required"));
            }

            throw new InvalidOperationException(T("launch.profile.refresh.microsoft.errors.failed", ("message", GetLaunchProfileFriendlyError(ex))));
        }

        var oauthTokens = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(oauthRefreshJson);

        onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.get_xbox_live_token"));
        var xboxLiveResponseJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(oauthTokens.AccessToken),
            cancellationToken);

        string xstsResponseJson;
        try
        {
            onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.get_xsts_token"));
            xstsResponseJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(
                    MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(xboxLiveResponseJson)),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex)
        {
            var prompt = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(ex.ResponseBody);
            if (prompt is not null)
            {
                throw new InvalidOperationException(prompt.Message);
            }

            throw new InvalidOperationException(T("launch.profile.refresh.microsoft.errors.failed", ("message", GetLaunchProfileFriendlyError(ex))));
        }

        var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(xstsResponseJson);

        onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.get_minecraft_access_token"));
        var minecraftAccessTokenJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(
                xstsResponse.UserHash,
                xstsResponse.Token),
            cancellationToken);
        var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(minecraftAccessTokenJson);

        onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.check_ownership"));
        var ownershipJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(minecraftAccessToken),
            cancellationToken);
        if (!MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(ownershipJson))
        {
            throw new InvalidOperationException(MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt().Message);
        }

        string profileJson;
        try
        {
            onStatusChanged?.Invoke(T("launch.profile.refresh.microsoft.sync_profile"));
            profileJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(minecraftAccessToken),
                cancellationToken);
        }
        catch (LaunchProfileRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt().Message);
        }

        var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(profileJson);
        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: false,
                SelectedProfileIndex: selectedProfileIndex,
                profileDocument.Profiles.Select(ToStoredProfile).ToArray(),
                profileResponse.Uuid,
                profileResponse.UserName,
                minecraftAccessToken,
                oauthTokens.RefreshToken,
                profileResponse.ProfileJson));

        FrontendProfileStorageService.Save(
            _shellActionService.RuntimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));

        return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.microsoft.completed", ("profile_name", profileResponse.UserName)), ShouldInvalidateAvatarCache: true);
    }

    private void ResetMicrosoftDeviceFlow(bool keepStatus = false)
    {
        _launchMicrosoftPromptPlan = null;
        LaunchMicrosoftDeviceCode = string.Empty;
        LaunchMicrosoftVerificationUrl = string.Empty;
        if (!keepStatus)
        {
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.initial");
        }

        RaiseLaunchProfileSurfaceProperties();
    }

    private async Task<MinecraftLaunchOAuthRefreshResponse> PollMicrosoftOAuthTokensAsync(
        MinecraftLaunchMicrosoftDeviceCodePromptPlan promptPlan,
        string clientId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(promptPlan.ExpiresInSeconds, 1));
        var pollDelay = Math.Max(promptPlan.PollIntervalSeconds, 1);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var responseJson = await SendLaunchProfileRequestAsync(
                    BuildMicrosoftDeviceTokenPollRequest(promptPlan.DeviceCode, clientId));
                return MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(responseJson);
            }
            catch (LaunchProfileRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.BadRequest)
            {
                var errorCode = TryReadJsonField(ex.ResponseBody, "error");
                if (string.Equals(errorCode, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(pollDelay));
                    continue;
                }

                if (string.Equals(errorCode, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    pollDelay += 5;
                    await Task.Delay(TimeSpan.FromSeconds(pollDelay));
                    continue;
                }

                if (string.Equals(errorCode, "authorization_declined", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(T("launch.profile.microsoft.errors.authorization_declined"));
                }

                if (string.Equals(errorCode, "expired_token", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(T("launch.profile.microsoft.errors.device_code_expired"));
                }

                throw new InvalidOperationException(GetLaunchProfileFriendlyError(ex));
            }
        }

        throw new TimeoutException(T("launch.profile.microsoft.errors.authorization_timeout"));
    }

    private static MinecraftLaunchHttpRequestPlan BuildMicrosoftDeviceTokenPollRequest(string deviceCode, string clientId)
    {
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            MinecraftLaunchMicrosoftDeviceCodePromptService.DefaultPollUrl,
            "application/x-www-form-urlencoded",
            string.Join(
                "&",
                new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId,
                    ["device_code"] = deviceCode,
                    ["scope"] = "XboxLive.signin offline_access"
                }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
    }

    private string ResolveMicrosoftClientId()
    {
        var clientId = FrontendEmbeddedSecrets.GetMicrosoftClientId();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException(T("launch.profile.microsoft.errors.missing_client_id"), "MS_CLIENT_ID");
        }

        return clientId;
    }

    private void TryCopyLaunchProfileText(string text)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _shellActionService.SetClipboardTextAsync(text);
            }
            catch
            {
                // Ignore clipboard failures during login flows.
            }
        });
    }
}
