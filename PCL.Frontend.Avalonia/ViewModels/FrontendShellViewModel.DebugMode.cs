using System.Collections.ObjectModel;
using System.Linq;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public ObservableCollection<KeyValueEntryViewModel> DebugInfoEntries { get; } = [];

    public bool ShowDebugModeSummaryCard => DebugModeEnabled;

    public bool HasDebugInfoEntries => DebugInfoEntries.Count > 0;

    public string DebugModeSummaryTitle => DebugModeEnabled ? "调试模式已启用" : "调试模式未启用";

    public string DebugModeSummaryDescription => DebugModeEnabled
        ? "实时日志会补充启动决策、关键路径与异常明细，下面会显示当前会话可用于排障的路径和运行环境。"
        : "启用后会显示更详细的启动、下载与故障诊断信息。";

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
        var runtimePaths = _shellActionService.RuntimePaths;
        var entries = new List<KeyValueEntryViewModel>
        {
            new("状态", DebugModeEnabled ? "已启用" : "已关闭"),
            new("启动器目录", runtimePaths.ExecutableDirectory),
            new("数据目录", runtimePaths.DataDirectory),
            new("共享配置", runtimePaths.SharedConfigPath),
            new("本地配置", runtimePaths.LocalConfigPath),
            new("日志目录", runtimePaths.LauncherLogDirectory),
            new("当前实例", _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : "未选择"),
            new("实例目录", _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceDirectory : "未选择"),
            new("当前 Java", _launchComposition.SelectedJavaRuntime?.DisplayName ?? "未解析"),
            new("Java 路径", _launchComposition.SelectedJavaRuntime?.ExecutablePath ?? "未解析"),
            new("Java 架构", FormatJavaArchitecture(_launchComposition.SelectedJavaRuntime)),
            new("最近启动脚本", _latestLaunchScriptPath ?? "暂无"),
            new("最近会话摘要", _latestLaunchSessionSummaryPath ?? "暂无"),
            new("最近原始输出", _latestLaunchRawOutputLogPath ?? "暂无")
        };

        return entries;
    }

    private void AppendLaunchDebugLine(string title, string? value)
    {
        if (!DebugModeEnabled || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AppendLaunchLogLine($"[调试模式] {title}: {value}");
    }

    private void AppendLaunchDebugException(string title, Exception ex)
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchLogLine($"[调试模式] {title}: {ex.GetType().FullName}: {ex.Message}");
        foreach (var line in EnumerateExceptionDetailLines(ex))
        {
            AppendLaunchLogLine($"[调试模式] {line}");
        }
    }

    private void AppendLaunchDebugCompositionSnapshot()
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchDebugLine("实例", _launchComposition.InstanceName);
        AppendLaunchDebugLine("实例目录", _launchComposition.InstancePath);
        AppendLaunchDebugLine("登录方式", LaunchAuthLabel);
        AppendLaunchDebugLine("账号", GetLaunchProfileIdentityLabel());
        AppendLaunchDebugLine("Java", _launchComposition.SelectedJavaRuntime?.DisplayName);
        AppendLaunchDebugLine("Java 路径", _launchComposition.SelectedJavaRuntime?.ExecutablePath);
        AppendLaunchDebugLine("工作目录", _launchComposition.SessionStartPlan.ProcessShellPlan.WorkingDirectory);
        AppendLaunchDebugLine("游戏执行文件", _launchComposition.SessionStartPlan.ProcessShellPlan.FileName);
        AppendLaunchDebugLine("进程优先级", _launchComposition.SessionStartPlan.ProcessShellPlan.PriorityKind.ToString());
        AppendLaunchDebugLine("环境变量数量", _launchComposition.SessionStartPlan.ProcessShellPlan.EnvironmentVariables.Count.ToString());
        AppendLaunchDebugLine("Classpath 条目", _launchComposition.ClasspathPlan.Entries.Count.ToString());
        AppendLaunchDebugLine("替换值数量", _launchComposition.ReplacementPlan.Values.Count.ToString());
        AppendLaunchDebugLine("Natives 目录", _launchComposition.NativesDirectory);
    }

    private void AppendRepairDebugSummary(FrontendInstanceRepairResult repairResult)
    {
        if (!DebugModeEnabled)
        {
            return;
        }

        AppendLaunchDebugLine("下载文件样例", BuildPathSample(repairResult.DownloadedFiles));
        AppendLaunchDebugLine("复用文件样例", BuildPathSample(repairResult.ReusedFiles));
    }

    private static string BuildPathSample(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return "无";
        }

        var visiblePaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Take(3)
            .ToArray();
        var suffix = paths.Count > visiblePaths.Length ? $" 等 {paths.Count} 个文件" : string.Empty;
        return string.Join(" | ", visiblePaths) + suffix;
    }

    private static string FormatJavaArchitecture(FrontendJavaRuntimeSummary? runtime)
    {
        if (runtime is null)
        {
            return "未解析";
        }

        var bits = runtime.Is64Bit switch
        {
            true => "64-bit",
            false => "32-bit",
            null => "位数未知"
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
                ? $"异常类型: {current.GetType().FullName}"
                : $"内部异常 {depth}: {current.GetType().FullName}: {current.Message}";

            if (!string.IsNullOrWhiteSpace(current.StackTrace))
            {
                foreach (var stackLine in current.StackTrace
                             .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                             .Take(12))
                {
                    yield return $"栈: {stackLine.Trim()}";
                }
            }

            current = current.InnerException;
            depth++;
        }
    }
}
