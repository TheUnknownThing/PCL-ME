using PCL.Core.App.Essentials;
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

        CurrentStandardLeftPaneDescriptor = leftPaneDescriptor;
        CurrentStandardRightPaneDescriptor = rightPaneDescriptor;
        CurrentStandardPaneResolution = new StandardShellPaneResolution(_currentRoute, leftPaneDescriptor, rightPaneDescriptor);
        CurrentStandardLeftPane = new StandardShellSidebarPaneViewModel(this, leftPaneDescriptor);
        CurrentStandardRightPane = ResolveStandardRightPane(rightPaneDescriptor);
    }

    private StandardShellLeftPaneDescriptor ResolveStandardLeftPaneDescriptor()
    {
        return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.Sidebar, "standard-sidebar");
    }

    private ShellRightPaneViewModel ResolveStandardRightPane(StandardShellRightPaneDescriptor descriptor)
    {
        if (descriptor.Kind == StandardShellRightPaneKind.ToolsHelp)
        {
            return new ToolsHelpShellRightPaneViewModel(this, descriptor);
        }

        return new LegacyStandardShellRightPaneViewModel(this, descriptor);
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
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "generic-shell")
        };
    }

    private StandardShellRightPaneDescriptor ResolveSetupRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupLaunch => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLaunch, StandardShellRightPaneGroup.SetupFamily, "setup-launch"),
            LauncherFrontendSubpageKey.SetupAbout => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupAbout, StandardShellRightPaneGroup.SetupFamily, "setup-about"),
            LauncherFrontendSubpageKey.SetupFeedback => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupFeedback, StandardShellRightPaneGroup.SetupFamily, "setup-feedback"),
            LauncherFrontendSubpageKey.SetupLog => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLog, StandardShellRightPaneGroup.SetupFamily, "setup-log"),
            LauncherFrontendSubpageKey.SetupUpdate => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupUpdate, StandardShellRightPaneGroup.SetupFamily, "setup-update"),
            LauncherFrontendSubpageKey.SetupGameLink => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupGameLink, StandardShellRightPaneGroup.SetupFamily, "setup-game-link"),
            LauncherFrontendSubpageKey.SetupGameManage => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupGameManage, StandardShellRightPaneGroup.SetupFamily, "setup-game-manage"),
            LauncherFrontendSubpageKey.SetupLauncherMisc => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLauncherMisc, StandardShellRightPaneGroup.SetupFamily, "setup-launcher-misc"),
            LauncherFrontendSubpageKey.SetupJava => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupJava, StandardShellRightPaneGroup.SetupFamily, "setup-java"),
            LauncherFrontendSubpageKey.SetupUI => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupUi, StandardShellRightPaneGroup.SetupFamily, "setup-ui"),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "setup-generic")
        };
    }

    private StandardShellRightPaneDescriptor ResolveDownloadRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadInstall => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadInstall, StandardShellRightPaneGroup.DownloadInstall, "download-install"),
            LauncherFrontendSubpageKey.DownloadClient
                or LauncherFrontendSubpageKey.DownloadOptiFine
                or LauncherFrontendSubpageKey.DownloadForge
                or LauncherFrontendSubpageKey.DownloadNeoForge
                or LauncherFrontendSubpageKey.DownloadCleanroom
                or LauncherFrontendSubpageKey.DownloadFabric
                or LauncherFrontendSubpageKey.DownloadQuilt
                or LauncherFrontendSubpageKey.DownloadLiteLoader
                or LauncherFrontendSubpageKey.DownloadLabyMod
                or LauncherFrontendSubpageKey.DownloadLegacyFabric => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadCatalog, StandardShellRightPaneGroup.DownloadCatalog, "download-catalog"),
            LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadResource, StandardShellRightPaneGroup.DownloadResource, "download-resource"),
            LauncherFrontendSubpageKey.DownloadCompFavorites => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadFavorites, StandardShellRightPaneGroup.DownloadFavorites, "download-favorites"),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "download-generic")
        };
    }

    private StandardShellRightPaneDescriptor ResolveToolsRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.ToolsGameLink => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsGameLink, StandardShellRightPaneGroup.ToolsFamily, "tools-game-link"),
            LauncherFrontendSubpageKey.ToolsLauncherHelp => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsHelp, StandardShellRightPaneGroup.ToolsFamily, "tools-help", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.ToolsTest => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsTest, StandardShellRightPaneGroup.ToolsFamily, "tools-test"),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "tools-generic")
        };
    }

    private StandardShellRightPaneDescriptor ResolveVersionSavesRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionSavesInfo => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveInfo, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-info"),
            LauncherFrontendSubpageKey.VersionSavesBackup => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveBackup, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-backup"),
            LauncherFrontendSubpageKey.VersionSavesDatapack => CreateRightPaneDescriptor(StandardShellRightPaneKind.VersionSaveDatapack, StandardShellRightPaneGroup.VersionSavesFamily, "version-save-datapack"),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "version-saves-generic")
        };
    }

    private StandardShellRightPaneDescriptor ResolveInstanceRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionOverall => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceOverview, StandardShellRightPaneGroup.InstanceOverviewFamily, "instance-overview"),
            LauncherFrontendSubpageKey.VersionSetup => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceSetup, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-setup"),
            LauncherFrontendSubpageKey.VersionExport => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceExport, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-export"),
            LauncherFrontendSubpageKey.VersionInstall => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceInstall, StandardShellRightPaneGroup.InstanceSetupFamily, "instance-install"),
            LauncherFrontendSubpageKey.VersionWorld => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceWorld, StandardShellRightPaneGroup.InstanceContentFamily, "instance-world"),
            LauncherFrontendSubpageKey.VersionScreenshot => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceScreenshot, StandardShellRightPaneGroup.InstanceContentFamily, "instance-screenshot"),
            LauncherFrontendSubpageKey.VersionServer => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceServer, StandardShellRightPaneGroup.InstanceContentFamily, "instance-server"),
            LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceResource, StandardShellRightPaneGroup.InstanceContentFamily, "instance-resource"),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "instance-generic")
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
