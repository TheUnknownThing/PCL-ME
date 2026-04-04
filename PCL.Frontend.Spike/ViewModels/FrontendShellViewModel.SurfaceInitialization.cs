using Avalonia.Media;
using Avalonia.Media.Imaging;
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
                "当前版本: 可移植前端 Spike（合同壳层验证）",
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
        [
            CreateSimpleEntry("latest.log", "最近一次启动会话的汇总日志与提示处理记录。"),
            CreateSimpleEntry("frontend-shell.log", "Avalonia 壳层路由、提示队列与页面切换的演示记录。"),
            CreateSimpleEntry("launch-plan.log", "Java、登录与 prerun 计划的便携摘要。"),
            CreateSimpleEntry("crash-export.log", "崩溃导出意图与归档建议的演示输出。")
        ]);
    }

    private void InitializeUpdateSurface()
    {
        _selectedUpdateChannelIndex = Math.Clamp((int)_startupPlan.StartupPlan.Bootstrap.DefaultUpdateChannel, 0, UpdateChannelOptions.Count - 1);
        _selectedUpdateModeIndex = 0;
        _mirrorCdk = string.Empty;
        _updateSurfaceState = UpdateSurfaceState.Available;
    }

    private void InitializeLaunchSettingsSurface()
    {
        _selectedLaunchIsolationIndex = 1;
        _launchWindowTitle = "{}{name} | 玩家 : {user} | 使用 {login} 登录";
        _launchCustomInfo = "PCL";
        _selectedLaunchVisibilityIndex = 4;
        _selectedLaunchPriorityIndex = 1;
        _selectedLaunchWindowTypeIndex = 1;
        _launchWindowWidth = "854";
        _launchWindowHeight = "480";
        _useAutomaticRamAllocation = true;
        _customRamAllocation = 3;
        _optimizeMemoryBeforeLaunch = true;
        _isLaunchAdvancedOptionsExpanded = false;
        _selectedLaunchRendererIndex = 0;
        _launchJvmArguments = "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions";
        _launchGameArguments = string.Empty;
        _launchBeforeCommand = string.Empty;
        _waitForLaunchBeforeCommand = false;
        _disableJavaLaunchWrapper = false;
        _disableRetroWrapper = false;
        _requireDedicatedGpu = true;
        _useJavaExecutable = false;
        _selectedLaunchMicrosoftAuthIndex = 0;
        _selectedLaunchPreferredIpStackIndex = 0;
    }

    private void InitializeToolsGameLinkSurface()
    {
        _gameLinkAnnouncement = "正在连接到大厅服务器...";
        _gameLinkNatStatus = "点击测试";
        _gameLinkAccountStatus = "点击登录 Natayark 账户";
        _gameLinkLobbyId = string.Empty;
        _gameLinkSessionPing = "-ms";
        _gameLinkSessionId = "尚未创建大厅";
        _gameLinkConnectionType = "连接中";
        _gameLinkConnectedUserName = "未登录";
        _gameLinkConnectedUserType = "大厅访客";
        _selectedGameLinkWorldIndex = 0;

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
        _toolDownloadFolder = "/Users/demo/Downloads/PCL";
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
            CreateToolboxAction("内存优化", "内存优化为 PCL CE 特供版演示按钮。", 110, PclButtonColorState.Normal, CreateIntentCommand("内存优化", "Would run the launcher memory optimization workflow.")),
            CreateToolboxAction("清理游戏垃圾", "清理 PCL 缓存与日志等垃圾文件。", 130, PclButtonColorState.Normal, CreateIntentCommand("清理游戏垃圾", "Would clear cache, logs, and crash reports.")),
            CreateToolboxAction("今日人品", "演示工具按钮。", 110, PclButtonColorState.Normal, CreateIntentCommand("今日人品", "Would calculate the daily luck value.")),
            CreateToolboxAction("崩溃测试", "危险操作，仅用于测试。", 110, PclButtonColorState.Red, CreateIntentCommand("崩溃测试", "Would trigger the launcher crash-test path.")),
            CreateToolboxAction("创建快捷方式", "创建一个指向启动器的快捷方式。", 130, PclButtonColorState.Normal, CreateIntentCommand("创建快捷方式", "Would create a shortcut to the launcher executable.")),
            CreateToolboxAction("查看启动计数", "查看启动器的累计启动次数。", 130, PclButtonColorState.Normal, CreateIntentCommand("查看启动计数", "Would show the launcher start-count dialog."))
        ]);
    }

    private void InitializeDownloadInstallSurface()
    {
        _downloadInstallName = "新的安装方案";

        ReplaceItems(DownloadInstallHints,
        [
            CreateNoticeStrip("如果不安装 Fabric API，大多数 Mod 都会无法使用！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("如果不安装 Legacy Fabric API，大多数 Mod 都会无法使用！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("必须安装 OptiFabric 才能正常使用 OptiFine！", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("安装结束后，请在 Mod 下载中搜索 OptiFabric Origins 并下载，否则 OptiFine 会无法使用！", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("安装结束后，请在 Mod 下载中搜索 LegacyOptiFabric 并下载，否则 OptiFine 会无法使用！", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("OptiFine 与一部分 Mod 的兼容性不佳，请谨慎安装。", "#FFF8DD", "#F2D777", "#6E5800"),
            CreateNoticeStrip("如果不安装 QFAPI / QSL，大多数 Mod 都会无法使用！如果 QFAPI / QSL 无可用版本，你可以选择安装 Fabric API。", "#FFF1EA", "#F1C8B6", "#A94F2B"),
            CreateNoticeStrip("你选择了在 Quilt 中安装 Fabric API，而当前存在适配的 QFAPI / QSL 可供安装。请优先考虑安装 QFAPI / QSL。", "#FFF8DD", "#F2D777", "#6E5800")
        ]);

        ReplaceItems(DownloadInstallOptions,
        [
            CreateDownloadInstallOption("Forge", "1.20.1 recommended", LoadLauncherBitmap("Images", "Blocks", "Anvil.png")),
            CreateDownloadInstallOption("Cleanroom", "1.12.2 experimental", LoadLauncherBitmap("Images", "Blocks", "Cleanroom.png")),
            CreateDownloadInstallOption("NeoForge", "21.1.2", LoadLauncherBitmap("Images", "Blocks", "NeoForge.png")),
            CreateDownloadInstallOption("Fabric", "0.16.9", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Legacy Fabric", "1.8.9 backport", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Fabric API", "0.118.0+1.21.1", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Legacy Fabric API", "1.7.4+1.8.9", LoadLauncherBitmap("Images", "Blocks", "Fabric.png")),
            CreateDownloadInstallOption("Quilt", "0.27.1", LoadLauncherBitmap("Images", "Blocks", "Quilt.png")),
            CreateDownloadInstallOption("QFAPI / QSL", "11.0.0-beta", LoadLauncherBitmap("Images", "Blocks", "Quilt.png")),
            CreateDownloadInstallOption("LabyMod", "4.1.23", LoadLauncherBitmap("Images", "Blocks", "LabyMod.png")),
            CreateDownloadInstallOption("OptiFine", "HD_U_I6", LoadLauncherBitmap("Images", "Blocks", "GrassPath.png")),
            CreateDownloadInstallOption("OptiFabric", "1.14.3", LoadLauncherBitmap("Images", "Blocks", "OptiFabric.png")),
            CreateDownloadInstallOption("LiteLoader", "1.12.2-SNAPSHOT", LoadLauncherBitmap("Images", "Blocks", "LiteLoader.png"))
        ]);
    }

    private void InitializeGameLinkSurface()
    {
        _linkUsername = "PCL CE 玩家";
        _selectedProtocolPreferenceIndex = 0;
        _preferLowestLatencyPath = true;
        _tryPunchSymmetricNat = true;
        _allowIpv6Communication = true;
        _enableLinkCliOutput = false;
    }

    private void InitializeGameManageSurface()
    {
        _selectedDownloadSourceIndex = 1;
        _selectedVersionSourceIndex = 1;
        _downloadThreadLimit = 63;
        _downloadSpeedLimit = 42;
        _autoSelectNewInstance = true;
        _upgradePartialAuthlib = true;
        _selectedCommunityDownloadSourceIndex = 1;
        _selectedFileNameFormatIndex = 1;
        _selectedModLocalNameStyleIndex = 0;
        _ignoreQuiltLoader = false;
        _notifyReleaseUpdates = true;
        _notifySnapshotUpdates = false;
        _autoSwitchGameLanguageToChinese = true;
        _detectClipboardResourceLinks = true;
    }

    private void InitializeLauncherMiscSurface()
    {
        _selectedSystemActivityIndex = 1;
        _animationFpsLimit = 59;
        _maxRealTimeLogValue = 13;
        _disableHardwareAcceleration = false;
        _enableTelemetry = true;
        _enableDoH = true;
        _selectedHttpProxyTypeIndex = 1;
        _httpProxyAddress = "http://127.0.0.1:1080/";
        _httpProxyUsername = string.Empty;
        _httpProxyPassword = string.Empty;
        _debugAnimationSpeed = 30;
        _skipCopyDuringDownload = false;
        _debugModeEnabled = false;
        _debugDelayEnabled = false;
    }

    private void InitializeJavaSurface()
    {
        _selectedJavaRuntimeKey = "auto";
        ReplaceItems(JavaRuntimeEntries,
        [
            CreateJavaRuntimeEntry(
                "temurin-17",
                "JDK 17",
                "/Users/demo/.pcl/java/temurin-17/Contents/Home",
                ["64 Bit", "Temurin"],
                isEnabled: true),
            CreateJavaRuntimeEntry(
                "zulu-21",
                "JDK 21",
                "/Users/demo/.pcl/java/zulu-21/Contents/Home",
                ["64 Bit", "Zulu"],
                isEnabled: true),
            CreateJavaRuntimeEntry(
                "graalvm-8",
                "JRE 8",
                "/Users/demo/.pcl/java/graalvm-8/Contents/Home",
                ["64 Bit", "GraalVM"],
                isEnabled: false)
        ]);
        SyncJavaSelection();
    }

    private void InitializeUiSurface()
    {
        _selectedDarkModeIndex = 2;
        _selectedLightColorIndex = 0;
        _selectedDarkColorIndex = 1;
        _launcherOpacity = 360;
        _showLauncherLogo = true;
        _lockWindowSize = false;
        _showLaunchingHint = true;
        _enableAdvancedMaterial = false;
        _blurRadius = 14;
        _blurSamplingRate = 65;
        _selectedBlurTypeIndex = 0;
        _selectedGlobalFontIndex = 0;
        _selectedMotdFontIndex = 1;
        _autoPauseVideo = true;
        _backgroundColorful = true;
        _musicVolume = 680;
        _musicRandomPlay = true;
        _musicAutoStart = false;
        _musicStartOnGameLaunch = true;
        _musicStopOnGameLaunch = false;
        _musicEnableSmtc = true;
        _selectedLogoTypeIndex = 1;
        _logoAlignLeft = true;
        _logoText = "Point Cloud Library";
        _selectedHomepageTypeIndex = 1;
        _homepageUrl = "https://example.invalid/homepage.json";
        _selectedHomepagePresetIndex = Math.Min(12, HomepagePresetOptions.Count - 1);

        ReplaceItems(UiFeatureToggleGroups,
        [
            new UiFeatureToggleGroupViewModel(
                "主页面",
                [
                    new UiFeatureToggleItemViewModel("下载", false),
                    new UiFeatureToggleItemViewModel("设置", false),
                    new UiFeatureToggleItemViewModel("工具", false)
                ]),
            new UiFeatureToggleGroupViewModel(
                "子页面 设置",
                [
                    new UiFeatureToggleItemViewModel("启动", false),
                    new UiFeatureToggleItemViewModel("Java", false),
                    new UiFeatureToggleItemViewModel("管理", false),
                    new UiFeatureToggleItemViewModel("联机", false),
                    new UiFeatureToggleItemViewModel("个性化", false),
                    new UiFeatureToggleItemViewModel("杂项", false),
                    new UiFeatureToggleItemViewModel("软件更新", false),
                    new UiFeatureToggleItemViewModel("关于", false),
                    new UiFeatureToggleItemViewModel("反馈", false),
                    new UiFeatureToggleItemViewModel("查看日志", false)
                ]),
            new UiFeatureToggleGroupViewModel(
                "子页面 工具",
                [
                    new UiFeatureToggleItemViewModel("联机", false),
                    new UiFeatureToggleItemViewModel("百宝箱", false),
                    new UiFeatureToggleItemViewModel("帮助", false)
                ]),
            new UiFeatureToggleGroupViewModel(
                "子页面 实例设置",
                [
                    new UiFeatureToggleItemViewModel("修改", false),
                    new UiFeatureToggleItemViewModel("导出", false),
                    new UiFeatureToggleItemViewModel("存档", false),
                    new UiFeatureToggleItemViewModel("截图", false),
                    new UiFeatureToggleItemViewModel("Mod", false),
                    new UiFeatureToggleItemViewModel("资源包", false),
                    new UiFeatureToggleItemViewModel("光影包", false),
                    new UiFeatureToggleItemViewModel("投影原理图", false),
                    new UiFeatureToggleItemViewModel("服务器", false)
                ]),
            new UiFeatureToggleGroupViewModel(
                "特定功能",
                [
                    new UiFeatureToggleItemViewModel("实例管理", false),
                    new UiFeatureToggleItemViewModel("Mod 更新", false),
                    new UiFeatureToggleItemViewModel("功能隐藏", false)
                ])
        ]);
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

    private ActionCommand CreateLinkCommand(string title, string url)
    {
        return new ActionCommand(() => AddActivity(title, url));
    }

    private ActionCommand CreateIntentCommand(string title, string detail)
    {
        return new ActionCommand(() => AddActivity(title, detail));
    }
}
