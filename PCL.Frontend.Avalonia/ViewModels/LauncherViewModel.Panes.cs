using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Panes;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private LeftPaneViewModel? _currentStandardLeftPane;
    private RightPaneViewModel? _currentStandardRightPane;
    private StandardLeftPaneDescriptor? _currentStandardLeftPaneDescriptor;
    private StandardRightPaneDescriptor? _currentStandardRightPaneDescriptor;
    private StandardPaneResolution? _currentStandardPaneResolution;
    private readonly Dictionary<string, RightPaneViewModel> _cachedRightPanes = new(StringComparer.Ordinal);

    public LeftPaneViewModel? CurrentStandardLeftPane
    {
        get => _currentStandardLeftPane;
        private set => SetProperty(ref _currentStandardLeftPane, value);
    }

    public RightPaneViewModel? CurrentStandardRightPane
    {
        get => _currentStandardRightPane;
        private set => SetProperty(ref _currentStandardRightPane, value);
    }

    public StandardLeftPaneDescriptor? CurrentStandardLeftPaneDescriptor
    {
        get => _currentStandardLeftPaneDescriptor;
        private set => SetProperty(ref _currentStandardLeftPaneDescriptor, value);
    }

    public StandardRightPaneDescriptor? CurrentStandardRightPaneDescriptor
    {
        get => _currentStandardRightPaneDescriptor;
        private set => SetProperty(ref _currentStandardRightPaneDescriptor, value);
    }

    public StandardPaneResolution? CurrentStandardPaneResolution
    {
        get => _currentStandardPaneResolution;
        private set => SetProperty(ref _currentStandardPaneResolution, value);
    }

    private void RefreshStandardPanes()
    {
        if (!IsStandardRoute)
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

        CurrentStandardPaneResolution = new StandardPaneResolution(_currentRoute, leftPaneDescriptor, rightPaneDescriptor);
    }

    private StandardLeftPaneDescriptor ResolveStandardLeftPaneDescriptor()
    {
        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
            || _currentRoute.Page == LauncherFrontendPageKey.GameLog)
        {
            return new StandardLeftPaneDescriptor(StandardLeftPaneKind.None, $"{_currentRoute.Page.ToString().ToLowerInvariant()}-full-width");
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.TaskManager)
        {
            return new StandardLeftPaneDescriptor(StandardLeftPaneKind.TaskManager, "task-manager-left");
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.InstanceSelect)
        {
            return new StandardLeftPaneDescriptor(StandardLeftPaneKind.InstanceSelection, "instance-select-left");
        }

        if (HasSidebarSections)
        {
            return new StandardLeftPaneDescriptor(StandardLeftPaneKind.Sidebar, "standard-sidebar");
        }

        return new StandardLeftPaneDescriptor(StandardLeftPaneKind.None, $"{_currentRoute.Page.ToString().ToLowerInvariant()}-full-width");
    }

    private LeftPaneViewModel? ResolveStandardLeftPane(StandardLeftPaneDescriptor descriptor)
    {
        return descriptor.Kind switch
        {
            StandardLeftPaneKind.Sidebar => new StandardNavigationListPaneViewModel(this, descriptor),
            StandardLeftPaneKind.InstanceSelection => new InstanceSelectLeftPaneViewModel(this, descriptor),
            StandardLeftPaneKind.TaskManager => new TaskManagerLeftPaneViewModel(this, descriptor),
            _ => null
        };
    }

    private RightPaneViewModel ResolveStandardRightPane(StandardRightPaneDescriptor descriptor)
    {
        if (ShouldCacheRightPane(descriptor)
            && _cachedRightPanes.TryGetValue(descriptor.Key, out var cachedPane))
        {
            return cachedPane;
        }

        var pane = PaneTemplateRegistry.CreateRightPane(this, descriptor);
        if (ShouldCacheRightPane(descriptor))
        {
            _cachedRightPanes[descriptor.Key] = pane;
        }

        return pane;
    }

    private static bool ShouldCacheRightPane(StandardRightPaneDescriptor descriptor)
    {
        return descriptor.Kind == StandardRightPaneKind.InstanceResource;
    }

    private StandardRightPaneDescriptor ResolveStandardRightPaneDescriptor()
    {
        return _currentRoute.Page switch
        {
            LauncherFrontendPageKey.Setup => ResolveSetupRightPaneDescriptor(),
            LauncherFrontendPageKey.Download => ResolveDownloadRightPaneDescriptor(),
            LauncherFrontendPageKey.Tools => ResolveToolsRightPaneDescriptor(),
            LauncherFrontendPageKey.VersionSaves => ResolveVersionSavesRightPaneDescriptor(),
            LauncherFrontendPageKey.InstanceSetup => ResolveInstanceRightPaneDescriptor(),
            LauncherFrontendPageKey.InstanceSelect => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceSelection, StandardRightPaneGroup.Generic, "instance-select-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.TaskManager => CreateRightPaneDescriptor(StandardRightPaneKind.TaskManager, StandardRightPaneGroup.Generic, "task-manager-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.GameLog => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "game-log-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.CompDetail => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "comp-detail-shell", usesCompatibilityView: false),
            LauncherFrontendPageKey.HelpDetail => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, $"help-detail-shell-{GetHelpDetailTransitionKey()}", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "generic-shell", usesCompatibilityView: false)
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

    private StandardRightPaneDescriptor ResolveSetupRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.SetupLaunch => CreateRightPaneDescriptor(StandardRightPaneKind.SetupLaunch, StandardRightPaneGroup.SetupFamily, "setup-launch", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupAbout => CreateRightPaneDescriptor(StandardRightPaneKind.SetupAbout, StandardRightPaneGroup.SetupFamily, "setup-about", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupFeedback => CreateRightPaneDescriptor(StandardRightPaneKind.SetupFeedback, StandardRightPaneGroup.SetupFamily, "setup-feedback", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLog => CreateRightPaneDescriptor(StandardRightPaneKind.SetupLog, StandardRightPaneGroup.SetupFamily, "setup-log", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupUpdate => CreateRightPaneDescriptor(StandardRightPaneKind.SetupUpdate, StandardRightPaneGroup.SetupFamily, "setup-update", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupGameManage => CreateRightPaneDescriptor(StandardRightPaneKind.SetupGameManage, StandardRightPaneGroup.SetupFamily, "setup-game-manage", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupLauncherMisc => CreateRightPaneDescriptor(StandardRightPaneKind.SetupLauncherMisc, StandardRightPaneGroup.SetupFamily, "setup-launcher-misc", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupJava => CreateRightPaneDescriptor(StandardRightPaneKind.SetupJava, StandardRightPaneGroup.SetupFamily, "setup-java", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.SetupUI => CreateRightPaneDescriptor(StandardRightPaneKind.SetupUi, StandardRightPaneGroup.SetupFamily, "setup-ui", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "setup-generic", usesCompatibilityView: false)
        };
    }

    private StandardRightPaneDescriptor ResolveDownloadRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.DownloadInstall => CreateRightPaneDescriptor(StandardRightPaneKind.DownloadInstall, StandardRightPaneGroup.DownloadInstall, "download-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadClient => CreateRightPaneDescriptor(StandardRightPaneKind.DownloadInstall, StandardRightPaneGroup.DownloadInstall, "download-client-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadOptiFine
                or LauncherFrontendSubpageKey.DownloadForge
                or LauncherFrontendSubpageKey.DownloadNeoForge
                or LauncherFrontendSubpageKey.DownloadCleanroom
                or LauncherFrontendSubpageKey.DownloadFabric
                or LauncherFrontendSubpageKey.DownloadQuilt
                or LauncherFrontendSubpageKey.DownloadLiteLoader
                or LauncherFrontendSubpageKey.DownloadLabyMod
                or LauncherFrontendSubpageKey.DownloadLegacyFabric => CreateRightPaneDescriptor(StandardRightPaneKind.DownloadCatalog, StandardRightPaneGroup.DownloadCatalog, "download-catalog", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadMod
                or LauncherFrontendSubpageKey.DownloadPack
                or LauncherFrontendSubpageKey.DownloadDataPack
                or LauncherFrontendSubpageKey.DownloadResourcePack
                or LauncherFrontendSubpageKey.DownloadShader
                or LauncherFrontendSubpageKey.DownloadWorld => CreateRightPaneDescriptor(StandardRightPaneKind.DownloadResource, StandardRightPaneGroup.DownloadResource, "download-resource", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.DownloadCompFavorites => CreateRightPaneDescriptor(StandardRightPaneKind.DownloadFavorites, StandardRightPaneGroup.DownloadFavorites, "download-favorites", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "download-generic", usesCompatibilityView: false)
        };
    }

    private StandardRightPaneDescriptor ResolveToolsRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.ToolsTest => CreateRightPaneDescriptor(StandardRightPaneKind.ToolsTest, StandardRightPaneGroup.ToolsFamily, "tools-test", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.ToolsLauncherHelp => CreateRightPaneDescriptor(StandardRightPaneKind.ToolsHelp, StandardRightPaneGroup.ToolsFamily, "tools-help", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.ToolsTest, StandardRightPaneGroup.ToolsFamily, "tools-test", usesCompatibilityView: false)
        };
    }

    private StandardRightPaneDescriptor ResolveVersionSavesRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionSavesInfo => CreateRightPaneDescriptor(StandardRightPaneKind.VersionSaveInfo, StandardRightPaneGroup.VersionSavesFamily, "version-save-info", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSavesBackup => CreateRightPaneDescriptor(StandardRightPaneKind.VersionSaveBackup, StandardRightPaneGroup.VersionSavesFamily, "version-save-backup", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSavesDatapack => CreateRightPaneDescriptor(StandardRightPaneKind.VersionSaveDatapack, StandardRightPaneGroup.VersionSavesFamily, "version-save-datapack", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "version-saves-generic", usesCompatibilityView: false)
        };
    }

    private StandardRightPaneDescriptor ResolveInstanceRightPaneDescriptor()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionOverall => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceOverview, StandardRightPaneGroup.InstanceOverviewFamily, "instance-overview", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionSetup => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceSetup, StandardRightPaneGroup.InstanceSetupFamily, "instance-setup", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionExport => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceExport, StandardRightPaneGroup.InstanceSetupFamily, "instance-export", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionInstall => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceInstall, StandardRightPaneGroup.InstanceSetupFamily, "instance-install", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionWorld => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceWorld, StandardRightPaneGroup.InstanceContentFamily, "instance-world", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionScreenshot => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceScreenshot, StandardRightPaneGroup.InstanceContentFamily, "instance-screenshot", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionServer => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceServer, StandardRightPaneGroup.InstanceContentFamily, "instance-server", usesCompatibilityView: false),
            LauncherFrontendSubpageKey.VersionMod
                or LauncherFrontendSubpageKey.VersionModDisabled
                or LauncherFrontendSubpageKey.VersionResourcePack
                or LauncherFrontendSubpageKey.VersionShader
                or LauncherFrontendSubpageKey.VersionSchematic => CreateRightPaneDescriptor(StandardRightPaneKind.InstanceResource, StandardRightPaneGroup.InstanceContentFamily, "instance-resource", usesCompatibilityView: false),
            _ => CreateRightPaneDescriptor(StandardRightPaneKind.Generic, StandardRightPaneGroup.Generic, "instance-generic", usesCompatibilityView: false)
        };
    }

    private StandardRightPaneDescriptor CreateRightPaneDescriptor(
        StandardRightPaneKind kind,
        StandardRightPaneGroup group,
        string key,
        bool usesCompatibilityView = true)
    {
        return new StandardRightPaneDescriptor(
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
