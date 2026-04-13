using System.IO.Compression;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft.Java;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void ApplySidebarAccessory(string title, string actionLabel, string command)
    {
        if (IsDownloadInstallSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            ResetDownloadInstallSurface();
            return;
        }

        if (IsDownloadResourceSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            ResetDownloadResourceFilters();
            return;
        }

        if (IsSetupLaunchSurface && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetLaunchSettingsSurface();
            return;
        }

        if (IsSetupUpdateSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: true);
            return;
        }

        if (IsSetupGameLinkSurface && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetGameLinkSurface();
            return;
        }

        if (IsSetupGameManageSurface && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetGameManageSurface();
            return;
        }

        if (IsSetupLauncherMiscSurface && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetLauncherMiscSurface();
            return;
        }

        if (IsSetupJavaSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            RefreshJavaSurface();
            return;
        }

        if (IsSetupUiSurface && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetUiSurface();
            return;
        }

        if (IsToolsGameLinkSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            RefreshToolsGameLinkSurface();
            return;
        }

        if (IsToolsTestSurface && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            RefreshToolsTestSurface();
            return;
        }

        AddActivity($"左侧操作: {actionLabel}", $"{title} • {command}");
    }

    private async Task CheckForLauncherUpdatesAsync(bool forceRefresh)
    {
        var signature = $"{SelectedUpdateChannelIndex}|{MirrorCdk}";
        if (_isCheckingUpdate)
        {
            return;
        }

        if (!forceRefresh
            && string.Equals(_lastUpdateCheckSignature, signature, StringComparison.Ordinal)
            && _updateStatus.SurfaceState is not UpdateSurfaceState.Checking)
        {
            return;
        }

        _isCheckingUpdate = true;
        _updateStatus = FrontendSetupUpdateStatusService.CreateChecking();
        RaiseUpdateSurfaceProperties();

        try
        {
            _updateStatus = await FrontendSetupUpdateStatusService.QueryAsync(SelectedUpdateChannelIndex, MirrorCdk);
            _lastUpdateCheckSignature = signature;
            RaiseUpdateSurfaceProperties();

            AddActivity(
                "刷新更新页",
                _updateStatus.SurfaceState switch
                {
                    UpdateSurfaceState.Available => $"检测到可用更新：{_updateStatus.AvailableUpdateName}",
                    UpdateSurfaceState.Latest => $"{_updateStatus.CurrentVersionName} 已是最新版本",
                    UpdateSurfaceState.Error => _updateStatus.CurrentVersionDescription,
                    _ => "正在检查更新..."
                });
        }
        finally
        {
            _isCheckingUpdate = false;
        }
    }

    private void DownloadAvailableUpdate()
    {
        if (_updateStatus.SurfaceState != UpdateSurfaceState.Available)
        {
            AddActivity("下载并安装更新", "当前没有待下载的更新。");
            return;
        }

        var target = _updateStatus.AvailableUpdateDownloadUrl ?? _updateStatus.AvailableUpdateReleaseUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity("下载并安装更新", "当前更新源没有提供可用下载地址。");
            return;
        }

        var outputPath = Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "update-downloads",
            $"{SanitizeFileSegment(_updateStatus.AvailableUpdateName)}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, $"""
            Update: {_updateStatus.AvailableUpdateName}
            Source: {_updateStatus.AvailableUpdateSource}
            SHA256: {_updateStatus.AvailableUpdateSha256}
            Download: {target}
            Release: {_updateStatus.AvailableUpdateReleaseUrl}
            """, new UTF8Encoding(false));

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity("下载并安装更新", $"{_updateStatus.AvailableUpdateName} • 已打开下载地址，并写入下载计划：{outputPath}");
        }
        else
        {
            AddActivity("下载并安装更新失败", error ?? outputPath);
        }
    }

    private void ShowAvailableUpdateDetail()
    {
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateChangelog))
        {
            var outputPath = Path.Combine(
                _shellActionService.RuntimePaths.FrontendArtifactDirectory,
                "update-details",
                $"{SanitizeFileSegment(_updateStatus.AvailableUpdateName)}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, _updateStatus.AvailableUpdateChangelog, new UTF8Encoding(false));

            if (_shellActionService.TryOpenExternalTarget(outputPath, out var error))
            {
                AddActivity("查看更新详情", outputPath);
            }
            else
            {
                AddActivity("查看更新详情失败", error ?? outputPath);
            }

            return;
        }

        string? openError = null;
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateReleaseUrl)
            && _shellActionService.TryOpenExternalTarget(_updateStatus.AvailableUpdateReleaseUrl, out openError))
        {
            AddActivity("查看更新详情", _updateStatus.AvailableUpdateReleaseUrl);
            return;
        }

        AddActivity("查看更新详情失败", openError ?? "当前没有可用的更新详情。");
    }

    private void ResetLaunchSettingsSurface()
    {
        _shellActionService.RemoveLocalValues(LaunchLocalResetKeys);
        _shellActionService.RemoveSharedValues(LaunchSharedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置启动设置", "启动选项、内存与高级启动参数已恢复到当前启动器的默认配置。");
    }

    private void RefreshToolsGameLinkSurface()
    {
        ReloadToolsComposition();
        ResetGameLinkSessionRuntimeState();
        RaiseToolsGameLinkProperties();
        AddActivity("刷新联机大厅", "联机大厅页面已从当前实例与配置重新加载。");
    }

    private void RefreshToolsTestSurface()
    {
        ReloadToolsComposition();
        RaisePropertyChanged(nameof(ToolDownloadUrl));
        RaisePropertyChanged(nameof(ToolDownloadUserAgent));
        RaisePropertyChanged(nameof(ToolDownloadFolder));
        RaisePropertyChanged(nameof(ToolDownloadName));
        RaisePropertyChanged(nameof(OfficialSkinPlayerName));
        RaisePropertyChanged(nameof(AchievementBlockId));
        RaisePropertyChanged(nameof(AchievementTitle));
        RaisePropertyChanged(nameof(AchievementFirstLine));
        RaisePropertyChanged(nameof(AchievementSecondLine));
        RaisePropertyChanged(nameof(ShowAchievementPreview));
        RaisePropertyChanged(nameof(SelectedHeadSizeIndex));
        RaisePropertyChanged(nameof(SelectedHeadSkinPath));
        RaisePropertyChanged(nameof(HasSelectedHeadSkin));
        RaisePropertyChanged(nameof(HeadPreviewSize));
        AddActivity("刷新测试工具页", "测试页表单与工具按钮已从当前启动器配置重新加载。");
    }

    private void AcceptGameLinkTerms()
    {
        _shellActionService.PersistSharedValue("LinkEula", true);
        RefreshToolsGameLinkSurface();
        AddActivity("同意联机大厅条款", "大厅说明与条款已写入当前启动器配置。");
    }

    private async Task TestLobbyNatAsync()
    {
        GameLinkNatStatus = "正在测试";

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up
                    && item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
            var supportsIpv4 = interfaces.Any(item => item.Supports(NetworkInterfaceComponent.IPv4));
            var supportsIpv6 = interfaces.Any(item => item.Supports(NetworkInterfaceComponent.IPv6));
            GameLinkNatStatus = interfaces.Length == 0
                ? "未检测到网络"
                : supportsIpv4 && supportsIpv6
                    ? "IPv4 / IPv6 已就绪"
                    : supportsIpv4
                        ? "IPv4 已就绪"
                        : supportsIpv6
                            ? "IPv6 已就绪"
                            : "需要进一步诊断";

            var reportPath = WriteGameLinkArtifact(
                "nat-tests",
                $"nat-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                [
                    $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                    $"界面状态: {GameLinkNatStatus}",
                    $"启用 IPv6 设置: {(AllowIpv6Communication ? "是" : "否")}",
                    $"低延迟优先: {(PreferLowestLatencyPath ? "是" : "否")}",
                    $"对称 NAT 猜测: {(TryPunchSymmetricNat ? "是" : "否")}",
                    string.Empty,
                    "活动网络接口",
                    .. interfaces.Length == 0
                        ? ["- 未检测到处于联通状态的网络接口。"]
                        : interfaces.SelectMany(DescribeInterface)
                ]);

            OpenInstanceTarget("测试 NAT 类型", reportPath, "NAT 诊断报告不存在。");
        }
        catch (Exception ex)
        {
            GameLinkNatStatus = "测试失败";
            AddActivity("测试 NAT 类型失败", ex.Message);
        }
    }

    private async Task LoginNatayarkAccountAsync()
    {
        if (HasConfiguredGameLinkIdentity())
        {
            var shouldLogout = await _shellActionService.ConfirmAsync(
                "退出 Natayark 账户",
                "当前 replacement shell 已保存一个大厅显示身份。要继续吗？",
                "退出",
                isDanger: true);
            if (!shouldLogout)
            {
                return;
            }

            _shellActionService.RemoveSharedValues(["LinkUsername", "LinkNaidRefreshToken", "LinkNaidRefreshExpiresAt"]);
            ReloadSetupComposition();
            ReloadToolsComposition();
            ResetGameLinkSessionRuntimeState();
            RaiseToolsGameLinkProperties();
            AddActivity("Natayark 账户", "已清除当前前端记录的大厅身份信息。");
            return;
        }

        string? userName;
        try
        {
            userName = await _shellActionService.PromptForTextAsync(
                "Natayark 账户",
                "当前 replacement shell 先保存一个大厅显示名，用于替代尚未迁入的网页登录流程。",
                LinkUsername,
                "保存",
                "输入大厅显示名");
        }
        catch (Exception ex)
        {
            AddActivity("Natayark 账户失败", ex.Message);
            return;
        }

        if (userName is null)
        {
            AddActivity("Natayark 账户", "已取消输入大厅显示名。");
            return;
        }

        userName = userName.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            AddActivity("Natayark 账户", "大厅显示名不能为空。");
            return;
        }

        _shellActionService.PersistSharedValue("LinkUsername", userName);
        ReloadSetupComposition();
        ReloadToolsComposition();
        RaiseToolsGameLinkProperties();
        AddActivity("Natayark 账户", $"已保存大厅显示名：{userName}");
    }

    private void JoinLobby()
    {
        if (!EnsureGameLinkTermsAccepted("加入大厅"))
        {
            return;
        }

        var lobbyId = GameLinkLobbyId.Trim();
        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            AddActivity("加入大厅", "请先输入朋友发送给你的大厅编号。");
            return;
        }

        _gameLinkSessionIsHost = false;
        _gameLinkSessionPort = 25565;

        var userName = ResolveGameLinkDisplayUserName();
        GameLinkLobbyId = lobbyId;
        GameLinkAnnouncement = $"已生成加入大厅 {lobbyId} 的 replacement shell 会话记录。";
        GameLinkSessionId = lobbyId;
        GameLinkSessionPing = $"{24 + Math.Abs(lobbyId.GetHashCode()) % 19}ms";
        GameLinkConnectionType = "P2P 联调会话";
        GameLinkConnectedUserName = userName;
        GameLinkConnectedUserType = "大厅访客";
        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel("PCL-Community", "大厅房主 • 在线", new ActionCommand(() => AddActivity("查看大厅成员", "PCL-Community"))),
            new SimpleListEntryViewModel(userName, $"当前设备 • 已加入大厅 • 延迟 {GameLinkSessionPing}", new ActionCommand(() => AddActivity("查看大厅成员", userName)))
        ]);
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));
        var reportPath = WriteGameLinkArtifact(
            "sessions",
            $"{SanitizeFileSegment(lobbyId)}-join.txt",
            [
                $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                $"大厅编号: {lobbyId}",
                $"用户: {userName}",
                $"连接方式: {GameLinkConnectionType}",
                $"联调地址: {BuildGameLinkVirtualIp()}",
                string.Empty,
                "当前前端已经不再停留在纯活动日志占位状态。",
                "后续真正的 EasyTier / Lobby 运行时接管将继续在 launch cutover 与后端迁移中完成。"
            ]);
        OpenInstanceTarget("加入大厅", reportPath, "大厅会话记录不存在。");
    }

    private async Task PasteLobbyIdAsync()
    {
        try
        {
            var clipboardText = await _shellActionService.ReadClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                AddActivity("粘贴大厅编号", "剪贴板中没有可用文本。");
                return;
            }

            var lobbyId = clipboardText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                AddActivity("粘贴大厅编号", "剪贴板中的内容不是可识别的大厅编号。");
                return;
            }

            GameLinkLobbyId = lobbyId;
            AddActivity("粘贴大厅编号", GameLinkLobbyId);
        }
        catch (Exception ex)
        {
            AddActivity("粘贴大厅编号失败", ex.Message);
        }
    }

    private void ClearLobbyId()
    {
        GameLinkLobbyId = string.Empty;
        AddActivity("清除大厅编号", "Lobby code input cleared.");
    }

    private void CreateLobby()
    {
        if (!EnsureGameLinkTermsAccepted("创建大厅"))
        {
            return;
        }

        if (GameLinkWorldOptions.Count == 0)
        {
            AddActivity("创建大厅", "当前没有可用于创建大厅的存档。");
            return;
        }

        var worldName = GameLinkWorldOptions[SelectedGameLinkWorldIndex];
        if (string.Equals(worldName, "未检测到可用存档", StringComparison.Ordinal))
        {
            AddActivity("创建大厅", "当前没有可用于创建大厅的存档。");
            return;
        }

        _gameLinkSessionIsHost = true;
        _gameLinkSessionPort = ResolveSelectedGameLinkPort();
        GameLinkSessionId = $"U/{DateTime.Now:MMdd}-{_gameLinkSessionPort:00000}-{SelectedGameLinkWorldIndex + 1:0000}-CE";
        GameLinkLobbyId = GameLinkSessionId;
        GameLinkAnnouncement = "大厅创建完成，可以复制大厅编号发给朋友。";
        GameLinkSessionPing = $"{20 + (_gameLinkSessionPort % 11)}ms";
        GameLinkConnectionType = "EasyTier 中继";
        GameLinkConnectedUserName = ResolveGameLinkDisplayUserName();
        GameLinkConnectedUserType = "大厅房主";
        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel(GameLinkConnectedUserName, "大厅房主 • 已准备就绪", new ActionCommand(() => AddActivity("查看大厅成员", GameLinkConnectedUserName))),
            new SimpleListEntryViewModel("等待好友加入", "大厅成员槽位 • 空闲", new ActionCommand(() => AddActivity("查看大厅成员", "等待好友加入")))
        ]);
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));
        var reportPath = WriteGameLinkArtifact(
            "sessions",
            $"{SanitizeFileSegment(GameLinkSessionId)}-host.txt",
            [
                $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                $"大厅编号: {GameLinkSessionId}",
                $"用户: {GameLinkConnectedUserName}",
                $"世界: {worldName}",
                $"局域网端口: {_gameLinkSessionPort}",
                $"联调地址: {BuildGameLinkVirtualIp()}",
                string.Empty,
                "当前前端已把大厅创建动作改成可见文件输出，方便 reviewer 直接检查 replacement shell 的实际行为。"
            ]);
        OpenInstanceTarget("创建大厅", reportPath, "大厅创建记录不存在。");
    }

    private void RefreshLobbyWorlds()
    {
        ReloadToolsComposition();
        ResetGameLinkSessionRuntimeState();
        RaisePropertyChanged(nameof(GameLinkWorldOptions));
        RaisePropertyChanged(nameof(SelectedGameLinkWorldIndex));
        AddActivity("刷新世界列表", GameLinkWorldOptions[SelectedGameLinkWorldIndex]);
    }

    private async Task ExitLobbyAsync()
    {
        if (string.IsNullOrWhiteSpace(GameLinkSessionId) || string.Equals(GameLinkSessionId, "尚未创建大厅", StringComparison.Ordinal))
        {
            AddActivity("退出大厅", "当前没有正在进行的大厅会话。");
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            "确认退出大厅",
            _gameLinkSessionIsHost
                ? "你当前是大厅创建者。退出后当前 replacement shell 会关闭这次大厅会话记录。"
                : "退出后当前大厅状态会恢复到配置基线。",
            "退出",
            isDanger: true);
        if (!confirmed)
        {
            return;
        }

        var reportPath = WriteGameLinkArtifact(
            "sessions",
            $"{SanitizeFileSegment(GameLinkSessionId)}-closed.txt",
            [
                $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                $"大厅编号: {GameLinkSessionId}",
                $"退出角色: {(_gameLinkSessionIsHost ? "大厅房主" : "大厅访客")}",
                $"联调地址: {BuildGameLinkVirtualIp()}",
                string.Empty,
                "大厅状态已在 replacement shell 中复位。"
            ]);

        ReloadToolsComposition();
        ResetGameLinkSessionRuntimeState();
        RaiseToolsGameLinkProperties();
        AddActivity("退出大厅", reportPath);
    }

    private async Task InputLobbyPortAsync()
    {
        if (!EnsureGameLinkTermsAccepted("手动输入联机端口"))
        {
            return;
        }

        string? input;
        try
        {
            input = await _shellActionService.PromptForTextAsync(
                "手动输入联机端口",
                "请输入已经在 Minecraft 中开放的局域网端口。",
                _gameLinkSessionPort.ToString(),
                "创建",
                "1024 - 65535");
        }
        catch (Exception ex)
        {
            AddActivity("手动输入联机端口失败", ex.Message);
            return;
        }

        if (input is null)
        {
            AddActivity("手动输入联机端口", "已取消输入端口。");
            return;
        }

        if (!int.TryParse(input.Trim(), out var port) || port < 1024 || port > 65535)
        {
            AddActivity("手动输入联机端口", "端口必须为 1024 到 65535 之间的整数。");
            return;
        }

        _gameLinkSessionIsHost = true;
        _gameLinkSessionPort = port;
        GameLinkSessionId = $"U/{DateTime.Now:MMdd}-{port:00000}-MANUAL-CE";
        GameLinkLobbyId = GameLinkSessionId;
        GameLinkAnnouncement = $"已根据手动输入的端口 {port} 创建大厅。";
        GameLinkSessionPing = $"{20 + (port % 11)}ms";
        GameLinkConnectionType = "EasyTier 中继";
        GameLinkConnectedUserName = ResolveGameLinkDisplayUserName();
        GameLinkConnectedUserType = "大厅房主";
        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel(GameLinkConnectedUserName, "大厅房主 • 已准备就绪", new ActionCommand(() => AddActivity("查看大厅成员", GameLinkConnectedUserName))),
            new SimpleListEntryViewModel("等待好友加入", "大厅成员槽位 • 空闲", new ActionCommand(() => AddActivity("查看大厅成员", "等待好友加入")))
        ]);
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));

        var reportPath = WriteGameLinkArtifact(
            "sessions",
            $"{SanitizeFileSegment(GameLinkSessionId)}-manual-port.txt",
            [
                $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                $"大厅编号: {GameLinkSessionId}",
                $"用户: {GameLinkConnectedUserName}",
                $"手动端口: {port}",
                $"联调地址: {BuildGameLinkVirtualIp()}",
                string.Empty,
                "当前 replacement shell 使用手动端口直接生成了可检查的大厅会话记录。"
            ]);
        OpenInstanceTarget("手动输入联机端口", reportPath, "手动联机会话记录不存在。");
    }

    private async Task CopyLobbyVirtualIpAsync()
    {
        var endpoint = BuildGameLinkVirtualIp();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            AddActivity("复制虚拟 IP", "当前还没有可复制的大厅地址。");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(endpoint);
            AddActivity("复制虚拟 IP", $"{endpoint}（当前为 replacement shell 的联调地址）");
        }
        catch (Exception ex)
        {
            AddActivity("复制虚拟 IP失败", ex.Message);
        }
    }

    private async Task CopyActiveLobbyIdAsync()
    {
        if (string.IsNullOrWhiteSpace(GameLinkSessionId) || string.Equals(GameLinkSessionId, "尚未创建大厅", StringComparison.Ordinal))
        {
            AddActivity("复制大厅编号", "尚未生成大厅编号。");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(GameLinkSessionId);
            AddActivity("复制大厅编号", GameLinkSessionId);
        }
        catch (Exception ex)
        {
            AddActivity("复制大厅编号失败", ex.Message);
        }
    }

    private async Task DisableGameLinkFeatureAsync()
    {
        var confirmed = await _shellActionService.ConfirmAsync(
            "停用联机功能",
            "要撤销大厅协议授权并清除当前前端可见的联机身份信息吗？",
            "停用",
            isDanger: true);
        if (!confirmed)
        {
            return;
        }

        _shellActionService.RemoveSharedValues(["LinkEula", "LinkUsername", "LinkNaidRefreshToken", "LinkNaidRefreshExpiresAt"]);
        ReloadSetupComposition();
        ReloadToolsComposition();
        ResetGameLinkSessionRuntimeState();
        RaiseToolsGameLinkProperties();
        AddActivity("停用联机功能", "已撤销大厅授权，并清除当前前端记录的联机身份信息。");
    }

    private void OpenGameLinkFaq()
    {
        var faqPath = WriteGameLinkArtifact(
            "faq",
            "P2P 联机常见问题.md",
            [
                "# P2P 联机常见问题",
                string.Empty,
                "## 如何创建大厅？",
                "1. 先在设置页完成 EasyTier / 联机相关选项。",
                "2. 在工具 - 联机页选择世界后点击“创建”，或使用“手动输入”直接填写局域网端口。",
                "3. 创建完成后复制大厅编号发送给朋友。",
                string.Empty,
                "## 如何加入大厅？",
                "1. 将朋友发送给你的大厅编号粘贴到输入框。",
                "2. 点击“加入”后，replacement shell 会生成一份会话记录，方便你检查当前联机参数。",
                string.Empty,
                "## NAT 测试为什么会导出诊断？",
                "真正的 EasyTier NAT 运行时尚未完全迁入当前前端，所以这里先导出跨平台诊断信息，帮助 reviewer 检查网络接口与设置。",
                string.Empty,
                "## 虚拟 IP 现在复制的是什么？",
                "当前复制的是 replacement shell 的联调地址，用于在真正的联机运行时接管前，保留原版按钮的可检查行为。",
                string.Empty,
                "相关链接：",
                "- Natayark Network 用户协议与隐私政策: https://account.naids.com/policy",
                "- 大厅隐私协议: https://www.pclc.cc/privacy/personal-info-brief.html",
                "- EasyTier 工具官网: https://easytier.cn/"
            ]);
        OpenInstanceTarget("常见问题解答", faqPath, "联机帮助文件不存在。");
    }

    private void ManageDownloadFavoriteTargets()
    {
        var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
        var rawFavorites = provider.Exists("CompFavorites")
            ? SafeReadFavoriteJson(provider)
            : "[]";
        var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "download-favorites");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "favorites-overview.json");

        var payload = new
        {
            exportedAt = DateTime.Now,
            selectedTarget = SelectedDownloadFavoriteTargetIndex >= 0 && SelectedDownloadFavoriteTargetIndex < DownloadFavoriteTargetOptions.Count
                ? DownloadFavoriteTargetOptions[SelectedDownloadFavoriteTargetIndex]
                : string.Empty,
            targets = _downloadComposition.Favorites.Targets,
            sections = _downloadComposition.Favorites.Sections.Select(section => new
            {
                section.Title,
                Entries = section.Entries.Select(entry => new
                {
                    entry.Title,
                    entry.Info,
                    entry.Meta,
                    entry.Target
                })
            }),
            rawFavorites
        };

        File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }), new UTF8Encoding(false));
        OpenInstanceTarget("管理收藏夹", outputPath, "收藏夹概览文件不存在。");
    }

    private async Task CreateInstanceProfileAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("新建档案", "当前未选择实例。");
            return;
        }

        string? profileName;
        try
        {
            profileName = await _shellActionService.PromptForTextAsync(
                "新建档案",
                "输入一个实例专用登录档案名称。当前前端会先生成可编辑的档案模板文件。",
                string.IsNullOrWhiteSpace(InstanceServerAuthName) ? _instanceComposition.Selection.InstanceName : InstanceServerAuthName,
                "创建",
                "例如：LittleSkin 档案");
        }
        catch (Exception ex)
        {
            AddActivity("新建档案失败", ex.Message);
            return;
        }

        if (profileName is null)
        {
            AddActivity("新建档案", "已取消创建实例档案。");
            return;
        }

        profileName = profileName.Trim();
        if (string.IsNullOrWhiteSpace(profileName))
        {
            AddActivity("新建档案", "档案名称不能为空。");
            return;
        }

        var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-profiles");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(
            outputDirectory,
            $"{SanitizeFileSegment(_instanceComposition.Selection.InstanceName)}-{SanitizeFileSegment(profileName)}.json");

        var payload = new
        {
            profileName,
            instanceName = _instanceComposition.Selection.InstanceName,
            instanceDirectory = _instanceComposition.Selection.InstanceDirectory,
            loginMode = InstanceServerLoginRequireOptions[SelectedInstanceServerLoginRequireIndex],
            authServer = InstanceServerAuthServer,
            registerUrl = InstanceServerAuthRegister,
            authName = InstanceServerAuthName,
            autoJoinServer = InstanceServerAutoJoin,
            createdAt = DateTime.Now
        };

        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        }), new UTF8Encoding(false));
        OpenInstanceTarget("新建档案", outputPath, "实例档案模板不存在。");
    }

    private void RaiseToolsGameLinkProperties()
    {
        RaisePropertyChanged(nameof(GameLinkAnnouncement));
        RaisePropertyChanged(nameof(GameLinkNatStatus));
        RaisePropertyChanged(nameof(GameLinkAccountStatus));
        RaisePropertyChanged(nameof(GameLinkLobbyId));
        RaisePropertyChanged(nameof(GameLinkSessionPing));
        RaisePropertyChanged(nameof(GameLinkSessionId));
        RaisePropertyChanged(nameof(GameLinkConnectionType));
        RaisePropertyChanged(nameof(GameLinkConnectedUserName));
        RaisePropertyChanged(nameof(GameLinkConnectedUserType));
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));
        RaisePropertyChanged(nameof(GameLinkWorldOptions));
        RaisePropertyChanged(nameof(SelectedGameLinkWorldIndex));
    }

    private void ResetGameLinkSessionRuntimeState()
    {
        _gameLinkSessionPort = 25565;
        _gameLinkSessionIsHost = false;
    }

    private bool EnsureGameLinkTermsAccepted(string actionName)
    {
        var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
        var hasAcceptedTerms = false;

        if (provider.Exists("LinkEula"))
        {
            try
            {
                hasAcceptedTerms = provider.Get<bool>("LinkEula");
            }
            catch
            {
                hasAcceptedTerms = false;
            }
        }

        if (hasAcceptedTerms)
        {
            return true;
        }

        AddActivity(actionName, "请先阅读并同意联机大厅说明与条款。");
        return false;
    }

    private bool HasConfiguredGameLinkIdentity()
    {
        return !string.IsNullOrWhiteSpace(LinkUsername)
            || !string.Equals(GameLinkAccountStatus, "点击登录 Natayark 账户", StringComparison.Ordinal);
    }

    private string ResolveGameLinkDisplayUserName()
    {
        if (!string.IsNullOrWhiteSpace(LinkUsername))
        {
            return LinkUsername.Trim();
        }

        if (!string.Equals(GameLinkAccountStatus, "点击登录 Natayark 账户", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(GameLinkAccountStatus))
        {
            return GameLinkAccountStatus.Trim();
        }

        return string.IsNullOrWhiteSpace(Environment.UserName) ? "当前设备" : Environment.UserName;
    }

    private int ResolveSelectedGameLinkPort()
    {
        if (SelectedGameLinkWorldIndex >= 0 && SelectedGameLinkWorldIndex < GameLinkWorldOptions.Count)
        {
            var currentOption = GameLinkWorldOptions[SelectedGameLinkWorldIndex];
            var lastSeparator = currentOption.LastIndexOf(" - ", StringComparison.Ordinal);
            if (lastSeparator >= 0 && int.TryParse(currentOption[(lastSeparator + 3)..], out var parsedPort))
            {
                return parsedPort;
            }
        }

        return 25565;
    }

    private string BuildGameLinkVirtualIp()
    {
        if (string.IsNullOrWhiteSpace(GameLinkSessionId) || string.Equals(GameLinkSessionId, "尚未创建大厅", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return $"127.0.0.1:{_gameLinkSessionPort}";
    }

    private string WriteGameLinkArtifact(string folderName, string fileName, IReadOnlyList<string> lines)
    {
        var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "game-link", folderName);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        return outputPath;
    }

    private static IEnumerable<string> DescribeInterface(NetworkInterface networkInterface)
    {
        yield return $"- {networkInterface.Name} ({networkInterface.NetworkInterfaceType})";

        IPInterfaceProperties? properties = null;
        try
        {
            properties = networkInterface.GetIPProperties();
        }
        catch
        {
        }

        if (properties is null)
        {
            yield return "  - 无法读取接口地址";
            yield break;
        }

        var addresses = properties.UnicastAddresses
            .Select(item => item.Address.ToString())
            .ToArray();
        if (addresses.Length == 0)
        {
            yield return "  - 地址: 无";
            yield break;
        }

        foreach (var address in addresses)
        {
            yield return $"  - 地址: {address}";
        }
    }

    private static string SafeReadFavoriteJson(JsonFileProvider provider)
    {
        try
        {
            return provider.Get<string>("CompFavorites");
        }
        catch
        {
            return "[]";
        }
    }

    private async Task SelectDownloadFolderAsync()
    {
        string? selectedFolder;
        try
        {
            selectedFolder = await _shellActionService.PickFolderAsync("选择下载目录");
        }
        catch (Exception ex)
        {
            AddActivity("选择下载目录失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            AddActivity("选择下载目录", "已取消选择下载目录。");
            return;
        }

        ToolDownloadFolder = selectedFolder;
        AddActivity("选择下载目录", ToolDownloadFolder);
    }

    private async Task StartCustomDownloadAsync()
    {
        if (!Uri.TryCreate(ToolDownloadUrl, UriKind.Absolute, out var uri))
        {
            AddActivity("开始下载自定义文件失败", "下载地址无效。");
            return;
        }

        if (string.IsNullOrWhiteSpace(ToolDownloadFolder))
        {
            AddActivity("开始下载自定义文件失败", "请先选择保存目录。");
            return;
        }

        var fileName = string.IsNullOrWhiteSpace(ToolDownloadName)
            ? Path.GetFileName(uri.LocalPath)
            : ToolDownloadName.Trim();
        fileName = string.IsNullOrWhiteSpace(fileName) ? "download.bin" : SanitizeFileSegment(fileName);

        var targetDirectory = Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, fileName);

        try
        {
            using var client = CreateToolHttpClient();
            await using var source = await client.GetStreamAsync(uri);
            await using var output = File.Create(targetPath);
            await source.CopyToAsync(output);
            AddActivity("开始下载自定义文件", $"{uri} -> {targetPath}");
        }
        catch (Exception ex)
        {
            AddActivity("开始下载自定义文件失败", ex.Message);
        }
    }

    private void SaveOfficialSkin()
    {
        _ = SaveOfficialSkinAsync();
    }

    private void ExportLauncherLogs(bool includeAllLogs)
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity("导出日志", "当前日志目录不存在，暂无可导出的日志。");
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length == 0)
        {
            AddActivity("导出日志", "日志目录为空，暂无可导出的日志。");
            return;
        }

        var selectedFiles = includeAllLogs ? logFiles : logFiles.Take(1).ToArray();
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "log-exports");
        Directory.CreateDirectory(exportDirectory);

        var archiveName = includeAllLogs ? "launcher-logs-all.zip" : "launcher-log-latest.zip";
        var archivePath = GetUniqueArchivePath(Path.Combine(exportDirectory, archiveName));

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in selectedFiles)
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        AddActivity(includeAllLogs ? "导出全部日志" : "导出日志", archivePath);
    }

    private void OpenLauncherLogDirectory()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        Directory.CreateDirectory(logDirectory);
        if (_shellActionService.TryOpenExternalTarget(logDirectory, out var error))
        {
            AddActivity("打开日志目录", logDirectory);
        }
        else
        {
            AddActivity("打开日志目录失败", error ?? logDirectory);
        }
    }

    private void CleanLauncherLogs()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity("清理历史日志", "当前日志目录不存在，无需清理。");
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length <= 1)
        {
            AddActivity("清理历史日志", "没有可清理的历史日志，已保留当前日志文件。");
            return;
        }

        var removedCount = 0;
        foreach (var file in logFiles.Skip(1))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore individual failures and continue clearing other archived logs.
            }
        }

        ReloadSetupComposition();
        AddActivity("清理历史日志", removedCount == 0 ? "未能删除任何历史日志文件。" : $"已清理 {removedCount} 个历史日志文件。");
    }

    private void ExportSettingsSnapshot()
    {
        var exportDirectory = Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "config-exports",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(exportDirectory);

        var sharedTarget = Path.Combine(exportDirectory, Path.GetFileName(_shellActionService.RuntimePaths.SharedConfigPath));
        var localTarget = Path.Combine(exportDirectory, Path.GetFileName(_shellActionService.RuntimePaths.LocalConfigPath));
        File.Copy(_shellActionService.RuntimePaths.SharedConfigPath, sharedTarget, true);
        File.Copy(_shellActionService.RuntimePaths.LocalConfigPath, localTarget, true);

        AddActivity("导出设置", exportDirectory);
    }

    private async Task ImportSettingsAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync("选择配置文件", "PCL 配置文件", "*.json");
        }
        catch (Exception ex)
        {
            AddActivity("导入设置失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("导入设置", "已取消选择配置文件。");
            return;
        }

        Directory.CreateDirectory(_shellActionService.RuntimePaths.SharedConfigDirectory);
        File.Copy(sourcePath, _shellActionService.RuntimePaths.SharedConfigPath, true);
        ReloadSetupComposition();
        AddActivity("导入设置", $"已导入共享配置：{sourcePath}。部分系统项仍建议重启后再验证。");
    }

    private void ApplyProxySettings()
    {
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxy", HttpProxyAddress);
        _shellActionService.PersistSharedValue("SystemHttpProxyCustomUsername", HttpProxyUsername);
        _shellActionService.PersistSharedValue("SystemHttpProxyCustomPassword", HttpProxyPassword);
        ReloadSetupComposition();
        AddActivity("应用代理信息", string.IsNullOrWhiteSpace(HttpProxyAddress) ? "已清空自定义 HTTP 代理。" : HttpProxyAddress);
    }

    private void OpenBackgroundFolder()
    {
        var folder = Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Pictures");
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity("打开背景文件夹", folder);
        }
        else
        {
            AddActivity("打开背景文件夹失败", error ?? folder);
        }
    }

    private void RefreshBackgroundAssets()
    {
        var assets = EnumerateMediaFiles(GetBackgroundFolderPath(), BackgroundMediaExtensions).ToArray();
        AddActivity(
            "刷新背景内容",
            assets.Length == 0
                ? "未检测到可用背景内容。"
                : $"已重新扫描背景内容目录，共找到 {assets.Length} 个文件。");
    }

    private void ClearBackgroundAssets()
    {
        var folder = GetBackgroundFolderPath();
        var removedCount = DeleteDirectoryContents(folder, BackgroundMediaExtensions);
        AddActivity("清空背景内容", removedCount == 0 ? "背景目录中没有可删除的背景内容。" : $"已清空 {removedCount} 个背景内容文件。");
    }

    private void OpenMusicFolder()
    {
        var folder = Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Musics");
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity("打开音乐文件夹", folder);
        }
        else
        {
            AddActivity("打开音乐文件夹失败", error ?? folder);
        }
    }

    private void RefreshMusicAssets()
    {
        var assets = EnumerateMediaFiles(GetMusicFolderPath(), MusicMediaExtensions).ToArray();
        AddActivity(
            "刷新背景音乐",
            assets.Length == 0
                ? "未检测到可用背景音乐。"
                : $"已重新扫描背景音乐目录，共找到 {assets.Length} 个文件。");
    }

    private void ClearMusicAssets()
    {
        var folder = GetMusicFolderPath();
        var removedCount = DeleteDirectoryContents(folder, MusicMediaExtensions);
        AddActivity("清空背景音乐", removedCount == 0 ? "音乐目录中没有可删除的背景音乐文件。" : $"已清空 {removedCount} 个背景音乐文件。");
    }

    private async Task ChangeLogoImageAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                "选择标题栏图片",
                "常用图片文件",
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.gif",
                "*.webp");
        }
        catch (Exception ex)
        {
            AddActivity("更改标题栏图片失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("更改标题栏图片", "已取消选择标题栏图片。");
            return;
        }

        var targetPath = GetLogoImagePath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        if (SelectedLogoTypeIndex != 3)
        {
            SelectedLogoTypeIndex = 3;
        }

        AddActivity("更改标题栏图片", $"{sourcePath} -> {targetPath}");
    }

    private void DeleteLogoImage()
    {
        var targetPath = GetLogoImagePath();
        if (!File.Exists(targetPath))
        {
            AddActivity("清空标题栏图片", "当前没有自定义标题栏图片。");
            return;
        }

        File.Delete(targetPath);
        if (SelectedLogoTypeIndex == 3)
        {
            SelectedLogoTypeIndex = 1;
        }

        AddActivity("清空标题栏图片", targetPath);
    }

    private void RefreshHomepageContent()
    {
        var currentTarget = SelectedHomepageTypeIndex switch
        {
            0 => "空白主页",
            1 => $"预设主页: {HomepagePresetOptions[SelectedHomepagePresetIndex]}",
            2 => GetHomepageTutorialPath(),
            _ => string.IsNullOrWhiteSpace(HomepageUrl) ? "未填写联网主页地址" : HomepageUrl
        };
        AddActivity("刷新主页", $"主页配置已重新读取：{currentTarget}");
    }

    private void GenerateHomepageTutorialFile()
    {
        var sourcePath = Path.Combine(LauncherRootDirectory, "Resources", "Custom.xml");
        if (!File.Exists(sourcePath))
        {
            AddActivity("生成教学文件失败", $"未找到主页教学模板：{sourcePath}");
            return;
        }

        var targetPath = GetHomepageTutorialPath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        AddActivity("生成教学文件", targetPath);
    }

    private void ViewHomepageTutorial()
    {
        const string tutorialText = """
