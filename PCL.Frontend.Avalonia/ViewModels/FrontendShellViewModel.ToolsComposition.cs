using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string ToolboxUnsupportedMessage = "为便于维护，跨平台版中不包含百宝箱功能……";
    private const string LauncherShortcutDisplayName = "PCL 跨平台版";
    private const string ShortcutDesktopOptionId = "desktop";
    private const string ShortcutStartMenuOptionId = "start-menu";

    private void ApplyToolsComposition(FrontendToolsComposition composition)
    {
        _toolsComposition = composition;
        _suppressToolsPersistence = true;
        try
        {
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
            "memory-optimize" => new ActionCommand(OpenMemoryOptimizeDialog),
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

    private void ShowDailyLuck() => _ = ShowDailyLuckAsync();

    private async Task ShowDailyLuckAsync()
    {
        var seed = GenerateDailyLuckSeed();
        var random = new Random(seed);
        var luckValue = random.Next(0, 101);
        var rating = GetDailyLuckRating(luckValue);
        var title = $"今日人品 - {DateTime.Now:yyyy/MM/dd}";
        var message = luckValue >= 60
            ? $"你今天的人品值是：{luckValue}！{rating}"
            : $"你今天的人品值是：{luckValue}... {rating}";
        var result = await ShowToolboxConfirmationAsync(title, message, isDanger: luckValue <= 30);
        if (result is null)
        {
            return;
        }

        AddActivity("今日人品", $"今日人品值: {luckValue}");
    }

    private void ShowLauncherLaunchCount() => _ = ShowLauncherLaunchCountAsync();

    private async Task ShowLauncherLaunchCountAsync()
    {
        var message = $"PCL 已经为你启动了 {_launchComposition.LaunchCount} 次游戏了。";
        var result = await ShowToolboxConfirmationAsync("启动次数", message);
        if (result is null)
        {
            return;
        }

        AddActivity("查看启动计数", message);
    }

    private void OpenMemoryOptimizeDialog() => _ = OpenMemoryOptimizeDialogAsync();

    private async Task OpenMemoryOptimizeDialogAsync()
    {
        var (totalMemoryGb, availableMemoryGb) = FrontendSystemMemoryService.GetPhysicalMemoryState();
        var memoryLoadPercent = totalMemoryGb <= 0
            ? 0
            : (int)Math.Round((1d - Math.Clamp(availableMemoryGb / totalMemoryGb, 0d, 1d)) * 100d);
        if (memoryLoadPercent <= 90)
        {
            var prompt = BuildMemoryOptimizePrompt(totalMemoryGb);
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                var confirmed = await ShowToolboxConfirmationAsync("确认内存优化？", prompt, "继续");
                if (confirmed is null)
                {
                    return;
                }

                if (confirmed == false)
                {
                    AddActivity("内存优化", "已取消内存优化。");
                    return;
                }
            }
        }

        var detail = OperatingSystem.IsWindows()
            ? "Avalonia 前端尚未接入与标准实现一致的 Windows 内存优化执行器。已保留标准确认弹窗行为，不再打开文本编辑器。"
            : "当前平台暂不支持与标准实现一致的内存优化执行器。已保留标准确认弹窗行为，不再打开文本编辑器。";
        var result = await ShowToolboxConfirmationAsync("内存优化", detail);
        if (result is null)
        {
            return;
        }

        AddActivity("内存优化", detail);
    }

    private void CreateLauncherShortcut() => _ = CreateLauncherShortcutAsync();

    private async Task CreateLauncherShortcutAsync()
    {
        var shortcutTargets = BuildShortcutTargets(LauncherShortcutDisplayName);
        if (shortcutTargets.Count == 0)
        {
            AddActivity("创建快捷方式失败", "当前系统未提供可用的快捷方式目录。");
            return;
        }

        var summary = "这个快捷方式不会自动移除，在删除/移动启动器前请手动移除快捷方式。"
                      + Environment.NewLine
                      + Environment.NewLine
                      + string.Join(Environment.NewLine, shortcutTargets.Select(target => $"{target.Title}位置: {target.ShortcutPath}"));

        ToolboxShortcutTarget? selectedTarget;
        try
        {
            if (shortcutTargets.Count == 1)
            {
                var onlyTarget = shortcutTargets[0];
                var confirmed = await ShowToolboxConfirmationAsync("创建快捷方式", summary, "创建");
                if (confirmed is null)
                {
                    return;
                }

                if (confirmed == false)
                {
                    AddActivity("创建快捷方式", "已取消创建快捷方式。");
                    return;
                }

                selectedTarget = onlyTarget;
            }
            else
            {
                var selectedId = await _shellActionService.PromptForChoiceAsync(
                    "选择快捷方式位置",
                    summary,
                    shortcutTargets.Select(target => new PclChoiceDialogOption(
                        target.Id,
                        target.Title,
                        $"位置: {target.ShortcutPath}")).ToArray(),
                    ShortcutDesktopOptionId,
                    "创建");
                if (selectedId is null)
                {
                    AddActivity("创建快捷方式", "已取消创建快捷方式。");
                    return;
                }

                selectedTarget = shortcutTargets.FirstOrDefault(target => string.Equals(target.Id, selectedId, StringComparison.Ordinal));
                if (selectedTarget is null)
                {
                    AddActivity("创建快捷方式失败", $"未识别的快捷方式位置: {selectedId}");
                    return;
                }
            }

            var shortcutPath = _shellActionService.CreateLauncherShortcutAt(selectedTarget.Directory, LauncherShortcutDisplayName);
            AvaloniaHintBus.Show($"已在{selectedTarget.Title}创建快捷方式", AvaloniaHintTheme.Success);
            AddActivity("创建快捷方式", shortcutPath);
        }
        catch (Exception ex)
        {
            AddActivity("创建快捷方式失败", ex.Message);
        }
    }

    private ActionCommand CreateUnsupportedToolboxActionCommand(string title)
    {
        return new ActionCommand(() => _ = ShowUnsupportedToolboxActionAsync(title));
    }

    private async Task ShowUnsupportedToolboxActionAsync(string title)
    {
        var result = await ShowToolboxConfirmationAsync(title, ToolboxUnsupportedMessage);
        if (result is null)
        {
            return;
        }

        AddActivity(title, ToolboxUnsupportedMessage);
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
        return ComputeDjb2Hash(
            DateTime.Today.ToString("yyyyMMdd")
            + Environment.MachineName
            + "|"
            + Environment.UserName);
    }

    private static int ComputeDjb2Hash(string value)
    {
        var hash = 5381L;
        foreach (var character in value)
        {
            hash = ((hash * 33) + character) % 0x100000000L;
        }

        return (int)(hash & 0x7fffffff);
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
            100 => "100！100！" + Environment.NewLine + "隐藏主题 欧皇…… 不对，跨平台版应该没有这玩意……",
            >= 95 => "差一点就到100了呢...",
            >= 90 => "好评如潮！",
            >= 60 => "还行啦，还行啦",
            >= 40 => "勉强还行吧...",
            >= 30 => "呜...",
            >= 10 => "不会吧！",
            _ => "（是百分制哦）"
        };
    }

    private static string BuildMemoryOptimizePrompt(double totalMemoryGb)
    {
        return totalMemoryGb switch
        {
            >= 32 => "当前总内存充足，建议关闭不必要的程序来腾出内存而不是尝试使用内存优化。",
            >= 16 => "当前内存比较充足，建议优先考虑让系统自动管理内存。",
            >= 6 => "建议在使用后静置一分钟等待系统响应完毕。",
            >= 2 => "内存资源比较紧张，建议通过加装内存以避免频繁使用内存优化功能，防止内存优化对硬盘造成过大压力。",
            > 0 => "嗯……？",
            _ => string.Empty
        };
    }

    private async Task<bool?> ShowToolboxConfirmationAsync(
        string title,
        string message,
        string confirmText = "确定",
        bool isDanger = false)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(title, message, confirmText, isDanger);
        }
        catch (Exception ex)
        {
            AddActivity($"{title} 失败", ex.Message);
            return null;
        }
    }

    private List<ToolboxShortcutTarget> BuildShortcutTargets(string displayName)
    {
        var targets = new List<ToolboxShortcutTarget>();
        var shortcutFileName = GetLauncherShortcutFileName(displayName);

        var desktopDirectory = _shellActionService.PlatformAdapter.TryGetDesktopDirectory();
        if (!string.IsNullOrWhiteSpace(desktopDirectory))
        {
            targets.Add(new ToolboxShortcutTarget(
                ShortcutDesktopOptionId,
                "桌面",
                desktopDirectory,
                Path.Combine(desktopDirectory, shortcutFileName)));
        }

        if (OperatingSystem.IsWindows())
        {
            var startMenuDirectory = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (!string.IsNullOrWhiteSpace(startMenuDirectory))
            {
                var programsDirectory = Path.Combine(startMenuDirectory, "Programs");
                targets.Add(new ToolboxShortcutTarget(
                    ShortcutStartMenuOptionId,
                    "开始菜单",
                    programsDirectory,
                    Path.Combine(programsDirectory, shortcutFileName)));
            }
        }

        return targets;
    }

    private static string GetLauncherShortcutFileName(string displayName)
    {
        var extension = OperatingSystem.IsWindows()
            ? ".lnk"
            : OperatingSystem.IsMacOS()
                ? ".command"
                : ".desktop";
        return $"{displayName}{extension}";
    }

    private sealed record ToolboxShortcutTarget(
        string Id,
        string Title,
        string Directory,
        string ShortcutPath);
}
