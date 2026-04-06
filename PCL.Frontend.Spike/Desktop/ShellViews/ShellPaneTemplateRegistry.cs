using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PCL.Frontend.Spike.Desktop.ShellViews.Left;
using PCL.Frontend.Spike.Desktop.ShellViews.Right;
using PCL.Frontend.Spike.ViewModels;
using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.Desktop.ShellViews;

internal static class ShellPaneTemplateRegistry
{
    private static readonly IReadOnlyList<Action<Application>> TemplateRegistrations =
    [
        RegisterTemplate<StandardShellNavigationListPaneViewModel, StandardShellNavigationListPaneView>,
        RegisterTemplate<StandardShellSummaryPaneViewModel, StandardShellSummaryPaneView>,
        RegisterTemplate<StandardShellEmptyPaneViewModel, StandardShellEmptyPaneView>,
        RegisterTemplate<GenericStandardShellRightPaneViewModel, GenericStandardShellRightPaneView>,
        RegisterTemplate<DownloadInstallShellRightPaneViewModel, DownloadInstallShellRightPaneView>,
        RegisterTemplate<DownloadCatalogShellRightPaneViewModel, DownloadCatalogShellRightPaneView>,
        RegisterTemplate<DownloadResourceShellRightPaneViewModel, DownloadResourceShellRightPaneView>,
        RegisterTemplate<DownloadFavoritesShellRightPaneViewModel, DownloadFavoritesShellRightPaneView>,
        RegisterTemplate<SetupLaunchShellRightPaneViewModel, SetupLaunchShellRightPaneView>,
        RegisterTemplate<SetupLinkShellRightPaneViewModel, SetupLinkShellRightPaneView>,
        RegisterTemplate<SetupAboutShellRightPaneViewModel, SetupAboutShellRightPaneView>,
        RegisterTemplate<SetupFeedbackShellRightPaneViewModel, SetupFeedbackShellRightPaneView>,
        RegisterTemplate<SetupLogShellRightPaneViewModel, SetupLogShellRightPaneView>,
        RegisterTemplate<SetupUpdateShellRightPaneViewModel, SetupUpdateShellRightPaneView>,
        RegisterTemplate<SetupGameLinkShellRightPaneViewModel, SetupGameLinkShellRightPaneView>,
        RegisterTemplate<SetupGameManageShellRightPaneViewModel, SetupGameManageShellRightPaneView>,
        RegisterTemplate<SetupLauncherMiscShellRightPaneViewModel, SetupLauncherMiscShellRightPaneView>,
        RegisterTemplate<SetupJavaShellRightPaneViewModel, SetupJavaShellRightPaneView>,
        RegisterTemplate<SetupUiShellRightPaneViewModel, SetupUiShellRightPaneView>,
        RegisterTemplate<ToolsGameLinkShellRightPaneViewModel, ToolsGameLinkShellRightPaneView>,
        RegisterTemplate<ToolsHelpShellRightPaneViewModel, ToolsHelpShellRightPaneView>,
        RegisterTemplate<ToolsTestShellRightPaneViewModel, ToolsTestShellRightPaneView>,
        RegisterTemplate<VersionSaveInfoShellRightPaneViewModel, VersionSaveInfoShellRightPaneView>,
        RegisterTemplate<VersionSaveBackupShellRightPaneViewModel, VersionSaveBackupShellRightPaneView>,
        RegisterTemplate<VersionSaveDatapackShellRightPaneViewModel, VersionSaveDatapackShellRightPaneView>,
        RegisterTemplate<InstanceOverviewShellRightPaneViewModel, InstanceOverviewShellRightPaneView>,
        RegisterTemplate<InstanceSetupShellRightPaneViewModel, InstanceSetupShellRightPaneView>,
        RegisterTemplate<InstanceExportShellRightPaneViewModel, InstanceExportShellRightPaneView>,
        RegisterTemplate<InstanceInstallShellRightPaneViewModel, InstanceInstallShellRightPaneView>,
        RegisterTemplate<InstanceWorldShellRightPaneViewModel, InstanceWorldShellRightPaneView>,
        RegisterTemplate<InstanceScreenshotShellRightPaneViewModel, InstanceScreenshotShellRightPaneView>,
        RegisterTemplate<InstanceServerShellRightPaneViewModel, InstanceServerShellRightPaneView>,
        RegisterTemplate<InstanceResourceShellRightPaneViewModel, InstanceResourceShellRightPaneView>
    ];

