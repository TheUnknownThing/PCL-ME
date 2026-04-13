namespace PCL.Frontend.Avalonia.ViewModels.ShellPanes;

internal abstract class ShellPaneViewModel(FrontendShellViewModel shell, string key)
{
    public FrontendShellViewModel Shell { get; } = shell;

    public string Key { get; } = key;
}

internal abstract class ShellLeftPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellPaneViewModel(shell, descriptor.Key)
{
    public StandardShellLeftPaneDescriptor Descriptor { get; } = descriptor;
}

internal abstract class ShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellPaneViewModel(shell, descriptor.Key)
{
    public StandardShellRightPaneDescriptor Descriptor { get; } = descriptor;
}

internal sealed class GenericStandardShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class StandardShellNavigationListPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class InstanceSelectShellLeftPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class TaskManagerShellLeftPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class DownloadInstallShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class DownloadCatalogShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class DownloadResourceShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class DownloadFavoritesShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupLaunchShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupAboutShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupFeedbackShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupLogShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupUpdateShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupGameManageShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupLauncherMiscShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupJavaShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class SetupUiShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class ToolsHelpShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class ToolsTestShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class VersionSaveInfoShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class VersionSaveBackupShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class VersionSaveDatapackShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceOverviewShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceSetupShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceExportShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceInstallShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceWorldShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceScreenshotShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceServerShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceResourceShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class InstanceSelectShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class TaskManagerShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);
