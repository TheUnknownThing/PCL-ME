using PCL.Core.App.Essentials;
using PCL.Frontend.Spike.Desktop.ShellViews;
using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private ShellLeftPaneViewModel? _currentStandardLeftPane;
    private ShellRightPaneViewModel? _currentStandardRightPane;
    private StandardShellLeftPaneDescriptor? _currentStandardLeftPaneDescriptor;
    private StandardShellRightPaneDescriptor? _currentStandardRightPaneDescriptor;
    private StandardShellPaneResolution? _currentStandardPaneResolution;

    public ShellLeftPaneViewModel? CurrentStandardLeftPane
    {
        get => _currentStandardLeftPane;
        private set => SetProperty(ref _currentStandardLeftPane, value);
    }

    public ShellRightPaneViewModel? CurrentStandardRightPane
    {
        get => _currentStandardRightPane;
        private set => SetProperty(ref _currentStandardRightPane, value);
    }

    public StandardShellLeftPaneDescriptor? CurrentStandardLeftPaneDescriptor
    {
        get => _currentStandardLeftPaneDescriptor;
        private set => SetProperty(ref _currentStandardLeftPaneDescriptor, value);
    }

    public StandardShellRightPaneDescriptor? CurrentStandardRightPaneDescriptor
    {
        get => _currentStandardRightPaneDescriptor;
        private set => SetProperty(ref _currentStandardRightPaneDescriptor, value);
    }

    public StandardShellPaneResolution? CurrentStandardPaneResolution
    {
        get => _currentStandardPaneResolution;
        private set => SetProperty(ref _currentStandardPaneResolution, value);
    }

    private void RefreshStandardShellPanes()
    {
        if (!IsStandardShellRoute)
        {
            CurrentStandardLeftPane = null;
            CurrentStandardRightPane = null;
            CurrentStandardLeftPaneDescriptor = null;
            CurrentStandardRightPaneDescriptor = null;
            CurrentStandardPaneResolution = null;
            return;
        }

        var leftPaneDescriptor = ResolveStandardLeftPaneDescriptor();
        var rightPaneDescriptor = ResolveStandardRightPaneDescriptor();
        var currentLeftKey = CurrentStandardLeftPaneDescriptor?.Key;
        var currentRightKey = CurrentStandardRightPaneDescriptor?.Key;

        if (!string.Equals(currentLeftKey, leftPaneDescriptor.Key, StringComparison.Ordinal))
        {
            CurrentStandardLeftPaneDescriptor = leftPaneDescriptor;
            CurrentStandardLeftPane = ResolveStandardLeftPane(leftPaneDescriptor);
        }

        if (!string.Equals(currentRightKey, rightPaneDescriptor.Key, StringComparison.Ordinal))
        {
            CurrentStandardRightPaneDescriptor = rightPaneDescriptor;
            CurrentStandardRightPane = ResolveStandardRightPane(rightPaneDescriptor);
        }

        CurrentStandardPaneResolution = new StandardShellPaneResolution(_currentRoute, leftPaneDescriptor, rightPaneDescriptor);
    }

    private StandardShellLeftPaneDescriptor ResolveStandardLeftPaneDescriptor()
    {
        if (HasSidebarSections)
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.Sidebar, "standard-sidebar");
        }

        if (HasStandardShellSummaryPaneContent())
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.Summary, "standard-summary");
        }

        return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.Empty, "standard-empty");
    }

    private ShellLeftPaneViewModel ResolveStandardLeftPane(StandardShellLeftPaneDescriptor descriptor)
    {
        return descriptor.Kind switch
        {
            StandardShellLeftPaneKind.Sidebar => new StandardShellNavigationListPaneViewModel(this, descriptor),
            StandardShellLeftPaneKind.Summary => new StandardShellSummaryPaneViewModel(this, descriptor),
            _ => new StandardShellEmptyPaneViewModel(this, descriptor)
        };
    }

    private bool HasStandardShellSummaryPaneContent()
    {
        return !string.IsNullOrWhiteSpace(Eyebrow)
            || !string.IsNullOrWhiteSpace(Title)
            || !string.IsNullOrWhiteSpace(Description)
            || !string.IsNullOrWhiteSpace(BreadcrumbTrail)
            || !string.IsNullOrWhiteSpace(SurfaceMeta)
            || HasSurfaceFacts;
    }

    private ShellRightPaneViewModel ResolveStandardRightPane(StandardShellRightPaneDescriptor descriptor)
    {
        return ShellPaneTemplateRegistry.CreateRightPane(this, descriptor);
    }

    private StandardShellRightPaneDescriptor ResolveStandardRightPaneDescriptor()
    {
        return _currentRoute.Page switch
        {
            LauncherFrontendPageKey.Setup => ResolveSetupRightPaneDescriptor(),
            LauncherFrontendPageKey.Download => ResolveDownloadRightPaneDescriptor(),
            LauncherFrontendPageKey.Tools => ResolveToolsRightPaneDescriptor(),
            LauncherFrontendPageKey.VersionSaves => ResolveVersionSavesRightPaneDescriptor(),
            LauncherFrontendPageKey.InstanceSetup => ResolveInstanceRightPaneDescriptor(),
            LauncherFrontendPageKey.InstanceSelect => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "instance-select-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.TaskManager => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "task-manager-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.GameLog => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "game-log-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.CompDetail => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "comp-detail-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.HelpDetail => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "help-detail-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.HomePageMarket => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "home-page-market-shell", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "generic-shell", usesCompatibilityView: false)
        };
    }

    private StandardShellRightPaneDescriptor ResolveSetupRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupLaunch => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLaunch, StandardShellRightPaneGroup.SetupFamily, "setup-launch", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLink => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLink, StandardShellRightPaneGroup.SetupFamily, "setup-link", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupAbout => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupAbout, StandardShellRightPaneGroup.SetupFamily, "setup-about", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupFeedback => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupFeedback, StandardShellRightPaneGroup.SetupFamily, "setup-feedback", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLog => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLog, StandardShellRightPaneGroup.SetupFamily, "setup-log", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupUpdate => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupUpdate, StandardShellRightPaneGroup.SetupFamily, "setup-update", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupGameLink => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupGameLink, StandardShellRightPaneGroup.SetupFamily, "setup-game-link", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupGameManage => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupGameManage, StandardShellRightPaneGroup.SetupFamily, "setup-game-manage", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLauncherMisc => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLauncherMisc, StandardShellRightPaneGroup.SetupFamily, "setup-launcher-misc", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupJava => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupJava, StandardShellRightPaneGroup.SetupFamily, "setup-java", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupUI => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupUi, StandardShellRightPaneGroup.SetupFamily, "setup-ui", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "setup-generic", usesCompatibilityView: false)
        };
    }

    private StandardShellRightPaneDescriptor ResolveDownloadRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadInstall => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadInstall, StandardShellRightPaneGroup.DownloadInstall, "download-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadClient
                or LauncherFrontendSubpageKey.DownloadOptiFine
                or LauncherFrontendSubpageKey.DownloadForge
                or LauncherFrontendSubpageKey.DownloadNeoForge
                or LauncherFrontendSubpageKey.DownloadCleanroom
                or LauncherFrontendSubpageKey.DownloadFabric
                or LauncherFrontendSubpageKey.DownloadQuilt
                or LauncherFrontendSubpageKey.DownloadLiteLoader
                or LauncherFrontendSubpageKey.DownloadLabyMod
                or LauncherFrontendSubpageKey.DownloadLegacyFabric => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadCatalog, StandardShellRightPaneGroup.DownloadCatalog, "download-catalog", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadResource, StandardShellRightPaneGroup.DownloadResource, "download-resource", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadCompFavorites => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadFavorites, StandardShellRightPaneGroup.DownloadFavorites, "download-favorites", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "download-generic", usesCompatibilityView: false)
        };
    }

    private StandardShellRightPaneDescriptor ResolveToolsRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.ToolsGameLink => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsGameLink, StandardShellRightPaneGroup.ToolsFamily, "tools-game-link", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.ToolsLauncherHelp => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsHelp, StandardShellRightPaneGroup.ToolsFamily, "tools-help", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.ToolsTest => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsTest, StandardShellRightPaneGroup.ToolsFamily, "tools-test", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "tools-generic", usesCompatibilityView: false)
        };
    }

    private StandardShellRightPaneDescriptor ResolveVersionSavesRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionSavesInfo => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveInfo, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-info", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSavesBackup => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveBackup, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-backup", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSavesDatapack => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveDatapack, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-datapack", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "version-saves-generic", usesCompatibilityView: false)
        };
    }

    private StandardShellRightPaneDescriptor ResolveInstanceRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionOverall => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceOverview, StandardShellRightPaneGroup.InstanceOverviewFamily, "instance-overview", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSetup => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceSetup, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-setup", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionExport => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceExport, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-export", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionInstall => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceInstall, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionWorld => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceWorld, StandardShellRightPaneGroup.InstanceContentFamily, "instance-world", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionScreenshot => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceScreenshot, StandardShellRightPaneGroup.InstanceContentFamily, "instance-screenshot", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionServer => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceServer, StandardShellRightPaneGroup.InstanceContentFamily, "instance-server", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceResource, StandardShellRightPaneGroup.InstanceContentFamily, "instance-resource", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "instance-generic", usesCompatibilityView: false)
        };
    }

    private static StandardShellRightPaneDescriptor CreateRightPaneDescriptor(
        StandardShellRightPaneKind kind,
        StandardShellRightPaneGroup group,
        string key,
        bool usesCompatibilityView = true)
    {
        return new StandardShellRightPaneDescriptor(kind, group, key, usesCompatibilityView);
    }
}
