using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed class FrontendShellViewModel : ViewModelBase
{
    private static readonly string LauncherRootDirectory = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "Plain Craft Launcher 2"));
    private static readonly string LaunchAvatarImageFilePath = GetLauncherAssetPath("Images", "Heads", "PCL-Community.png");
    private static readonly string LaunchNewsImageFilePath = GetLauncherAssetPath("Images", "Backgrounds", "server_bg.png");
    private static readonly string UpdateAvailableIconFilePath = GetLauncherAssetPath("Images", "Heads", "Logo-CE.png");
    private static readonly string UpdateCurrentIconFilePath = GetLauncherAssetPath("Images", "icon.png");
    private static readonly string UpdateOptionalIconFilePath = GetLauncherAssetPath("Images", "Heads", "Logo-CE.png");
    private readonly ShellSpikeInputs _shellInputs;
    private readonly StartupSpikePlan _startupPlan;
    private readonly LaunchSpikePlan _launchPlan;
    private readonly CrashSpikePlan _crashPlan;
    private readonly Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> _promptCatalog;
    private readonly IReadOnlyList<HelpTopicViewModel> _allHelpTopics;
    private readonly List<LauncherFrontendRoute> _routeHistory = [];
    private readonly ActionCommand _backCommand;
    private readonly ActionCommand _togglePromptOverlayCommand;
    private readonly ActionCommand _dismissPromptOverlayCommand;
    private readonly ActionCommand _launchCommand;
    private readonly ActionCommand _versionSelectCommand;
    private readonly ActionCommand _versionSetupCommand;
    private readonly ActionCommand _toggleLaunchMigrationCommand;
    private readonly ActionCommand _toggleLaunchNewsCommand;
    private readonly ActionCommand _dismissLaunchCommunityHintCommand;
    private readonly ActionCommand _openFeedbackCommand;
    private readonly ActionCommand _exportLogCommand;
    private readonly ActionCommand _exportAllLogsCommand;
    private readonly ActionCommand _openLogDirectoryCommand;
    private readonly ActionCommand _cleanLogsCommand;
    private readonly ActionCommand _getMirrorCdkCommand;
    private readonly ActionCommand _downloadUpdateCommand;
    private readonly ActionCommand _showUpdateDetailCommand;
    private readonly ActionCommand _checkUpdateAgainCommand;
    private readonly ActionCommand _openFullChangelogCommand;
    private readonly ActionCommand _downloadOptionalUpdateCommand;
    private readonly ActionCommand _showOptionalUpdateDetailCommand;
    private readonly ActionCommand _resetGameLinkSettingsCommand;
    private LauncherFrontendRoute _currentRoute;
    private LauncherFrontendNavigationView? _currentNavigation;
    private SpikePromptLaneKind _selectedPromptLane;
    private string _eyebrow = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _status = string.Empty;
    private string _breadcrumbTrail = string.Empty;
    private string _surfaceMeta = string.Empty;
    private string _promptInboxTitle = string.Empty;
    private string _promptInboxSummary = string.Empty;
    private string _promptEmptyState = string.Empty;
    private bool _canGoBack;
    private bool _isPromptOverlayOpen;
    private bool _isLaunchMigrationExpanded = true;
    private bool _isLaunchNewsExpanded = true;
    private bool _showLaunchCommunityHint = true;
    private string _helpSearchQuery = string.Empty;
    private int _selectedUpdateChannelIndex;
    private int _selectedUpdateModeIndex;
    private string _mirrorCdk = string.Empty;
    private UpdateSurfaceState _updateSurfaceState = UpdateSurfaceState.Available;
    private string _linkUsername = string.Empty;
    private int _selectedProtocolPreferenceIndex;
    private bool _preferLowestLatencyPath = true;
    private bool _tryPunchSymmetricNat = true;
    private bool _allowIpv6Communication = true;
    private bool _enableLinkCliOutput;

    private FrontendShellViewModel(SpikeCommandOptions options)
    {
        _shellInputs = SpikeInputResolver.ResolveShellInputs(options);
        _startupPlan = SpikeSampleFactory.BuildStartupPlan(_shellInputs.StartupInputs);
        _launchPlan = SpikeSampleFactory.BuildLaunchPlan(SpikeInputResolver.ResolveLaunchInputs(options), options.SaveBatchPath);
        _crashPlan = SpikeSampleFactory.BuildCrashPlan(SpikeInputResolver.ResolveCrashInputs(options));
        _currentRoute = _shellInputs.NavigationRequest.CurrentRoute;
        _selectedPromptLane = SpikePromptLaneKind.Startup;
        _backCommand = new ActionCommand(NavigateBack, () => CanGoBack);
        _togglePromptOverlayCommand = new ActionCommand(TogglePromptOverlay);
        _dismissPromptOverlayCommand = new ActionCommand(() => SetPromptOverlayOpen(false));
        _launchCommand = new ActionCommand(() => AddActivity("Launch requested.", $"Would start {LaunchVersionSubtitle}."));
        _versionSelectCommand = new ActionCommand(() => NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect), "Opened instance selection from the launch pane."));
        _versionSetupCommand = new ActionCommand(() => NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), "Opened instance settings from the launch pane."));
        _toggleLaunchMigrationCommand = new ActionCommand(ToggleLaunchMigrationCard);
        _toggleLaunchNewsCommand = new ActionCommand(ToggleLaunchNewsCard);
        _dismissLaunchCommunityHintCommand = new ActionCommand(() => ShowLaunchCommunityHint = false);
        _openFeedbackCommand = CreateIntentCommand("打开反馈入口", "Would open the GitHub issue tracker surface.");
        _exportLogCommand = CreateIntentCommand("导出日志", "Would export the current launcher log bundle.");
        _exportAllLogsCommand = CreateIntentCommand("导出全部日志", "Would export the complete launcher log archive.");
        _openLogDirectoryCommand = CreateIntentCommand("打开日志目录", "Would reveal the launcher log directory.");
        _cleanLogsCommand = CreateIntentCommand("清理历史日志", "Would remove archived launcher logs.");
        _getMirrorCdkCommand = CreateLinkCommand("获取 Mirror 酱 CDK", "https://mirrorchyan.com/");
        _downloadUpdateCommand = CreateIntentCommand("下载并安装更新", "Would start the launcher self-update workflow.");
        _showUpdateDetailCommand = CreateIntentCommand("查看更新详情", "Would open the markdown changelog dialog for the selected launcher update.");
        _checkUpdateAgainCommand = new ActionCommand(CycleUpdateSurfaceState);
        _openFullChangelogCommand = CreateLinkCommand("查看更新日志", "https://github.com/PCL-Community/PCL2-CE/releases");
        _downloadOptionalUpdateCommand = CreateIntentCommand("下载可选更新", "Would start the optional AquaCL upgrade flow.");
        _showOptionalUpdateDetailCommand = CreateIntentCommand("查看 AquaCL 更新详情", "Would open the optional upgrade changelog surface.");
        _resetGameLinkSettingsCommand = new ActionCommand(ResetGameLinkSurface);

        ScenarioLabel = $"Scenario: {options.Scenario}";
        EnvironmentLabel = options.UseHostEnvironment ? "Host-backed shell inputs" : "Fixture-driven shell inputs";
        InputLabel = string.IsNullOrWhiteSpace(options.InputRoot) ? "Using built-in frontend fixtures" : $"Input root: {options.InputRoot}";

        _promptCatalog = BuildPromptCatalog(options.Scenario);
        _allHelpTopics = CreateHelpTopics();
        InitializeAboutEntries();
        InitializeFeedbackSections();
        InitializeLogEntries();
        InitializeUpdateSurface();
        InitializeGameLinkSurface();
        InitializePromptLanes();
        RefreshHelpTopics();
        RefreshShell("Shell initialized from portable frontend contracts.");
    }

    public ObservableCollection<NavigationEntryViewModel> TopLevelEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> SidebarEntries { get; } = [];

    public ObservableCollection<SidebarSectionViewModel> SidebarSections { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> UtilityEntries { get; } = [];

    public ObservableCollection<SurfaceFactViewModel> SurfaceFacts { get; } = [];

    public ObservableCollection<SurfaceSectionViewModel> SurfaceSections { get; } = [];

    public ObservableCollection<ActivityItemViewModel> ActivityEntries { get; } = [];

    public ObservableCollection<PromptLaneViewModel> PromptLanes { get; } = [];

    public ObservableCollection<PromptCardViewModel> ActivePrompts { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutProjectEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutAcknowledgementEntries { get; } = [];

    public ObservableCollection<FeedbackSectionViewModel> FeedbackSections { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> LogEntries { get; } = [];

    public ObservableCollection<HelpTopicGroupViewModel> HelpTopicGroups { get; } = [];

    public string ScenarioLabel { get; }

    public string EnvironmentLabel { get; }

    public string InputLabel { get; }

    public string HelpSearchQuery
    {
        get => _helpSearchQuery;
        set
        {
            if (SetProperty(ref _helpSearchQuery, value))
            {
                RefreshHelpTopics();
            }
        }
    }

    public string Eyebrow
    {
        get => _eyebrow;
        private set => SetProperty(ref _eyebrow, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string BreadcrumbTrail
    {
        get => _breadcrumbTrail;
        private set => SetProperty(ref _breadcrumbTrail, value);
    }

    public string SurfaceMeta
    {
        get => _surfaceMeta;
        private set => SetProperty(ref _surfaceMeta, value);
    }

    public string PromptInboxTitle
    {
        get => _promptInboxTitle;
        private set => SetProperty(ref _promptInboxTitle, value);
    }

    public string PromptInboxSummary
    {
        get => _promptInboxSummary;
        private set => SetProperty(ref _promptInboxSummary, value);
    }

    public string PromptEmptyState
    {
        get => _promptEmptyState;
        private set => SetProperty(ref _promptEmptyState, value);
    }

    public bool IsLaunchRoute => _currentRoute.Page == LauncherFrontendPageKey.Launch;

    public bool IsStandardShellRoute => !IsLaunchRoute;

    public bool ShowTopLevelNavigation => !CanGoBack;

    public bool ShowInnerNavigation => CanGoBack;

    public bool HasActivePrompts => ActivePrompts.Count > 0;

    public bool HasNoActivePrompts => !HasActivePrompts;

    public bool IsPromptOverlayVisible => HasActivePrompts && _isPromptOverlayOpen;

    public bool HasSidebarEntries => SidebarEntries.Count > 0;

    public bool HasSidebarSections => SidebarSections.Count > 0;

    public bool HasNoSidebarSections => !HasSidebarSections;

    public bool HasSurfaceFacts => SurfaceFacts.Count > 0;

    public bool HasSurfaceSections => SurfaceSections.Count > 0;

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool HasUtilityEntries => UtilityEntries.Count > 0;

    public bool IsSetupAboutSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupAbout;

    public bool IsSetupFeedbackSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupFeedback;

    public bool IsSetupLogSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLog;

    public bool IsSetupUpdateSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate;

    public bool IsSetupGameLinkSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupGameLink;

    public bool IsToolsHelpSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsLauncherHelp;

    public bool IsGenericShellSurface => IsStandardShellRoute
        && !IsSetupAboutSurface
        && !IsSetupFeedbackSurface
        && !IsSetupLogSurface
        && !IsSetupUpdateSurface
        && !IsSetupGameLinkSurface
        && !IsToolsHelpSurface;

    public bool HasAboutProjectEntries => AboutProjectEntries.Count > 0;

    public bool HasAboutAcknowledgementEntries => AboutAcknowledgementEntries.Count > 0;

    public bool HasFeedbackSections => FeedbackSections.Count > 0;

    public bool HasHelpTopicGroups => HelpTopicGroups.Count > 0;

    public bool HasNoHelpTopicGroups => !HasHelpTopicGroups;

    public string TitleBarLabel => _currentNavigation?.CurrentPage.SidebarItemTitle
        ?? _currentNavigation?.CurrentPage.Title
        ?? Title;

    public IReadOnlyList<string> UpdateChannelOptions { get; } =
    [
        "正式版 / Release",
        "测试版 / Beta",
        "开发版 / Dev"
    ];

    public IReadOnlyList<string> UpdateModeOptions { get; } =
    [
        "自动下载并安装更新",
        "自动下载并提示更新",
        "提示更新",
        "不自动检查更新（不推荐）"
    ];

    public int SelectedUpdateChannelIndex
    {
        get => _selectedUpdateChannelIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, UpdateChannelOptions.Count - 1);
            if (SetProperty(ref _selectedUpdateChannelIndex, clampedValue))
            {
                AddActivity("切换更新通道", UpdateChannelOptions[clampedValue]);
            }
        }
    }

    public int SelectedUpdateModeIndex
    {
        get => _selectedUpdateModeIndex;
        set
        {
            var clampedValue = Math.Clamp(value, 0, UpdateModeOptions.Count - 1);
            if (SetProperty(ref _selectedUpdateModeIndex, clampedValue))
            {
                AddActivity("切换自动更新设置", UpdateModeOptions[clampedValue]);
            }
        }
    }

    public string MirrorCdk
    {
        get => _mirrorCdk;
        set => SetProperty(ref _mirrorCdk, value);
    }

    public Bitmap? UpdateAvailableIcon => File.Exists(UpdateAvailableIconFilePath)
        ? new Bitmap(UpdateAvailableIconFilePath)
        : null;

    public Bitmap? UpdateCurrentIcon => File.Exists(UpdateCurrentIconFilePath)
        ? new Bitmap(UpdateCurrentIconFilePath)
        : null;

    public Bitmap? UpdateOptionalIcon => File.Exists(UpdateOptionalIconFilePath)
        ? new Bitmap(UpdateOptionalIconFilePath)
        : null;

    public bool ShowAvailableUpdateCard => _updateSurfaceState == UpdateSurfaceState.Available;

    public bool ShowCurrentVersionCard => _updateSurfaceState == UpdateSurfaceState.Latest;

    public bool ShowOptionalUpdateCard => false;

    public string AvailableUpdateName => "PCL CE 2.14.0";

    public string AvailableUpdatePublisher => "by PCL-Community";

    public string AvailableUpdateSummary => "PCL CE 2.14.0 带来了让人眼前一亮的新设计和 Pigeon 智能的多项功能，同时还带来了令人愉快的跨实例工作方式，助你提高工作效率。";

    public string CurrentVersionName => "PCL CE 2.13.4";

    public string CurrentVersionDescription => ShowCurrentVersionCard ? "已是最新版本" : "正在检查更新...";

    public string OptionalUpdateName => "AquaCL 3.0.0";

    public string OptionalUpdateDescription => "20.0 MB";

    public string OptionalUpdateSummary => "AquaCL 3 是 PCL CE 的第一个主要更新，带来了让人眼前一亮的新设计，使用了最新开发技术，完全重构了基础体验，并更支持 macOS、Linux 等其他平台。";

    public IReadOnlyList<string> LinkProtocolPreferenceOptions { get; } =
    [
        "TCP",
        "UDP"
    ];

    public string LinkUsername
    {
        get => _linkUsername;
        set => SetProperty(ref _linkUsername, value);
    }

    public int SelectedProtocolPreferenceIndex
    {
        get => _selectedProtocolPreferenceIndex;
        set => SetProperty(ref _selectedProtocolPreferenceIndex, Math.Clamp(value, 0, LinkProtocolPreferenceOptions.Count - 1));
    }

    public bool PreferLowestLatencyPath
    {
        get => _preferLowestLatencyPath;
        set => SetProperty(ref _preferLowestLatencyPath, value);
    }

    public bool TryPunchSymmetricNat
    {
        get => _tryPunchSymmetricNat;
        set => SetProperty(ref _tryPunchSymmetricNat, value);
    }

    public bool AllowIpv6Communication
    {
        get => _allowIpv6Communication;
        set => SetProperty(ref _allowIpv6Communication, value);
    }

    public bool EnableLinkCliOutput
    {
        get => _enableLinkCliOutput;
        set => SetProperty(ref _enableLinkCliOutput, value);
    }

    public string LaunchUserName => _launchPlan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
        ? authPlayerName
        : "DemoPlayer";

    public string LaunchAuthLabel => _launchPlan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
        ? "正版验证"
        : "外置验证";

    public string LaunchButtonTitle => "启动游戏";

    public string LaunchVersionSubtitle => _launchPlan.ReplacementPlan.Values.TryGetValue("${version_name}", out var versionName)
        ? versionName
        : "Demo Instance";

    public string LaunchWelcomeBanner => "欢迎使用新闻主页";

    public string LaunchMigrationHeadline => "新特性与迁移";

    public string LaunchNewsTitle => "最新快照版 - 25w20a";

    public string LaunchCommunityHintPrimaryText => "你正在使用 PCL 社区版！此版本为独立开发和维护，与官方版本维护路线不同，体验有所出入。";

    public string LaunchCommunityHintSecondaryText => "若要永久隐藏此提示，请输入正确的 PCL CE 开发组织名称。";

    public bool ShowLaunchCommunityHint
    {
        get => _showLaunchCommunityHint;
        private set => SetProperty(ref _showLaunchCommunityHint, value);
    }

    public bool ShowLaunchLog => false;

    public string LaunchLogText => "正在等待启动日志输出。";

    public bool IsLaunchMigrationExpanded
    {
        get => _isLaunchMigrationExpanded;
        private set => SetProperty(ref _isLaunchMigrationExpanded, value);
    }

    public bool IsLaunchNewsExpanded
    {
        get => _isLaunchNewsExpanded;
        private set => SetProperty(ref _isLaunchNewsExpanded, value);
    }

    public IReadOnlyList<string> LaunchMigrationLines =>
    [
        "新的主页内容区会优先展示信息卡片，并逐步替换旧的调试式布局。",
        "后续将继续接入原始主页渲染路径，而不是在 MainWindow 里手写所有内容。"
    ];

    public Bitmap? LaunchAvatarImage => File.Exists(LaunchAvatarImageFilePath)
        ? new Bitmap(LaunchAvatarImageFilePath)
        : null;

    public Bitmap? LaunchNewsImage => File.Exists(LaunchNewsImageFilePath)
        ? new Bitmap(LaunchNewsImageFilePath)
        : null;

    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (SetProperty(ref _canGoBack, value))
            {
                _backCommand.NotifyCanExecuteChanged();
                RaisePropertyChanged(nameof(ShowTopLevelNavigation));
                RaisePropertyChanged(nameof(ShowInnerNavigation));
            }
        }
    }

    public ActionCommand BackCommand => _backCommand;

    public ActionCommand TogglePromptOverlayCommand => _togglePromptOverlayCommand;

    public ActionCommand DismissPromptOverlayCommand => _dismissPromptOverlayCommand;

    public ActionCommand LaunchCommand => _launchCommand;

    public ActionCommand VersionSelectCommand => _versionSelectCommand;

    public ActionCommand VersionSetupCommand => _versionSetupCommand;

    public ActionCommand ToggleLaunchMigrationCommand => _toggleLaunchMigrationCommand;

    public ActionCommand ToggleLaunchNewsCommand => _toggleLaunchNewsCommand;

    public ActionCommand DismissLaunchCommunityHintCommand => _dismissLaunchCommunityHintCommand;

    public ActionCommand OpenFeedbackCommand => _openFeedbackCommand;

    public ActionCommand ExportLogCommand => _exportLogCommand;

    public ActionCommand ExportAllLogsCommand => _exportAllLogsCommand;

    public ActionCommand OpenLogDirectoryCommand => _openLogDirectoryCommand;

    public ActionCommand CleanLogsCommand => _cleanLogsCommand;

    public ActionCommand GetMirrorCdkCommand => _getMirrorCdkCommand;

    public ActionCommand DownloadUpdateCommand => _downloadUpdateCommand;

    public ActionCommand ShowUpdateDetailCommand => _showUpdateDetailCommand;

    public ActionCommand CheckUpdateAgainCommand => _checkUpdateAgainCommand;

    public ActionCommand OpenFullChangelogCommand => _openFullChangelogCommand;

    public ActionCommand DownloadOptionalUpdateCommand => _downloadOptionalUpdateCommand;

    public ActionCommand ShowOptionalUpdateDetailCommand => _showOptionalUpdateDetailCommand;

    public ActionCommand ResetGameLinkSettingsCommand => _resetGameLinkSettingsCommand;

    public static FrontendShellViewModel CreateBootstrap(SpikeCommandOptions options)
    {
        return new FrontendShellViewModel(options);
    }

    private Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = _startupPlan.StartupPlan.EnvironmentWarningPrompt is null
            ? LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent)
            : LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            BuildLaunchPrecheckResult(scenario),
            MinecraftLaunchShellService.GetSupportPrompt(10),
            _launchPlan.JavaWorkflow.MissingJavaPrompt);
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_crashPlan.OutputPrompt);

        return new Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>>
        {
            [SpikePromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Startup, prompt)).ToList(),
            [SpikePromptLaneKind.Launch] = launchPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Launch, prompt)).ToList(),
            [SpikePromptLaneKind.Crash] = crashPrompts.Select(prompt => CreatePromptCard(SpikePromptLaneKind.Crash, prompt)).ToList()
        };
    }

    private void InitializePromptLanes()
    {
        PromptLanes.Clear();
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Startup,
            "启动前",
            "许可、环境与首次启动提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Startup))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Launch,
            "启动中",
            "启动前检查、赞助与 Java 下载提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Launch))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Crash,
            "崩溃恢复",
            "崩溃输出与导出恢复提示。",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Crash))));

        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);
    }

    private void RefreshShell(string activityMessage)
    {
        var shellPlan = BuildShellPlan();
        var pageContent = BuildPageContent(shellPlan);
        _currentNavigation = shellPlan.Navigation;

        Eyebrow = pageContent.Eyebrow;
        Title = shellPlan.Navigation.CurrentPage.Title;
        Description = pageContent.Summary;
        Status = $"Immediate command: {shellPlan.StartupPlan.ImmediateCommand.Kind} | Splash: {(shellPlan.StartupPlan.Visual.ShouldShowSplashScreen ? "on" : "off")} | Backstack depth: {_routeHistory.Count}";
        BreadcrumbTrail = string.Join(" / ", shellPlan.Navigation.Breadcrumbs.Select(crumb => crumb.Title));
        SurfaceMeta = $"{shellPlan.Navigation.CurrentPage.Kind} surface • {(shellPlan.Navigation.CurrentPage.SidebarGroupTitle ?? "No sidebar group")} • {(shellPlan.Navigation.ShowsBackButton ? shellPlan.Navigation.BackTarget?.Label ?? "Back available" : "Top-level route")}";
        CanGoBack = shellPlan.Navigation.ShowsBackButton;

        ReplaceItems(TopLevelEntries, shellPlan.Navigation.TopLevelEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.TopLevel)));
        ReplaceItems(SidebarEntries, shellPlan.Navigation.SidebarEntries.Select(entry => CreateNavigationEntry(entry, NavigationVisualStyle.Sidebar)));
        ReplaceItems(SidebarSections, BuildSidebarSections(shellPlan.Navigation));
        ReplaceItems(UtilityEntries, shellPlan.Navigation.UtilityEntries.Where(entry => entry.IsVisible).Select(CreateUtilityEntry));
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        SelectPromptLane(_selectedPromptLane, updateActivity: false);
        AddActivity(activityMessage, $"{shellPlan.Navigation.CurrentPage.Title} • {shellPlan.Navigation.CurrentPage.Route.Page}/{shellPlan.Navigation.CurrentPage.Route.Subpage}");
        RaiseShellStateProperties();
    }

    private NavigationEntryViewModel CreateNavigationEntry(LauncherFrontendNavigationEntry entry, NavigationVisualStyle style)
    {
        var (iconPath, iconScale) = GetNavigationIcon(entry.Title);
        return new NavigationEntryViewModel(
            entry.Title,
            entry.Summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            entry.IsSelected,
            iconPath,
            iconScale,
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the {(style == NavigationVisualStyle.Sidebar ? "sidebar" : "top bar")}."))
        );
    }

    private NavigationEntryViewModel CreateUtilityEntry(LauncherFrontendUtilityEntry entry)
    {
        var meta = entry.Id switch
        {
            "back" => "返",
            "task-manager" => "任",
            "game-log" => "志",
            _ => entry.Route.Page.ToString()
        };

        return new NavigationEntryViewModel(
            entry.Title,
            entry.IsSelected ? "Utility surface is active in the shell." : "Pinned shell utility surface.",
            meta,
            entry.IsSelected,
            GetUtilityIcon(entry.Id),
            1.0,
            GetNavigationPalette(entry.IsSelected, NavigationVisualStyle.Utility),
            new ActionCommand(() => NavigateTo(entry.Route, $"Opened utility surface {entry.Title}.")));
    }

    private IEnumerable<SidebarSectionViewModel> BuildSidebarSections(LauncherFrontendNavigationView navigation)
    {
        if (navigation.SidebarEntries.Count == 0)
        {
            return [];
        }

        return navigation.SidebarEntries
            .GroupBy(entry => GetSidebarSectionTitle(navigation.CurrentRoute.Page, entry.Route.Subpage))
            .Select(group => new SidebarSectionViewModel(
                group.Key,
                string.IsNullOrWhiteSpace(group.Key)
                    ? false
                    : true,
                group.Select(entry =>
                {
                    var (iconPath, iconScale) = GetSidebarIcon(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    var accessory = GetSidebarAccessory(entry.Route.Page, entry.Route.Subpage, entry.Title);
                    return new SidebarListItemViewModel(
                        entry.Title,
                        entry.Summary,
                        entry.IsSelected,
                        iconPath,
                        iconScale,
                        new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the launcher-style left pane.")),
                        accessory.ToolTip,
                        accessory.IconPath,
                        accessory.Command is null
                            ? null
                            : new ActionCommand(() => ApplySidebarAccessory(entry.Title, accessory.ActionLabel, accessory.Command)));
                }).ToArray()))
            .ToArray();
    }

    private LauncherFrontendShellPlan BuildShellPlan()
    {
        var request = _shellInputs.NavigationRequest with
        {
            CurrentRoute = _currentRoute,
            BackstackDepth = _routeHistory.Count
        };
        return LauncherFrontendShellService.BuildPlan(new LauncherFrontendShellRequest(
            _shellInputs.StartupInputs.StartupWorkflowRequest,
            _shellInputs.StartupInputs.StartupConsentRequest,
            request));
    }

    private void NavigateTo(LauncherFrontendRoute route, string activityMessage)
    {
        if (route == _currentRoute)
        {
            AddActivity("Stayed on the current route.", $"{route.Page}/{route.Subpage}");
            return;
        }

        _routeHistory.Add(_currentRoute);
        _currentRoute = route;
        RefreshShell(activityMessage);
    }

    private void NavigateBack()
    {
        if (_currentNavigation is null)
        {
            return;
        }

        if (_routeHistory.Count > 0)
        {
            var previousRoute = _routeHistory[^1];
            _routeHistory.RemoveAt(_routeHistory.Count - 1);
            _currentRoute = previousRoute;
            RefreshShell("Returned to the previous shell route.");
            return;
        }

        if (_currentNavigation.BackTarget?.Route is { } backRoute)
        {
            _currentRoute = backRoute;
            RefreshShell($"Followed shell back target to {backRoute.Page}.");
        }
    }

    private void SelectPromptLane(SpikePromptLaneKind lane, bool updateActivity = true)
    {
        _selectedPromptLane = lane;
        SyncPromptLaneState();
        ReplaceItems(ActivePrompts, _promptCatalog[lane]);
        RaisePropertyChanged(nameof(HasActivePrompts));
        RaisePropertyChanged(nameof(HasNoActivePrompts));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));

        var selectedLane = PromptLanes.First(item => item.Kind == lane);
        PromptInboxTitle = $"{selectedLane.Title}提示";
        PromptInboxSummary = selectedLane.Summary;
        PromptEmptyState = $"当前没有待处理的{selectedLane.Title}提示。";
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));
        RaiseCollectionStateProperties();

        if (updateActivity)
        {
            AddActivity("Switched prompt lane.", $"{selectedLane.Title} now has {selectedLane.Count} queued prompt(s).");
        }
    }

    private void SyncPromptLaneState()
    {
        foreach (var lane in PromptLanes)
        {
            lane.Count = _promptCatalog[lane.Kind].Count;
            lane.IsSelected = lane.Kind == _selectedPromptLane;
        }
    }

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

    private void InitializeGameLinkSurface()
    {
        _linkUsername = "PCL CE 玩家";
        _selectedProtocolPreferenceIndex = 0;
        _preferLowestLatencyPath = true;
        _tryPunchSymmetricNat = true;
        _allowIpv6Communication = true;
        _enableLinkCliOutput = false;
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

    private ActionCommand CreateLinkCommand(string title, string url)
    {
        return new ActionCommand(() => AddActivity(title, url));
    }

    private ActionCommand CreateIntentCommand(string title, string detail)
    {
        return new ActionCommand(() => AddActivity(title, detail));
    }

    private PromptCardViewModel CreatePromptCard(SpikePromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            prompt.Title,
            prompt.Message,
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#A94F2B") : Brush.Parse("#256A61"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning ? Brush.Parse("#FFF1EA") : Brush.Parse("#EAF7F5"),
            prompt.Options.Select(option => new PromptOptionViewModel(
                option.Label,
                DescribePromptOption(option),
                new ActionCommand(() => ApplyPromptOption(lane, prompt.Id, option)))).ToList());
    }

    private void ApplyPromptOption(SpikePromptLaneKind lane, string promptId, LauncherFrontendPromptOption option)
    {
        var commandSummary = option.Commands.Count == 0
            ? "No commands attached."
            : string.Join(" • ", option.Commands.Select(DescribePromptCommand));
        AddActivity($"Prompt action: {option.Label}", commandSummary);

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(command);
        }

        if (option.ClosesPrompt)
        {
            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (!HasActivePrompts)
            {
                SetPromptOverlayOpen(false);
            }
            AddActivity("Prompt closed.", $"{promptId} was dismissed from the {lane} lane.");
        }
    }

    private void ExecutePromptCommand(LauncherFrontendPromptCommand command)
    {
        switch (command.Kind)
        {
            case LauncherFrontendPromptCommandKind.ViewGameLog:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), "Prompt routed the shell to the live game log surface.");
                break;
            case LauncherFrontendPromptCommandKind.OpenInstanceSettings:
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup), "Prompt routed the shell to instance settings.");
                break;
            case LauncherFrontendPromptCommandKind.ExportCrashReport:
                AddActivity("Crash export intent issued.", _crashPlan.ExportPlan.SuggestedArchiveName);
                break;
            case LauncherFrontendPromptCommandKind.DownloadJavaRuntime:
                AddActivity("Java download intent issued.", command.Value ?? _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget ?? "No download target");
                break;
            case LauncherFrontendPromptCommandKind.OpenUrl:
                AddActivity("External URL intent issued.", command.Value ?? "No URL supplied");
                break;
            case LauncherFrontendPromptCommandKind.AppendLaunchArgument:
                AddActivity("Launch argument intent issued.", command.Value ?? "No argument supplied");
                break;
            case LauncherFrontendPromptCommandKind.SetTelemetryEnabled:
            case LauncherFrontendPromptCommandKind.AcceptConsent:
            case LauncherFrontendPromptCommandKind.RejectConsent:
            case LauncherFrontendPromptCommandKind.ContinueFlow:
            case LauncherFrontendPromptCommandKind.AbortLaunch:
            case LauncherFrontendPromptCommandKind.PersistSetting:
            case LauncherFrontendPromptCommandKind.ClosePrompt:
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity("Shell intent recorded.", DescribePromptCommand(command));
                break;
            default:
                AddActivity("Unhandled prompt command encountered.", command.Kind.ToString());
                break;
        }
    }

    private void ApplySidebarAccessory(string title, string actionLabel, string command)
    {
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

        AddActivity($"左侧操作: {actionLabel}", $"{title} • {command}");
    }

    private void AddActivity(string title, string body)
    {
        ActivityEntries.Insert(0, new ActivityItemViewModel(DateTime.Now.ToString("HH:mm:ss"), title, body));
        while (ActivityEntries.Count > 12)
        {
            ActivityEntries.RemoveAt(ActivityEntries.Count - 1);
        }

        RaisePropertyChanged(nameof(HasActivityEntries));
    }

    private void TogglePromptOverlay()
    {
        SetPromptOverlayOpen(!IsPromptOverlayVisible);
    }

    private void ToggleLaunchMigrationCard()
    {
        IsLaunchMigrationExpanded = !IsLaunchMigrationExpanded;
    }

    private void ToggleLaunchNewsCard()
    {
        IsLaunchNewsExpanded = !IsLaunchNewsExpanded;
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

    private void ResetGameLinkSurface()
    {
        InitializeGameLinkSurface();
        RaisePropertyChanged(nameof(LinkUsername));
        RaisePropertyChanged(nameof(SelectedProtocolPreferenceIndex));
        RaisePropertyChanged(nameof(PreferLowestLatencyPath));
        RaisePropertyChanged(nameof(TryPunchSymmetricNat));
        RaisePropertyChanged(nameof(AllowIpv6Communication));
        RaisePropertyChanged(nameof(EnableLinkCliOutput));
        AddActivity("重置联机设置", "EasyTier 设置已恢复到 Spike 的默认演示值。");
    }

    private void SetPromptOverlayOpen(bool isOpen)
    {
        if (_isPromptOverlayOpen == isOpen)
        {
            RaisePropertyChanged(nameof(IsPromptOverlayVisible));
            return;
        }

        _isPromptOverlayOpen = isOpen;
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }

    private void RaiseShellStateProperties()
    {
        RaisePropertyChanged(nameof(IsLaunchRoute));
        RaisePropertyChanged(nameof(IsStandardShellRoute));
        RaisePropertyChanged(nameof(IsSetupAboutSurface));
        RaisePropertyChanged(nameof(IsSetupFeedbackSurface));
        RaisePropertyChanged(nameof(IsSetupLogSurface));
        RaisePropertyChanged(nameof(IsSetupUpdateSurface));
        RaisePropertyChanged(nameof(IsSetupGameLinkSurface));
        RaisePropertyChanged(nameof(IsToolsHelpSurface));
        RaisePropertyChanged(nameof(IsGenericShellSurface));
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        RaiseUpdateSurfaceProperties();
    }

    private void RaiseCollectionStateProperties()
    {
        RaisePropertyChanged(nameof(HasSidebarEntries));
        RaisePropertyChanged(nameof(HasSidebarSections));
        RaisePropertyChanged(nameof(HasNoSidebarSections));
        RaisePropertyChanged(nameof(HasSurfaceFacts));
        RaisePropertyChanged(nameof(HasSurfaceSections));
        RaisePropertyChanged(nameof(HasUtilityEntries));
        RaisePropertyChanged(nameof(HasActivityEntries));
        RaisePropertyChanged(nameof(HasAboutProjectEntries));
        RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
        RaisePropertyChanged(nameof(HasFeedbackSections));
        RaisePropertyChanged(nameof(HasHelpTopicGroups));
        RaisePropertyChanged(nameof(HasNoHelpTopicGroups));
    }

    private void RaiseUpdateSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowAvailableUpdateCard));
        RaisePropertyChanged(nameof(ShowCurrentVersionCard));
        RaisePropertyChanged(nameof(ShowOptionalUpdateCard));
        RaisePropertyChanged(nameof(CurrentVersionDescription));
    }

    private static string DescribePromptOption(LauncherFrontendPromptOption option)
    {
        return option.Commands.Count == 0
            ? "No shell commands."
            : string.Join(", ", option.Commands.Select(DescribePromptCommand));
    }

    private static string DescribePromptCommand(LauncherFrontendPromptCommand command)
    {
        return command.Kind switch
        {
            LauncherFrontendPromptCommandKind.ContinueFlow => "Continue flow",
            LauncherFrontendPromptCommandKind.AcceptConsent => "Accept consent",
            LauncherFrontendPromptCommandKind.RejectConsent => "Reject consent",
            LauncherFrontendPromptCommandKind.OpenUrl => $"Open URL ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ExitLauncher => "Exit launcher",
            LauncherFrontendPromptCommandKind.SetTelemetryEnabled => $"Set telemetry = {command.Value ?? "n/a"}",
            LauncherFrontendPromptCommandKind.AbortLaunch => "Abort launch",
            LauncherFrontendPromptCommandKind.AppendLaunchArgument => $"Append launch arg ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.PersistSetting => $"Persist setting ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.DownloadJavaRuntime => $"Download Java ({command.Value ?? "n/a"})",
            LauncherFrontendPromptCommandKind.ClosePrompt => "Close prompt",
            LauncherFrontendPromptCommandKind.ViewGameLog => "Open game log",
            LauncherFrontendPromptCommandKind.OpenInstanceSettings => "Open instance settings",
            LauncherFrontendPromptCommandKind.ExportCrashReport => "Export crash report",
            _ => command.Kind.ToString()
        };
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendShellPlan shellPlan)
    {
        var content = LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            shellPlan.Navigation,
            shellPlan.StartupPlan,
            shellPlan.Consent,
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData()));

        if (shellPlan.Navigation.CurrentPage.Route.Page != LauncherFrontendPageKey.Launch)
        {
            return content;
        }

        return content with
        {
            Eyebrow = "启动主页",
            Summary = "基于原始启动页结构重建的 Avalonia 主窗口原型。",
            Facts =
            [
                new LauncherFrontendPageFact("账号", LaunchUserName),
                new LauncherFrontendPageFact("验证方式", LaunchAuthLabel),
                new LauncherFrontendPageFact("版本", LaunchVersionSubtitle),
                new LauncherFrontendPageFact("主页", "新闻主页")
            ],
            Sections =
            [
                new LauncherFrontendPageSection(
                    "快照版",
                    "25w20a",
                    [
                        "增加了由 Amos Roddy 创作的新音乐唱片《Tears》。",
                        "鞍具现在可以合成，并且能够用剪刀拆下。",
                        "刷怪蛋与部分实体的视觉表现获得了进一步统一。"
                    ]),
                new LauncherFrontendPageSection(
                    "迁移",
                    "新版主页结构",
                    [
                        "顶部入口、启动区和右侧内容区按原始比例重新收紧。",
                        "卡片标题、箭头、阴影和留白改回接近 PCL 的层级关系。"
                    ])
            ]
        };
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return
        [
            new LauncherFrontendPromptLaneSummary(
                "startup",
                "启动前",
                "许可、环境与首次启动提示。",
                _promptCatalog[SpikePromptLaneKind.Startup].Count,
                _selectedPromptLane == SpikePromptLaneKind.Startup),
            new LauncherFrontendPromptLaneSummary(
                "launch",
                "启动中",
                "启动前检查、赞助与 Java 下载提示。",
                _promptCatalog[SpikePromptLaneKind.Launch].Count,
                _selectedPromptLane == SpikePromptLaneKind.Launch),
            new LauncherFrontendPromptLaneSummary(
                "crash",
                "崩溃恢复",
                "崩溃输出与导出恢复提示。",
                _promptCatalog[SpikePromptLaneKind.Crash].Count,
                _selectedPromptLane == SpikePromptLaneKind.Crash)
        ];
    }

    private LauncherFrontendLaunchSurfaceData BuildLaunchSurfaceData()
    {
        var playerName = _launchPlan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
            ? authPlayerName
            : "Unknown player";
        var provider = _launchPlan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
            ? "Microsoft account"
            : "Authlib account";

        return new LauncherFrontendLaunchSurfaceData(
            _launchPlan.Scenario,
            provider,
            playerName,
            _launchPlan.LoginPlan.Steps.Count,
            _launchPlan.JavaWorkflow.RecommendedComponent is null
                ? $"Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion}"
                : $"{_launchPlan.JavaWorkflow.RecommendedComponent} (Java {_launchPlan.JavaWorkflow.RecommendedMajorVersion})",
            _launchPlan.JavaWorkflow.MissingJavaPrompt.DownloadTarget,
            $"{_launchPlan.ResolutionPlan.Width} x {_launchPlan.ResolutionPlan.Height}",
            _launchPlan.ClasspathPlan.Entries.Count,
            _launchPlan.ReplacementPlan.Values.Count,
            _launchPlan.NativesDirectory,
            _launchPlan.PrerunPlan.Options.TargetFilePath,
            _launchPlan.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite,
            _launchPlan.ScriptExportPlan is not null,
            _launchPlan.ScriptExportPlan?.TargetPath,
            _launchPlan.CompletionNotification.Message);
    }

    private LauncherFrontendCrashSurfaceData BuildCrashSurfaceData()
    {
        return new LauncherFrontendCrashSurfaceData(
            _crashPlan.ExportPlan.SuggestedArchiveName,
            _crashPlan.ExportPlan.ExportRequest.SourceFiles.Count,
            !string.IsNullOrWhiteSpace(_crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath),
            _crashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath);
    }

    private static SurfaceFactViewModel CreateSurfaceFact(LauncherFrontendPageFact fact, int index)
    {
        var palette = GetSurfacePalette(index);
        return new SurfaceFactViewModel(
            fact.Label,
            fact.Value,
            palette.Accent,
            palette.Background,
            palette.Border,
            palette.Foreground);
    }

    private static SurfaceSectionViewModel CreateSurfaceSection(LauncherFrontendPageSection section, int index)
    {
        var palette = GetSurfacePalette(index);
        return new SurfaceSectionViewModel(
            section.Eyebrow,
            section.Title,
            section.Lines.Select(line => new SurfaceLineViewModel(line, palette.Accent, palette.Foreground)).ToArray(),
            palette.Accent,
            palette.Background,
            palette.Border,
            palette.Foreground);
    }

    private static SurfacePalette GetSurfacePalette(int index)
    {
        return (index % 4) switch
        {
            0 => new SurfacePalette(
                Brush.Parse("#FFF3EA"),
                Brush.Parse("#F1D2BD"),
                Brush.Parse("#B05A2C"),
                Brush.Parse("#3D2C21")),
            1 => new SurfacePalette(
                Brush.Parse("#EAF7F4"),
                Brush.Parse("#C8E6DF"),
                Brush.Parse("#1B7A6F"),
                Brush.Parse("#234744")),
            2 => new SurfacePalette(
                Brush.Parse("#EEF3FE"),
                Brush.Parse("#D3DDF8"),
                Brush.Parse("#3E67B0"),
                Brush.Parse("#223855")),
            _ => new SurfacePalette(
                Brush.Parse("#F6F0FD"),
                Brush.Parse("#E2D6F6"),
                Brush.Parse("#6D4AA4"),
                Brush.Parse("#352645"))
        };
    }

    private static MinecraftLaunchPrecheckResult BuildLaunchPrecheckResult(string scenario)
    {
        var requiresAuth = string.Equals(scenario, "legacy-forge", StringComparison.OrdinalIgnoreCase);
        return MinecraftLaunchPrecheckService.Evaluate(new MinecraftLaunchPrecheckRequest(
            InstanceName: requiresAuth ? "Legacy Forge Demo" : "Modern Fabric Demo",
            InstancePathIndie: "/Users/demo/.pcl/instances/示例实例",
            InstancePath: "/Users/demo/.minecraft/instances/示例实例",
            IsInstanceSelected: true,
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: true,
            IsNonAsciiPathWarningDisabled: false,
            IsInstancePathAscii: false,
            ProfileValidationMessage: string.Empty,
            SelectedProfileKind: requiresAuth ? MinecraftLaunchProfileKind.Auth : MinecraftLaunchProfileKind.Microsoft,
            HasLabyMod: false,
            LoginRequirement: requiresAuth ? MinecraftLaunchLoginRequirement.Auth : MinecraftLaunchLoginRequirement.Microsoft,
            RequiredAuthServer: requiresAuth ? "https://auth.example.invalid/authserver" : null,
            SelectedAuthServer: requiresAuth ? "https://auth.example.invalid/authserver" : null,
            HasMicrosoftProfile: false,
            IsRestrictedFeatureAllowed: true));
    }

    private static NavigationPalette GetNavigationPalette(bool isSelected, NavigationVisualStyle style)
    {
        return style switch
        {
            NavigationVisualStyle.TopLevel when isSelected => new NavigationPalette(
                Brushes.White,
                Brushes.White,
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3")),
            NavigationVisualStyle.Sidebar when isSelected => new NavigationPalette(
                Brush.Parse("#EAF2FE"),
                Brush.Parse("#D5E6FD"),
                Brush.Parse("#343D4A"),
                Brush.Parse("#1370F3")),
            NavigationVisualStyle.Utility when isSelected => new NavigationPalette(
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3"),
                Brushes.White,
                Brush.Parse("#EAF2FE")),
            NavigationVisualStyle.TopLevel => new NavigationPalette(
                Brush.Parse("#01EAF2FE"),
                Brush.Parse("#01EAF2FE"),
                Brushes.White,
                Brush.Parse("#FFFFFF")),
            NavigationVisualStyle.Sidebar => new NavigationPalette(
                Brush.Parse("#01FFFFFF"),
                Brush.Parse("#01FFFFFF"),
                Brush.Parse("#404040"),
                Brush.Parse("#D5E6FD")),
            _ => new NavigationPalette(
                Brush.Parse("#1370F3"),
                Brush.Parse("#1370F3"),
                Brushes.White,
                Brush.Parse("#EAF2FE"))
        };
    }

    private static (string IconPath, double IconScale) GetNavigationIcon(string title)
    {
        return title switch
        {
            "启动" => ("M52.1,164.5c-1.4,0-3.1-0.5-4.2-1.3c-2.6-1.7-4-4.2-4-7V43.8c0-2.9,1.6-5.8,4.1-7c1.2-0.8,2.7-1.2,4.1-1.2c1.5,0,2.9,0.4,4.2,1.2L153.1,93c0,0,0.1,0,0.1,0.1c2.6,1.7,4,4.2,4,7c0,3-1.7,5.8-4.2,7.1l-96.8,56.2C55.1,164,53.5,164.5,52.1,164.5z M60.4,142.1l72.1-42.1L60.4,58.2V142.1z", 0.9),
            "下载" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29zM492 740c11 11 29 11 41 0l265-265c11-11 11-29 0-41l-41-41c-11-11-29-11-41 0l-110 110c-11 11-33 3-33-13V68C571 53 555 39 541 39h-59c-15 0-29 13-29 29v417c0 17-21 25-33 13l-110-110c-11-11-29-11-41 0L226 433c-11 11-11 29 0 41L492 740z", 0.9),
            "设置" => ("M940.4 463.7L773.3 174.2c-17.3-30-49.2-48.4-83.8-48.4H340.2c-34.6 0-66.5 18.5-83.8 48.4L89.2 463.7c-17.3 30-17.3 66.9 0 96.8L256.4 850c17.3 30 49.2 48.4 83.8 48.4h349.2c34.6 0 66.5-18.5 83.8-48.4l167.2-289.5c17.3-29.9 17.3-66.8 0-96.8z m-94.6 96.8L725.9 768.1c-17.3 30-49.2 48.4-83.8 48.4H387.5c-34.6 0-66.5-18.5-83.8-48.4L183.9 560.5c-17.3-30-17.3-66.9 0-96.8l119.8-207.5c17.3-30 49.2-48.4 83.8-48.4h254.6c34.6 0 66.5 18.5 83.8 48.4l119.8 207.5c17.3 30 17.3 66.9 0.1 96.8z M522.3 321.2c-2.5-0.1-5-0.2-7.5-0.2-119.9 0-214 110.3-186.3 235 15.8 70.9 71.5 126.6 142.4 142.4 17.5 3.9 34.7 5.4 51.4 4.7 102.1-3.9 183.6-87.9 183.6-191 0.1-103-81.5-187-183.6-190.9z m68.6 269.1c-18.5 18-43 28.9-68.6 30.7l-6 0.3c-30.2 0.4-58.6-11.4-79.7-33-19.5-20.1-30.7-47-30.9-75-0.3-29.6 11.1-57.4 32-78.3 20.6-20.6 48-32 77.2-32 2.5 0 5 0.1 7.5 0.3 26.7 1.8 51.5 13.2 70.5 32.5 19.6 20 30.8 46.9 31.2 74.9 0.2 30.2-11.5 58.6-33.2 79.6z", 1.1),
            "工具" => ("M623.0016 208.5376c-103.6288-103.6288-269.4144-103.6288-352.256-20.736L415.744 332.8512 332.8 415.7952 187.8016 270.6944c-82.944 82.944-82.944 248.6784 20.736 352.3072 66.56 66.6112 158.9248 88.32 276.8896 64.9728l13.2608-2.7648 198.656 198.656a41.472 41.472 0 0 0 54.7328 3.4304l3.8912-3.4304 127.8976-127.8976a41.472 41.472 0 0 0 3.4304-54.7328l-3.4304-3.8912-198.656-198.656c27.648-124.3648 6.912-221.0816-62.208-290.1504z m-253.2352-9.6256l1.1776-0.4096c64.9728-20.736 150.6304-3.4816 208.0768 54.016 50.6368 50.5344 67.4816 121.7024 48.128 220.16l-2.56 12.4928-7.4752 33.28 208.1792 208.1792-98.6624 98.6112-208.128-208.128-33.28 7.3728c-105.0624 23.3472-180.0192 7.2704-232.704-45.4656-55.04-54.9376-73.216-135.68-56.5248-199.5264l2.9696-9.728L332.8 503.6544 503.7056 332.8 369.7664 198.912z", 1.0),
            _ => ("", 1.0)
        };
    }

    private static string GetUtilityIcon(string id)
    {
        return id switch
        {
            "back" => "M858.496 188.9024 173.1072 188.9024c-30.2848 0-54.8352-24.5504-54.8352-54.8352L118.272 106.6496c0-30.2848 24.5504-54.8352 54.8352-54.8352l685.3888 0c30.2848 0 54.8352 24.5504 54.8352 54.8352l0 27.4176C913.3312 164.352 888.7808 188.9024 858.496 188.9024L858.496 188.9024zM150.6048 550.8608c0 0 300.0064-240.3584 303.0272-243.328 13.9776-13.5936 31.1808-21.8624 48.8192-24.7552 1.7152-0.3072 3.4304-0.5888 5.1456-0.768 2.7392-0.3072 5.4528-0.3584 8.192-0.3328 2.7392-0.0256 5.4272 0.0256 8.1664 0.3328 1.7408 0.1792 3.4304 0.4864 5.1456 0.768 17.664 2.8928 34.8672 11.1616 48.8192 24.7552 3.0464 2.944 303.0016 243.328 303.0016 243.328 32.384 31.5136 29.6192 63.9744-2.7392 95.5136-32.3328 31.5392-75.648 2.9696-108.0064-28.544l-185.8816-147.1232 0 485.8368c0 30.3104-24.5248 54.8608-54.8352 54.8608l-27.392 0c-30.2848 0-54.8352-24.5504-54.8352-54.8608L447.232 470.7072l-185.8304 147.0976c-32.3584 31.5392-75.6992 60.1344-108.032 28.5696C121.0368 614.8352 118.272 582.3744 150.6048 550.8608L150.6048 550.8608zM150.6048 550.8608",
            "task-manager" => "M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29zM492 740c11 11 29 11 41 0l265-265c11-11 11-29 0-41l-41-41c-11-11-29-11-41 0l-110 110c-11 11-33 3-33-13V68C571 53 555 39 541 39h-59c-15 0-29 13-29 29v417c0 17-21 25-33 13l-110-110c-11-11-29-11-41 0L226 433c-11 11-11 29 0 41L492 740z",
            "game-log" => "M1091.291429 0H78.935771C35.34848 0.035109 0.029257 35.354331 0 78.935771v863.331475c0 43.534629 35.401143 78.994286 78.935771 78.994285H1091.291429c43.534629 0 78.994286-35.401143 78.994285-78.994285V78.871406C1170.156983 35.319223 1134.849463 0.064366 1091.291429 0z m-8.835658 87.771429v78.754377H87.771429v-78.760229h994.684342zM87.771429 933.425737V254.232869h994.684342v679.140205H87.771429v0.058515zM724.95104 340.00896l-206.19264 547.605943a43.903269 43.903269 0 0 1-82.154057-31.012572l206.139977-547.547428a43.944229 43.944229 0 0 1 82.20672 30.954057zM369.558674 545.909029l-85.489371 85.489371 85.489371 85.542034a43.885714 43.885714 0 0 1-62.025143 62.083657l-116.554605-116.560457a43.8272 43.8272 0 0 1 0-62.025143l116.560457-116.49024a43.885714 43.885714 0 0 1 62.019291 61.966629z m610.567315-37.566172a43.885714 43.885714 0 0 1 0 62.083657l-116.560458 116.560457a43.768686 43.768686 0 0 1-62.019291 0 43.885714 43.885714 0 0 1 0-62.083657l85.547886-85.547885-85.547886-85.542035a43.897417 43.897417 0 0 1 62.083657-62.083657l116.496092 116.618972z",
            _ => string.Empty
        };
    }

    private static string GetSidebarSectionTitle(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage)
    {
        return page switch
        {
            LauncherFrontendPageKey.Download when subpage == LauncherFrontendSubpageKey.DownloadInstall => string.Empty,
            LauncherFrontendPageKey.Download when subpage is LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld
                or LauncherFrontendSubpageKey.DownloadCompFavorites => "社区资源",
            LauncherFrontendPageKey.Download => "安装器",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupLaunch
                or LauncherFrontendSubpageKey.SetupJava
                or LauncherFrontendSubpageKey.SetupGameManage => "游戏",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupLink
                or LauncherFrontendSubpageKey.SetupGameLink => "工具",
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupUI
                or LauncherFrontendSubpageKey.SetupLauncherMisc => "启动器",
            LauncherFrontendPageKey.Setup => "关于",
            LauncherFrontendPageKey.Tools when subpage == LauncherFrontendSubpageKey.ToolsGameLink => "联机",
            LauncherFrontendPageKey.Tools => "奇妙小工具",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionOverall
                or LauncherFrontendSubpageKey.VersionSetup
                or LauncherFrontendSubpageKey.VersionExport => "实例",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionWorld
                or LauncherFrontendSubpageKey.VersionScreenshot => "内容",
            LauncherFrontendPageKey.InstanceSetup when subpage is LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic => "资源",
            LauncherFrontendPageKey.InstanceSetup => "其他",
            LauncherFrontendPageKey.VersionSaves => "存档",
            _ => string.Empty
        };
    }

    private static (string IconPath, double IconScale) GetSidebarIcon(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage, string title)
    {
        return page switch
        {
            LauncherFrontendPageKey.Download => title switch
            {
                "自动安装" => ("M17.5 2.00586c-0.56635 0-1.13224 0.212382-1.55859 0.638672l-5.29688 5.29687c-0.85258 0.852707-0.85258 2.26448 0 3.11719l2.29688 2.29688c0.852708 0.85258 2.26448 0.85258 3.11719 0l5.29688-5.29688c0.85258-0.852707 0.85258-2.26448 0-3.11719L19.0586 2.64453C18.6322 2.21824 18.0663 2.00586 17.5 2.00586Z", 0.95),
                "Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28a40.448 40.448 0 0 0-40.448 39.936v77.312a34.816 34.816 0 0 1-34.816 35.328H204.8a41.984 41.984 0 0 1-40.448-42.496v-200.192a35.328 35.328 0 0 1 35.328-35.328h72.704a39.936 39.936 0 0 0 39.936-39.936v-37.888a39.936 39.936 0 0 0-39.936-40.448H199.68a35.328 35.328 0 0 1-35.328-35.328V287.744A41.984 41.984 0 0 1 204.8 245.76h176.64v-32.768a102.4 102.4 0 0 1 102.4-102.4h33.792a102.4 102.4 0 0 1 102.4 102.4v32.768h170.496a41.984 41.984 0 0 1 41.984 41.984V460.8h28.672a102.4 102.4 0 0 1 102.4 102.4v33.792a102.4 102.4 0 0 1-102.4 102.4h-28.672V870.4a41.984 41.984 0 0 1-43.008 42.496z", 0.97),
                "整合包" => ("M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13a128 128 0 0 1 112 0l383 190a126 126 0 0 1 72 113v360a126 126 0 0 1-70 115L568 984c-17 7-37 11-57 11z", 0.98),
                "数据包" => ("M445 545c18 0 33 14 33 33v149a182 182 0 1 1-182-182z m282 0a182 182 0 1 1-182 182v-149c0-18 14-33 33-33zM412 611H296a116 116 0 1 0 116 116V611z", 0.96),
                "资源包" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6c0-41.9-34.1-76-76-76z", 0.81),
                "光影包" => ("M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 213c-166 0-298 132-298 298s132 298 298 298 298-132 298-298-132-298-298-298z", 1.04),
                "存档" => ("M17.9 17.39C17.64 16.59 16.89 16 16 16H15V13A1 1 0 0 0 14 12H8V10H10A1 1 0 0 0 11 9V7H13A2 2 0 0 0 15 5V4.59C17.93 5.77 20 8.64 20 12C20 14.08 19.2 15.97 17.9 17.39", 0.9),
                "收藏夹" => ("M10.3633 4.94727C9.49901 4.9267 8.70665 5.26821 8.10156 5.79883C7.49648 6.32944 7.05536 7.07013 6.96289 7.92969C6.87042 8.78924 7.18013 9.74328 7.89844 10.4922l2.59375 2.82226c0.784548 0.900757 2.22912 0.900759 3.01367 0l2.59766-2.82617", 0.95),
                "客户端" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29z", 0.9),
                "OptiFine" => ("M439.667 538.133l-323.311-174.763c-6.554-3.641-13.836-5.098-20.389-5.098-23.301 0-45.147 20.389-45.147 48.788v341.516c0 35.681 18.205 67.721 48.06 83.741l323.311 174.763", 1.0),
                "Forge" => ("M402.807089 481.189274c-12.040221-6.991228-27.412326-2.797719-34.397415 9.242502l-8.387018 14.512529c-6.987135 12.035104-2.793626 27.40721 9.246595 34.392298", 1.0),
                "NeoForge" => ("M544.6 921.2l-23.7 32.6c14.1 10.2 33.3 10.2 47.5 0l-23.7-32.6zM114.6 608.5l-23.9 32.5 0.2 0.1 23.6-32.6z", 0.95),
                "Cleanroom" => ("M718.1 653.2c-8.5-10.5-25.6-31.1-37.9-45.9-12.3-14.7-22.2-27-22-27.1.2-.2 10-.6 21.8-1 9.1-.6 17.1-.4 25.6-2.3", 0.95),
                "Fabric" => ("M826.453333 170.666667c-100.266667-93.866667-248.96-76.373333-256-75.52a31.786667 31.786667 0 0 0-26.666666 22.613333c-1.066667 3.2-101.546667 330.666667-426.666667 445.013333", 1.02),
                "Quilt" => ("M115.6 140.4H57.2c-8.9 0-16.1 7.2-16.1 16.1V215c0 8.9 7.2 16.1 16.1 16.1h58.5c8.9 0 16.1-7.2 16.1-16.1v-58.5C131.7 147.6 124.5 140.4 115.6 140.4Z", 1.02),
                "LiteLoader" => ("M517.41 186.99l22.45 83.78h-55.04l-18.76-70.02c-2.85-10.63-13.78-16.95-24.42-14.1-10.63 2.85-16.95 13.78-14.1 24.42", 1.06),
                "LabyMod" => ("M710 350L805 350 945 250 920 470 990 565 758 820 525 565 590 470 560 250 710 350", 1.02),
                "Legacy Fabric" => ("M826.453333 170.666667c-100.266667-93.866667-248.96-76.373333-256-75.52a31.786667 31.786667 0 0 0-26.666666 22.613333c-1.066667 3.2-101.546667 330.666667-426.666667 445.013333", 1.02),
                _ => ("", 1.0)
            },
            LauncherFrontendPageKey.Setup => title switch
            {
                "启动" => ("M924.009688 527.92367C875.975633 601.905321 808.729835 663.317138 733.564477 712.920073 726.302532 832.878385 665.670459 944.569541 567.084624 1015.075229", 1.0),
                "Java" => ("M6 1A1 1 0 0 0 5 2V4A1 1 0 0 0 6 5A1 1 0 0 0 7 4V2A1 1 0 0 0 6 1ZM4 7C2.90728 7 2 7.90728 2 9v8c0 2.74958 2.25042 5 5 5h6", 1.0),
                "游戏管理" => ("M224 423.84V231.744l192-0.096 0.096 192.096L224 423.84z m192.096-256.096H223.904A64 64 0 0 0 160 231.68v192.192a64 64 0 0 0 63.904 63.904h192.192", 0.95),
                "联机" => ("M7.5 1C5.57885 1 4 2.57885 4 4.5C4 6.42115 5.57885 8 7.5 8C9.42115 8 11 6.42115 11 4.5C11 2.57885 9.42115 1 7.5 1Z", 1.0),
                "界面" => ("M106 755a59 59 0 0 1-12 9c-12 7-25 8-38 5-27-10-56 11-53 40 24 224 378 233 468 10a39 39 0 0 0-8-42l-178-176", 1.0),
                "启动器杂项" => ("M4 13c-1.0907 0-2 0.909297-2 2v5c0 1.0907 0.909297 2 2 2h5c1.0907 0 2-0.909297 2-2v-5c0-1.0907-0.909297-2-2-2z", 0.95),
                "关于" => ("M149.883623 873.911618c47.094581 47.094581 101.765247 83.95121 162.783444 109.75085 63.065787 26.618676 130.226755 40.337532 199.230553 40.337532", 0.95),
                "更新" => ("M12 1a1 1 0 0 0-1 1 1 1 0 0 0 1 1c3.9525-0.0007205 6.89118 2.31366 8.23828 5.37109 1.3471 3.05744 1.07224 6.7869-1.5957 9.70313", 1.0),
                "反馈" => ("M613.717333 64.426667l3.413334 3.242666 331.861333 331.861334a85.333333 85.333333 0 0 1 3.2 117.269333l-3.2 3.413333L573.162667 896H960", 0.9),
                "日志" => ("M4 2C3.27778 2 2.54212 2.23535 1.96094 2.75195C1.37976 3.26856 1 4.08333 1 5v2c0 1.09272 0.907275 2 2 2h2v10c0 0.916666 0.379756 1.73144 0.960938 2.24805", 0.9),
                "游戏联机" => ("M554.496 170.496c141.312 0 256 114.688 256 256s-114.688 256-256 256H402.432c-22.016-1.536-39.424-19.968-39.424-42.496", 0.85),
                _ => ("", 1.0)
            },
            LauncherFrontendPageKey.Tools => title switch
            {
                "联机大厅" => ("M554.496 170.496c141.312 0 256 114.688 256 256s-114.688 256-256 256H402.432c-22.016-1.536-39.424-19.968-39.424-42.496", 0.85),
                "测试" => ("M511 995a128 128 0 0 1-57-13L70 791a126 126 0 0 1-70-113V311a126 126 0 0 1 15-60V248c1-2 3-5 5-8a127 127 0 0 1 49-42L454 13", 0.9),
                "帮助" => ("M520.6 620.3c-11.3-0.6-20.1-4-26.9-10.5-6.6-6.3-8.1-11-10.1-20.8-1.9-9.5-1.5-16.7-1-24.9l0.3-5.3c0.5-9.9 3.5-19.6 8.8-28.1", 0.97),
                _ => ("", 1.0)
            },
            _ => title switch
            {
                "概览" => ("M149.883623 873.911618c47.094581 47.094581 101.765247 83.95121 162.783444 109.75085 63.065787 26.618676 130.226755 40.337532 199.230553 40.337532", 0.95),
                "设置" => ("M940.4 463.7L773.3 174.2c-17.3-30-49.2-48.4-83.8-48.4H340.2c-34.6 0-66.5 18.5-83.8 48.4L89.2 463.7", 1.0),
                "导出" => ("M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59", 0.9),
                "世界" => ("M17.9 17.39C17.64 16.59 16.89 16 16 16H15V13A1 1 0 0 0 14 12H8V10H10A1 1 0 0 0 11 9V7H13A2 2 0 0 0 15 5V4.59", 0.9),
                "截图" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6", 0.81),
                "资源包" => ("M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6", 0.81),
                "光影包" => ("M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 213c-166 0-298 132-298 298s132 298 298 298", 1.04),
                "Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28", 0.97),
                "已禁用 Mod" => ("M789.504 912.896h-195.072a35.328 35.328 0 0 1-34.816-35.328v-77.312a39.936 39.936 0 0 0-40.448-39.936H481.28", 0.97),
                "安装" => ("M17.5 2.00586c-0.56635 0-1.13224 0.212382-1.55859 0.638672l-5.29688 5.29687c-0.85258 0.852707-0.85258 2.26448 0 3.11719", 0.95),
                "服务器" => ("M7.5 1C5.57885 1 4 2.57885 4 4.5C4 6.42115 5.57885 8 7.5 8C9.42115 8 11 6.42115 11 4.5C11 2.57885 9.42115 1 7.5 1Z", 1.0),
                _ => ("", 1.0)
            }
        };
    }

    private static (string ToolTip, string IconPath, string ActionLabel, string? Command) GetSidebarAccessory(LauncherFrontendPageKey page, LauncherFrontendSubpageKey subpage, string title)
    {
        const string refreshIcon = "M875.52 148.48C783.36 56.32 655.36 0 512 0 291.84 0 107.52 138.24 30.72 332.8l122.88 46.08C204.8 230.4 348.16 128 512 128c107.52 0 199.68 40.96 271.36 112.64L640 384h384V0L875.52 148.48zM512 896c-107.52 0-199.68-40.96-271.36-112.64L384 640H0v384l148.48-148.48C240.64 967.68 368.64 1024 512 1024c220.16 0 404.48-138.24 481.28-332.8L870.4 645.12C819.2 793.6 675.84 896 512 896z";
        const string resetIcon = "M530 0c287 0 521 229 521 511s-233 511-521 511c-233 0-436-151-500-368a63 63 0 0 1 44-79 65 65 0 0 1 80 43c48 162 200 276 375 276 215 0 390-171 390-383s-174-383-390-383c-103 0-199 39-270 106l21-5a63 63 0 0 1 33 123l-157 42a65 65 0 0 1-90-42l-49-183a65 65 0 1 1 126-33l6 26A524 524 0 0 1 530 0z";

        return page switch
        {
            LauncherFrontendPageKey.Download => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Tools => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when subpage is LauncherFrontendSubpageKey.SetupJava
                or LauncherFrontendSubpageKey.SetupFeedback
                or LauncherFrontendSubpageKey.SetupUpdate => ("刷新", refreshIcon, "刷新", $"刷新 {title} 页面"),
            LauncherFrontendPageKey.Setup when title is "关于" or "日志" => (string.Empty, string.Empty, string.Empty, null),
            LauncherFrontendPageKey.Setup => ("初始化设置", resetIcon, "重置", $"初始化 {title} 页面设置"),
            _ => (string.Empty, string.Empty, string.Empty, null)
        };
    }

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }

    private static string GetLauncherAssetPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine([LauncherRootDirectory, .. segments]));
    }

    private static Bitmap? LoadLauncherBitmap(params string[] segments)
    {
        var filePath = GetLauncherAssetPath(segments);
        return File.Exists(filePath) ? new Bitmap(filePath) : null;
    }
}

