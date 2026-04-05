namespace PCL.Frontend.Spike.ViewModels.ShellPanes;

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

internal sealed class StandardShellNavigationListPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class StandardShellSummaryPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class StandardShellEmptyPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellLeftPaneDescriptor descriptor)
    : ShellLeftPaneViewModel(shell, descriptor);

internal sealed class LegacyStandardShellRightPaneViewModel(
    FrontendShellViewModel shell,
    StandardShellRightPaneDescriptor descriptor)
    : ShellRightPaneViewModel(shell, descriptor);

internal sealed class ToolsHelpShellRightPaneViewModel(
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
