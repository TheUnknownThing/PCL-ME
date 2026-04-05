using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PCL.Frontend.Spike.Desktop.ShellViews.Left;
using PCL.Frontend.Spike.Desktop.ShellViews.Right;
using PCL.Frontend.Spike.ViewModels.ShellPanes;

namespace PCL.Frontend.Spike.Desktop.ShellViews;

internal static class ShellPaneTemplateRegistry
{
    public static void Register(Application application)
    {
        application.DataTemplates.Add(CreateTemplate<StandardShellNavigationListPaneViewModel, StandardShellNavigationListPaneView>());
        application.DataTemplates.Add(CreateTemplate<StandardShellSummaryPaneViewModel, StandardShellSummaryPaneView>());
        application.DataTemplates.Add(CreateTemplate<StandardShellEmptyPaneViewModel, StandardShellEmptyPaneView>());
        application.DataTemplates.Add(CreateTemplate<GenericStandardShellRightPaneViewModel, GenericStandardShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<LegacyStandardShellRightPaneViewModel, LegacyStandardShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<DownloadInstallShellRightPaneViewModel, DownloadInstallShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<DownloadCatalogShellRightPaneViewModel, DownloadCatalogShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<DownloadResourceShellRightPaneViewModel, DownloadResourceShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<DownloadFavoritesShellRightPaneViewModel, DownloadFavoritesShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupLaunchShellRightPaneViewModel, SetupLaunchShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupAboutShellRightPaneViewModel, SetupAboutShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupFeedbackShellRightPaneViewModel, SetupFeedbackShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupLogShellRightPaneViewModel, SetupLogShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupUpdateShellRightPaneViewModel, SetupUpdateShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupGameLinkShellRightPaneViewModel, SetupGameLinkShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupGameManageShellRightPaneViewModel, SetupGameManageShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupLauncherMiscShellRightPaneViewModel, SetupLauncherMiscShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupJavaShellRightPaneViewModel, SetupJavaShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<SetupUiShellRightPaneViewModel, SetupUiShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<ToolsGameLinkShellRightPaneViewModel, ToolsGameLinkShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<ToolsHelpShellRightPaneViewModel, ToolsHelpShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<ToolsTestShellRightPaneViewModel, ToolsTestShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<VersionSaveInfoShellRightPaneViewModel, VersionSaveInfoShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<VersionSaveBackupShellRightPaneViewModel, VersionSaveBackupShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<VersionSaveDatapackShellRightPaneViewModel, VersionSaveDatapackShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceOverviewShellRightPaneViewModel, InstanceOverviewShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceSetupShellRightPaneViewModel, InstanceSetupShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceExportShellRightPaneViewModel, InstanceExportShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceInstallShellRightPaneViewModel, InstanceInstallShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceWorldShellRightPaneViewModel, InstanceWorldShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceScreenshotShellRightPaneViewModel, InstanceScreenshotShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceServerShellRightPaneViewModel, InstanceServerShellRightPaneView>());
        application.DataTemplates.Add(CreateTemplate<InstanceResourceShellRightPaneViewModel, InstanceResourceShellRightPaneView>());
    }

    private static FuncDataTemplate<TViewModel> CreateTemplate<TViewModel, TView>()
        where TViewModel : class
        where TView : Control, new()
    {
        return new FuncDataTemplate<TViewModel>((_, _) => new TView());
    }
}
