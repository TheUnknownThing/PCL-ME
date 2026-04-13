using System.Text;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void ApplyToolsComposition(FrontendToolsComposition composition)
    {
        _toolsComposition = composition;
        _suppressToolsPersistence = true;
        try
        {
            InitializeToolsGameLinkSurface();
            InitializeToolsTestSurface();
            RefreshHelpTopics();
        }
        finally
        {
            _suppressToolsPersistence = false;
        }
    }

    private void ReloadToolsComposition()
    {
        ApplyToolsComposition(FrontendToolsCompositionService.Compose(_shellActionService.RuntimePaths, _instanceComposition));
    }

    private void PersistToolsSetting(string? propertyName)
    {
        if (_suppressToolsPersistence || string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(ToolDownloadFolder):
                _shellActionService.PersistSharedValue("CacheDownloadFolder", ToolDownloadFolder);
                break;
            case nameof(ToolDownloadUserAgent):
                _shellActionService.PersistSharedValue("ToolDownloadCustomUserAgent", ToolDownloadUserAgent);
                break;
        }
    }

    private ActionCommand ResolveToolboxActionCommand(string actionKey, string title)
    {
        return actionKey switch
        {
            "crash-test" => new ActionCommand(TriggerCrashPromptTest),
            "memory-optimize" => new ActionCommand(ExportMemoryOptimizeReport),
            "clear-rubbish" => new ActionCommand(ClearToolboxRubbish),
            "daily-luck" => new ActionCommand(ShowDailyLuck),
            "create-shortcut" => new ActionCommand(CreateLauncherShortcut),
            "launch-count" => new ActionCommand(ShowLauncherLaunchCount),
            _ => CreateUnsupportedToolboxActionCommand(title)
        };
    }

    private ToolboxActionViewModel CreateToolboxAction(FrontendToolboxActionDefinition action)
    {
        return new ToolboxActionViewModel(
            action.Title,
            action.ToolTip,
            action.MinWidth,
            action.IsDanger ? PclButtonColorState.Red : PclButtonColorState.Normal,
            ResolveToolboxActionCommand(action.ActionKey, action.Title));
    }

    private HelpTopicViewModel CreateHelpTopic(FrontendToolsHelpEntry entry)
    {
        return new HelpTopicViewModel(
            entry.GroupTitle,
            entry.Title,
            entry.Summary,
            entry.Keywords,
            new ActionCommand(() => OpenHelpTopic(entry)));
    }

    private void ClearToolboxRubbish()
    {
        var removedCount = 0;
        removedCount += DeleteDirectorySafely(_shellActionService.RuntimePaths.FrontendArtifactDirectory);
        removedCount += DeleteDirectorySafely(_shellActionService.RuntimePaths.FrontendTempDirectory);
        removedCount += DeleteDirectoryContentsSafely(Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log"));
        if (!string.IsNullOrWhiteSpace(_instanceComposition.Selection.LauncherDirectory))
        {
            removedCount += DeleteDirectorySafely(Path.Combine(_instanceComposition.Selection.LauncherDirectory, "crash-reports"));
            removedCount += DeleteDirectorySafely(Path.Combine(_instanceComposition.Selection.LauncherDirectory, "logs"));
        }

        AddActivity(
            "清理游戏垃圾",
            removedCount == 0
                ? "没有检测到需要清理的缓存、日志或崩溃报告。"
                : $"已清理 {removedCount} 个缓存或日志项目。");
    }

    private void ShowDailyLuck()
    {
        var seed = GenerateDailyLuckSeed();
        var random = new Random(seed);
        var luckValue = random.Next(0, 101);
        var reportPath = WriteToolboxReport(
            "daily-luck",
            "今日人品.txt",
            [
                $"日期: {DateTime.Now:yyyy/MM/dd}",
                $"种子: {seed}",
                $"今日人品: {luckValue}",
                $"评价: {GetDailyLuckRating(luckValue)}"
            ]);
        OpenInstanceTarget("今日人品", reportPath, "人品报告不存在。");
    }

    private void ShowLauncherLaunchCount()
    {
        var reportPath = WriteToolboxReport(
            "launch-count",
            "启动计数.txt",
            [
                $"启动器累计启动次数: {_launchComposition.LaunchCount}",
                $"当前实例: {_instanceComposition.Selection.InstanceName}"
            ]);
        OpenInstanceTarget("查看启动计数", reportPath, "启动计数报告不存在。");
    }

    private void ExportMemoryOptimizeReport()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var reportPath = WriteToolboxReport(
            "memory-optimize",
            "内存优化诊断.txt",
            [
                $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                $"系统: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                $"当前进程: {Environment.ProcessPath ?? "未知"}",
                $"当前工作集: {FormatBytes(Environment.WorkingSet)}",
                $"GC 可用内存上限: {FormatBytes(gcInfo.TotalAvailableMemoryBytes)}",
                $"GC 已提交字节: {FormatBytes(gcInfo.TotalCommittedBytes)}",
                string.Empty,
                "当前 replacement shell 已不再把“内存优化”按钮留作纯意图日志。",
                "但跨平台的内存优化执行器尚未迁入前端壳层，因此这里会先导出诊断信息，明确提示当前缺口。"
            ]);
        OpenInstanceTarget("内存优化", reportPath, "内存优化诊断报告不存在。");
    }

    private void CreateLauncherShortcut()
    {
        try
        {
            var shortcutPath = _shellActionService.CreateLauncherShortcut("PCL 社区版");
            OpenInstanceTarget("创建快捷方式", shortcutPath, "快捷方式文件不存在。");
        }
        catch (Exception ex)
        {
            AddActivity("创建快捷方式失败", ex.Message);
        }
    }

    private ActionCommand CreateUnsupportedToolboxActionCommand(string title)
    {
        return new ActionCommand(() =>
        {
            var reportPath = WriteToolboxReport(
                "unsupported-actions",
                $"{SanitizeFileSegment(title)}.md",
                [
                    $"# {title}",
                    string.Empty,
                    "这个百宝箱动作已经从纯活动日志升级为可追踪的诊断报告输出。",
                    "当前前端没有找到它的专属执行器，因此先导出这份说明文件，避免静默退回到意图占位。",
                    string.Empty,
                    $"时间: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                    $"动作: {title}",
                    $"当前实例: {_instanceComposition.Selection.InstanceName}"
                ]);
            OpenInstanceTarget(title, reportPath, "百宝箱诊断文件不存在。");
        });
    }

    private string WriteToolboxReport(string folderName, string fileName, IReadOnlyList<string> lines)
    {
        var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "toolbox", folderName);
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        return outputPath;
    }

    private static int DeleteDirectorySafely(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var entryCount = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Count();
            Directory.Delete(path, recursive: true);
            return Math.Max(1, entryCount);
        }
        catch
        {
            return 0;
        }
    }

    private static int DeleteDirectoryContentsSafely(string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        var removedCount = 0;
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Keep clearing other files.
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
        {
            removedCount += DeleteDirectorySafely(directory);
        }

        return removedCount;
    }

    private static int GenerateDailyLuckSeed()
    {
        var hash = 5381L;
        var datePart = DateTime.Now.ToString("yyyyMMdd");
        foreach (var character in datePart)
        {
            hash = ((hash * 33) + character) & 0x7fffffff;
        }

        return (int)hash;
    }

    private static string FormatBytes(long value)
    {
        if (value <= 0)
        {
            return "未知";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)value;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private static string GetDailyLuckRating(int luckValue)
    {
        return luckValue switch
        {
            100 => "100！100！",
            >= 95 => "差一点就到 100 了呢……",
            >= 90 => "好评如潮！",
            >= 60 => "还行啦，还行啦",
            >= 40 => "勉强还行吧……",
            >= 30 => "呜……",
            >= 10 => "不会吧！",
            _ => "（是百分制哦）"
        };
    }
}