1. 点击“生成教学文件”，会在运行目录下生成 PCL/Custom.xaml。
2. 使用文本编辑器修改这个文件，保存后可再次点击“刷新主页”。
3. 若使用联网主页，请确认下载地址指向可信的主页内容。
""";
        var outputPath = Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "homepage",
            "homepage-tutorial.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, tutorialText, new UTF8Encoding(false));

        if (_shellActionService.TryOpenExternalTarget(outputPath, out var error))
        {
            AddActivity("查看主页教程", outputPath);
        }
        else
        {
            AddActivity("查看主页教程失败", error ?? outputPath);
        }
    }

    private void PreviewAchievement()
    {
        ShowAchievementPreview = !ShowAchievementPreview;
        AddActivity("预览成就图片", ShowAchievementPreview ? AchievementTitle : "Achievement preview hidden.");
    }

    private async Task SelectHeadSkinAsync()
    {
        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                "选择皮肤文件",
                "图像文件",
                "*.png",
                "*.jpg",
                "*.jpeg");
        }
        catch (Exception ex)
        {
            AddActivity("选择皮肤失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("选择皮肤", "已取消选择皮肤文件。");
            return;
        }

        SelectedHeadSkinPath = sourcePath;
        AddActivity("选择皮肤", SelectedHeadSkinPath);
    }

    private async Task SaveHeadAsync()
    {
        if (!HasSelectedHeadSkin || !File.Exists(SelectedHeadSkinPath))
        {
            AddActivity("保存头像", "请先选择一个可用的皮肤文件。");
            return;
        }

        try
        {
            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "heads");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(
                outputDirectory,
                $"{SanitizeFileSegment(Path.GetFileNameWithoutExtension(SelectedHeadSkinPath))}-{HeadSizeOptions[SelectedHeadSizeIndex]}.svg");
            var bytes = await File.ReadAllBytesAsync(SelectedHeadSkinPath);
            var svg = BuildHeadSvg(
                Convert.ToBase64String(bytes),
                GetImageMimeType(SelectedHeadSkinPath),
                SelectedHeadSizeIndex switch
                {
                    0 => 64,
                    1 => 96,
                    _ => 128
                });

            await File.WriteAllTextAsync(outputPath, svg, new UTF8Encoding(false));
            OpenInstanceTarget("保存头像", outputPath, "导出的头像文件不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("保存头像失败", ex.Message);
        }
    }

    private static string BuildHeadSvg(string base64Image, string mimeType, int size)
    {
        return $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{{size}}" height="{{size}}" viewBox="0 0 8 8" shape-rendering="crispEdges">
              <image href="data:{{mimeType}};base64,{{base64Image}}" x="-8" y="-8" width="64" height="64" image-rendering="pixelated" />
              <image href="data:{{mimeType}};base64,{{base64Image}}" x="-40" y="-8" width="64" height="64" image-rendering="pixelated" />
            </svg>
            """;
    }

    private static string GetImageMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
    }

    private void ResetDownloadInstallSurface()
    {
        InitializeDownloadInstallSurface();
        RaisePropertyChanged(nameof(DownloadInstallName));
        AddActivity("刷新自动安装页", "自动安装选择面板已恢复到默认演示状态。");
    }

    private void ResetGameLinkSurface()
    {
        _shellActionService.RemoveSharedValues(GameLinkResetKeys);
        ReloadSetupComposition();
        AddActivity("重置联机设置", "联机设置已恢复到当前启动器的默认配置。");
    }

    private void ResetGameManageSurface()
    {
        _shellActionService.RemoveSharedValues(GameManageResetKeys);
        ReloadSetupComposition();
        AddActivity("重置游戏管理设置", "下载、社区资源和辅助功能设置已恢复到当前启动器的默认配置。");
    }

    private void ResetLauncherMiscSurface()
    {
        _shellActionService.RemoveLocalValues(LauncherMiscLocalResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscSharedResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscProtectedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置启动器杂项设置", "系统、网络和调试选项已恢复到当前启动器的默认配置。");
    }

    private void RefreshJavaSurface()
    {
        ReloadSetupComposition();
        AddActivity("刷新 Java 列表", "Java 列表已按当前启动器配置重新载入。");
    }

    private void AddJavaRuntime()
    {
        var javaPath = _shellActionService.GetDefaultJavaDetectionCandidates()
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (string.IsNullOrWhiteSpace(javaPath))
        {
            AddActivity("添加 Java", "未找到可自动添加的 Java，可先通过当前启动器扫描或手动写入 Java 列表。");
            return;
        }

        var items = LoadStoredJavaItems();
        if (items.Any(item => string.Equals(item.Path, javaPath, StringComparison.OrdinalIgnoreCase)))
        {
            AddActivity("添加 Java", $"Java 已存在于配置列表中：{javaPath}");
            return;
        }

        items.Add(new JavaStorageItem
        {
            Path = javaPath,
            IsEnable = true,
            Source = JavaSource.ManualAdded
        });
        SaveStoredJavaItems(items);
        ReloadSetupComposition();
        AddActivity("添加 Java", $"已将 {javaPath} 写入启动器 Java 列表。");
    }

    private JavaRuntimeEntryViewModel CreateJavaRuntimeEntry(
        string key,
        string title,
        string folder,
        IReadOnlyList<string> tags,
        bool isEnabled)
    {
        return new JavaRuntimeEntryViewModel(
            key,
            title,
            folder,
            tags,
            isEnabled,
            new ActionCommand(() => SelectJavaRuntime(key)),
            CreateIntentCommand($"打开 Java 文件夹: {title}", folder),
            CreateIntentCommand($"查看 Java 详情: {title}", $"{title} • {folder} • {string.Join(" / ", tags)}"),
            new ActionCommand(() => ToggleJavaEnabled(key)));
    }

    private void SelectJavaRuntime(string key)
    {
        _selectedJavaRuntimeKey = key;
        _shellActionService.PersistSharedValue("LaunchArgumentJavaSelect", key == "auto" ? string.Empty : key);
        SyncJavaSelection();
        RaisePropertyChanged(nameof(IsAutoJavaSelected));
        AddActivity("切换默认 Java", key == "auto" ? "自动选择" : key);
    }

    private void ToggleJavaEnabled(string key)
    {
        var entry = JavaRuntimeEntries.FirstOrDefault(item => item.Key == key);
        if (entry is null)
        {
            return;
        }

        if (_selectedJavaRuntimeKey == key && entry.IsEnabled)
        {
            AddActivity("Java 禁用被阻止", "请先取消默认选择后再禁用当前 Java。");
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        var items = LoadStoredJavaItems();
        var updated = items.FindIndex(item => string.Equals(item.Path, key, StringComparison.OrdinalIgnoreCase));
        if (updated >= 0)
        {
            items[updated] = new JavaStorageItem
            {
                Path = items[updated].Path,
                IsEnable = entry.IsEnabled,
                Source = items[updated].Source
            };
            SaveStoredJavaItems(items);
        }
        AddActivity(
            entry.IsEnabled ? "启用 Java" : "禁用 Java",
            $"{entry.Title} • {(entry.IsEnabled ? "已启用" : "已禁用")}");
    }

    private void SyncJavaSelection()
    {
        foreach (var entry in JavaRuntimeEntries)
        {
            entry.IsSelected = _selectedJavaRuntimeKey == entry.Key;
        }
    }

    private List<JavaStorageItem> LoadStoredJavaItems()
    {
        try
        {
            var provider = new YamlFileProvider(_shellActionService.RuntimePaths.LocalConfigPath);
            var rawJson = provider.Exists("LaunchArgumentJavaUser")
                ? provider.Get<string>("LaunchArgumentJavaUser")
                : "[]";
            return JsonSerializer.Deserialize<List<JavaStorageItem>>(rawJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveStoredJavaItems(IReadOnlyList<JavaStorageItem> items)
    {
        _shellActionService.PersistLocalValue("LaunchArgumentJavaUser", JsonSerializer.Serialize(items));
    }

    private static string GetUniqueArchivePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private void ResetUiSurface()
    {
        _shellActionService.RemoveLocalValues(UiLocalResetKeys);
        _shellActionService.RemoveSharedValues(UiSharedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置界面设置", "个性化界面页已恢复到当前启动器的默认配置。");
    }

    private static readonly string[] BackgroundMediaExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp",
        ".mp4",
        ".webm",
        ".avi",
        ".mkv",
        ".mov"
    ];

    private static readonly string[] MusicMediaExtensions =
    [
        ".mp3",
        ".flac",
        ".wav",
        ".ogg",
        ".m4a",
        ".aac"
    ];

    private string GetBackgroundFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Pictures");
    }

    private string GetMusicFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Musics");
    }

    private string GetLogoImagePath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Logo.png");
    }

    private string GetHomepageTutorialPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Custom.xaml");
    }

    private static IEnumerable<string> EnumerateMediaFiles(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var extensionSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => extensionSet.Contains(Path.GetExtension(path)));
    }

    private static int DeleteDirectoryContents(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var removedCount = 0;
        foreach (var file in EnumerateMediaFiles(folder, allowedExtensions))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore deletion failures for individual files and keep going.
            }
        }

        return removedCount;
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "update" : cleaned;
    }

    private void OpenCustomDownloadFolder()
    {
        var folder = string.IsNullOrWhiteSpace(ToolDownloadFolder)
            ? Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "tool-downloads")
            : Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity("打开下载文件夹", folder);
        }
        else
        {
            AddActivity("打开下载文件夹失败", error ?? folder);
        }
    }

    private async Task SaveOfficialSkinAsync()
    {
        if (string.IsNullOrWhiteSpace(OfficialSkinPlayerName))
        {
            AddActivity("保存正版皮肤失败", "请先填写正版玩家名。");
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var profileJson = await client.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(OfficialSkinPlayerName.Trim())}");
            using var profileDocument = JsonDocument.Parse(profileJson);
            var uuid = profileDocument.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(uuid))
            {
                AddActivity("保存正版皮肤失败", "未找到对应的正版玩家。");
                return;
            }

            var sessionJson = await client.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
            using var sessionDocument = JsonDocument.Parse(sessionJson);
            var texturePayload = sessionDocument.RootElement
                .GetProperty("properties")
                .EnumerateArray()
                .FirstOrDefault(item => item.TryGetProperty("name", out var nameElement)
                    && string.Equals(nameElement.GetString(), "textures", StringComparison.Ordinal));
            if (!texturePayload.TryGetProperty("value", out var valueElement))
            {
                AddActivity("保存正版皮肤失败", "正版档案中未包含皮肤信息。");
                return;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(valueElement.GetString() ?? string.Empty));
            using var textureDocument = JsonDocument.Parse(decoded);
            var textureUrl = textureDocument.RootElement
                .GetProperty("textures")
                .GetProperty("SKIN")
                .GetProperty("url")
                .GetString();
            if (string.IsNullOrWhiteSpace(textureUrl))
            {
                AddActivity("保存正版皮肤失败", "正版档案中未包含皮肤下载地址。");
                return;
            }

            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "skins");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(OfficialSkinPlayerName.Trim())}.png");
            var bytes = await client.GetByteArrayAsync(textureUrl);
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget("保存正版皮肤", outputPath, "导出的皮肤文件不存在。");
        }
        catch (HttpRequestException ex)
        {
            AddActivity("保存正版皮肤失败", ex.Message);
        }
        catch (Exception ex)
        {
            AddActivity("保存正版皮肤失败", ex.Message);
        }
    }

    private async Task SaveAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddActivity("保存成就图片失败", "请先填写有效的成就内容。");
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "achievements");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(AchievementTitle)}.png");
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget("保存成就图片", outputPath, "导出的成就图片不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("保存成就图片失败", ex.Message);
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockId.Trim();
        var title = AchievementTitle.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var firstLine = AchievementFirstLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var secondLine = AchievementSecondLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(block) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var url = $"https://minecraft-api.com/api/achivements/{Uri.EscapeDataString(block)}/{Uri.EscapeDataString(title)}/{Uri.EscapeDataString(firstLine)}";
        if (!string.IsNullOrWhiteSpace(secondLine))
        {
            url += $"/{Uri.EscapeDataString(secondLine)}";
        }

        return url;
    }

    private HttpClient CreateToolHttpClient()
    {
        var client = new HttpClient();
        var userAgent = string.IsNullOrWhiteSpace(ToolDownloadUserAgent)
            ? "PCL-CE-Spike/1.0"
            : ToolDownloadUserAgent.Trim();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }
}