    private static readonly IReadOnlyDictionary<StandardShellRightPaneKind, Func<FrontendShellViewModel, StandardShellRightPaneDescriptor, ShellRightPaneViewModel>> RightPaneFactories =
        new Dictionary<StandardShellRightPaneKind, Func<FrontendShellViewModel, StandardShellRightPaneDescriptor, ShellRightPaneViewModel>>
        {
            [StandardShellRightPaneKind.Generic] = static (shell, descriptor) => new GenericStandardShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.DownloadInstall] = static (shell, descriptor) => new DownloadInstallShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.DownloadCatalog] = static (shell, descriptor) => new DownloadCatalogShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.DownloadResource] = static (shell, descriptor) => new DownloadResourceShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.DownloadFavorites] = static (shell, descriptor) => new DownloadFavoritesShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupLaunch] = static (shell, descriptor) => new SetupLaunchShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupLink] = static (shell, descriptor) => new SetupLinkShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupAbout] = static (shell, descriptor) => new SetupAboutShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupFeedback] = static (shell, descriptor) => new SetupFeedbackShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupLog] = static (shell, descriptor) => new SetupLogShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupUpdate] = static (shell, descriptor) => new SetupUpdateShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupGameLink] = static (shell, descriptor) => new SetupGameLinkShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupGameManage] = static (shell, descriptor) => new SetupGameManageShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupLauncherMisc] = static (shell, descriptor) => new SetupLauncherMiscShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupJava] = static (shell, descriptor) => new SetupJavaShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.SetupUi] = static (shell, descriptor) => new SetupUiShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.ToolsGameLink] = static (shell, descriptor) => new ToolsGameLinkShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.ToolsHelp] = static (shell, descriptor) => new ToolsHelpShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.ToolsTest] = static (shell, descriptor) => new ToolsTestShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.VersionSaveInfo] = static (shell, descriptor) => new VersionSaveInfoShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.VersionSaveBackup] = static (shell, descriptor) => new VersionSaveBackupShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.VersionSaveDatapack] = static (shell, descriptor) => new VersionSaveDatapackShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceOverview] = static (shell, descriptor) => new InstanceOverviewShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceSetup] = static (shell, descriptor) => new InstanceSetupShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceExport] = static (shell, descriptor) => new InstanceExportShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceInstall] = static (shell, descriptor) => new InstanceInstallShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceWorld] = static (shell, descriptor) => new InstanceWorldShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceScreenshot] = static (shell, descriptor) => new InstanceScreenshotShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceServer] = static (shell, descriptor) => new InstanceServerShellRightPaneViewModel(shell, descriptor),
            [StandardShellRightPaneKind.InstanceResource] = static (shell, descriptor) => new InstanceResourceShellRightPaneViewModel(shell, descriptor)
        };

    public static void Register(Application application)
    {
        foreach (var registerTemplate in TemplateRegistrations)
        {
            registerTemplate(application);
        }
    }

    public static ShellRightPaneViewModel CreateRightPane(FrontendShellViewModel shell, StandardShellRightPaneDescriptor descriptor)
    {
        if (RightPaneFactories.TryGetValue(descriptor.Kind, out var factory))
        {
            return factory(shell, descriptor);
        }

        return new GenericStandardShellRightPaneViewModel(shell, descriptor);
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
