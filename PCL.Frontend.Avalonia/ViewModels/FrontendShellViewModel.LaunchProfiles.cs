using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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

    public IReadOnlyList<string> LaunchOfflineUuidModeOptions { get; } =
    [
        "行业规范",
        "旧版",
        "自定义"
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
        ? "选择一个档案以启动游戏"
        : "新建并选择一个档案以启动游戏";

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
            var clampedValue = Math.Clamp(value, 0, LaunchOfflineUuidModeOptions.Count - 1);
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
        ? "开始正版验证"
        : "完成授权后继续";

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
            AddActivity("切换档案", "当前还没有可用档案。");
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
        LaunchOfflineUserName = HasSelectedLaunchProfile && !string.Equals(LaunchUserName, "未选择档案", StringComparison.Ordinal)
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
        LaunchMicrosoftStatusText = "点击下方按钮后，启动器会打开微软登录网页。";
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
        if (!TryBeginLaunchProfileAction("刷新档案"))
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
                AddActivity("刷新档案", result.Message);
                return;
            }

            if (result.ShouldInvalidateAvatarCache)
            {
                InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
            }

            await RefreshLaunchProfileCompositionAsync();
            AddActivity("刷新档案", result.Message);
            AvaloniaHintBus.Show(result.Message, AvaloniaHintTheme.Success);
        }
        catch (OperationCanceledException)
        {
            AddActivity("刷新档案", "档案刷新已取消。");
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity("刷新档案失败", message);
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
        if (!TryBeginLaunchProfileAction("离线创建"))
        {
            return;
        }

        try
        {
            var userName = LaunchOfflineUserName.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                LaunchOfflineStatusText = "玩家 ID 不能为空。";
                return;
            }

            var uuid = ResolveOfflineUuid(userName);
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var nextDocument = FrontendProfileStorageService.CreateOfflineProfile(profileDocument, userName, uuid);
            FrontendProfileStorageService.Save(_shellActionService.RuntimePaths, nextDocument);
            LaunchOfflineStatusText = string.Empty;
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity("离线创建", $"已创建离线档案 {userName}。");
        }
        catch (Exception ex)
        {
            LaunchOfflineStatusText = ex.Message.Trim().TrimStart('$');
            AddFailureActivity("离线创建失败", LaunchOfflineStatusText);
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

        if (!TryBeginLaunchProfileAction("微软登录"))
        {
            return;
        }

        try
        {
            LaunchMicrosoftStatusText = "正在等待微软完成授权。";
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
                    LaunchMicrosoftStatusText = decisionPrompt.Message;
                    if (decisionPrompt.Options.FirstOrDefault()?.Decision == MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort &&
                        !string.IsNullOrWhiteSpace(decisionPrompt.Options[0].Url))
                    {
                        OpenExternalTarget(decisionPrompt.Options[0].Url, "已打开相关网页。");
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
                LaunchMicrosoftStatusText = MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt().Message;
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
                LaunchMicrosoftStatusText = prompt.Message;
                if (prompt.Options.FirstOrDefault()?.Decision == MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort &&
                    !string.IsNullOrWhiteSpace(prompt.Options[0].Url))
                {
                    OpenExternalTarget(prompt.Options[0].Url, "已打开 Minecraft 档案创建页面。");
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
            AddActivity("微软登录", $"已添加微软档案 {profileResponse.UserName}。");
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            LaunchMicrosoftStatusText = message;
            AddFailureActivity("微软登录失败", message);
            if (ex is TimeoutException || message.Contains("过期", StringComparison.Ordinal))
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
        if (!TryBeginLaunchProfileAction("外置登录"))
        {
            return;
        }

        try
        {
            var serverBaseUrl = NormalizeAuthlibServerBaseUrl(LaunchAuthlibServer);
            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
            {
                LaunchAuthlibStatusText = "输入的验证服务器地址无效。";
                return;
            }

            var loginName = LaunchAuthlibLoginName.Trim();
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(LaunchAuthlibPassword))
            {
                LaunchAuthlibStatusText = "验证服务器、用户名与密码均不能为空。";
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
                LaunchAuthlibStatusText = (authenticatePlan.FailureMessage ?? "外置登录失败。").TrimStart('$');
                return;
            }

            var selectedProfileId = authenticatePlan.SelectedProfileId
                                    ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
            if (string.IsNullOrWhiteSpace(selectedProfileId))
            {
                LaunchAuthlibStatusText = "该账户下没有可用角色。";
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
            AddActivity("外置登录", $"已添加外置档案 {authenticateResult.Session.ProfileName}。");
        }
        catch (Exception ex)
        {
            LaunchAuthlibStatusText = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity("外置登录失败", LaunchAuthlibStatusText);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task BeginMicrosoftDeviceLoginAsync(string microsoftClientId)
    {
        if (!TryBeginLaunchProfileAction("微软登录"))
        {
            return;
        }

        try
        {
            LaunchMicrosoftStatusText = "正在请求设备代码。";
            var deviceCodeJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest(microsoftClientId));
            var promptPlan = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(deviceCodeJson);
            _launchMicrosoftPromptPlan = promptPlan;
            LaunchMicrosoftDeviceCode = promptPlan.UserCode;
            LaunchMicrosoftVerificationUrl = promptPlan.OpenBrowserUrl;
            LaunchMicrosoftStatusText = "网页登录已打开，完成授权后点击下方按钮继续。授权码已复制到剪贴板。";
            TryCopyLaunchProfileText(promptPlan.UserCode);
            OpenExternalTarget(promptPlan.OpenBrowserUrl, "已打开微软登录网页。");
            RaiseLaunchProfileSurfaceProperties();
        }
        catch (Exception ex)
        {
            LaunchMicrosoftStatusText = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity("微软登录失败", LaunchMicrosoftStatusText);
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
            OpenExternalTarget(LaunchMicrosoftVerificationUrl, "已打开微软登录网页。");
        }
    }

    private void ApplyLittleSkinLaunchProfilePreset()
    {
        LaunchAuthlibServer = DefaultAuthlibServer;
        LaunchAuthlibStatusText = string.Empty;
        AddActivity("设置为 LittleSkin", LaunchAuthlibServer);
    }

    private bool TryBeginLaunchProfileAction(string actionName)
    {
        if (_isLaunchProfileActionInProgress)
        {
            AddActivity(actionName, "当前已有一个档案操作正在进行。");
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
            return new LaunchProfileRefreshResult(false, "当前还没有可用档案。");
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
            _ => new LaunchProfileRefreshResult(false, "当前档案不需要刷新。")
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
                onStatusChanged?.Invoke("校验微软档案会话");
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(selectedProfile.AccessToken),
                    cancellationToken);
                return new LaunchProfileRefreshResult(true, $"已确认微软档案 {selectedProfile.Username ?? "未命名档案"} 仍然有效。");
            }
            catch (LaunchProfileRequestException)
            {
                // Follow HMCL's flow: validate first, then refresh when the current token is no longer accepted.
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.RefreshToken))
        {
            throw new InvalidOperationException("当前微软档案缺少刷新令牌，请重新登录。");
        }

        var clientId = ResolveMicrosoftClientId();
        onStatusChanged?.Invoke("刷新微软账号状态");

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
                throw new InvalidOperationException("当前微软档案登录已失效，请重新登录。");
            }

            throw new InvalidOperationException($"刷新微软档案失败：{GetLaunchProfileFriendlyError(ex)}");
        }

        var oauthTokens = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(oauthRefreshJson);

        onStatusChanged?.Invoke("获取 Xbox Live 令牌");
        var xboxLiveResponseJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(oauthTokens.AccessToken),
            cancellationToken);

        string xstsResponseJson;
        try
        {
            onStatusChanged?.Invoke("获取 Xbox 安全令牌");
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

            throw new InvalidOperationException($"刷新微软档案失败：{GetLaunchProfileFriendlyError(ex)}");
        }

        var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(xstsResponseJson);

        onStatusChanged?.Invoke("获取 Minecraft 访问令牌");
        var minecraftAccessTokenJson = await SendLaunchProfileRequestAsync(
            MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(
                xstsResponse.UserHash,
                xstsResponse.Token),
            cancellationToken);
        var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(minecraftAccessTokenJson);

        onStatusChanged?.Invoke("校验 Minecraft 购买状态");
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
            onStatusChanged?.Invoke("同步 Minecraft 玩家档案");
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

        return new LaunchProfileRefreshResult(true, $"已刷新微软档案 {profileResponse.UserName}。", ShouldInvalidateAvatarCache: true);
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
            throw new InvalidOperationException("当前外置档案缺少有效的验证服务器地址，请重新登录。");
        }

        if (!forceRefresh &&
            !string.IsNullOrWhiteSpace(selectedProfile.AccessToken) &&
            !string.IsNullOrWhiteSpace(selectedProfile.ClientToken))
        {
            try
            {
                onStatusChanged?.Invoke("校验外置档案会话");
                await SendLaunchProfileRequestAsync(
                    MinecraftLaunchAuthlibRequestWorkflowService.BuildValidateRequest(
                        serverBaseUrl,
                        selectedProfile.AccessToken,
                        selectedProfile.ClientToken),
                    cancellationToken);
                return new LaunchProfileRefreshResult(true, $"已确认外置档案 {selectedProfile.Username ?? "未命名档案"} 仍然有效。");
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
                onStatusChanged?.Invoke("刷新外置档案会话");
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
                    throw new InvalidOperationException("外置档案刷新后返回了不同的角色，请重新登录该档案。");
                }

                FrontendProfileStorageService.Save(
                    _shellActionService.RuntimePaths,
                    FrontendProfileStorageService.ApplyMutation(profileDocument, refreshResult.MutationPlan, out _));
                return new LaunchProfileRefreshResult(true, $"已刷新外置档案 {refreshResult.Session.ProfileName}。", ShouldInvalidateAvatarCache: true);
            }
            catch (LaunchProfileRequestException ex)
            {
                if (IsAuthlibCredentialExpired(ex.ResponseBody))
                {
                    throw new InvalidOperationException("当前外置档案登录已失效，请重新登录。");
                }

                throw new InvalidOperationException($"刷新外置档案失败：{GetLaunchProfileFriendlyError(ex)}");
            }
        }

        if (string.IsNullOrWhiteSpace(selectedProfile.LoginName) || string.IsNullOrWhiteSpace(selectedProfile.Password))
        {
            throw new InvalidOperationException("当前外置档案缺少完整会话信息，请重新登录。");
        }

        onStatusChanged?.Invoke("重新登录外置档案");
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
            throw new InvalidOperationException((authenticatePlan.FailureMessage ?? "外置登录失败。").TrimStart('$'));
        }

        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection &&
            string.IsNullOrWhiteSpace(authenticatePlan.SelectedProfileId))
        {
            throw new InvalidOperationException("该外置账户包含多个角色，无法自动确认当前角色，请重新登录该档案。");
        }

        var selectedProfileId = authenticatePlan.SelectedProfileId
                                ?? authenticatePlan.PromptOptions.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(selectedProfileId))
        {
            throw new InvalidOperationException("该外置账户下没有可用角色。");
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
        return new LaunchProfileRefreshResult(true, $"已重新登录外置档案 {authenticateResult.Session.ProfileName}。", ShouldInvalidateAvatarCache: true);
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
                string.IsNullOrWhiteSpace(profile.Username) ? "未命名档案" : profile.Username!,
                BuildProfileChoiceSummary(profile),
                index == selectedIndex,
                new ActionCommand(() => _ = SelectLaunchProfileEntryAsync(index)),
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? FrontendIconCatalog.Refresh.Data
                    : string.Empty,
                index == selectedIndex && IsRefreshableLaunchProfile(profile) && IsLaunchProfileRefreshInProgress,
                "刷新档案",
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? _refreshLaunchProfileCommand
                    : null,
                FrontendIconCatalog.DeleteOutline.Data,
                "删除档案",
                new ActionCommand(() => _ = DeleteLaunchProfileAsync(index)))));
        RaisePropertyChanged(nameof(HasLaunchProfileEntries));
    }

    private static bool IsRefreshableLaunchProfile(MinecraftLaunchPersistedProfile profile)
    {
        return profile.Kind is MinecraftLaunchStoredProfileKind.Microsoft or MinecraftLaunchStoredProfileKind.Authlib;
    }

    private async Task DeleteLaunchProfileAsync(int profileIndex)
    {
        if (!TryBeginLaunchProfileAction("删除档案"))
        {
            return;
        }

        try
        {
            var confirmed = await _shellActionService.ConfirmAsync(
                "删除档案确认",
                $"你正在选择删除此档案，该操作无法撤销。{Environment.NewLine}确定继续？",
                "继续",
                isDanger: true);
            if (!confirmed)
            {
                AddActivity("删除档案", "已取消删除。");
                return;
            }

            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (profileIndex < 0 || profileIndex >= profileDocument.Profiles.Count)
            {
                AddActivity("删除档案", "档案列表已更新，请重新查看。");
                return;
            }

            var profileName = string.IsNullOrWhiteSpace(profileDocument.Profiles[profileIndex].Username)
                ? "未命名档案"
                : profileDocument.Profiles[profileIndex].Username!;
            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.DeleteProfile(profileDocument, profileIndex));
            await RefreshLaunchProfileCompositionAsync();
            AddActivity("删除档案", $"已删除档案 {profileName}。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("删除档案失败", ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task SelectLaunchProfileEntryAsync(int selectedIndex)
    {
        if (!TryBeginLaunchProfileAction("切换档案"))
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
            AddActivity("切换档案", $"当前档案已切换为 {profileDocument.Profiles[selectedIndex].Username ?? "未命名档案"}。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("切换档案失败", ex.Message);
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
            LaunchMicrosoftStatusText = "点击下方按钮后，启动器会打开微软登录网页。";
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
            throw new InvalidOperationException("UUID 不符合要求，应为 32 位 16 进制字符串。");
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
                    throw new InvalidOperationException("微软账号授权已被取消。");
                }

                if (string.Equals(errorCode, "expired_token", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("微软登录授权码已过期，请重新发起登录。");
                }

                throw new InvalidOperationException(GetLaunchProfileFriendlyError(ex));
            }
        }

        throw new TimeoutException("等待微软登录确认超时，请重新发起登录。");
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

    private static string ResolveMicrosoftClientId()
    {
        var clientId = FrontendEmbeddedSecrets.GetMicrosoftClientId();

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("缺少微软 OAuth Client ID。请先配置 PCL_MS_CLIENT_ID。", "MS_CLIENT_ID");
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

    private static string BuildProfileChoiceSummary(MinecraftLaunchPersistedProfile profile)
    {
        var authLabel = profile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => "正版验证",
            MinecraftLaunchStoredProfileKind.Authlib => string.IsNullOrWhiteSpace(profile.ServerName)
                ? "外置验证"
                : $"外置验证 / {profile.ServerName}",
            _ => "离线验证"
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

    private static string GetLaunchProfileFriendlyError(Exception exception)
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

            return $"请求失败（HTTP {requestException.StatusCode}）。";
        }

        return exception.Message.Trim().TrimStart('$');
    }

    private sealed class LaunchProfileRequestException(string url, int statusCode, string responseBody)
        : Exception($"请求失败（HTTP {statusCode}）：{url}")
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
