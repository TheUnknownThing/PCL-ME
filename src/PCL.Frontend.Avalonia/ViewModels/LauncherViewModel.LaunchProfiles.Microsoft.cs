using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.UI.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private CancellationTokenSource? _microsoftLoginCancellation;
    private bool _isMicrosoftWaitingForAuthorization;
    private bool _microsoftCodeCopied;

    internal Action<string>? MicrosoftLoginBrowserOpener { get; set; }

    public string LaunchMicrosoftStatusText
    {
        get => _launchMicrosoftStatusText;
        private set => SetProperty(ref _launchMicrosoftStatusText, value);
    }

    public string LaunchMicrosoftPrimaryButtonText => T("launch.profile.microsoft.actions.start");

    public bool IsLaunchMicrosoftLoginInProgress => _microsoftLoginCancellation is not null;

    public bool CanStartLaunchMicrosoftLogin => !IsLaunchMicrosoftLoginInProgress;

    public bool ShowLaunchMicrosoftDeviceCode => HasLaunchMicrosoftDeviceCode && _isMicrosoftWaitingForAuthorization;

    public bool ShowLaunchMicrosoftStatus => !ShowLaunchMicrosoftDeviceCode;

    public bool ShowLaunchMicrosoftProgress => IsLaunchMicrosoftLoginInProgress && !ShowLaunchMicrosoftDeviceCode;

    public bool ShowLaunchMicrosoftRetry => ShowLaunchMicrosoftEditor && CanStartLaunchMicrosoftLogin;

    public string LaunchMicrosoftCopyCodeText => T(_microsoftCodeCopied
        ? "launch.profile.microsoft.actions.code_copied"
        : "launch.profile.microsoft.actions.copy_code");

    public string LaunchMicrosoftCodeHint => T(_microsoftCodeCopied
        ? "launch.profile.microsoft.code_copied_hint"
        : "launch.profile.microsoft.copy_code_hint");

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
        ClearLaunchProfileEdit();
        ResetMicrosoftDeviceFlow();
        LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.initial");
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.MicrosoftEditor);
        // Let the popup render and the selected account's edit context settle before starting I/O.
        Dispatcher.UIThread.Post(() =>
        {
            if (ShowLaunchMicrosoftEditor && !_isLaunchProfileActionInProgress)
                _ = SubmitMicrosoftLaunchProfileAsync();
        }, DispatcherPriority.Loaded);
        return Task.CompletedTask;
    }

    private async Task SubmitMicrosoftLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.microsoft_login")))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _microsoftLoginCancellation = cancellation;
        var cancellationToken = cancellation.Token;
        RaiseMicrosoftLoginProperties();
        NotifyLaunchProfileCommandsChanged();

        try
        {
            var microsoftClientId = ResolveMicrosoftClientId();
            var promptPlan = await BeginMicrosoftDeviceLoginAsync(microsoftClientId, cancellationToken);
            var oauthTokens = await PollMicrosoftOAuthTokensAsync(promptPlan, microsoftClientId, cancellationToken);
            _isMicrosoftWaitingForAuthorization = false;
            RaiseMicrosoftLoginProperties();
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.signing_in");
            var xboxLiveResponseJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(oauthTokens.AccessToken),
                cancellationToken);

            string xstsResponseJson;
            try
            {
                xstsResponseJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(
                        MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(xboxLiveResponseJson)), cancellationToken);
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
                    xstsResponse.Token), cancellationToken);
            var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(minecraftAccessTokenJson);
            var ownershipJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(minecraftAccessToken), cancellationToken);
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
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(minecraftAccessToken), cancellationToken);
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

            cancellationToken.ThrowIfCancellationRequested();
            var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(profileJson);
            var profileDocument = FrontendProfileStorageService.Load(_launcherActionService.RuntimePaths).Document;
            var editIndex = GetLaunchProfileEditIndex(profileDocument);
            if (editIndex.HasValue && !string.Equals(profileResponse.Uuid, _editingLaunchProfile?.Uuid, StringComparison.OrdinalIgnoreCase))
            {
                LaunchMicrosoftStatusText = T("launch.profile.edit.microsoft_account_changed");
                ResetMicrosoftDeviceFlow(keepStatus: true);
                return;
            }

            var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
                new MinecraftLaunchMicrosoftProfileMutationRequest(
                    IsCreatingProfile: !editIndex.HasValue,
                    SelectedProfileIndex: editIndex ?? GetSelectedProfileIndexOrNull(profileDocument),
                    profileDocument.Profiles.Select(ToStoredProfile).ToArray(),
                    profileResponse.Uuid,
                    profileResponse.UserName,
                    minecraftAccessToken,
                    oauthTokens.RefreshToken,
                    profileResponse.ProfileJson));

            FrontendProfileStorageService.Save(
                _launcherActionService.RuntimePaths,
                FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));
            if (editIndex.HasValue)
            {
                InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
            }
            ClearLaunchProfileEdit();
            ResetMicrosoftDeviceFlow();
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.microsoft_login"), T("launch.profile.microsoft.completed", ("user_name", profileResponse.UserName)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Closing the popup cancels both polling and the remaining token exchange.
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            LaunchMicrosoftStatusText = message;
            AddFailureActivity(T("launch.profile.activities.microsoft_login_failed"), message);
            ResetMicrosoftDeviceFlow(keepStatus: true);
        }
        finally
        {
            _microsoftLoginCancellation = null;
            _isMicrosoftWaitingForAuthorization = false;
            RaiseMicrosoftLoginProperties();
            EndLaunchProfileAction();
        }
    }

    private async Task<MinecraftLaunchMicrosoftDeviceCodePromptPlan> BeginMicrosoftDeviceLoginAsync(
        string microsoftClientId,
        CancellationToken cancellationToken)
    {
        LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.requesting_device_code");
        var deviceCodeJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest(microsoftClientId), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var promptPlan = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(deviceCodeJson);
        LaunchMicrosoftDeviceCode = promptPlan.UserCode;
        LaunchMicrosoftVerificationUrl = promptPlan.OpenBrowserUrl;
        _isMicrosoftWaitingForAuthorization = true;
        RaiseLaunchProfileSurfaceProperties();
        RaiseMicrosoftLoginProperties();
        NotifyLaunchProfileCommandsChanged();
        await CopyMicrosoftDeviceCodeAsync();
        cancellationToken.ThrowIfCancellationRequested();
        LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.waiting_for_auth");
        OpenMicrosoftDeviceLink();
        return promptPlan;
    }

    private void OpenMicrosoftDeviceLink()
    {
        if (!string.IsNullOrWhiteSpace(LaunchMicrosoftVerificationUrl))
        {
            if (MicrosoftLoginBrowserOpener is not null)
            {
                MicrosoftLoginBrowserOpener(LaunchMicrosoftVerificationUrl);
                return;
            }

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
            _launcherActionService.RuntimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, mutationPlan, out _));

        return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.microsoft.completed", ("profile_name", profileResponse.UserName)), ShouldInvalidateAvatarCache: true);
    }

    private void ResetMicrosoftDeviceFlow(bool keepStatus = false)
    {
        _isMicrosoftWaitingForAuthorization = false;
        _microsoftCodeCopied = false;
        LaunchMicrosoftDeviceCode = string.Empty;
        LaunchMicrosoftVerificationUrl = string.Empty;
        if (!keepStatus)
        {
            LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.initial");
        }

        RaiseLaunchProfileSurfaceProperties();
        RaiseMicrosoftLoginProperties();
        NotifyLaunchProfileCommandsChanged();
    }

    private async Task<MinecraftLaunchOAuthRefreshResponse> PollMicrosoftOAuthTokensAsync(
        MinecraftLaunchMicrosoftDeviceCodePromptPlan promptPlan,
        string clientId,
        CancellationToken cancellationToken)
    {
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        expiry.CancelAfter(TimeSpan.FromSeconds(Math.Max(promptPlan.ExpiresInSeconds, 1)));
        try
        {
            return await PollMicrosoftOAuthTokensCoreAsync(promptPlan, clientId, expiry.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(T("launch.profile.microsoft.errors.authorization_timeout"));
        }
    }

    private async Task<MinecraftLaunchOAuthRefreshResponse> PollMicrosoftOAuthTokensCoreAsync(
        MinecraftLaunchMicrosoftDeviceCodePromptPlan promptPlan,
        string clientId,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(promptPlan.ExpiresInSeconds, 1));
        var pollDelay = Math.Max(promptPlan.PollIntervalSeconds, 1);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var responseJson = await SendLaunchProfileRequestAsync(
                    BuildMicrosoftDeviceTokenPollRequest(promptPlan.DeviceCode, clientId), cancellationToken);
                return MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(responseJson);
            }
            catch (LaunchProfileRequestException ex) when (ex.StatusCode == (int)HttpStatusCode.BadRequest)
            {
                var errorCode = TryReadJsonField(ex.ResponseBody, "error");
                if (string.Equals(errorCode, "authorization_pending", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(pollDelay, Math.Max(0, (deadline - DateTimeOffset.UtcNow).TotalSeconds))), cancellationToken);
                    continue;
                }

                if (string.Equals(errorCode, "slow_down", StringComparison.OrdinalIgnoreCase))
                {
                    pollDelay += 5;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(pollDelay, Math.Max(0, (deadline - DateTimeOffset.UtcNow).TotalSeconds))), cancellationToken);
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

    private async Task CopyMicrosoftDeviceCodeAsync()
    {
        var code = LaunchMicrosoftDeviceCode;
        if (string.IsNullOrWhiteSpace(code)) return;

        var copied = false;
        try
        {
            // Clipboard APIs belong to the UI thread. Finish copying before opening the browser.
            await _launcherActionService.SetClipboardTextAsync(code);
            copied = true;
        }
        catch
        {
            // The code remains selectable and the copy button allows another attempt.
        }

        if (code == LaunchMicrosoftDeviceCode)
        {
            _microsoftCodeCopied = copied;
            RaiseMicrosoftLoginProperties();
        }
    }

    private void RaiseMicrosoftLoginProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchMicrosoftLoginInProgress));
        RaisePropertyChanged(nameof(CanStartLaunchMicrosoftLogin));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftDeviceCode));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftStatus));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftProgress));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftRetry));
        RaisePropertyChanged(nameof(LaunchMicrosoftCopyCodeText));
        RaisePropertyChanged(nameof(LaunchMicrosoftCodeHint));
    }
}
