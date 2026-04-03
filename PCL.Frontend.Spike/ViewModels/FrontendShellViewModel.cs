using System.Collections.ObjectModel;
using Avalonia.Media;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed class FrontendShellViewModel : ViewModelBase
{
    private readonly ShellSpikeInputs _shellInputs;
    private readonly StartupSpikePlan _startupPlan;
    private readonly LaunchSpikePlan _launchPlan;
    private readonly CrashSpikePlan _crashPlan;
    private readonly Dictionary<SpikePromptLaneKind, List<PromptCardViewModel>> _promptCatalog;
    private readonly List<LauncherFrontendRoute> _routeHistory = [];
    private readonly ActionCommand _backCommand;
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

    private FrontendShellViewModel(SpikeCommandOptions options)
    {
        _shellInputs = SpikeInputResolver.ResolveShellInputs(options);
        _startupPlan = SpikeSampleFactory.BuildStartupPlan(_shellInputs.StartupInputs);
        _launchPlan = SpikeSampleFactory.BuildLaunchPlan(SpikeInputResolver.ResolveLaunchInputs(options), options.SaveBatchPath);
        _crashPlan = SpikeSampleFactory.BuildCrashPlan(SpikeInputResolver.ResolveCrashInputs(options));
        _currentRoute = _shellInputs.NavigationRequest.CurrentRoute;
        _selectedPromptLane = SpikePromptLaneKind.Startup;
        _backCommand = new ActionCommand(NavigateBack, () => CanGoBack);

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

    public bool HasActivePrompts => ActivePrompts.Count > 0;

    public bool HasNoActivePrompts => !HasActivePrompts;

    public bool CanGoBack
    {
        get => _canGoBack;
        private set
        {
            if (SetProperty(ref _canGoBack, value))
            {
                _backCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public ActionCommand BackCommand => _backCommand;

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
            "Startup",
            "Consent, environment, and first-run prompts.",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Startup))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Launch,
            "Launch",
            "Precheck, support, and Java download prompts.",
            new ActionCommand(() => SelectPromptLane(SpikePromptLaneKind.Launch))));
        PromptLanes.Add(new PromptLaneViewModel(
            SpikePromptLaneKind.Crash,
            "Crash",
            "Output and export recovery prompts.",
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

        SelectPromptLane(_selectedPromptLane, updateActivity: false);
        AddActivity(activityMessage, $"{shellPlan.Navigation.CurrentPage.Title} • {shellPlan.Navigation.CurrentPage.Route.Page}/{shellPlan.Navigation.CurrentPage.Route.Subpage}");
    }

    private NavigationEntryViewModel CreateNavigationEntry(LauncherFrontendNavigationEntry entry, NavigationVisualStyle style)
    {
        return new NavigationEntryViewModel(
            entry.Title,
            entry.Summary,
            style == NavigationVisualStyle.Sidebar ? entry.Route.Subpage.ToString() : entry.Route.Page.ToString(),
            GetNavigationPalette(entry.IsSelected, style),
            new ActionCommand(() => NavigateTo(entry.Route, $"Navigated to {entry.Title} from the {(style == NavigationVisualStyle.Sidebar ? "sidebar" : "top bar")}."))
        );
    }

    private NavigationEntryViewModel CreateUtilityEntry(LauncherFrontendUtilityEntry entry)
    {
        return new NavigationEntryViewModel(
            entry.Title,
            entry.IsSelected ? "Utility surface is active in the shell." : "Pinned shell utility surface.",
            entry.Route.Page.ToString(),
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

        var selectedLane = PromptLanes.First(item => item.Kind == lane);
        PromptInboxTitle = $"{selectedLane.Title} prompt lane";
        PromptInboxSummary = selectedLane.Summary;
        PromptEmptyState = $"No queued {selectedLane.Title.ToLowerInvariant()} prompts remain in this prototype shell.";
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceItems(SurfaceFacts, pageContent.Facts.Select((fact, index) => CreateSurfaceFact(fact, index)));
        ReplaceItems(SurfaceSections, pageContent.Sections.Select((section, index) => CreateSurfaceSection(section, index)));

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
        return LauncherFrontendPageContentService.Build(new LauncherFrontendPageContentRequest(
            shellPlan.Navigation,
            shellPlan.StartupPlan,
            shellPlan.Consent,
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData()));
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return
        [
            new LauncherFrontendPromptLaneSummary(
                "startup",
                "Startup",
                "Consent, environment, and first-run prompts.",
                _promptCatalog[SpikePromptLaneKind.Startup].Count,
                _selectedPromptLane == SpikePromptLaneKind.Startup),
            new LauncherFrontendPromptLaneSummary(
                "launch",
                "Launch",
                "Precheck, support, and Java download prompts.",
                _promptCatalog[SpikePromptLaneKind.Launch].Count,
                _selectedPromptLane == SpikePromptLaneKind.Launch),
            new LauncherFrontendPromptLaneSummary(
                "crash",
                "Crash",
                "Output and export recovery prompts.",
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
                Brush.Parse("#0E4D57"),
                Brush.Parse("#0E4D57"),
                Brushes.White,
                Brush.Parse("#86E4D3")),
            NavigationVisualStyle.Sidebar when isSelected => new NavigationPalette(
                Brush.Parse("#EAF6F5"),
                Brush.Parse("#1D7C73"),
                Brush.Parse("#144640"),
                Brush.Parse("#1D7C73")),
            NavigationVisualStyle.Utility when isSelected => new NavigationPalette(
                Brush.Parse("#EEF1FE"),
                Brush.Parse("#375F9C"),
                Brush.Parse("#1E3563"),
                Brush.Parse("#375F9C")),
            NavigationVisualStyle.TopLevel => new NavigationPalette(
                Brush.Parse("#F5FAFA"),
                Brush.Parse("#D5E7E8"),
                Brush.Parse("#21454B"),
                Brush.Parse("#8AB7BC")),
            NavigationVisualStyle.Sidebar => new NavigationPalette(
                Brush.Parse("#FCF7F0"),
                Brush.Parse("#E7DAC9"),
                Brush.Parse("#5B4A33"),
                Brush.Parse("#D4A46B")),
            _ => new NavigationPalette(
                Brush.Parse("#F4F4FA"),
                Brush.Parse("#E1E0F3"),
                Brush.Parse("#37335A"),
                Brush.Parse("#8C82C7"))
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
    NavigationPalette palette,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Meta { get; } = meta;

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

    public IBrush BackgroundBrush => IsSelected ? Brush.Parse("#133F49") : Brush.Parse("#F4FBFB");

    public IBrush BorderBrush => IsSelected ? Brush.Parse("#133F49") : Brush.Parse("#CFE3E5");

    public IBrush ForegroundBrush => IsSelected ? Brushes.White : Brush.Parse("#21454B");
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
