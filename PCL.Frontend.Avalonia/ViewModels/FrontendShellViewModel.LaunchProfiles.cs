using System.Collections.ObjectModel;
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
    private const string DefaultAuthlibServer = "https://littleskin.cn/api/yggdrasil";
    private static readonly Regex OfflineUserNamePattern = new("^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);
    private static readonly Regex OfflineUuidPattern = new("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    private MinecraftLaunchMicrosoftDeviceCodePromptPlan? _launchMicrosoftPromptPlan;

    public IReadOnlyList<string> LaunchOfflineUuidModeOptions =>
    [
        T("launch.profile.offline.uuid_modes.standard"),
        T("launch.profile.offline.uuid_modes.legacy"),
        T("launch.profile.offline.uuid_modes.custom")
    ];

    public bool ShowLaunchProfileSummaryCard => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Summary;

    public bool ShowLaunchProfileChooser => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Chooser;

    public bool ShowLaunchProfileSelection => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Selection;

    public bool ShowLaunchOfflineEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.OfflineEditor;

    public bool ShowLaunchMicrosoftEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.MicrosoftEditor;

    public bool ShowLaunchAuthlibEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.AuthlibEditor;

    public bool CanRefreshLaunchProfile => _launchComposition.SelectedProfile.Kind is MinecraftLaunchProfileKind.Microsoft or MinecraftLaunchProfileKind.Auth;

    public bool IsLaunchProfileRefreshInProgress => _isLaunchProfileRefreshInProgress;

    public bool ShowLaunchProfileBackButton => GetEffectiveLaunchProfileSurface() switch
    {
        LaunchProfileSurfaceKind.Summary => false,
        LaunchProfileSurfaceKind.Chooser => HasSelectedLaunchProfile,
        LaunchProfileSurfaceKind.Selection => HasSelectedLaunchProfile,
        _ => true
    };

    public bool HasLaunchProfileEntries => LaunchProfileEntries.Count > 0;

    public bool HasNoLaunchProfileEntries => !HasLaunchProfileEntries;

    public string LaunchProfileSelectionHint => HasLaunchProfileEntries
        ? T("launch.profile.selection.hint")
        : T("launch.profile.selection.hint_empty");

    public string LaunchOfflineUserName
    {
        get => _launchOfflineUserName;
        set => SetProperty(ref _launchOfflineUserName, value);
    }

    public int SelectedLaunchOfflineUuidModeIndex
    {
        get => _selectedLaunchOfflineUuidModeIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, LaunchOfflineUuidModeOptions.Count, out var clampedValue))
            {
                return;
            }

            if (SetProperty(ref _selectedLaunchOfflineUuidModeIndex, clampedValue))
            {
                RaisePropertyChanged(nameof(IsLaunchOfflineCustomUuidVisible));
            }
        }
    }

    public bool IsLaunchOfflineCustomUuidVisible => SelectedLaunchOfflineUuidModeIndex == 2;

    public string LaunchOfflineCustomUuid
    {
        get => _launchOfflineCustomUuid;
        set => SetProperty(ref _launchOfflineCustomUuid, value);
    }

    public string LaunchOfflineStatusText
    {
        get => _launchOfflineStatusText;
        private set => SetProperty(ref _launchOfflineStatusText, value);
    }

    public bool HasLaunchOfflineStatus => !string.IsNullOrWhiteSpace(LaunchOfflineStatusText);

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

    public string LaunchAuthlibServer
    {
        get => _launchAuthlibServer;
        set => SetProperty(ref _launchAuthlibServer, value);
    }

    public string LaunchAuthlibLoginName
    {
        get => _launchAuthlibLoginName;
        set => SetProperty(ref _launchAuthlibLoginName, value);
    }

    public string LaunchAuthlibPassword
    {
        get => _launchAuthlibPassword;
        set => SetProperty(ref _launchAuthlibPassword, value);
    }

    public string LaunchAuthlibStatusText
    {
        get => _launchAuthlibStatusText;
        private set => SetProperty(ref _launchAuthlibStatusText, value);
    }

    public bool HasLaunchAuthlibStatus => !string.IsNullOrWhiteSpace(LaunchAuthlibStatusText);

    private Task SelectLaunchProfileAsync()
    {
        RefreshLaunchProfileEntries();
        if (!HasLaunchProfileEntries)
        {
            AddActivity(T("launch.profile.activities.switch"), T("launch.profile.selection.empty"));
            SetLaunchProfileSurface(LaunchProfileSurfaceKind.Selection);
            return Task.CompletedTask;
        }

        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Selection);
        return Task.CompletedTask;
    }

    private Task AddLaunchProfileAsync()
    {
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Chooser);
        return Task.CompletedTask;
    }

    private Task CreateOfflineLaunchProfileAsync()
    {
        LaunchOfflineUserName = HasSelectedLaunchProfile && !string.Equals(LaunchUserName, T("launch.profile.none_selected"), StringComparison.Ordinal)
            ? LaunchUserName
            : string.Empty;
        SelectedLaunchOfflineUuidModeIndex = 0;
        LaunchOfflineCustomUuid = string.Empty;
        LaunchOfflineStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.OfflineEditor);
        return Task.CompletedTask;
    }

    private Task LoginMicrosoftLaunchProfileAsync()
    {
        ResetMicrosoftDeviceFlow();
        LaunchMicrosoftStatusText = T("launch.profile.microsoft.status.initial");
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.MicrosoftEditor);
        return Task.CompletedTask;
    }

    private Task LoginAuthlibLaunchProfileAsync()
    {
        PopulateAuthlibDefaults();
        LaunchAuthlibStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.AuthlibEditor);
        return Task.CompletedTask;
    }

    private async Task RefreshSelectedLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.refresh")))
        {
            return;
        }

        try
        {
            _isLaunchProfileRefreshInProgress = true;
            RefreshLaunchProfileEntries();
            RaiseLaunchProfileSurfaceProperties();
            NotifyLaunchProfileCommandsChanged();
            var result = await RefreshSelectedLaunchProfileCoreAsync(
                CancellationToken.None,
                forceRefresh: true);
            if (!result.WasChecked)
            {
                AddActivity(T("launch.profile.activities.refresh"), result.Message);
                return;
            }

            if (result.ShouldInvalidateAvatarCache)
            {
                InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
            }

            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.refresh"), result.Message);
            AvaloniaHintBus.Show(result.Message, AvaloniaHintTheme.Success);
        }
        catch (OperationCanceledException)
        {
            AddActivity(T("launch.profile.activities.refresh"), T("launch.profile.refresh.canceled"));
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity(T("launch.profile.activities.refresh_failed"), message);
            AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error);
        }
        finally
        {
            _isLaunchProfileRefreshInProgress = false;
            RefreshLaunchProfileEntries();
            RaiseLaunchProfileSurfaceProperties();
            NotifyLaunchProfileCommandsChanged();
            EndLaunchProfileAction();
        }
    }

    private void BackLaunchProfileSurface()
    {
        if (_launchProfileSurface == LaunchProfileSurfaceKind.MicrosoftEditor)
        {
            ResetMicrosoftDeviceFlow();
        }

        LaunchOfflineStatusText = string.Empty;
        LaunchAuthlibStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Auto);
    }

    private async Task SubmitOfflineLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.create_offline")))
        {
            return;
        }

        try
        {
            var userName = LaunchOfflineUserName.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                LaunchOfflineStatusText = T("launch.profile.offline.errors.empty_user_name");
                return;
            }

            var uuid = ResolveOfflineUuid(userName);
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var nextDocument = FrontendProfileStorageService.CreateOfflineProfile(profileDocument, userName, uuid);
            FrontendProfileStorageService.Save(_shellActionService.RuntimePaths, nextDocument);
            LaunchOfflineStatusText = string.Empty;
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.create_offline"), T("launch.profile.offline.completed", ("user_name", userName)));
        }
        catch (Exception ex)
        {
            LaunchOfflineStatusText = ex.Message.Trim().TrimStart('$');
            AddFailureActivity(T("launch.profile.activities.create_offline_failed"), LaunchOfflineStatusText);
        }
        finally
        {
            EndLaunchProfileAction();
        }
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

    private async Task SubmitAuthlibLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.authlib_login")))
        {
            return;
        }

        try
        {
            var serverBaseUrl = NormalizeAuthlibServerBaseUrl(LaunchAuthlibServer);
            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.invalid_server");
                return;
            }

            var loginName = LaunchAuthlibLoginName.Trim();
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(LaunchAuthlibPassword))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.required_fields");
                return;
            }

            var authenticateJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                    serverBaseUrl,
                    loginName,
                    LaunchAuthlibPassword));
            var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
                new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                    ForceReselectProfile: false,
                    CachedProfileId: null,
                    AuthenticateResponseJson: authenticateJson));
            if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
            {
                LaunchAuthlibStatusText = (authenticatePlan.FailureMessage ?? T("launch.profile.authlib.errors.login_failed")).TrimStart('$');
                return;
            }

            var selectedProfileId = authenticatePlan.SelectedProfileId
                                    ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
            if (string.IsNullOrWhiteSpace(selectedProfileId))
            {
                LaunchAuthlibStatusText = T("launch.profile.authlib.errors.no_roles");
                return;
            }

            var metadataJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl);
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
                new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                    authenticatePlan,
                    metadataJson,
                    IsExistingProfile: false,
                    SelectedProfileIndex: GetSelectedProfileIndex(profileDocument),
                    serverBaseUrl,
                    loginName,
                    LaunchAuthlibPassword,
                    selectedProfileId));
            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.ApplyMutation(profileDocument, authenticateResult.MutationPlan, out _));
            LaunchAuthlibStatusText = string.Empty;
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.authlib_login"), T("launch.profile.authlib.completed", ("profile_name", authenticateResult.Session.ProfileName)));
        }
        catch (Exception ex)
        {
            LaunchAuthlibStatusText = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity(T("launch.profile.activities.authlib_login_failed"), LaunchAuthlibStatusText);
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

    private void ApplyLittleSkinLaunchProfilePreset()
    {
        LaunchAuthlibServer = DefaultAuthlibServer;
        LaunchAuthlibStatusText = string.Empty;
        AddActivity(T("launch.profile.authlib.activities.use_littleskin"), LaunchAuthlibServer);
    }

    private bool TryBeginLaunchProfileAction(string actionName)
    {
        if (_isLaunchProfileActionInProgress)
        {
            AddActivity(actionName, T("launch.profile.activities.busy"));
            return false;
        }

        _isLaunchProfileActionInProgress = true;
        NotifyLaunchProfileCommandsChanged();
        return true;
    }

    private void EndLaunchProfileAction()
    {
        _isLaunchProfileActionInProgress = false;
        NotifyLaunchProfileCommandsChanged();
    }

    private void NotifyLaunchProfileCommandsChanged()
    {
        _selectLaunchProfileCommand.NotifyCanExecuteChanged();
        _addLaunchProfileCommand.NotifyCanExecuteChanged();
        _createOfflineLaunchProfileCommand.NotifyCanExecuteChanged();
        _loginMicrosoftLaunchProfileCommand.NotifyCanExecuteChanged();
        _loginAuthlibLaunchProfileCommand.NotifyCanExecuteChanged();
        _refreshLaunchProfileCommand.NotifyCanExecuteChanged();
        _backLaunchProfileCommand.NotifyCanExecuteChanged();
        _submitOfflineLaunchProfileCommand.NotifyCanExecuteChanged();
        _submitMicrosoftLaunchProfileCommand.NotifyCanExecuteChanged();
        _openMicrosoftDeviceLinkCommand.NotifyCanExecuteChanged();
        _submitAuthlibLaunchProfileCommand.NotifyCanExecuteChanged();
        _useLittleSkinLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    private async Task<LaunchProfileRefreshResult> RefreshSelectedLaunchProfileCoreAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false,
        Action<string>? onStatusChanged = null)
    {
        if (!TryGetSelectedStoredLaunchProfile(out var profileDocument, out var selectedProfileIndex, out var selectedProfile))
        {
            return new LaunchProfileRefreshResult(false, T("launch.profile.selection.empty"));
        }

        return selectedProfile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => await RefreshMicrosoftLaunchProfileAsync(
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            MinecraftLaunchStoredProfileKind.Authlib => await RefreshAuthlibLaunchProfileAsync(
                profileDocument,
                selectedProfileIndex,
                selectedProfile,
                cancellationToken,
                forceRefresh,
                onStatusChanged),
            _ => new LaunchProfileRefreshResult(false, T("launch.profile.refresh.not_required"))
        };
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

    private async Task<LaunchProfileRefreshResult> RefreshAuthlibLaunchProfileAsync(
        MinecraftLaunchProfileDocument profileDocument,
        int selectedProfileIndex,
        MinecraftLaunchPersistedProfile selectedProfile,
        CancellationToken cancellationToken,
        bool forceRefresh,
        Action<string>? onStatusChanged)
    {
        var serverBaseUrl = NormalizeAuthlibServerBaseUrl(selectedProfile.Server ?? LaunchAuthlibServer);
        if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.invalid_server"));
        }

        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
            !string.IsNullOrWhiteSpace(selectedProfile.ClientToken))
        {
            try
            {
                onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.validate_session"));
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildValidateRequest(
                        serverBaseUrl,
                        selectedProfile.AccessToken,
                        selectedProfile.ClientToken),
                    cancellationToken);
                return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.valid", ("profile_name", selectedProfile.Username ?? T("launch.profile.entry.unnamed"))));
            }
            catch (LaunchProfileRequestException)
            {
                // Continue with refresh/authenticate fallback when the cached token is no longer accepted.
            }
        }

        var hasRefreshableSession = !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
                                    !string.IsNullOrWhiteSpace(selectedProfile.ClientToken);
        if (hasRefreshableSession)
        {
            var accessToken = selectedProfile.AccessToken!;
            var clientToken = selectedProfile.ClientToken!;
            try
            {
                onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.refresh_session"));
                var refreshJson = await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                        serverBaseUrl,
                        accessToken,
                        clientToken),
                    cancellationToken);
                var metadataJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl, cancellationToken);
                var serverName = MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(metadataJson);
                var refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
                    new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                        refreshJson,
                        selectedProfileIndex,
                        serverBaseUrl,
                        serverName,
                        selectedProfile.LoginName ?? string.Empty,
                        selectedProfile.Password ?? string.Empty));
                if (!string.IsNullOrWhiteSpace(selectedProfile.Uuid) &&
                    !string.Equals(refreshResult.Session.ProfileId, selectedProfile.Uuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.profile_changed"));
                }

                FrontendProfileStorageService.Save(
                    _shellActionService.RuntimePaths,
                    FrontendProfileStorageService.ApplyMutation(profileDocument, refreshResult.MutationPlan, out _));
                return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.completed", ("profile_name", refreshResult.Session.ProfileName)), ShouldInvalidateAvatarCache: true);
            }
            catch (LaunchProfileRequestException ex)
            {
                if (IsAuthlibCredentialExpired(ex.ResponseBody))
                {
                    throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.relogin_required"));
                }

                throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.failed", ("message", GetLaunchProfileFriendlyError(ex))));
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.LoginName) || string.IsNullOrWhiteSpace(selectedProfile.Password))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.incomplete_session"));
        }

        onStatusChanged?.Invoke(T("launch.profile.refresh.authlib.reauthenticate"));
        var authenticateJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                serverBaseUrl,
                selectedProfile.LoginName,
                selectedProfile.Password),
            cancellationToken);
        var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                ForceReselectProfile: false,
                CachedProfileId: selectedProfile.Uuid,
                AuthenticateResponseJson: authenticateJson));
        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
        {
            throw new InvalidOperationException((authenticatePlan.FailureMessage ?? T("launch.profile.authlib.errors.login_failed")).TrimStart('$'));
        }

        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection &&
            string.IsNullOrWhiteSpace(authenticatePlan.SelectedProfileId))
        {
            throw new InvalidOperationException(T("launch.profile.refresh.authlib.errors.multiple_roles"));
        }

        var selectedProfileId = authenticatePlan.SelectedProfileId
                                ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            throw new InvalidOperationException(T("launch.profile.authlib.errors.no_roles"));
        }

        var metadataResponseJson = await TryReadAuthlibMetadataJsonAsync(serverBaseUrl, cancellationToken);
        var authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
            new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                authenticatePlan,
                metadataResponseJson,
                IsExistingProfile: true,
                SelectedProfileIndex: selectedProfileIndex,
                serverBaseUrl,
                selectedProfile.LoginName,
                selectedProfile.Password,
                selectedProfileId));
        FrontendProfileStorageService.Save(
            _shellActionService.RuntimePaths,
            FrontendProfileStorageService.ApplyMutation(profileDocument, authenticateResult.MutationPlan, out _));
        return new LaunchProfileRefreshResult(true, T("launch.profile.refresh.authlib.reauthenticated", ("profile_name", authenticateResult.Session.ProfileName)), ShouldInvalidateAvatarCache: true);
    }

    private void SetLaunchProfileSurface(LaunchProfileSurfaceKind surface)
    {
        _launchProfileSurface = surface;
        if (surface == LaunchProfileSurfaceKind.Selection)
        {
            RefreshLaunchProfileEntries();
        }

        RaiseLaunchProfileSurfaceProperties();
    }

    private void RefreshLaunchProfileEntries()
    {
        var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
        var selectedIndex = GetSelectedProfileIndex(profileDocument);
        ReplaceItems(
            LaunchProfileEntries,
            profileDocument.Profiles.Select((profile, index) => new LaunchProfileEntryViewModel(
                string.IsNullOrWhiteSpace(profile.Username) ? T("launch.profile.entry.unnamed") : profile.Username!,
                BuildProfileChoiceSummary(profile),
                index == selectedIndex,
                new ActionCommand(() => _ = SelectLaunchProfileEntryAsync(index)),
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? FrontendIconCatalog.Refresh.Data
                    : string.Empty,
                index == selectedIndex && IsRefreshableLaunchProfile(profile) && IsLaunchProfileRefreshInProgress,
                T("launch.profile.activities.refresh"),
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? _refreshLaunchProfileCommand
                    : null,
                FrontendIconCatalog.DeleteOutline.Data,
                T("launch.profile.activities.delete"),
                new ActionCommand(() => _ = DeleteLaunchProfileAsync(index)))));
        RaisePropertyChanged(nameof(HasLaunchProfileEntries));
    }

    private static bool IsRefreshableLaunchProfile(MinecraftLaunchPersistedProfile profile)
    {
        return profile.Kind is MinecraftLaunchStoredProfileKind.Microsoft or MinecraftLaunchStoredProfileKind.Authlib;
    }

    private async Task DeleteLaunchProfileAsync(int profileIndex)
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.delete")))
        {
            return;
        }

        try
        {
            var confirmed = await _shellActionService.ConfirmAsync(
                T("launch.profile.delete.confirmation.title"),
                T("launch.profile.delete.confirmation.message"),
                T("launch.profile.delete.confirmation.confirm"),
                isDanger: true);
            if (!confirmed)
            {
                AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.canceled"));
                return;
            }

            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (profileIndex < 0 || profileIndex >= profileDocument.Profiles.Count)
            {
                AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.list_changed"));
                return;
            }

            var profileName = string.IsNullOrWhiteSpace(profileDocument.Profiles[profileIndex].Username)
                ? T("launch.profile.entry.unnamed")
                : profileDocument.Profiles[profileIndex].Username!;
            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.DeleteProfile(profileDocument, profileIndex));
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.completed", ("profile_name", profileName)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("launch.profile.activities.delete_failed"), ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task SelectLaunchProfileEntryAsync(int selectedIndex)
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.switch")))
        {
            return;
        }

        try
        {
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (selectedIndex < 0 || selectedIndex >= profileDocument.Profiles.Count)
            {
                return;
            }

            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.SelectProfile(profileDocument, selectedIndex));
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(
                T("launch.profile.activities.switch"),
                T(
                    "launch.profile.switch.completed",
                    ("profile_name", profileDocument.Profiles[selectedIndex].Username ?? T("launch.profile.entry.unnamed"))));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("launch.profile.activities.switch_failed"), ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private void NormalizeLaunchProfileSurface()
    {
        RefreshLaunchProfileEntries();
        var effectiveSurface = GetEffectiveLaunchProfileSurface();
        if (effectiveSurface == LaunchProfileSurfaceKind.Selection)
        {
            if (!HasLaunchProfileEntries)
            {
                _launchProfileSurface = HasSelectedLaunchProfile ? LaunchProfileSurfaceKind.Summary : LaunchProfileSurfaceKind.Selection;
            }
        }
        else if (!HasSelectedLaunchProfile &&
                 effectiveSurface == LaunchProfileSurfaceKind.Summary)
        {
            _launchProfileSurface = LaunchProfileSurfaceKind.Selection;
        }

        RaiseLaunchProfileSurfaceProperties();
    }

    private LaunchProfileSurfaceKind GetEffectiveLaunchProfileSurface()
    {
        if (_launchProfileSurface != LaunchProfileSurfaceKind.Auto)
        {
            return _launchProfileSurface;
        }

        return HasSelectedLaunchProfile
            ? LaunchProfileSurfaceKind.Summary
            : LaunchProfileSurfaceKind.Selection;
    }

    private void RaiseLaunchProfileSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowLaunchProfileSummaryCard));
        RaisePropertyChanged(nameof(ShowLaunchProfileChooser));
        RaisePropertyChanged(nameof(ShowLaunchProfileSelection));
        RaisePropertyChanged(nameof(ShowLaunchOfflineEditor));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftEditor));
        RaisePropertyChanged(nameof(ShowLaunchAuthlibEditor));
        RaisePropertyChanged(nameof(ShowLaunchProfileBackButton));
        RaisePropertyChanged(nameof(HasLaunchProfileEntries));
        RaisePropertyChanged(nameof(HasNoLaunchProfileEntries));
        RaisePropertyChanged(nameof(LaunchProfileSelectionHint));
        RaisePropertyChanged(nameof(LaunchOfflineUuidModeOptions));
        RaisePropertyChanged(nameof(IsLaunchOfflineCustomUuidVisible));
        RaisePropertyChanged(nameof(HasLaunchOfflineStatus));
        RaisePropertyChanged(nameof(LaunchMicrosoftPrimaryButtonText));
        RaisePropertyChanged(nameof(HasLaunchMicrosoftDeviceCode));
        RaisePropertyChanged(nameof(HasLaunchMicrosoftVerificationUrl));
        RaisePropertyChanged(nameof(HasLaunchAuthlibStatus));
        RaisePropertyChanged(nameof(IsLaunchProfileRefreshInProgress));
    }

    private void PopulateAuthlibDefaults()
    {
        if (_launchComposition.SelectedProfile.Kind == MinecraftLaunchProfileKind.Auth &&
            !string.IsNullOrWhiteSpace(_launchComposition.SelectedProfile.AuthServer))
        {
            LaunchAuthlibServer = _launchComposition.SelectedProfile.AuthServer!;
        }
        else if (!string.IsNullOrWhiteSpace(_instanceComposition.Setup.AuthServer))
        {
            LaunchAuthlibServer = _instanceComposition.Setup.AuthServer;
        }
        else if (string.IsNullOrWhiteSpace(LaunchAuthlibServer))
        {
            LaunchAuthlibServer = DefaultAuthlibServer;
        }
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

    private string ResolveOfflineUuid(string userName)
    {
        return SelectedLaunchOfflineUuidModeIndex switch
        {
            2 => ResolveCustomOfflineUuid(),
            1 => CreateOfflineLegacyUuid(userName),
            _ => CreateOfflineUuid(userName)
        };
    }

    private string ResolveCustomOfflineUuid()
    {
        var uuid = LaunchOfflineCustomUuid.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        if (!OfflineUuidPattern.IsMatch(uuid))
        {
            throw new InvalidOperationException(T("launch.profile.offline.errors.invalid_uuid"));
        }

        return uuid;
    }

    private async Task<string> TryReadAuthlibMetadataJsonAsync(string serverBaseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(serverBaseUrl),
                cancellationToken);
        }
        catch
        {
            var serverName = TryGetHostLabel(serverBaseUrl) ?? "Authlib Server";
            return JsonSerializer.Serialize(new
            {
                meta = new
                {
                    serverName
                }
            });
        }
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

    private async Task<string> SendLaunchProfileRequestAsync(
        MinecraftLaunchHttpRequestPlan plan,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateLaunchProfileHttpClient();
        using var request = new HttpRequestMessage(new HttpMethod(plan.Method), plan.Url);
        if (plan.Headers is not null)
        {
            foreach (var header in plan.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (!string.IsNullOrWhiteSpace(plan.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plan.BearerToken);
        }

        if (!string.IsNullOrWhiteSpace(plan.Body))
        {
            request.Content = new StringContent(
                plan.Body,
                Encoding.UTF8,
                string.IsNullOrWhiteSpace(plan.ContentType) ? "application/json" : plan.ContentType);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new LaunchProfileRequestException(plan.Url, (int)response.StatusCode, responseBody);
        }

        return responseBody;
    }

    private bool TryGetSelectedStoredLaunchProfile(
        out MinecraftLaunchProfileDocument document,
        out int selectedProfileIndex,
        out MinecraftLaunchPersistedProfile selectedProfile)
    {
        document = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
        if (document.Profiles.Count == 0)
        {
            selectedProfileIndex = 0;
            selectedProfile = null!;
            return false;
        }

        selectedProfileIndex = GetSelectedProfileIndex(document);
        if (selectedProfileIndex < 0 || selectedProfileIndex >= document.Profiles.Count)
        {
            selectedProfile = null!;
            return false;
        }

        selectedProfile = document.Profiles[selectedProfileIndex];
        return true;
    }

    private HttpClient CreateLaunchProfileHttpClient()
    {
        var client = CreateToolHttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
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

    private static string NormalizeAuthlibServerBaseUrl(string serverInput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverInput);

        var normalized = serverInput.Trim().TrimEnd('/');
        if (normalized.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        return normalized + "/authserver";
    }

    private static int GetSelectedProfileIndex(MinecraftLaunchProfileDocument document)
    {
        if (document.Profiles.Count == 0)
        {
            return 0;
        }

        return document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
            ? document.LastUsedProfile
            : 0;
    }

    private static int? GetSelectedProfileIndexOrNull(MinecraftLaunchProfileDocument document)
    {
        return document.Profiles.Count == 0 ? null : GetSelectedProfileIndex(document);
    }

    private string BuildProfileChoiceSummary(MinecraftLaunchPersistedProfile profile)
    {
        var authLabel = profile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => T("launch.profile.kinds.microsoft"),
            MinecraftLaunchStoredProfileKind.Authlib => string.IsNullOrWhiteSpace(profile.ServerName)
                ? T("launch.profile.kinds.authlib")
                : T("launch.profile.kinds.authlib_with_server", ("server_name", profile.ServerName)),
            _ => T("launch.profile.kinds.offline")
        };

        if (string.IsNullOrWhiteSpace(profile.Desc))
        {
            return authLabel;
        }

        return $"{authLabel}，{profile.Desc}";
    }

    private static MinecraftLaunchStoredProfile ToStoredProfile(MinecraftLaunchPersistedProfile profile)
    {
        return new MinecraftLaunchStoredProfile(
            profile.Kind,
            profile.Uuid ?? string.Empty,
            profile.Username ?? string.Empty,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.LoginName,
            profile.Password,
            profile.ClientToken,
            profile.SkinHeadId,
            profile.RawJson);
    }

    private static string CreateOfflineUuid(string userName)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash).ToString("N");
    }

    private static string CreateOfflineLegacyUuid(string userName)
    {
        var fullUuid = userName.Length.ToString("X").PadLeft(16, '0') + GetLegacyHash(userName).ToString("X").PadLeft(16, '0');
        return fullUuid[..12] + "3" + fullUuid[13..16] + "9" + fullUuid[17..];
    }

    private static ulong GetLegacyHash(string value)
    {
        ulong result = 5381;
        foreach (var character in value)
        {
            result = (result << 5) ^ result ^ character;
        }

        return result ^ 0xA98F501BC684032FUL;
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

    private static string? TryGetHostLabel(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static string? TryReadJsonField(string json, string propertyName)
    {
        try
        {
            return JsonNode.Parse(json)?[propertyName]?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsAuthlibCredentialExpired(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return false;
        }

        var error = TryReadJsonField(responseBody, "error");
        var message = TryReadJsonField(responseBody, "errorMessage")
                      ?? TryReadJsonField(responseBody, "message");

        return string.Equals(error, "ForbiddenOperationException", StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(message) &&
                message.Contains("Invalid token", StringComparison.OrdinalIgnoreCase));
    }

    private string GetLaunchProfileFriendlyError(Exception exception)
    {
        if (exception is LaunchProfileRequestException requestException)
        {
            var directMessage = TryReadJsonField(requestException.ResponseBody, "errorMessage")
                                ?? TryReadJsonField(requestException.ResponseBody, "message")
                                ?? TryReadJsonField(requestException.ResponseBody, "error_description")
                                ?? TryReadJsonField(requestException.ResponseBody, "error");
            if (!string.IsNullOrWhiteSpace(directMessage))
            {
                return directMessage.Trim().TrimStart('$');
            }

            return T("launch.profile.refresh.errors.request_failed_status", ("status_code", requestException.StatusCode));
        }

        return exception.Message.Trim().TrimStart('$');
    }

    private sealed class LaunchProfileRequestException(string url, int statusCode, string responseBody)
        : Exception($"HTTP {statusCode}: {url}")
    {
        public int StatusCode { get; } = statusCode;

        public string ResponseBody { get; } = responseBody;
    }

    private sealed record LaunchProfileRefreshResult(
        bool WasChecked,
        string Message,
        bool ShouldInvalidateAvatarCache = false);
}

internal sealed class LaunchProfileEntryViewModel(
    string title,
    string info,
    bool isSelected,
    ActionCommand command,
    string accessoryIconData,
    bool accessoryIsSpinning,
    string accessoryToolTip,
    ActionCommand? accessoryCommand,
    string secondaryAccessoryIconData,
    string secondaryAccessoryToolTip,
    ActionCommand? secondaryAccessoryCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public bool IsSelected { get; } = isSelected;

    public ActionCommand Command { get; } = command;

    public string AccessoryIconData { get; } = accessoryIconData;

    public bool AccessoryIsSpinning { get; } = accessoryIsSpinning;

    public string AccessoryToolTip { get; } = accessoryToolTip;

    public ActionCommand? AccessoryCommand { get; } = accessoryCommand;

    public string SecondaryAccessoryIconData { get; } = secondaryAccessoryIconData;

    public string SecondaryAccessoryToolTip { get; } = secondaryAccessoryToolTip;

    public ActionCommand? SecondaryAccessoryCommand { get; } = secondaryAccessoryCommand;
}

internal enum LaunchProfileSurfaceKind
{
    Auto = 0,
    Summary = 1,
    Chooser = 2,
    Selection = 3,
    OfflineEditor = 4,
    MicrosoftEditor = 5,
    AuthlibEditor = 6
}
