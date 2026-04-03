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
    private static readonly string LaunchAvatarImageFilePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "Plain Craft Launcher 2",
        "Images",
        "Heads",
        "PCL-Community.png"));
    private static readonly string LaunchNewsImageFilePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "Plain Craft Launcher 2",
        "Images",
        "Backgrounds",
        "server_bg.png"));
    private readonly ShellSpikeInputs _shellInputs;
    private readonly StartupSpikePlan _startupPlan;
    private readonly LaunchSpikePlan _launchPlan;
    private readonly CrashSpikePlan _crashPlan;
    private readonly Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> _promptCatalog;
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

        ScenarioLabel = $"Scenario: {options.Scenario}";
        EnvironmentLabel = options.UseHostEnvironment ? "Host-backed shell inputs" : "Fixture-driven shell inputs";
        InputLabel = string.IsNullOrWhiteSpace(options.InputRoot) ? "Using built-in frontend fixtures" : $"Input root: {options.InputRoot}";

        _promptCatalog = BuildPromptCatalog(options.Scenario);
        InitializePromptLanes();
        RefreshShell("Shell initialized from portable frontend contracts.");
    }

    public ObservableCollection<NavigationEntryViewModel> TopLevelEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> SidebarEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> UtilityEntries { get; } = [];

    public ObservableCollection<SurfaceFactViewModel> SurfaceFacts { get; } = [];

    public ObservableCollection<SurfaceSectionViewModel> SurfaceSections { get; } = [];

    public ObservableCollection<ActivityItemViewModel> ActivityEntries { get; } = [];

    public ObservableCollection<PromptLaneViewModel> PromptLanes { get; } = [];

    public ObservableCollection<PromptCardViewModel> ActivePrompts { get; } = [];

    public string ScenarioLabel { get; }

    public string EnvironmentLabel { get; }

    public string InputLabel { get; }

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

    public bool HasSurfaceFacts => SurfaceFacts.Count > 0;

    public bool HasSurfaceSections => SurfaceSections.Count > 0;

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool HasUtilityEntries => UtilityEntries.Count > 0;

    public string TitleBarLabel => _currentNavigation?.CurrentPage.SidebarItemTitle
        ?? _currentNavigation?.CurrentPage.Title
        ?? Title;

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
        RaisePropertyChanged(nameof(ShowTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowInnerNavigation));
        RaisePropertyChanged(nameof(TitleBarLabel));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }

    private void RaiseCollectionStateProperties()
    {
        RaisePropertyChanged(nameof(HasSidebarEntries));
        RaisePropertyChanged(nameof(HasSurfaceFacts));
        RaisePropertyChanged(nameof(HasSurfaceSections));
        RaisePropertyChanged(nameof(HasUtilityEntries));
        RaisePropertyChanged(nameof(HasActivityEntries));
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

    private static void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
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
