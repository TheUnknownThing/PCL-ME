using System.Collections.ObjectModel;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel : ViewModelBase
{
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

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPolicyEntries { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> GameLinkPlayerEntries { get; } = [];

    public ObservableCollection<ToolboxActionViewModel> ToolboxActions { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> DownloadInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> DownloadInstallOptions { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> DownloadCatalogIntroActions { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadCatalogSections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> DownloadFavoriteSections { get; } = [];

    public ObservableCollection<SurfaceNoticeViewModel> InstanceInstallHints { get; } = [];

    public ObservableCollection<DownloadInstallOptionViewModel> InstanceInstallOptions { get; } = [];

    public ObservableCollection<SimpleListEntryViewModel> InstanceWorldEntries { get; } = [];

    public ObservableCollection<InstanceScreenshotEntryViewModel> InstanceScreenshotEntries { get; } = [];

    public ObservableCollection<InstanceServerEntryViewModel> InstanceServerEntries { get; } = [];

    public ObservableCollection<InstanceResourceEntryViewModel> InstanceResourceEntries { get; } = [];

    public ObservableCollection<HelpTopicGroupViewModel> HelpTopicGroups { get; } = [];

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

    public bool IsSetupLaunchSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLaunch;

    public bool IsSetupFeedbackSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupFeedback;

    public bool IsSetupLogSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLog;

    public bool IsSetupUpdateSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUpdate;

    public bool IsSetupGameLinkSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupGameLink;

    public bool IsSetupGameManageSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupGameManage;

    public bool IsSetupLauncherMiscSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupLauncherMisc;

    public bool IsSetupJavaSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupJava;

    public bool IsSetupUiSurface => _currentRoute.Page == LauncherFrontendPageKey.Setup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.SetupUI;

    public bool IsDownloadInstallSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadInstall;

    public bool IsDownloadCatalogSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage is LauncherFrontendSubpageKey.DownloadClient
            or LauncherFrontendSubpageKey.DownloadOptiFine
            or LauncherFrontendSubpageKey.DownloadForge
            or LauncherFrontendSubpageKey.DownloadNeoForge
            or LauncherFrontendSubpageKey.DownloadCleanroom
            or LauncherFrontendSubpageKey.DownloadFabric
            or LauncherFrontendSubpageKey.DownloadQuilt
            or LauncherFrontendSubpageKey.DownloadLiteLoader
            or LauncherFrontendSubpageKey.DownloadLabyMod
            or LauncherFrontendSubpageKey.DownloadLegacyFabric;

    public bool IsDownloadResourceSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage is LauncherFrontendSubpageKey.DownloadMod
            or LauncherFrontendSubpageKey.DownloadPack
            or LauncherFrontendSubpageKey.DownloadDataPack
            or LauncherFrontendSubpageKey.DownloadResourcePack
            or LauncherFrontendSubpageKey.DownloadShader
            or LauncherFrontendSubpageKey.DownloadWorld;

    public bool IsDownloadFavoritesSurface => _currentRoute.Page == LauncherFrontendPageKey.Download
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadCompFavorites;

    public bool IsToolsGameLinkSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsGameLink;

    public bool IsToolsHelpSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsLauncherHelp;

    public bool IsToolsTestSurface => _currentRoute.Page == LauncherFrontendPageKey.Tools
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.ToolsTest;

    public bool IsInstanceOverviewSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionOverall;

    public bool IsInstanceSetupSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionSetup;

    public bool IsInstanceExportSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionExport;

    public bool IsInstanceInstallSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionInstall;

    public bool IsInstanceWorldSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionWorld;

    public bool IsInstanceScreenshotSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionScreenshot;

    public bool IsInstanceServerSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionServer;

    public bool IsInstanceResourceSurface => _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup
        && _currentRoute.Subpage is LauncherFrontendSubpageKey.VersionMod
            or LauncherFrontendSubpageKey.VersionModDisabled
            or LauncherFrontendSubpageKey.VersionResourcePack
            or LauncherFrontendSubpageKey.VersionShader
            or LauncherFrontendSubpageKey.VersionSchematic;

    public bool IsGenericShellSurface => IsStandardShellRoute
        && !IsSetupLaunchSurface
        && !IsSetupAboutSurface
        && !IsSetupFeedbackSurface
        && !IsSetupLogSurface
        && !IsSetupUpdateSurface
        && !IsSetupGameLinkSurface
        && !IsSetupGameManageSurface
        && !IsSetupLauncherMiscSurface
        && !IsSetupJavaSurface
        && !IsSetupUiSurface
        && !IsDownloadInstallSurface
        && !IsDownloadCatalogSurface
        && !IsDownloadResourceSurface
        && !IsDownloadFavoritesSurface
        && !IsToolsGameLinkSurface
        && !IsToolsHelpSurface
        && !IsToolsTestSurface
        && !IsInstanceOverviewSurface
        && !IsInstanceSetupSurface
        && !IsInstanceExportSurface
        && !IsInstanceInstallSurface
        && !IsInstanceWorldSurface
        && !IsInstanceScreenshotSurface
        && !IsInstanceServerSurface
        && !IsInstanceResourceSurface;

    public bool HasAboutProjectEntries => AboutProjectEntries.Count > 0;

    public bool HasAboutAcknowledgementEntries => AboutAcknowledgementEntries.Count > 0;

    public bool HasFeedbackSections => FeedbackSections.Count > 0;

    public bool HasHelpTopicGroups => HelpTopicGroups.Count > 0;

    public bool HasNoHelpTopicGroups => !HasHelpTopicGroups;

    public string TitleBarLabel => _currentNavigation?.CurrentPage.SidebarItemTitle
        ?? _currentNavigation?.CurrentPage.Title
        ?? Title;

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
}