internal sealed class NavigationEntryViewModel(
    string title,
    string summary,
    string meta,
    bool isSelected,
    string iconPath,
    double iconScale,
    NavigationPalette palette,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Meta { get; } = meta;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public IBrush BackgroundBrush { get; } = palette.Background;

    public IBrush BorderBrush { get; } = palette.Border;

    public IBrush ForegroundBrush { get; } = palette.Foreground;

    public IBrush AccentBrush { get; } = palette.Accent;

    public ActionCommand Command { get; } = command;
}

internal sealed class SidebarSectionViewModel(
    string title,
    bool hasTitle,
    IReadOnlyList<SidebarListItemViewModel> items)
{
    public string Title { get; } = title;

    public bool HasTitle { get; } = hasTitle;

    public IReadOnlyList<SidebarListItemViewModel> Items { get; } = items;
}

internal sealed class SidebarListItemViewModel(
    string title,
    string summary,
    bool isSelected,
    string iconPath,
    double iconScale,
    ActionCommand command,
    string accessoryToolTip,
    string accessoryIconPath,
    ActionCommand? accessoryCommand)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public ActionCommand Command { get; } = command;

    public string AccessoryToolTip { get; } = accessoryToolTip;

    public string AccessoryIconPath { get; } = accessoryIconPath;

    public ActionCommand? AccessoryCommand { get; } = accessoryCommand;
}

