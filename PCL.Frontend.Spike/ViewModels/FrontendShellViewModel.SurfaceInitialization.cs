using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.ViewModels.ShellPanes;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InitializeAboutEntries()
    {
        ReplaceItems(AboutProjectEntries,
        [
            new AboutEntryViewModel(
                "龙腾猫跃",
                "Plain Craft Launcher 的原作者！",
                LoadLauncherBitmap("Images", "Heads", "LTCat.jpg"),
                "赞助原作者",
                CreateLinkCommand("赞助原作者", "https://ifdian.net/a/LTCat")),
            new AboutEntryViewModel(
                "PCL Community",
                "Plain Craft Launcher 社区版的开发团队！",
                LoadLauncherBitmap("Images", "Heads", "PCL-Community.png"),
                "GitHub 主页",
                CreateLinkCommand("打开 PCL Community GitHub", "https://github.com/PCL-Community")),
            new AboutEntryViewModel(
                "Plain Craft Launcher 社区版",
                _setupComposition.About.LauncherVersionSummary,
                LoadLauncherBitmap("Images", "Heads", "Logo-CE.png"),
                "查看源代码",
                CreateLinkCommand("查看仓库源代码", "https://github.com/PCL-Community/PCL2-CE"))
        ]);

        ReplaceItems(AboutAcknowledgementEntries,
        [
            new AboutEntryViewModel("bangbang93", "提供 BMCLAPI 镜像源和 Forge 安装工具。", LoadLauncherBitmap("Images", "Heads", "bangbang93.png"), "赞助镜像源", CreateLinkCommand("赞助 BMCLAPI 镜像源", "https://afdian.com/a/bangbang93")),
            new AboutEntryViewModel("MC 百科", "提供了 Mod 名称的中文翻译和更多相关信息！", LoadLauncherBitmap("Images", "Heads", "wiki.png"), "打开百科", CreateLinkCommand("打开 MC 百科", "https://www.mcmod.cn")),
            new AboutEntryViewModel("Pysio @ Akaere Network", "提供了 PCL CE 的相关云服务", LoadLauncherBitmap("Images", "Heads", "Pysio.jpg"), "转到博客", CreateLinkCommand("打开 Pysio 博客", "https://www.pysio.online")),
            new AboutEntryViewModel("云默安 @ 至远光辉", "提供了 PCL CE 的相关云服务", LoadLauncherBitmap("Images", "Heads", "Yunmoan.jpg"), "打开网站", CreateLinkCommand("打开至远光辉", "https://www.zyghit.cn")),
            new AboutEntryViewModel("EasyTier", "提供了联机模块", LoadLauncherBitmap("Images", "Heads", "EasyTier.png"), "打开网站", CreateLinkCommand("打开 EasyTier", "https://easytier.cn")),
            new AboutEntryViewModel("z0z0r4", "提供了 MCIM 中国 Mod 下载镜像源和帮助库图床！", LoadLauncherBitmap("Images", "Heads", "z0z0r4.png"), null, null),
            new AboutEntryViewModel("00ll00", "提供了 Java Launch Wrapper 和一些重要服务支持！", LoadLauncherBitmap("Images", "Heads", "00ll00.png"), null, null),
            new AboutEntryViewModel("Patrick", "设计并制作了 PCL 图标，让龙猫从做图标的水深火热中得到了解脱……", LoadLauncherBitmap("Images", "Heads", "Patrick.png"), null, null),
            new AboutEntryViewModel("Hao_Tian", "在 PCL 内测中找出了一大堆没人想得到的诡异 Bug，有非同寻常的 Bug 体质", LoadLauncherBitmap("Images", "Heads", "Hao_Tian.jpg"), null, null),
            new AboutEntryViewModel("Minecraft 中文论坛", "虽然已经关站了，但感谢此前提供了 MCBBS 镜像源……", LoadLauncherBitmap("Images", "Heads", "MCBBS.png"), null, null),
            new AboutEntryViewModel("PCL 内群的各位", "感谢内群的沙雕网友们这么久以来对龙猫和 PCL 的支持与鼓励！", LoadLauncherBitmap("Images", "Heads", "PCL2.png"), null, null)
        ]);
    }

    private void InitializeLogEntries()
    {
        ReplaceItems(LogEntries,
            _setupComposition.Log.Entries.Select(entry =>
                CreateSimpleEntry(
                    entry.Title,
                    entry.Summary,
                    CreateOpenTargetCommand($"打开日志: {entry.Title}", entry.Path, entry.Summary))));
    }

    private void InitializeUpdateSurface()
    {
        _selectedUpdateChannelIndex = Math.Clamp(_setupComposition.Update.UpdateChannelIndex, 0, UpdateChannelOptions.Count - 1);
        _selectedUpdateModeIndex = Math.Clamp(_setupComposition.Update.UpdateModeIndex, 0, UpdateModeOptions.Count - 1);
        _mirrorCdk = _setupComposition.Update.MirrorCdk;
    }

    private void InitializeLaunchSettingsSurface()
    {
        _selectedLaunchIsolationIndex = _setupComposition.Launch.IsolationIndex;
        _launchWindowTitle = _setupComposition.Launch.WindowTitle;
        _launchCustomInfo = _setupComposition.Launch.CustomInfo;
        _selectedLaunchVisibilityIndex = _setupComposition.Launch.VisibilityIndex;
        _selectedLaunchPriorityIndex = _setupComposition.Launch.PriorityIndex;
        _selectedLaunchWindowTypeIndex = _setupComposition.Launch.WindowTypeIndex;
        _launchWindowWidth = _setupComposition.Launch.WindowWidth;
        _launchWindowHeight = _setupComposition.Launch.WindowHeight;
        _useAutomaticRamAllocation = _setupComposition.Launch.UseAutomaticRamAllocation;
        _customRamAllocation = _setupComposition.Launch.CustomRamAllocationGb;
        _optimizeMemoryBeforeLaunch = _setupComposition.Launch.OptimizeMemoryBeforeLaunch;
        _isLaunchAdvancedOptionsExpanded = false;
        _selectedLaunchRendererIndex = _setupComposition.Launch.RendererIndex;
        _launchJvmArguments = _setupComposition.Launch.JvmArguments;
        _launchGameArguments = _setupComposition.Launch.GameArguments;
        _launchBeforeCommand = _setupComposition.Launch.BeforeCommand;
        _waitForLaunchBeforeCommand = _setupComposition.Launch.WaitForBeforeCommand;
        _disableJavaLaunchWrapper = _setupComposition.Launch.DisableJavaLaunchWrapper;
        _disableRetroWrapper = _setupComposition.Launch.DisableRetroWrapper;
        _requireDedicatedGpu = _setupComposition.Launch.RequireDedicatedGpu;
        _useJavaExecutable = _setupComposition.Launch.UseJavaExecutable;
        _selectedLaunchMicrosoftAuthIndex = _setupComposition.Launch.MicrosoftAuthIndex;
        _selectedLaunchPreferredIpStackIndex = _setupComposition.Launch.PreferredIpStackIndex;
    }

    private void InitializeToolsGameLinkSurface()
    {
        var gameLinkState = _toolsComposition.GameLink;
        _gameLinkWorldOptions = gameLinkState.WorldOptions;
        _gameLinkAnnouncement = gameLinkState.Announcement;
        _gameLinkNatStatus = gameLinkState.NatStatus;
        _gameLinkAccountStatus = gameLinkState.AccountStatus;
        _gameLinkLobbyId = gameLinkState.LobbyId;
        _gameLinkSessionPing = gameLinkState.SessionPing;
        _gameLinkSessionId = gameLinkState.SessionId;
        _gameLinkConnectionType = gameLinkState.ConnectionType;
        _gameLinkConnectedUserName = gameLinkState.ConnectedUserName;
        _gameLinkConnectedUserType = gameLinkState.ConnectedUserType;
        _selectedGameLinkWorldIndex = Math.Clamp(gameLinkState.SelectedWorldIndex, 0, GameLinkWorldOptions.Count - 1);

        ReplaceItems(
            GameLinkPolicyEntries,
            gameLinkState.PolicyEntries.Select(entry =>
                new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    string.Equals(entry.Title, "PCL CE 大厅相关隐私政策", StringComparison.Ordinal)
                        ? _openLobbyPrivacyPolicyCommand
                        : _openNatayarkPolicyCommand)));

        ReplaceItems(
            GameLinkPlayerEntries,
            gameLinkState.PlayerEntries.Select(entry =>
                new SimpleListEntryViewModel(
                    entry.Title,
                    entry.Summary,
                    new ActionCommand(() => AddActivity("查看大厅成员", entry.Title)))));

        InitializeLobbyRuntimeBridge();
        SyncLobbyRuntimeState(preserveTypedLobbyId: !string.IsNullOrWhiteSpace(_gameLinkLobbyId));
    }

    private void InitializeToolsTestSurface()
    {
        var testState = _toolsComposition.Test;
        _toolDownloadUrl = testState.DownloadUrl;
        _toolDownloadUserAgent = testState.DownloadUserAgent;
        _toolDownloadFolder = testState.DownloadFolder;
        _toolDownloadName = testState.DownloadName;
        _officialSkinPlayerName = testState.OfficialSkinPlayerName;
        _achievementBlockId = testState.AchievementBlockId;
        _achievementTitle = testState.AchievementTitle;
        _achievementFirstLine = testState.AchievementFirstLine;
        _achievementSecondLine = testState.AchievementSecondLine;
        _showAchievementPreview = testState.ShowAchievementPreview;
        _selectedHeadSizeIndex = testState.SelectedHeadSizeIndex;
        _selectedHeadSkinPath = testState.SelectedHeadSkinPath;

        ReplaceItems(ToolboxActions, testState.ToolboxActions.Select(CreateToolboxAction));
    }

    private void InitializeDownloadInstallSurface()
    {
        EnsureDownloadInstallEditableState();
        _downloadInstallName = _downloadComposition.Install.Name;
        DownloadInstallMinecraftVersion = _downloadInstallMinecraftChoice is null
            ? _downloadComposition.Install.MinecraftVersion
            : $"Minecraft {_downloadInstallMinecraftChoice.Version}";
        DownloadInstallMinecraftIcon = LoadLauncherBitmap("Images", "Blocks", _downloadComposition.Install.MinecraftIconName ?? "Grass.png");

        ReplaceItems(
            DownloadInstallHints,
            GetEffectiveInstallHints(isExistingInstance: false).Select(hint =>
                CreateNoticeStrip(hint, "#FFF1EA", "#F1C8B6", "#A94F2B")));

        ReplaceItems(
            DownloadInstallOptions,
            _downloadComposition.Install.Options.Select(option =>
                CreateInstallOptionViewModel(isExistingInstance: false, option.Title, option.IconName)));
    }

    private void RefreshDownloadCatalogSurface()
    {
        DownloadCatalogIntroTitle = string.Empty;
        DownloadCatalogIntroBody = string.Empty;
        ReplaceItems(DownloadCatalogIntroActions, []);
        ReplaceItems(DownloadCatalogSections, []);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadCatalog))
        {
            return;
        }

        if (_downloadComposition.CatalogStates.TryGetValue(_currentRoute.Subpage, out var state))
        {
            SetDownloadCatalogIntro(
                state.IntroTitle,
                state.IntroBody,
                state.Actions.Select(action =>
                    new DownloadCatalogActionViewModel(
                        action.Text,
                        action.IsHighlight ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
                        string.IsNullOrWhiteSpace(action.Target)
                            ? CreateIntentCommand(action.Text, state.IntroTitle)
                            : CreateOpenTargetCommand(action.Text, action.Target, action.Target))).ToArray());
            ReplaceItems(
                DownloadCatalogSections,
                state.Sections.Select(section =>
                    CreateDownloadCatalogSection(
                        section.Title,
                        section.Entries.Select(entry =>
                            new DownloadCatalogEntryViewModel(
                                entry.Title,
                                entry.Info,
                                entry.Meta,
                                entry.ActionText,
                                string.IsNullOrWhiteSpace(entry.Target)
                                    ? CreateIntentCommand($"下载页操作: {entry.Title}", $"{entry.Info} • {entry.Meta}")
                                    : CreateOpenTargetCommand($"打开条目: {entry.Title}", entry.Target, entry.Target))).ToArray())));
        }
    }

    private void RefreshDownloadFavoriteSurface()
    {
        ReplaceItems(DownloadFavoriteSections, []);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadFavorites))
        {
            return;
        }

        ShowDownloadFavoriteWarning = _downloadComposition.Favorites.ShowWarning;
        DownloadFavoriteWarningText = _downloadComposition.Favorites.WarningText;
        var selectedTarget = SelectedDownloadFavoriteTargetIndex >= 0 && SelectedDownloadFavoriteTargetIndex < DownloadFavoriteTargetOptions.Count
            ? DownloadFavoriteTargetOptions[SelectedDownloadFavoriteTargetIndex]
            : string.Empty;
        var sections = _downloadComposition.Favorites.Sections
            .Where(section => string.IsNullOrWhiteSpace(selectedTarget) || string.Equals(section.Title, selectedTarget, StringComparison.OrdinalIgnoreCase))
            .Select(section => new DownloadCatalogSectionViewModel(
                section.Title,
                section.Entries
                    .Where(item =>
                        string.IsNullOrWhiteSpace(DownloadFavoriteSearchQuery)
                        || item.Title.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase)
                        || item.Info.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase)
                        || item.Meta.Contains(DownloadFavoriteSearchQuery, StringComparison.OrdinalIgnoreCase))
                    .Select(item => new DownloadCatalogEntryViewModel(
                        item.Title,
                        item.Info,
                        item.Meta,
                        item.ActionText,
                        string.IsNullOrWhiteSpace(item.Target)
                            ? CreateIntentCommand($"收藏夹条目: {item.Title}", item.Info)
                            : CreateDownloadFavoriteCommand(item)))
                    .ToArray()))
            .Where(section => section.Items.Count > 0)
            .ToArray();

        ReplaceItems(DownloadFavoriteSections, sections);
        RaisePropertyChanged(nameof(HasDownloadFavoriteSections));
        RaisePropertyChanged(nameof(HasNoDownloadFavoriteSections));
    }

    private void InitializeGameLinkSurface()
    {
        _linkUsername = _setupComposition.GameLink.Username;
        _selectedProtocolPreferenceIndex = _setupComposition.GameLink.ProtocolPreferenceIndex;
        _preferLowestLatencyPath = _setupComposition.GameLink.PreferLowestLatencyPath;
        _tryPunchSymmetricNat = _setupComposition.GameLink.TryPunchSymmetricNat;
        _allowIpv6Communication = _setupComposition.GameLink.AllowIpv6Communication;
        _enableLinkCliOutput = _setupComposition.GameLink.EnableCliOutput;
    }

    private void InitializeGameManageSurface()
    {
        _selectedDownloadSourceIndex = _setupComposition.GameManage.DownloadSourceIndex;
        _selectedVersionSourceIndex = _setupComposition.GameManage.VersionSourceIndex;
        _downloadThreadLimit = _setupComposition.GameManage.DownloadThreadLimit;
        _downloadSpeedLimit = _setupComposition.GameManage.DownloadSpeedLimit;
        _autoSelectNewInstance = _setupComposition.GameManage.AutoSelectNewInstance;
        _upgradePartialAuthlib = _setupComposition.GameManage.UpgradePartialAuthlib;
        _selectedCommunityDownloadSourceIndex = _setupComposition.GameManage.CommunityDownloadSourceIndex;
        _selectedFileNameFormatIndex = _setupComposition.GameManage.FileNameFormatIndex;
        _selectedModLocalNameStyleIndex = _setupComposition.GameManage.ModLocalNameStyleIndex;
        _ignoreQuiltLoader = _setupComposition.GameManage.IgnoreQuiltLoader;
        _notifyReleaseUpdates = _setupComposition.GameManage.NotifyReleaseUpdates;
        _notifySnapshotUpdates = _setupComposition.GameManage.NotifySnapshotUpdates;
        _autoSwitchGameLanguageToChinese = _setupComposition.GameManage.AutoSwitchGameLanguageToChinese;
        _detectClipboardResourceLinks = _setupComposition.GameManage.DetectClipboardResourceLinks;
    }

    private void InitializeLauncherMiscSurface()
    {
        _selectedSystemActivityIndex = _setupComposition.LauncherMisc.SystemActivityIndex;
        _animationFpsLimit = _setupComposition.LauncherMisc.AnimationFpsLimit;
        _maxRealTimeLogValue = _setupComposition.LauncherMisc.MaxRealTimeLogValue;
        _disableHardwareAcceleration = _setupComposition.LauncherMisc.DisableHardwareAcceleration;
        _enableTelemetry = _setupComposition.LauncherMisc.EnableTelemetry;
        _enableDoH = _setupComposition.LauncherMisc.EnableDoH;
        _selectedHttpProxyTypeIndex = _setupComposition.LauncherMisc.HttpProxyTypeIndex;
        _httpProxyAddress = _setupComposition.LauncherMisc.HttpProxyAddress;
        _httpProxyUsername = _setupComposition.LauncherMisc.HttpProxyUsername;
        _httpProxyPassword = _setupComposition.LauncherMisc.HttpProxyPassword;
        _debugAnimationSpeed = _setupComposition.LauncherMisc.DebugAnimationSpeed;
        _skipCopyDuringDownload = _setupComposition.LauncherMisc.SkipCopyDuringDownload;
        _debugModeEnabled = _setupComposition.LauncherMisc.DebugModeEnabled;
        _debugDelayEnabled = _setupComposition.LauncherMisc.DebugDelayEnabled;
    }

    private void InitializeJavaSurface()
    {
        _selectedJavaRuntimeKey = _setupComposition.Java.SelectedRuntimeKey;
        ReplaceItems(JavaRuntimeEntries,
            _setupComposition.Java.Entries.Select(entry =>
                CreateJavaRuntimeEntry(
                    entry.Key,
                    entry.Title,
                    entry.Folder,
                    entry.Tags,
                    entry.IsEnabled)));
        SyncJavaSelection();
    }

    private void InitializeUiSurface()
    {
        _selectedDarkModeIndex = _setupComposition.Ui.DarkModeIndex;
        _selectedLightColorIndex = _setupComposition.Ui.LightColorIndex;
        _selectedDarkColorIndex = _setupComposition.Ui.DarkColorIndex;
        _launcherOpacity = _setupComposition.Ui.LauncherOpacity;
        _showLauncherLogo = _setupComposition.Ui.ShowLauncherLogo;
        _lockWindowSize = _setupComposition.Ui.LockWindowSize;
        _showLaunchingHint = _setupComposition.Ui.ShowLaunchingHint;
        _enableAdvancedMaterial = _setupComposition.Ui.EnableAdvancedMaterial;
        _blurRadius = _setupComposition.Ui.BlurRadius;
        _blurSamplingRate = _setupComposition.Ui.BlurSamplingRate;
        _selectedBlurTypeIndex = _setupComposition.Ui.BlurTypeIndex;
        _selectedGlobalFontIndex = _setupComposition.Ui.GlobalFontIndex;
        _selectedMotdFontIndex = _setupComposition.Ui.MotdFontIndex;
        _autoPauseVideo = _setupComposition.Ui.AutoPauseVideo;
        _backgroundColorful = _setupComposition.Ui.BackgroundColorful;
        _musicVolume = _setupComposition.Ui.MusicVolume;
        _musicRandomPlay = _setupComposition.Ui.MusicRandomPlay;
        _musicAutoStart = _setupComposition.Ui.MusicAutoStart;
        _musicStartOnGameLaunch = _setupComposition.Ui.MusicStartOnGameLaunch;
        _musicStopOnGameLaunch = _setupComposition.Ui.MusicStopOnGameLaunch;
        _musicEnableSmtc = _setupComposition.Ui.MusicEnableSmtc;
        _selectedLogoTypeIndex = _setupComposition.Ui.LogoTypeIndex;
        _logoAlignLeft = _setupComposition.Ui.LogoAlignLeft;
        _logoText = _setupComposition.Ui.LogoText;
        _selectedHomepageTypeIndex = _setupComposition.Ui.HomepageTypeIndex;
        _homepageUrl = _setupComposition.Ui.HomepageUrl;
        _selectedHomepagePresetIndex = Math.Clamp(_setupComposition.Ui.HomepagePresetIndex, 0, HomepagePresetOptions.Count - 1);

        ReplaceItems(UiFeatureToggleGroups,
            _setupComposition.Ui.ToggleGroups.Select(group =>
                new UiFeatureToggleGroupViewModel(
                    group.Title,
                    group.Items.Select(item =>
                        new UiFeatureToggleItemViewModel(
                            item.Title,
                            item.IsChecked,
                            isChecked => PersistUiToggle(item.ConfigKey, isChecked))).ToArray())));
    }

    private void RefreshHelpTopics()
    {
        var query = HelpSearchQuery.Trim();
        var topics = _toolsComposition.Help.Entries
            .Select(CreateHelpTopic)
            .Where(topic =>
                string.IsNullOrWhiteSpace(query)
                || topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || topic.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                || topic.GroupTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
                || topic.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(topic => string.Equals(topic.GroupTitle, "指南", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(topic => topic.GroupTitle, StringComparer.Ordinal)
            .ThenBy(topic => topic.Title, StringComparer.Ordinal)
            .ToArray();

        ReplaceItems(
            HelpTopicGroups,
            topics
                .GroupBy(topic => topic.GroupTitle)
                .Select(group => new HelpTopicGroupViewModel(group.Key, group.ToArray())));

        RaiseCollectionStateProperties();
    }

    private FeedbackSectionViewModel CreateFeedbackSection(string title, bool isExpanded, IReadOnlyList<SimpleListEntryViewModel> items)
    {
        return new FeedbackSectionViewModel(title, items, isExpanded);
    }

    private SimpleListEntryViewModel CreateSimpleEntry(string title, string info)
    {
        return new SimpleListEntryViewModel(title, info, new ActionCommand(() => AddActivity($"查看条目: {title}", info)));
    }

    private SimpleListEntryViewModel CreateSimpleEntry(string title, string info, ActionCommand command)
    {
        return new SimpleListEntryViewModel(title, info, command);
    }

    private ToolboxActionViewModel CreateToolboxAction(string title, string toolTip, double minWidth, PclButtonColorState colorType, ActionCommand command)
    {
        return new ToolboxActionViewModel(title, toolTip, minWidth, colorType, command);
    }

    private SurfaceNoticeViewModel CreateNoticeStrip(string text, string background, string border, string foreground)
    {
        return new SurfaceNoticeViewModel(text, Brush.Parse(background), Brush.Parse(border), Brush.Parse(foreground));
    }

    private DownloadInstallOptionViewModel CreateDownloadInstallOption(
        string title,
        string selection,
        Bitmap? icon,
        string detailText,
        string selectText,
        bool canSelect,
        ActionCommand selectCommand,
        bool canClear,
        ActionCommand clearCommand)
    {
        return new DownloadInstallOptionViewModel(
            title,
            selection,
            icon,
            detailText,
            selectText,
            canSelect,
            selectCommand,
            canClear,
            clearCommand);
    }

    private void SetDownloadCatalogIntro(string title, string body, IReadOnlyList<DownloadCatalogActionViewModel> actions)
    {
        DownloadCatalogIntroTitle = title;
        DownloadCatalogIntroBody = body;
        ReplaceItems(DownloadCatalogIntroActions, actions);
    }

    private DownloadCatalogSectionViewModel CreateDownloadCatalogSection(string title, IReadOnlyList<DownloadCatalogEntryViewModel> items)
    {
        return new DownloadCatalogSectionViewModel(title, items);
    }

    private DownloadCatalogEntryViewModel CreateDownloadCatalogEntry(string title, string info, string meta, string actionText)
    {
        return new DownloadCatalogEntryViewModel(
            title,
            info,
            meta,
            actionText,
            new ActionCommand(() => AddActivity($"下载页操作: {title}", $"{meta} • {info}")));
    }

    private IReadOnlyList<DownloadCatalogSectionViewModel> BuildDownloadFavoriteSections()
    {
        return
        [
            CreateDownloadCatalogSection("Mod",
            [
                CreateDownloadCatalogEntry("Sodium", "现代性能优化模组。", "Fabric • 已收藏", "查看详情"),
                CreateDownloadCatalogEntry("Mod Menu", "管理 Fabric 模组菜单。", "Fabric • 已收藏", "查看详情")
            ]),
            CreateDownloadCatalogSection("整合包",
            [
                CreateDownloadCatalogEntry("All The Mods 9", "大型整合包演示条目。", "整合包 • 已收藏", "查看详情")
            ]),
            CreateDownloadCatalogSection("资源包",
            [
                CreateDownloadCatalogEntry("Faithful 32x", "经典风格资源包。", "资源包 • 已收藏", "查看详情"),
                CreateDownloadCatalogEntry("Complementary Shaders", "常用光影收藏条目。", "光影包 • 已收藏", "查看详情")
            ])
        ];
    }

    private ActionCommand CreateLinkCommand(string title, string url)
    {
        return CreateOpenTargetCommand(title, url, url);
    }

    private ActionCommand CreateIntentCommand(string title, string detail)
    {
        return new ActionCommand(() => AddActivity(title, detail));
    }

    private ActionCommand CreateDownloadFavoriteCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return CreateOpenTargetCommand($"打开收藏夹条目: {entry.Title}", entry.Target!, entry.Target!);
    }

    private ActionCommand CreateOpenTargetCommand(string title, string target, string detail)
    {
        return new ActionCommand(() =>
        {
            if (_shellActionService.TryOpenExternalTarget(target, out var error))
            {
                AddActivity(title, detail);
            }
            else
            {
                AddActivity($"{title} 失败", error ?? detail);
            }
        });
    }

    private void RefreshGameLinkWorldOptions()
    {
        var options = _instanceComposition.World.Entries
            .Select((entry, index) => $"{entry.Title} - {25565 + index}")
            .ToArray();
        _gameLinkWorldOptions = options.Length == 0
            ? ["未检测到可用存档"]
            : options;
        _selectedGameLinkWorldIndex = Math.Clamp(_selectedGameLinkWorldIndex, 0, _gameLinkWorldOptions.Count - 1);
    }
}
