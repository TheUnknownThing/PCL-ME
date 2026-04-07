using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Desktop.Dialogs;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string DefaultAuthlibServer = "https://littleskin.cn/api/yggdrasil";
    private static readonly Regex OfflineUserNamePattern = new("^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);

    private async Task SelectLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction("切换档案"))
        {
            return;
        }

        try
        {
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (profileDocument.Profiles.Count == 0)
            {
                AddActivity("切换档案", "当前还没有可用档案。");
                return;
            }

            var options = profileDocument.Profiles
                .Select((profile, index) => new PclChoiceDialogOption(
                    index.ToString(),
                    string.IsNullOrWhiteSpace(profile.Username) ? "未命名档案" : profile.Username!,
                    BuildProfileChoiceSummary(profile)))
                .ToArray();

            var selectedId = await _shellActionService.PromptForChoiceAsync(
                "选择档案",
                "选择要用于启动游戏的档案。",
                options,
                GetSelectedProfileIndex(profileDocument).ToString(),
                "选中");
            if (selectedId is null)
            {
                AddActivity("切换档案", "已取消切换。");
                return;
            }

            if (!int.TryParse(selectedId, out var selectedIndex) ||
                selectedIndex < 0 ||
                selectedIndex >= profileDocument.Profiles.Count)
            {
                AddActivity("切换档案失败", "所选档案无效。");
                return;
            }

            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.SelectProfile(profileDocument, selectedIndex));
            RefreshLaunchState();
            AddActivity("切换档案", $"当前档案已切换为 {profileDocument.Profiles[selectedIndex].Username ?? "未命名档案"}。");
        }
        catch (Exception ex)
        {
            AddActivity("切换档案失败", ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task AddLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction("新建档案"))
        {
            return;
        }

        string? selectedId = null;
        try
        {
            selectedId = await _shellActionService.PromptForChoiceAsync(
                "新建档案",
                "选择要添加的档案类型。",
                [
                    new PclChoiceDialogOption("offline", "离线创建", "创建本地离线档案。"),
                    new PclChoiceDialogOption("microsoft", "微软登录", "通过设备代码流添加正版账户。"),
                    new PclChoiceDialogOption("authlib", "外置登录", "添加 Authlib / LittleSkin 等外置验证账户。")
                ],
                confirmText: "继续");
        }
        catch (Exception ex)
        {
            AddActivity("新建档案失败", ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }

        switch (selectedId)
        {
            case "offline":
                await CreateOfflineLaunchProfileAsync();
                break;
            case "microsoft":
                await LoginMicrosoftLaunchProfileAsync();
                break;
            case "authlib":
                await LoginAuthlibLaunchProfileAsync();
                break;
            case null:
                AddActivity("新建档案", "已取消创建。");
                break;
        }
    }

    private async Task CreateOfflineLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction("离线创建"))
        {
            return;
        }

        try
        {
            var initialName = HasSelectedLaunchProfile && !string.Equals(LaunchUserName, "未选择档案", StringComparison.Ordinal)
                ? LaunchUserName
                : string.Empty;
            var userName = await _shellActionService.PromptForTextAsync(
                "离线创建",
                "输入离线档案的玩家 ID。",
                initialName,
                "创建",
                "3 - 16 位，可包含字母、数字与下划线");
            if (userName is null)
            {
                AddActivity("离线创建", "已取消创建。");
                return;
            }

            userName = userName.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                AddActivity("离线创建失败", "玩家 ID 不能为空。");
                return;
            }

            if (!OfflineUserNamePattern.IsMatch(userName))
            {
                var shouldContinue = await _shellActionService.ConfirmAsync(
                    "玩家 ID 不符合规范",
                    "你输入的玩家 ID 不符合标准（3 - 16 位，只可以包含英文字母、数字与下划线），可能导致部分版本的游戏无法启动或发生错误。\n\n如果你坚持，仍然可以继续创建档案。",
                    "继续");
                if (!shouldContinue)
                {
                    AddActivity("离线创建", "已取消创建。");
                    return;
                }
            }

            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            var nextDocument = FrontendProfileStorageService.CreateOfflineProfile(
                profileDocument,
                userName,
                CreateOfflineUuid(userName));
            FrontendProfileStorageService.Save(_shellActionService.RuntimePaths, nextDocument);
            RefreshLaunchState();
            AddActivity("离线创建", $"已创建离线档案 {userName}。");
        }
        catch (Exception ex)
        {
            AddActivity("离线创建失败", ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task LoginMicrosoftLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction("微软登录"))
        {
            return;
        }

        try
        {
            AddActivity("微软登录", "正在请求设备代码。");
            var deviceCodeJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest());
            var promptPlan = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(deviceCodeJson);
            TryCopyLaunchProfileText(promptPlan.UserCode);
            OpenExternalTarget(promptPlan.OpenBrowserUrl, "已打开微软登录网页。");

            var shouldPoll = await _shellActionService.ConfirmAsync(
                promptPlan.Title,
                $"{promptPlan.Message}\n\n授权码：{promptPlan.UserCode}\n\n完成授权后点击“继续”，启动器会自动拉取登录结果。",
                "继续");
            if (!shouldPoll)
            {
                AddActivity("微软登录", "已取消登录。");
                return;
            }

            AddActivity("微软登录", "正在等待微软完成授权。");
            var oauthTokens = await PollMicrosoftOAuthTokensAsync(promptPlan);
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
                if (await TryHandleMicrosoftDecisionPromptAsync(
                        MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(ex.ResponseBody)))
                {
                    AddActivity("微软登录", "已按照登录提示中断当前流程。");
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
                await TryHandleMicrosoftDecisionPromptAsync(MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt());
                AddActivity("微软登录失败", "该账户没有可用的 Minecraft Java 版所有权。");
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
                await TryHandleMicrosoftDecisionPromptAsync(MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt());
                AddActivity("微软登录失败", "该账户尚未创建 Minecraft 玩家档案。");
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
            RefreshLaunchState();
            AddActivity("微软登录", $"已添加微软档案 {profileResponse.UserName}。");
        }
        catch (Exception ex)
        {
            AddActivity("微软登录失败", GetLaunchProfileFriendlyError(ex));
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task LoginAuthlibLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction("外置登录"))
        {
            return;
        }

        try
        {
            var serverChoice = await _shellActionService.PromptForChoiceAsync(
                "外置登录",
                "选择要使用的验证服务器。",
                [
                    new PclChoiceDialogOption("littleskin", "预设 - LittleSkin", DefaultAuthlibServer),
                    new PclChoiceDialogOption("custom", "自定义", "手动输入 Authlib 服务器地址。")
                ],
                "littleskin",
                "继续");
            if (serverChoice is null)
            {
                AddActivity("外置登录", "已取消登录。");
                return;
            }

            var serverInput = serverChoice == "littleskin"
                ? DefaultAuthlibServer
                : await _shellActionService.PromptForTextAsync(
                    "验证服务器",
                    "输入外置验证服务器地址。标准实现会在末尾自动补上 /authserver。",
                    DefaultAuthlibServer,
                    "继续",
                    "例如：https://littleskin.cn/api/yggdrasil");
            if (serverInput is null)
            {
                AddActivity("外置登录", "已取消登录。");
                return;
            }

            var loginName = await _shellActionService.PromptForTextAsync(
                "外置登录",
                "输入用户名或邮箱。",
                string.Empty,
                "继续",
                "用户名或邮箱");
            if (loginName is null)
            {
                AddActivity("外置登录", "已取消登录。");
                return;
            }

            var password = await _shellActionService.PromptForTextAsync(
                "外置登录",
                "输入密码。",
                string.Empty,
                "登录",
                "密码",
                isPassword: true);
            if (password is null)
            {
                AddActivity("外置登录", "已取消登录。");
                return;
            }

            loginName = loginName.Trim();
            if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(password))
            {
                AddActivity("外置登录失败", "用户名与密码不能为空。");
                return;
            }

            var serverBaseUrl = NormalizeAuthlibServerBaseUrl(serverInput);
            if (!Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out _))
            {
                AddActivity("外置登录失败", "输入的验证服务器地址无效。");
                return;
            }

            var authenticateJson = await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
                    serverBaseUrl,
                    loginName,
                    password));
            var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
                new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                    ForceReselectProfile: false,
                    CachedProfileId: null,
                    AuthenticateResponseJson: authenticateJson));
            if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
            {
                throw new InvalidOperationException((authenticatePlan.FailureMessage ?? "外置登录失败。").TrimStart('$'));
            }

            string? selectedProfileId = authenticatePlan.SelectedProfileId;
            if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection)
            {
                var selectedId = await _shellActionService.PromptForChoiceAsync(
                    authenticatePlan.PromptTitle ?? "选择角色",
                    "该账户下存在多个角色，请选择一个。",
                    authenticatePlan.PromptOptions
                        .Select(profile => new PclChoiceDialogOption(profile.Id, profile.Name, profile.Id))
                        .ToArray(),
                    authenticatePlan.PromptOptions.FirstOrDefault()?.Id,
                    "选中");
                if (selectedId is null)
                {
                    AddActivity("外置登录", "已取消角色选择。");
                    return;
                }

                selectedProfileId = selectedId;
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
                    password,
                    selectedProfileId));
            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.ApplyMutation(profileDocument, authenticateResult.MutationPlan, out _));
            RefreshLaunchState();
            AddActivity("外置登录", $"已添加外置档案 {authenticateResult.Session.ProfileName}。");
        }
        catch (Exception ex)
        {
            AddActivity("外置登录失败", GetLaunchProfileFriendlyError(ex));
        }
        finally
        {
            EndLaunchProfileAction();
        }
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
    }

    private async Task<string> TryReadAuthlibMetadataJsonAsync(string serverBaseUrl)
    {
        try
        {
            return await SendLaunchProfileRequestAsync(
                MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(serverBaseUrl));
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
        MinecraftLaunchMicrosoftDeviceCodePromptPlan promptPlan)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(promptPlan.ExpiresInSeconds, 1));
        var pollDelay = Math.Max(promptPlan.PollIntervalSeconds, 1);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var responseJson = await SendLaunchProfileRequestAsync(BuildMicrosoftDeviceTokenPollRequest(promptPlan.DeviceCode));
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

    private async Task<string> SendLaunchProfileRequestAsync(MinecraftLaunchHttpRequestPlan plan)
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

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new LaunchProfileRequestException(plan.Url, (int)response.StatusCode, responseBody);
        }

        return responseBody;
    }

    private HttpClient CreateLaunchProfileHttpClient()
    {
        var client = CreateToolHttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private async Task<bool> TryHandleMicrosoftDecisionPromptAsync(MinecraftLaunchAccountDecisionPrompt? prompt)
    {
        if (prompt is null)
        {
            return false;
        }

        var selectedId = await _shellActionService.PromptForChoiceAsync(
            prompt.Title,
            prompt.Message,
            prompt.Options
                .Select((option, index) => new PclChoiceDialogOption(index.ToString(), option.Label, option.Url ?? string.Empty))
                .ToArray(),
            "0",
            "继续");
        if (selectedId is null ||
            !int.TryParse(selectedId, out var selectedIndex) ||
            selectedIndex < 0 ||
            selectedIndex >= prompt.Options.Count)
        {
            return true;
        }

        var selectedOption = prompt.Options[selectedIndex];
        if (selectedOption.Decision == MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort &&
            !string.IsNullOrWhiteSpace(selectedOption.Url))
        {
            OpenExternalTarget(selectedOption.Url, "已打开相关网页。");
        }

        if (selectedOption.Followup is not null)
        {
            await _shellActionService.ConfirmAsync(
                selectedOption.Followup.Title,
                selectedOption.Followup.Message,
                "我知道了",
                selectedOption.Followup.IsWarning);
        }

        return true;
    }

    private static MinecraftLaunchHttpRequestPlan BuildMicrosoftDeviceTokenPollRequest(string deviceCode)
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
                    ["client_id"] = MinecraftLaunchMicrosoftRequestWorkflowService.DefaultMicrosoftClientId,
                    ["device_code"] = deviceCode
                }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")));
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
}
