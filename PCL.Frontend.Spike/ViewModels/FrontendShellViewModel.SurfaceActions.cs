using System.Text.Json;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft.Java;

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
            CycleUpdateSurfaceState();
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

    private void CycleUpdateSurfaceState()
    {
        _updateSurfaceState = _updateSurfaceState switch
        {
            UpdateSurfaceState.Available => UpdateSurfaceState.Latest,
            _ => UpdateSurfaceState.Available
        };

        RaiseUpdateSurfaceProperties();
        AddActivity(
            "刷新更新页",
            _updateSurfaceState == UpdateSurfaceState.Available
                ? "检测到可用的新版本，右侧面板切换为更新摘要卡。"
                : "当前已是最新版本，右侧面板切换为本地版本状态卡。");
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
        InitializeToolsGameLinkSurface();
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
        RaisePropertyChanged(nameof(SelectedGameLinkWorldIndex));
        AddActivity("刷新联机大厅", "联机大厅页面已恢复到初始演示状态。");
    }

    private void RefreshToolsTestSurface()
    {
        InitializeToolsTestSurface();
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
        AddActivity("刷新测试工具页", "测试页表单与工具按钮已恢复到默认演示状态。");
    }

    private void AcceptGameLinkTerms()
    {
        GameLinkAnnouncement = "已同意说明与条款，可以继续加入或创建大厅。";
        AddActivity("同意联机大厅条款", "大厅说明与条款已确认。");
    }

    private void TestLobbyNat()
    {
        GameLinkNatStatus = GameLinkNatStatus == "点击测试" ? "Port Restricted Cone NAT" : "点击测试";
        AddActivity("测试 NAT 类型", GameLinkNatStatus);
    }

    private void LoginNatayarkAccount()
    {
        GameLinkAccountStatus = GameLinkAccountStatus == "点击登录 Natayark 账户"
            ? "PCL-Community"
            : "点击登录 Natayark 账户";
        GameLinkConnectedUserName = GameLinkAccountStatus == "点击登录 Natayark 账户" ? "未登录" : "PCL-Community";
        GameLinkConnectedUserType = GameLinkAccountStatus == "点击登录 Natayark 账户" ? "大厅访客" : "大厅房主";
        AddActivity("Natayark 账户", GameLinkAccountStatus);
    }

    private void JoinLobby()
    {
        var lobbyId = string.IsNullOrWhiteSpace(GameLinkLobbyId) ? "U/2398-AX4A-SSSS-EEEE" : GameLinkLobbyId;
        GameLinkLobbyId = lobbyId;
        GameLinkAnnouncement = $"正在准备加入大厅 {lobbyId}……";
        GameLinkSessionId = lobbyId;
        GameLinkSessionPing = "28ms";
        GameLinkConnectionType = "P2P 直连";
        GameLinkConnectedUserName = GameLinkAccountStatus == "点击登录 Natayark 账户" ? "PCL-Community" : GameLinkAccountStatus;
        GameLinkConnectedUserType = "大厅访客";
        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel("PCL-Community", "大厅房主 • 在线", new ActionCommand(() => AddActivity("查看大厅成员", "PCL-Community"))),
            new SimpleListEntryViewModel("当前设备", "已加入大厅 • 延迟 28ms", new ActionCommand(() => AddActivity("查看大厅成员", "当前设备")))
        ]);
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));
        AddActivity("加入大厅", $"Would join lobby {lobbyId}.");
    }

    private void PasteLobbyId()
    {
        GameLinkLobbyId = "U/2398-AX4A-SSSS-EEEE";
        AddActivity("粘贴大厅编号", GameLinkLobbyId);
    }

    private void ClearLobbyId()
    {
        GameLinkLobbyId = string.Empty;
        AddActivity("清除大厅编号", "Lobby code input cleared.");
    }

    private void CreateLobby()
    {
        GameLinkSessionId = $"U/2398-AX4A-SSSS-{SelectedGameLinkWorldIndex + 1:0000}";
        GameLinkLobbyId = GameLinkSessionId;
        GameLinkAnnouncement = "大厅创建完成，可以复制大厅编号发给朋友。";
        GameLinkSessionPing = "24ms";
        GameLinkConnectionType = "EasyTier 中继";
        GameLinkConnectedUserName = GameLinkAccountStatus == "点击登录 Natayark 账户" ? "PCL-Community" : GameLinkAccountStatus;
        GameLinkConnectedUserType = "大厅房主";
        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel(GameLinkConnectedUserName, "大厅房主 • 已准备就绪", new ActionCommand(() => AddActivity("查看大厅成员", GameLinkConnectedUserName))),
            new SimpleListEntryViewModel("等待好友加入", "大厅成员槽位 • 空闲", new ActionCommand(() => AddActivity("查看大厅成员", "等待好友加入")))
        ]);
        RaisePropertyChanged(nameof(GameLinkPlayerListTitle));
        AddActivity("创建大厅", $"Would create a lobby from {GameLinkWorldOptions[SelectedGameLinkWorldIndex]}.");
    }

    private void RefreshLobbyWorlds()
    {
        SelectedGameLinkWorldIndex = (SelectedGameLinkWorldIndex + 1) % GameLinkWorldOptions.Count;
        AddActivity("刷新世界列表", GameLinkWorldOptions[SelectedGameLinkWorldIndex]);
    }

    private void ExitLobby()
    {
        InitializeToolsGameLinkSurface();
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
        RaisePropertyChanged(nameof(SelectedGameLinkWorldIndex));
        AddActivity("退出大厅", "大厅状态已重置为未连接。");
    }

    private void SelectDownloadFolder()
    {
        ToolDownloadFolder = "/Users/demo/Downloads/PCL/custom";
        AddActivity("选择下载目录", ToolDownloadFolder);
    }

    private void StartCustomDownload()
    {
        AddActivity("开始下载自定义文件", $"{ToolDownloadUrl} -> {ToolDownloadFolder}/{ToolDownloadName}");
    }

    private void SaveOfficialSkin()
    {
        AddActivity("保存正版皮肤", $"Would save the skin for {OfficialSkinPlayerName}.");
    }

    private void PreviewAchievement()
    {
        ShowAchievementPreview = !ShowAchievementPreview;
        AddActivity("预览成就图片", ShowAchievementPreview ? AchievementTitle : "Achievement preview hidden.");
    }

    private void SelectHeadSkin()
    {
        SelectedHeadSkinPath = "/Users/demo/Downloads/skin.png";
        AddActivity("选择皮肤", SelectedHeadSkinPath);
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
        var candidates = new[]
        {
            Path.Combine(Environment.GetEnvironmentVariable("JAVA_HOME") ?? string.Empty, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java"),
            OperatingSystem.IsWindows() ? "C:\\Program Files\\Java\\bin\\java.exe" : "/usr/bin/java"
        };

        var javaPath = candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
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

    private void ResetUiSurface()
    {
        _shellActionService.RemoveLocalValues(UiLocalResetKeys);
        _shellActionService.RemoveSharedValues(UiSharedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置界面设置", "个性化界面页已恢复到当前启动器的默认配置。");
    }
}
