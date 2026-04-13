using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.ShellViews;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

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
        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
            || _currentRoute.Page == LauncherFrontendPageKey.GameLog)
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.None, $"{_currentRoute.Page.ToString().ToLowerInvariant()}-full-width");
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.TaskManager)
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.TaskManager, "task-manager-left");
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSelect)
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.InstanceSelection, "instance-select-left");
        }

        if (HasSidebarSections)
        {
            return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.Sidebar, "standard-sidebar");
        }

        return new StandardShellLeftPaneDescriptor(StandardShellLeftPaneKind.None, $"{_currentRoute.Page.ToString().ToLowerInvariant()}-full-width");
    }

    private ShellLeftPaneViewModel? ResolveStandardLeftPane(StandardShellLeftPaneDescriptor descriptor)
    {
        return descriptor.Kind switch
        {
            StandardShellLeftPaneKind.Sidebar => new StandardShellNavigationListPaneViewModel(this, descriptor),
            StandardShellLeftPaneKind.InstanceSelection => new InstanceSelectShellLeftPaneViewModel(this, descriptor),
            StandardShellLeftPaneKind.TaskManager => new TaskManagerShellLeftPaneViewModel(this, descriptor),
            _ => null
        };
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
            LauncherFrontendPageKey.InstanceSelect => CreateRightPaneDescriptor(StandardShellRightPaneKind.InstanceSelection, StandardShellRightPaneGroup.Generic, "instance-select-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.TaskManager => CreateRightPaneDescriptor(StandardShellRightPaneKind.TaskManager, StandardShellRightPaneGroup.Generic, "task-manager-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.GameLog => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "game-log-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.CompDetail => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "comp-detail-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.HelpDetail => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, $"help-detail-shell-{GetHelpDetailTransitionKey()}", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.Generic, StandardShellRightPaneGroup.Generic, "generic-shell", usesCompatibilityView: false)
        };
    }

    private string GetHelpDetailTransitionKey()
    {
        if (_currentHelpDetailEntry is null)
        {
            return "empty";
        }

        var normalized = NormalizeHelpReference(_currentHelpDetailEntry.RawPath);
        return string.IsNullOrWhiteSpace(normalized)
            ? "empty"
            : normalized
                .Replace('/', '-')
                .Replace('\\', '-')
                .Replace(':', '-')
                .Replace('.', '-');
    }

    private StandardShellRightPaneDescriptor ResolveSetupRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupLaunch => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLaunch, StandardShellRightPaneGroup.SetupFamily, "setup-launch", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupAbout => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupAbout, StandardShellRightPaneGroup.SetupFamily, "setup-about", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupFeedback => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupFeedback, StandardShellRightPaneGroup.SetupFamily, "setup-feedback", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLog => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupLog, StandardShellRightPaneGroup.SetupFamily, "setup-log", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupUpdate => CreateRightPaneDescriptor(StandardShellRightPaneKind.SetupUpdate, StandardShellRightPaneGroup.SetupFamily, "setup-update", usesCompatibilityView: false),
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
            LauncherFrontendSubpageKey.DownloadClient => CreateRightPaneDescriptor(StandardShellRightPaneKind.DownloadInstall, StandardShellRightPaneGroup.DownloadInstall, "download-client-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadOptiFine
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
            LauncherFrontendSubpageKey.ToolsTest => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsTest, StandardShellRightPaneGroup.ToolsFamily, "tools-test", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.ToolsLauncherHelp => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsHelp, StandardShellRightPaneGroup.ToolsFamily, "tools-help", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardShellRightPaneKind.ToolsTest, StandardShellRightPaneGroup.ToolsFamily, "tools-test", usesCompatibilityView: false)
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

    private StandardShellRightPaneDescriptor CreateRightPaneDescriptor(
        StandardShellRightPaneKind kind,
        StandardShellRightPaneGroup group,
        string key,
        bool usesCompatibilityView = true)
    {
        return new StandardShellRightPaneDescriptor(
            kind,
            group,
            $"{key}-{GetRouteTransitionKey(_currentRoute)}",
            usesCompatibilityView);
    }

    private static string GetRouteTransitionKey(LauncherFrontendRoute route)
    {
        return $"{route.Page.ToString().ToLowerInvariant()}-{route.Subpage.ToString().ToLowerInvariant()}";
    }
}
