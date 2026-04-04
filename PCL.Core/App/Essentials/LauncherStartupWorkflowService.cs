using System;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupWorkflowService
{
    public static LauncherStartupWorkflowPlan BuildPlan(LauncherStartupWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var immediateCommand = LauncherStartupShellService.ResolveImmediateCommand(request.CommandLineArguments);
        var bootstrap = LauncherStartupPreparationService.Prepare(
            new LauncherStartupPreparationRequest(
                request.ExecutableDirectory,
                request.TempDirectory,
                request.AppDataDirectory,
                request.IsBetaVersion,
                request.DetectedWindowsVersion,
                request.Is64BitOperatingSystem));
        var visual = LauncherStartupVisualService.GetVisualPlan(request.ShowStartupLogo);
        var environmentWarningPrompt = LauncherStartupShellService.GetEnvironmentWarningPrompt(bootstrap.EnvironmentWarningMessage);

        return new LauncherStartupWorkflowPlan(
            immediateCommand,
            bootstrap,
            visual,
            environmentWarningPrompt);
    }
}

public sealed record LauncherStartupWorkflowRequest(
    string[]? CommandLineArguments,
    string ExecutableDirectory,
    string TempDirectory,
    string AppDataDirectory,
    bool IsBetaVersion,
    Version DetectedWindowsVersion,
    bool Is64BitOperatingSystem,
    bool ShowStartupLogo);

public sealed record LauncherStartupWorkflowPlan(
    LauncherStartupImmediateCommandPlan ImmediateCommand,
    LauncherStartupBootstrapResult Bootstrap,
    LauncherStartupVisualPlan Visual,
    LauncherStartupPrompt? EnvironmentWarningPrompt);
