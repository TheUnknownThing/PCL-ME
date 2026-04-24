using System.Collections.ObjectModel;
using System.Linq;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    public ObservableCollection<KeyValueEntryViewModel> DebugInfoEntries { get; } = [];

    public bool ShowDebugModeSummaryCard => DebugModeEnabled;

    public bool HasDebugInfoEntries => DebugInfoEntries.Count > 0;

    public string DebugModeSummaryTitle => DebugModeEnabled ? "Debug mode enabled" : "Debug mode disabled";

    public string DebugModeSummaryDescription => DebugModeEnabled
        ? "Live logs add launch decisions, key paths, and exception details. The current session paths and runtime environment shown below can help with troubleshooting."
        : "Enabling this shows more detailed launch, download, and diagnostics information.";

    private void RefreshDebugModeSurface()
    {
        ReplaceItems(DebugInfoEntries, BuildDebugInfoEntries());
        RaisePropertyChanged(nameof(ShowDebugModeSummaryCard));
        RaisePropertyChanged(nameof(HasDebugInfoEntries));
        RaisePropertyChanged(nameof(DebugModeSummaryTitle));
        RaisePropertyChanged(nameof(DebugModeSummaryDescription));
    }

    private IReadOnlyList<KeyValueEntryViewModel> BuildDebugInfoEntries()
    {
        var runtimePaths = _launcherActionService.RuntimePaths;
        var entries = new List<KeyValueEntryViewModel>
        {
            new("Status", DebugModeEnabled ? "Enabled" : "Disabled"),
            new("Launcher directory", runtimePaths.ExecutableDirectory),
            new("Data directory", runtimePaths.DataDirectory),
            new("Shared config", runtimePaths.SharedConfigPath),
            new("Local config", runtimePaths.LocalConfigPath),
            new("Logs directory", runtimePaths.LauncherLogDirectory),
            new(
                T("setup.launcher_misc.debug_summary.fields.current_instance"),
                _instanceComposition.Selection.HasSelection
                    ? _instanceComposition.Selection.InstanceName
                    : T("setup.launcher_misc.debug_summary.values.not_selected")),
            new(
                T("setup.launcher_misc.debug_summary.fields.instance_directory"),
                _instanceComposition.Selection.HasSelection
                    ? _instanceComposition.Selection.InstanceDirectory
                    : T("setup.launcher_misc.debug_summary.values.not_selected")),
            new("Current Java", _launchComposition.SelectedJavaRuntime?.DisplayName ?? "Not resolved"),
            new("Java path", _launchComposition.SelectedJavaRuntime?.ExecutablePath ?? "Not resolved"),
            new("Java architecture", FormatJavaArchitecture(_launchComposition.SelectedJavaRuntime)),
            new("Recent launch script", _latestLaunchScriptPath ?? "None"),
            new("Recent session summary", _latestLaunchSessionSummaryPath ?? "None"),
            new("Recent raw output", _latestLaunchRawOutputLogPath ?? "None")
        };

        return entries;
    }

    private void AppendLaunchDebugLine(string title, string? value)
    {
        if (!DebugModeEnabled || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AppendLaunchLogLine($"[Debug mode] {title}: {value}");
    }

    private void AppendLaunchDebugException(string title, Exception ex)
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchLogLine($"[Debug mode] {title}: {ex.GetType().FullName}: {ex.Message}");
        foreach (var line in EnumerateExceptionDetailLines(ex))
        {
            AppendLaunchLogLine($"[Debug mode] {line}");
        }
    }

    private void AppendLaunchDebugCompositionSnapshot()
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchDebugLine("Instance", _launchComposition.InstanceName);
        AppendLaunchDebugLine("Instance directory", _launchComposition.InstancePath);
        AppendLaunchDebugLine("Login method", LaunchAuthLabel);
        AppendLaunchDebugLine("Account", GetLaunchProfileIdentityLabel());
        AppendLaunchDebugLine("Java", _launchComposition.SelectedJavaRuntime?.DisplayName);
        AppendLaunchDebugLine("Java path", _launchComposition.SelectedJavaRuntime?.ExecutablePath);
        AppendLaunchDebugLine("Working directory", _launchComposition.SessionStartPlan.ProcessShellPlan.WorkingDirectory);
        AppendLaunchDebugLine("Game executable", _launchComposition.SessionStartPlan.ProcessShellPlan.FileName);
        AppendLaunchDebugLine("Process priority", _launchComposition.SessionStartPlan.ProcessShellPlan.PriorityKind.ToString());
        AppendLaunchDebugLine("Environment variable count", _launchComposition.SessionStartPlan.ProcessShellPlan.EnvironmentVariables.Count.ToString());
        AppendLaunchDebugLine("Classpath entries", _launchComposition.ClasspathPlan.Entries.Count.ToString());
        AppendLaunchDebugLine("Replacement count", _launchComposition.ReplacementPlan.Values.Count.ToString());
        AppendLaunchDebugLine("Natives directory", _launchComposition.NativesDirectory);
    }

    private void AppendRepairDebugSummary(FrontendInstanceRepairResult repairResult)
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchDebugLine("Downloaded file sample", BuildPathSample(repairResult.DownloadedFiles));
        AppendLaunchDebugLine("Reused file sample", BuildPathSample(repairResult.ReusedFiles));
    }

    private static string BuildPathSample(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return "None";
        }

        var visiblePaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Take(3)
            .ToArray();
        var suffix = paths.Count > visiblePaths.Length ? $" and {paths.Count} more files" : string.Empty;
        return string.Join(" | ", visiblePaths) + suffix;
    }

    private static string FormatJavaArchitecture(FrontendJavaRuntimeSummary? runtime)
    {
        if (runtime is null)
        {
            return "Not resolved";
        }

        var bits = runtime.Is64Bit switch
        {
            true => "64-bit",
            false => "32-bit",
            null => "Bitness unknown"
        };

        return runtime.Architecture is null
            ? bits
            : $"{runtime.Architecture} / {bits}";
    }

    private static IEnumerable<string> EnumerateExceptionDetailLines(Exception ex)
    {
        var current = ex;
        var depth = 0;
        while (current is not null && depth < 4)
        {
            yield return depth == 0
                ? $"Exception type: {current.GetType().FullName}"
                : $"Inner exception {depth}: {current.GetType().FullName}: {current.Message}";

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                foreach (var stackLine in current.StackTrace
                             .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                             .Take(12))
                {
                    yield return $"Stack: {stackLine.Trim()}";
                }
            }

            current = current.InnerException;
            depth++;
        }
    }
}
