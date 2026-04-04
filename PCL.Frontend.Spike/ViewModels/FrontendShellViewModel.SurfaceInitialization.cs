using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Desktop.Controls;

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

    private void InitializeFeedbackSections()
    {
        ReplaceItems(FeedbackSections,
        [
            CreateFeedbackSection("正在处理", false,
            [
                CreateSimpleEntry("前端迁移右侧页面复制", "继续将设置与工具页从通用摘要卡片替换成原始页面结构。"),
                CreateSimpleEntry("路由感知壳层对齐", "让 Avalonia 右侧内容区按当前子页面切换而不是统一模板。")
            ]),
            CreateFeedbackSection("等待处理", false,
            [
                CreateSimpleEntry("下载页自动安装面板", "等待更细的安装器展示合同后继续贴近 PageDownloadInstall。")
            ]),
            CreateFeedbackSection("等待", false,
            [
                CreateSimpleEntry("实例页资源面板", "等实例详情数据面完成后再复制资源包、光影包与服务器区块。")
            ]),
            CreateFeedbackSection("暂停", false,
            [
                CreateSimpleEntry("主题与动画设置细节", "当前先保留为后续界面页深入复制任务。")
            ]),
            CreateFeedbackSection("在即", false,
            [
                CreateSimpleEntry("帮助页搜索交互", "优先补齐搜索框、结果区与帮助列表卡片布局。")
            ]),
            CreateFeedbackSection("已完成", true,
            [
                CreateSimpleEntry("主壳顶栏对齐", "已切换为更接近原版的顶栏标签与返回标题模式。")
            ]),
            CreateFeedbackSection("已拒绝", true,
            [
                CreateSimpleEntry("重新设计迁移视觉语言", "当前迁移阶段不接受脱离原版控件语义的重新设计。")
            ]),
            CreateFeedbackSection("已忽略", true,
            [
                CreateSimpleEntry("把后端工作流搬回前端", "保持策略在后端，前端只负责组合与意图收集。")
            ]),
            CreateFeedbackSection("重复", true,
            [
                CreateSimpleEntry("用更多通用卡片替代原页面", "这和当前迁移规则冲突，继续复制原布局会更合适。")
            ])
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
        RefreshGameLinkWorldOptions();
        _gameLinkAnnouncement = "正在连接到大厅服务器...";
        _gameLinkNatStatus = "点击测试";
        _gameLinkAccountStatus = "点击登录 Natayark 账户";
        _gameLinkLobbyId = string.Empty;
        _gameLinkSessionPing = "-ms";
        _gameLinkSessionId = "尚未创建大厅";
        _gameLinkConnectionType = "连接中";
        _gameLinkConnectedUserName = "未登录";
        _gameLinkConnectedUserType = "大厅访客";
        _selectedGameLinkWorldIndex = Math.Clamp(_selectedGameLinkWorldIndex, 0, GameLinkWorldOptions.Count - 1);

        ReplaceItems(GameLinkPolicyEntries,
        [
            new SimpleListEntryViewModel(
                "PCL CE 大厅相关隐私政策",
                "了解 PCL CE 如何处理您的个人信息",
                _openLobbyPrivacyPolicyCommand),
            new SimpleListEntryViewModel(
                "Natayark Network 用户协议与隐私政策",
                "查看 Natayark OpenID 服务条款",
                _openNatayarkPolicyCommand)
        ]);

        ReplaceItems(GameLinkPlayerEntries,
        [
            new SimpleListEntryViewModel("PCL-Community", "大厅创建者 • 等待加入", new ActionCommand(() => AddActivity("查看大厅成员", "PCL-Community"))),
            new SimpleListEntryViewModel("EasyTier Bridge", "联机模块服务 • 在线", new ActionCommand(() => AddActivity("查看大厅成员", "EasyTier Bridge")))
        ]);
    }

    private void InitializeToolsTestSurface()
    {
        _toolDownloadUrl = "https://example.invalid/files/demo-pack.zip";
        _toolDownloadUserAgent = "PCL-CE-Spike/1.0";
        _toolDownloadFolder = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "tool-downloads");
        _toolDownloadName = "demo-pack.zip";
        _officialSkinPlayerName = "Steve";
        _achievementBlockId = "diamond_sword";
        _achievementTitle = "Achievement Get!";
        _achievementFirstLine = "Time to Strike!";
        _achievementSecondLine = "PCL Frontend Spike";
        _showAchievementPreview = false;
        _selectedHeadSizeIndex = 0;
        _selectedHeadSkinPath = "尚未选择皮肤";

        ReplaceItems(ToolboxActions,
        [
            CreateToolboxAction("内存优化", "内存优化为 PCL CE 特供版，效果加强！\n\n将物理内存占用降低约 1/3，不仅限于 MC！\n如果使用机械硬盘，这可能会导致一小段时间的严重卡顿。", 110, PclButtonColorState.Normal, CreateIntentCommand("内存优化", "Would run the launcher memory optimization workflow.")),
            CreateToolboxAction("清理游戏垃圾", "清理 PCL 的缓存与 MC 的日志、崩溃报告等垃圾文件", 130, PclButtonColorState.Normal, CreateIntentCommand("清理游戏垃圾", "Would clear cache, logs, and crash reports.")),
            CreateToolboxAction("今日人品", "演示工具按钮。", 110, PclButtonColorState.Normal, CreateIntentCommand("今日人品", "Would calculate the daily luck value.")),
            CreateToolboxAction("崩溃测试", "点这个按钮会让启动器直接崩掉，没事别点，造成的一切问题均不受理，相关 issue 会被直接关闭", 110, PclButtonColorState.Red, new ActionCommand(TriggerCrashPromptTest)),
            CreateToolboxAction("创建快捷方式", "创建一个指向 PCL 社区版可执行文件的快捷方式", 130, PclButtonColorState.Normal, CreateIntentCommand("创建快捷方式", "Would create a shortcut to the launcher executable.")),
            CreateToolboxAction("查看启动计数", "查看启动器的累计启动次数。", 130, PclButtonColorState.Normal, CreateIntentCommand("查看启动计数", "Would show the launcher start-count dialog."))
        ]);
    }

    private void InitializeDownloadInstallSurface()
    {
        _downloadInstallName = _downloadComposition.Install.Name;

        ReplaceItems(
            DownloadInstallHints,
            _downloadComposition.Install.Hints.Select(hint =>
                CreateNoticeStrip(hint, "#FFF1EA", "#F1C8B6", "#A94F2B")));

        ReplaceItems(
            DownloadInstallOptions,
            _downloadComposition.Install.Options.Select(option =>
                CreateDownloadInstallOption(
                    option.Title,
                    option.Selection,
                    string.IsNullOrWhiteSpace(option.IconName)
                        ? null
                        : LoadLauncherBitmap("Images", "Blocks", option.IconName))));
    }

    private void RefreshDownloadCatalogSurface()
    {
        DownloadCatalogIntroTitle = string.Empty;
        DownloadCatalogIntroBody = string.Empty;
        ReplaceItems(DownloadCatalogIntroActions, []);
        ReplaceItems(DownloadCatalogSections, []);

        if (!IsDownloadCatalogSurface)
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

        if (!IsDownloadFavoritesSurface)
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
                            : CreateOpenTargetCommand($"打开收藏夹条目: {item.Title}", item.Target, item.Target)))
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

    private IReadOnlyList<HelpTopicViewModel> CreateHelpTopics()
    {
        return
        [
            new HelpTopicViewModel("启动与版本", "如何选择实例", "从启动页进入实例选择，然后再返回主启动面板继续启动。", CreateIntentCommand("查看帮助: 如何选择实例", "Would open the launch and instance-selection help topic.")),
            new HelpTopicViewModel("启动与版本", "Java 下载提示", "Java 缺失时由后端给出下载提示，前端只负责渲染选择与跳转。", CreateIntentCommand("查看帮助: Java 下载提示", "Would open the Java runtime help topic.")),
            new HelpTopicViewModel("诊断与恢复", "导出日志", "可以在设置的日志页导出当前日志或全部历史日志压缩包。", CreateIntentCommand("查看帮助: 导出日志", "Would open the log export help topic.")),
            new HelpTopicViewModel("诊断与恢复", "崩溃恢复提示", "崩溃报告、导出与恢复动作都通过可移植提示合同提供给壳层。", CreateIntentCommand("查看帮助: 崩溃恢复提示", "Would open the crash recovery help topic.")),
            new HelpTopicViewModel("迁移说明", "为什么先复制原页面", "当前目标是保持 PCL 的页面结构和控件语言，而不是重新设计。", CreateIntentCommand("查看帮助: 为什么先复制原页面", "Would open the frontend migration guidance topic.")),
            new HelpTopicViewModel("迁移说明", "哪些逻辑不应放回前端", "启动、登录、Java 与崩溃策略仍应保留在后端服务中。", CreateIntentCommand("查看帮助: 哪些逻辑不应放回前端", "Would open the portability boundary topic."))
        ];
    }

    private void RefreshHelpTopics()
    {
        var query = HelpSearchQuery.Trim();
        var topics = string.IsNullOrWhiteSpace(query)
            ? _allHelpTopics
            : _allHelpTopics
                .Where(topic => topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || topic.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || topic.GroupTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
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

    private DownloadInstallOptionViewModel CreateDownloadInstallOption(string title, string selection, Bitmap? icon)
    {
        return new DownloadInstallOptionViewModel(
            title,
            selection,
            icon,
            new ActionCommand(() => AddActivity($"选择安装项: {title}", selection)),
            new ActionCommand(() => AddActivity($"清除安装项: {title}", $"Would clear the selected {title} version.")));
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
