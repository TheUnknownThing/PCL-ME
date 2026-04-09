using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel : ViewModelBase
{
    public event EventHandler<ShellNavigationTransitionEventArgs>? NavigationTransitionRequested;

    public ObservableCollection<NavigationEntryViewModel> TopLevelEntries { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> SidebarEntries { get; } = [];

    public ObservableCollection<SidebarSectionViewModel> SidebarSections { get; } = [];

    public ObservableCollection<NavigationEntryViewModel> UtilityEntries { get; } = [];

    public ObservableCollection<SurfaceFactViewModel> SurfaceFacts { get; } = [];

    public ObservableCollection<SurfaceSectionViewModel> SurfaceSections { get; } = [];

    public ObservableCollection<ActivityItemViewModel> ActivityEntries { get; } = [];

    public ObservableCollection<PromptLaneViewModel> PromptLanes { get; } = [];

    public ObservableCollection<PromptCardViewModel> ActivePrompts { get; } = [];

    public ObservableCollection<LaunchProfileEntryViewModel> LaunchProfileEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutProjectEntries { get; } = [];

    public ObservableCollection<AboutEntryViewModel> AboutAcknowledgementEntries { get; } = [];

    public ObservableCollection<FeedbackSectionViewModel> FeedbackSections { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> LogEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPolicyEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPlayerEntries { get; } = [];

    public ObservableCollection<ToolboxActionViewModel> ToolboxActions { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> DownloadInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallMinecraftSectionViewModel> DownloadInstallMinecraftSections { get; } = [];

    public ObservableCollection<DownloadInstallOptionCardViewModel> DownloadInstallOptionCards { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> DownloadInstallOptions { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> DownloadCatalogIntroActions { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadCatalogSections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadFavoriteSections { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> VersionSaveInfoEntries { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> VersionSaveSettingEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> VersionSaveBackupEntries { get; } = [];

    public ObservableCollection<InstanceResourceEntryViewModel> VersionSaveDatapackEntries { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> InstanceInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallOptionCardViewModel> InstanceInstallOptionCards { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> InstanceInstallOptions { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> InstanceWorldEntries { get; } = [];

    public ObservableCollection<InstanceScreenshotEntryViewModel> InstanceScreenshotEntries { get; } = [];

    public ObservableCollection<InstanceServerEntryViewModel> InstanceServerEntries { get; } = [];

    public ObservableCollection<InstanceResourceEntryViewModel> InstanceResourceEntries { get; } = [];

    public ObservableCollection<HelpTopicGroupViewModel> HelpTopicGroups { get; } = [];

    public ObservableCollection<HelpTopicViewModel> HelpSearchResults { get; } = [];

    public ObservableCollection<JavaRuntimeEntryViewModel> JavaRuntimeEntries { get; } = [];

    public ObservableCollection<UiFeatureToggleGroupViewModel> UiFeatureToggleGroups { get; } = [];

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

    public bool IsContextModeRoute => _currentNavigation?.ShowsBackButton ?? false;

    public bool ShowWindowBranding => !IsContextModeRoute;

    public bool ShowWindowUtilityButtons => !IsContextModeRoute;

    public bool HasRunningTaskManagerTasks => LauncherFrontendRuntimeStateService.HasRunningTasks();

    public bool ShowTaskManagerShortcutButton => !ShowWindowUtilityButtons && HasRunningTaskManagerTasks && !ShowTaskManagerSurface;

    public bool ShowBottomRightUtilityEntryButtons => ShowWindowUtilityButtons && HasUtilityEntries;

    public bool ShowBottomRightPromptQueueButton => ShowWindowUtilityButtons;

    public bool ShowBottomRightExtraButtons => ShowWindowUtilityButtons || ShowTaskManagerShortcutButton;

    public bool ShowMaximizeButton => false;

    public bool HasActivePrompts => ActivePrompts.Count > 0;

    public bool HasNoActivePrompts => !HasActivePrompts;

    public PromptCardViewModel? CurrentPrompt => ActivePrompts.Count > 0 ? ActivePrompts[0] : null;

    public bool HasCurrentPrompt => CurrentPrompt is not null;

    public bool IsPromptOverlayVisible => HasActivePrompts && _isPromptOverlayOpen;

    public bool HasSidebarEntries => SidebarEntries.Count > 0;

    public bool HasSidebarSections => SidebarSections.Count > 0;

    public bool HasNoSidebarSections => !HasSidebarSections;

    public bool HasSurfaceFacts => SurfaceFacts.Count > 0;

    public bool HasSurfaceSections => SurfaceSections.Count > 0;

    public bool HasActivityEntries => ActivityEntries.Count > 0;

    public bool HasUtilityEntries => UtilityEntries.Count > 0;

    public bool HasAboutProjectEntries => AboutProjectEntries.Count > 0;

    public bool HasAboutAcknowledgementEntries => AboutAcknowledgementEntries.Count > 0;

    public bool HasFeedbackSections => FeedbackSections.Count > 0;

    public bool HasHelpTopicGroups => HelpTopicGroups.Count > 0;

    public bool HasNoHelpTopicGroups => !HasHelpTopicGroups;

    public bool IsHelpSearchActive => !string.IsNullOrWhiteSpace(HelpSearchQuery);

    public bool ShowHelpTopicLibrary => !IsHelpSearchActive;

    public bool HasHelpSearchResults => HelpSearchResults.Count > 0;

    public bool HasNoHelpSearchResults => !HasHelpSearchResults;

    public string HelpSearchResultsHeader => HasHelpSearchResults ? "搜索结果" : "无搜索结果";

    public string TitleBarLabel => _currentNavigation?.CurrentPage.SidebarItemTitle
        ?? _currentNavigation?.CurrentPage.Title
        ?? Title;

    public double StandardShellLeftPaneWidth => CurrentStandardLeftPaneDescriptor?.Kind switch
    {
        StandardShellLeftPaneKind.InstanceSelection => 276,
        StandardShellLeftPaneKind.TaskManager => 200,
        StandardShellLeftPaneKind.None => 0,
        StandardShellLeftPaneKind.Sidebar => _standardSidebarAutoWidth,
        _ => 236
    };

    public bool ShowStandardShellLeftPane => CurrentStandardLeftPaneDescriptor?.Kind != StandardShellLeftPaneKind.None;

    public double CurrentShellLeftPaneWidth => IsLaunchRoute
        ? 300
        : StandardShellLeftPaneWidth;

    internal void SetStandardSidebarAutoWidth(double width)
    {
        var clamped = Math.Clamp(width, 152, 320);
        if (Math.Abs(_standardSidebarAutoWidth - clamped) < 0.5)
        {
            return;
        }

        _standardSidebarAutoWidth = clamped;
        RaisePropertyChanged(nameof(StandardShellLeftPaneWidth));
        RaisePropertyChanged(nameof(CurrentShellLeftPaneWidth));
    }

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

    private bool IsCurrentStandardRightPane(StandardShellRightPaneKind kind)
    {
        return CurrentStandardRightPaneDescriptor?.Kind == kind;
    }
}