internal sealed class SurfaceFactViewModel(
    string label,
    string value,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Label { get; } = label;

    public string Value { get; } = value;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceSectionViewModel(
    string eyebrow,
    string title,
    IReadOnlyList<SurfaceLineViewModel> lines,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Eyebrow { get; } = eyebrow;

    public string Title { get; } = title;

    public IReadOnlyList<SurfaceLineViewModel> Lines { get; } = lines;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceLineViewModel(
    string text,
    IBrush accentBrush,
    IBrush foregroundBrush)
{
    public string Text { get; } = text;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class ActivityItemViewModel(string time, string title, string body)
{
    public string Time { get; } = time;

    public string Title { get; } = title;

    public string Body { get; } = body;
}

internal sealed class AboutEntryViewModel(
    string title,
    string info,
    Bitmap? avatar,
    string? actionText,
    ActionCommand? actionCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public Bitmap? Avatar { get; } = avatar;

    public string ActionText { get; } = actionText ?? string.Empty;

    public ActionCommand? ActionCommand { get; } = actionCommand;

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionText);
}

internal sealed class SimpleListEntryViewModel(string title, string info, ActionCommand command)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public ActionCommand Command { get; } = command;
}

internal sealed class HelpTopicViewModel(
    string groupTitle,
    string title,
    string summary,
    ActionCommand command)
{
    public string GroupTitle { get; } = groupTitle;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public ActionCommand Command { get; } = command;
}

internal sealed class HelpTopicGroupViewModel(string title, IReadOnlyList<HelpTopicViewModel> items)
{
    public string Title { get; } = title;

    public IReadOnlyList<HelpTopicViewModel> Items { get; } = items;
}

internal sealed class FeedbackSectionViewModel(
    string title,
    IReadOnlyList<SimpleListEntryViewModel> items,
    bool isExpanded) : ViewModelBase
{
    private bool _isExpanded = isExpanded;

    public string Title { get; } = title;

    public IReadOnlyList<SimpleListEntryViewModel> Items { get; } = items;

    public ActionCommand ToggleCommand => new(() => IsExpanded = !IsExpanded);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

internal sealed class PromptLaneViewModel(
    SpikePromptLaneKind kind,
    string title,
    string summary,
    ActionCommand command) : ViewModelBase
{
    private int _count;
    private bool _isSelected;

    public SpikePromptLaneKind Kind { get; } = kind;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public ActionCommand Command { get; } = command;

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(BackgroundBrush));
                RaisePropertyChanged(nameof(BorderBrush));
                RaisePropertyChanged(nameof(ForegroundBrush));
            }
        }
    }

    public IBrush BackgroundBrush => IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#01EAF2FE");

    public IBrush BorderBrush => IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#D5E6FD");

    public IBrush ForegroundBrush => IsSelected ? Brushes.White : Brush.Parse("#404040");
}

internal sealed class PromptCardViewModel(
    SpikePromptLaneKind lane,
    string id,
    string title,
    string message,
    string source,
    string severity,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IReadOnlyList<PromptOptionViewModel> options)
{
    public SpikePromptLaneKind Lane { get; } = lane;

    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Source { get; } = source;

    public string Severity { get; } = severity;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IReadOnlyList<PromptOptionViewModel> Options { get; } = options;
}

internal sealed class PromptOptionViewModel(
    string label,
    string detail,
    ActionCommand command)
{
    public string Label { get; } = label;

    public string Detail { get; } = detail;

    public ActionCommand Command { get; } = command;
}

internal sealed record NavigationPalette(
    IBrush Background,
    IBrush Border,
    IBrush Foreground,
    IBrush Accent);

internal sealed record SurfacePalette(
    IBrush Background,
    IBrush Border,
    IBrush Accent,
    IBrush Foreground);

internal enum NavigationVisualStyle
{
    TopLevel = 0,
    Sidebar = 1,
    Utility = 2
}

internal enum SpikePromptLaneKind
{
    Startup = 0,
    Launch = 1,
    Crash = 2
}

internal enum UpdateSurfaceState
{
    Available = 0,
    Latest = 1
}
