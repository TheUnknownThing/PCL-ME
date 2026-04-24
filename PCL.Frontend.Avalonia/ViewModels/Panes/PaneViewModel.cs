namespace PCL.Frontend.Avalonia.ViewModels.Panes;

internal abstract class PaneViewModel(LauncherViewModel launcher, string key)
{
    public LauncherViewModel Launcher { get; } = launcher;

    public string Key { get; } = key;
}

internal abstract class LeftPaneViewModel(
    LauncherViewModel launcher,
    StandardLeftPaneDescriptor descriptor)
    : PaneViewModel(launcher, descriptor.Key)
{
    public StandardLeftPaneDescriptor Descriptor { get; } = descriptor;
}

internal abstract class RightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : PaneViewModel(launcher, descriptor.Key)
{
    public StandardRightPaneDescriptor Descriptor { get; } = descriptor;
}

internal sealed class SharedRouteRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class StandardNavigationListPaneViewModel(
    LauncherViewModel launcher,
    StandardLeftPaneDescriptor descriptor)
    : LeftPaneViewModel(launcher, descriptor);

internal sealed class InstanceSelectLeftPaneViewModel(
    LauncherViewModel launcher,
    StandardLeftPaneDescriptor descriptor)
    : LeftPaneViewModel(launcher, descriptor);

internal sealed class TaskManagerLeftPaneViewModel(
    LauncherViewModel launcher,
    StandardLeftPaneDescriptor descriptor)
    : LeftPaneViewModel(launcher, descriptor);

internal sealed class DownloadInstallRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class DownloadCatalogRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class DownloadResourceRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class DownloadFavoritesRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupLaunchRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupAboutRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupFeedbackRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupLogRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupUpdateRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupGameManageRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupLauncherMiscRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupJavaRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class SetupUiRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class ToolsHelpRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class ToolsTestRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class VersionSaveInfoRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class VersionSaveBackupRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class VersionSaveDatapackRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceOverviewRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceSetupRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceExportRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceInstallRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceWorldRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceScreenshotRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceServerRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceResourceRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class InstanceSelectRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);

internal sealed class TaskManagerRightPaneViewModel(
    LauncherViewModel launcher,
    StandardRightPaneDescriptor descriptor)
    : RightPaneViewModel(launcher, descriptor);
