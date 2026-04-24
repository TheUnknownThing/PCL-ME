using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PCL.Frontend.Avalonia.Desktop.Panes.Left;
using PCL.Frontend.Avalonia.Desktop.Panes.Right;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.Desktop.Panes;

internal static class PaneTemplateRegistry
{
    private static readonly IReadOnlyList<Action<Application>> TemplateRegistrations =
    [
        RegisterTemplate<StandardNavigationListPaneViewModel, StandardNavigationListPaneView>,
        RegisterTemplate<InstanceSelectLeftPaneViewModel, InstanceSelectLeftPaneView>,
        RegisterTemplate<TaskManagerLeftPaneViewModel, TaskManagerLeftPaneView>,
        RegisterTemplate<SharedRouteRightPaneViewModel, SharedRouteRightPaneView>,
        RegisterTemplate<InstanceSelectRightPaneViewModel, InstanceSelectRightPaneView>,
        RegisterTemplate<TaskManagerRightPaneViewModel, TaskManagerRightPaneView>,
        RegisterTemplate<DownloadInstallRightPaneViewModel, DownloadInstallRightPaneView>,
        RegisterTemplate<DownloadCatalogRightPaneViewModel, DownloadCatalogRightPaneView>,
        RegisterTemplate<DownloadResourceRightPaneViewModel, DownloadResourceRightPaneView>,
        RegisterTemplate<DownloadFavoritesRightPaneViewModel, DownloadFavoritesRightPaneView>,
        RegisterTemplate<SetupLaunchRightPaneViewModel, SetupLaunchRightPaneView>,
        RegisterTemplate<SetupAboutRightPaneViewModel, SetupAboutRightPaneView>,
        RegisterTemplate<SetupFeedbackRightPaneViewModel, SetupFeedbackRightPaneView>,
        RegisterTemplate<SetupLogRightPaneViewModel, SetupLogRightPaneView>,
        RegisterTemplate<SetupUpdateRightPaneViewModel, SetupUpdateRightPaneView>,
        RegisterTemplate<SetupGameManageRightPaneViewModel, SetupGameManageRightPaneView>,
        RegisterTemplate<SetupLauncherMiscRightPaneViewModel, SetupLauncherMiscRightPaneView>,
        RegisterTemplate<SetupJavaRightPaneViewModel, SetupJavaRightPaneView>,
        RegisterTemplate<SetupUiRightPaneViewModel, SetupUiRightPaneView>,
        RegisterTemplate<ToolsHelpRightPaneViewModel, ToolsHelpRightPaneView>,
        RegisterTemplate<ToolsTestRightPaneViewModel, ToolsTestRightPaneView>,
        RegisterTemplate<VersionSaveInfoRightPaneViewModel, VersionSaveInfoRightPaneView>,
        RegisterTemplate<VersionSaveBackupRightPaneViewModel, VersionSaveBackupRightPaneView>,
        RegisterTemplate<VersionSaveDatapackRightPaneViewModel, VersionSaveDatapackRightPaneView>,
        RegisterTemplate<InstanceOverviewRightPaneViewModel, InstanceOverviewRightPaneView>,
        RegisterTemplate<InstanceSetupRightPaneViewModel, InstanceSetupRightPaneView>,
        RegisterTemplate<InstanceExportRightPaneViewModel, InstanceExportRightPaneView>,
        RegisterTemplate<InstanceInstallRightPaneViewModel, InstanceInstallRightPaneView>,
        RegisterTemplate<InstanceWorldRightPaneViewModel, InstanceWorldRightPaneView>,
        RegisterTemplate<InstanceScreenshotRightPaneViewModel, InstanceScreenshotRightPaneView>,
        RegisterTemplate<InstanceServerRightPaneViewModel, InstanceServerRightPaneView>,
        RegisterTemplate<InstanceResourceRightPaneViewModel, InstanceResourceRightPaneView>
    ];

    private static readonly IReadOnlyDictionary<StandardRightPaneKind, Func<LauncherViewModel, StandardRightPaneDescriptor, RightPaneViewModel>> RightPaneFactories =
        new Dictionary<StandardRightPaneKind, Func<LauncherViewModel, StandardRightPaneDescriptor, RightPaneViewModel>>
        {
            [StandardRightPaneKind.Generic] = static (launcher, descriptor) => new SharedRouteRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.DownloadInstall] = static (launcher, descriptor) => new DownloadInstallRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.DownloadCatalog] = static (launcher, descriptor) => new DownloadCatalogRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.DownloadResource] = static (launcher, descriptor) => new DownloadResourceRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.DownloadFavorites] = static (launcher, descriptor) => new DownloadFavoritesRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupLaunch] = static (launcher, descriptor) => new SetupLaunchRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupAbout] = static (launcher, descriptor) => new SetupAboutRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupFeedback] = static (launcher, descriptor) => new SetupFeedbackRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupLog] = static (launcher, descriptor) => new SetupLogRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupUpdate] = static (launcher, descriptor) => new SetupUpdateRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupGameManage] = static (launcher, descriptor) => new SetupGameManageRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupLauncherMisc] = static (launcher, descriptor) => new SetupLauncherMiscRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupJava] = static (launcher, descriptor) => new SetupJavaRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.SetupUi] = static (launcher, descriptor) => new SetupUiRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.ToolsHelp] = static (launcher, descriptor) => new ToolsHelpRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.ToolsTest] = static (launcher, descriptor) => new ToolsTestRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.VersionSaveInfo] = static (launcher, descriptor) => new VersionSaveInfoRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.VersionSaveBackup] = static (launcher, descriptor) => new VersionSaveBackupRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.VersionSaveDatapack] = static (launcher, descriptor) => new VersionSaveDatapackRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceOverview] = static (launcher, descriptor) => new InstanceOverviewRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceSetup] = static (launcher, descriptor) => new InstanceSetupRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceExport] = static (launcher, descriptor) => new InstanceExportRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceInstall] = static (launcher, descriptor) => new InstanceInstallRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceWorld] = static (launcher, descriptor) => new InstanceWorldRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceScreenshot] = static (launcher, descriptor) => new InstanceScreenshotRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceServer] = static (launcher, descriptor) => new InstanceServerRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceResource] = static (launcher, descriptor) => new InstanceResourceRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.InstanceSelection] = static (launcher, descriptor) => new InstanceSelectRightPaneViewModel(launcher, descriptor),
            [StandardRightPaneKind.TaskManager] = static (launcher, descriptor) => new TaskManagerRightPaneViewModel(launcher, descriptor)
        };

    public static void Register(Application application)
    {
        foreach (var registerTemplate in TemplateRegistrations)
        {
            registerTemplate(application);
        }
    }

    public static RightPaneViewModel CreateRightPane(LauncherViewModel launcher, StandardRightPaneDescriptor descriptor)
    {
        if (RightPaneFactories.TryGetValue(descriptor.Kind, out var factory))
        {
            return factory(launcher, descriptor);
        }

        return new SharedRouteRightPaneViewModel(launcher, descriptor);
    }

    private static void RegisterTemplate<TViewModel, TView>(Application application)
        where TViewModel : class
        where TView : Control, new()
    {
        application.DataTemplates.Add(CreateTemplate<TViewModel, TView>());
    }

    private static FuncDataTemplate<TViewModel> CreateTemplate<TViewModel, TView>()
        where TViewModel : class
        where TView : Control, new()
    {
        return new FuncDataTemplate<TViewModel>((_, _) => new TView());
    }
}
