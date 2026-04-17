using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const string LauncherShortcutDisplayName = FrontendApplicationIdentity.DisplayName;
    private const string ShortcutDesktopOptionId = "desktop";
    private const string ShortcutStartMenuOptionId = "start-menu";

    public string ToolsTestToolboxCardHeader => LT("shell.tools.test.cards.toolbox");

    public string ToolsTestCustomDownloadCardHeader => LT("shell.tools.test.cards.custom_download");

    public string ToolsTestCustomDownloadDescription => LT("shell.tools.test.custom_download.description");

    public string ToolsTestCustomDownloadAddressLabel => LT("shell.tools.test.custom_download.address");

    public string ToolsTestCustomDownloadUserAgentLabel => LT("shell.tools.test.custom_download.user_agent");

    public string ToolsTestCustomDownloadSaveToLabel => LT("shell.tools.test.custom_download.save_to");

    public string ToolsTestCustomDownloadFileNameLabel => LT("shell.tools.test.custom_download.file_name");

    public string ToolsTestCustomDownloadSelectButtonText => LT("shell.tools.test.custom_download.select");

    public string ToolsTestCustomDownloadStartButtonText => LT("shell.tools.test.custom_download.start");

    public string ToolsTestCustomDownloadOpenFolderButtonText => LT("shell.tools.test.custom_download.open_folder");

    public string ToolsTestOfficialSkinCardHeader => LT("shell.tools.test.cards.official_skin");

    public string ToolsTestOfficialSkinPlayerNameLabel => LT("shell.tools.test.official_skin.player_name");

    public string ToolsTestOfficialSkinSaveButtonText => LT("shell.tools.test.official_skin.save");

    public string ToolsTestServerQueryCardHeader => LT("shell.tools.test.cards.server_query");

    public string ToolsTestAchievementCardHeader => LT("shell.tools.test.cards.achievement");

    public string ToolsTestAchievementBlockIdLabel => LT("shell.tools.test.achievement.block_id");

    public string ToolsTestAchievementTitleLabel => LT("shell.tools.test.achievement.title");

    public string ToolsTestAchievementFirstLineLabel => LT("shell.tools.test.achievement.first_line");

    public string ToolsTestAchievementSecondLineLabel => LT("shell.tools.test.achievement.second_line");

    public string ToolsTestAchievementPreviewButtonText => LT("shell.tools.test.achievement.preview");

    public string ToolsTestAchievementSaveButtonText => LT("shell.tools.test.achievement.save");

    public string ToolsTestHeadCardHeader => LT("shell.tools.test.cards.head");

    public string ToolsTestHeadSizeLabel => LT("shell.tools.test.head.size");

    public string ToolsTestHeadPreviewLabel => LT("shell.tools.test.head.preview");

    public string ToolsTestHeadSelectButtonText => LT("shell.tools.test.head.select_skin");

    public string ToolsTestHeadSaveButtonText => LT("shell.tools.test.head.save");

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

    private void ApplyHelpState(FrontendToolsHelpState helpState)
    {
        var currentReference = _currentHelpDetailEntry?.RawPath;
        _toolsComposition = _toolsComposition with { Help = helpState };
        if (!string.IsNullOrWhiteSpace(currentReference) && TryResolveHelpEntry(currentReference, out var refreshedEntry))
        {
            _currentHelpDetailEntry = refreshedEntry;
        }

        RefreshHelpTopics();
        RefreshHelpDetailSurface();
    }

    private void ReloadToolsComposition()
    {
        ApplyToolsComposition(FrontendToolsCompositionService.Compose(_shellActionService.RuntimePaths, _i18n));
    }

    private void ReloadHelpState()
    {
        ApplyHelpState(FrontendToolsCompositionService.LoadHelpState(_shellActionService.RuntimePaths, _i18n.Locale));
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
        var title = ResolveToolboxActionTitle(action.ActionKey, action.Title);
        var toolTip = ResolveToolboxActionToolTip(action.ActionKey, action.ToolTip);
        return new ToolboxActionViewModel(
            title,
            toolTip,
            action.MinWidth,
            action.IsDanger ? PclButtonColorState.Red : PclButtonColorState.Normal,
            ResolveToolboxActionCommand(action.ActionKey, title));
    }

    private string ResolveToolboxActionTitle(string actionKey, string fallback)
    {
        return actionKey switch
        {
            "memory-optimize" => LT("shell.tools.test.toolbox.actions.memory_optimize.title"),
            "clear-rubbish" => LT("shell.tools.test.toolbox.actions.clear_rubbish.title"),
            "daily-luck" => LT("shell.tools.test.toolbox.actions.daily_luck.title"),
            "crash-test" => LT("shell.tools.test.toolbox.actions.crash_test.title"),
            "create-shortcut" => LT("shell.tools.test.toolbox.actions.create_shortcut.title"),
            "launch-count" => LT("shell.tools.test.toolbox.actions.launch_count.title"),
            _ => fallback
        };
    }

    private string ResolveToolboxActionToolTip(string actionKey, string fallback)
    {
        return actionKey switch
        {
            "memory-optimize" => LT("shell.tools.test.toolbox.actions.memory_optimize.tooltip"),
            "clear-rubbish" => LT("shell.tools.test.toolbox.actions.clear_rubbish.tooltip"),
            "daily-luck" => LT("shell.tools.test.toolbox.actions.daily_luck.tooltip"),
            "crash-test" => LT("shell.tools.test.toolbox.actions.crash_test.tooltip"),
            "create-shortcut" => LT("shell.tools.test.toolbox.actions.create_shortcut.tooltip"),
            "launch-count" => LT("shell.tools.test.toolbox.actions.launch_count.tooltip"),
            _ => fallback
        };
    }

    private void RefreshToolsTestLocalization()
    {
        ReplaceItems(ToolboxActions, _toolsComposition.Test.ToolboxActions.Select(CreateToolboxAction));
        RaisePropertyChanged(nameof(ToolsTestToolboxCardHeader));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadCardHeader));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadDescription));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadAddressLabel));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadUserAgentLabel));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadSaveToLabel));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadFileNameLabel));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadSelectButtonText));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadStartButtonText));
        RaisePropertyChanged(nameof(ToolsTestCustomDownloadOpenFolderButtonText));
        RaisePropertyChanged(nameof(ToolsTestOfficialSkinCardHeader));
        RaisePropertyChanged(nameof(ToolsTestOfficialSkinPlayerNameLabel));
        RaisePropertyChanged(nameof(ToolsTestOfficialSkinSaveButtonText));
        RaisePropertyChanged(nameof(ToolsTestServerQueryCardHeader));
        RaisePropertyChanged(nameof(MinecraftServerQueryAddressWatermark));
        RaisePropertyChanged(nameof(MinecraftServerQueryQueryButtonText));
        RaisePropertyChanged(nameof(ToolsTestAchievementCardHeader));
        RaisePropertyChanged(nameof(ToolsTestAchievementBlockIdLabel));
        RaisePropertyChanged(nameof(ToolsTestAchievementTitleLabel));
        RaisePropertyChanged(nameof(ToolsTestAchievementFirstLineLabel));
        RaisePropertyChanged(nameof(ToolsTestAchievementSecondLineLabel));
        RaisePropertyChanged(nameof(ToolsTestAchievementPreviewButtonText));
        RaisePropertyChanged(nameof(ToolsTestAchievementSaveButtonText));
        RaisePropertyChanged(nameof(ToolsTestHeadCardHeader));
        RaisePropertyChanged(nameof(ToolsTestHeadSizeLabel));
        RaisePropertyChanged(nameof(ToolsTestHeadPreviewLabel));
        RaisePropertyChanged(nameof(ToolsTestHeadSelectButtonText));
        RaisePropertyChanged(nameof(ToolsTestHeadSaveButtonText));
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
            LT("shell.tools.test.toolbox.actions.clear_rubbish.title"),
            removedCount == 0
                ? LT("shell.tools.test.toolbox.actions.clear_rubbish.none_removed")
                : LT("shell.tools.test.toolbox.actions.clear_rubbish.removed", ("count", removedCount)));
    }

    private void ShowDailyLuck() => _ = ShowDailyLuckAsync();

    private async Task ShowDailyLuckAsync()
    {
        var seed = GenerateDailyLuckSeed();
        var random = new Random(seed);
        var luckValue = random.Next(0, 101);
        var rating = GetDailyLuckRating(luckValue);
        var title = LT("shell.tools.test.toolbox.actions.daily_luck.dialog_title", ("date", DateTime.Now.ToString("yyyy/MM/dd")));
        var message = luckValue >= 60
            ? LT("shell.tools.test.toolbox.actions.daily_luck.high_message", ("value", luckValue), ("rating", rating))
            : LT("shell.tools.test.toolbox.actions.daily_luck.low_message", ("value", luckValue), ("rating", rating));
        var result = await ShowToolboxConfirmationAsync(title, message, isDanger: luckValue <= 30);
        if (result is null)
        {
            return;
        }

        AddActivity(
            LT("shell.tools.test.toolbox.actions.daily_luck.title"),
            LT("shell.tools.test.toolbox.actions.daily_luck.activity", ("value", luckValue)));
    }

    private void ShowLauncherLaunchCount() => _ = ShowLauncherLaunchCountAsync();

    private async Task ShowLauncherLaunchCountAsync()
    {
        var message = LT("shell.tools.test.toolbox.actions.launch_count.message", ("count", _launchComposition.LaunchCount));
        var result = await ShowToolboxConfirmationAsync(
            LT("shell.tools.test.toolbox.actions.launch_count.dialog_title"),
            message);
        if (result is null)
        {
            return;
        }

        AddActivity(LT("shell.tools.test.toolbox.actions.launch_count.title"), message);
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
                var confirmed = await ShowToolboxConfirmationAsync(
                    LT("shell.tools.test.toolbox.actions.memory_optimize.confirm_title"),
                    prompt,
                    LT("shell.tools.test.toolbox.actions.memory_optimize.continue"));
                if (confirmed is null)
                {
                    return;
                }

                if (confirmed == false)
                {
                    AddActivity(
                        LT("shell.tools.test.toolbox.actions.memory_optimize.title"),
                        LT("shell.tools.test.toolbox.actions.memory_optimize.cancelled"));
                    return;
                }
            }
        }

        var detail = OperatingSystem.IsWindows()
            ? LT("shell.tools.test.toolbox.actions.memory_optimize.unsupported_windows")
            : LT("shell.tools.test.toolbox.actions.memory_optimize.unsupported_other");
        var result = await ShowToolboxConfirmationAsync(LT("shell.tools.test.toolbox.actions.memory_optimize.title"), detail);
        if (result is null)
        {
            return;
        }

        AddActivity(LT("shell.tools.test.toolbox.actions.memory_optimize.title"), detail);
    }

    private void CreateLauncherShortcut() => _ = CreateLauncherShortcutAsync();

    private async Task CreateLauncherShortcutAsync()
    {
        var shortcutTargets = BuildShortcutTargets(LauncherShortcutDisplayName);
        if (shortcutTargets.Count == 0)
        {
            AddFailureActivity(
                LT("shell.tools.test.toolbox.actions.create_shortcut.failure_title"),
                LT("shell.tools.test.toolbox.actions.create_shortcut.no_targets"));
            return;
        }

        var summary = LT("shell.tools.test.toolbox.actions.create_shortcut.summary_warning")
                      + Environment.NewLine
                      + Environment.NewLine
                      + string.Join(Environment.NewLine, shortcutTargets.Select(target =>
                          LT("shell.tools.test.toolbox.actions.create_shortcut.location", ("title", target.Title), ("path", target.ShortcutPath))));

        ToolboxShortcutTarget? selectedTarget;
        try
        {
            if (shortcutTargets.Count == 1)
            {
                var onlyTarget = shortcutTargets[0];
                var confirmed = await ShowToolboxConfirmationAsync(
                    LT("shell.tools.test.toolbox.actions.create_shortcut.title"),
                    summary,
                    LT("shell.tools.test.toolbox.actions.create_shortcut.confirm"));
                if (confirmed is null)
                {
                    return;
                }

                if (confirmed == false)
                {
                    AddActivity(
                        LT("shell.tools.test.toolbox.actions.create_shortcut.title"),
                        LT("shell.tools.test.toolbox.actions.create_shortcut.cancelled"));
                    return;
                }

                selectedTarget = onlyTarget;
            }
            else
            {
                var selectedId = await _shellActionService.PromptForChoiceAsync(
                    LT("shell.tools.test.toolbox.actions.create_shortcut.choose_title"),
                    summary,
                    shortcutTargets.Select(target => new PclChoiceDialogOption(
                        target.Id,
                        target.Title,
                        LT("shell.tools.test.toolbox.actions.create_shortcut.choice_info", ("path", target.ShortcutPath)))).ToArray(),
                    ShortcutDesktopOptionId,
                    LT("shell.tools.test.toolbox.actions.create_shortcut.confirm"));
                if (selectedId is null)
                {
                    AddActivity(
                        LT("shell.tools.test.toolbox.actions.create_shortcut.title"),
                        LT("shell.tools.test.toolbox.actions.create_shortcut.cancelled"));
                    return;
                }

                selectedTarget = shortcutTargets.FirstOrDefault(target => string.Equals(target.Id, selectedId, StringComparison.Ordinal));
                if (selectedTarget is null)
                {
                    AddFailureActivity(
                        LT("shell.tools.test.toolbox.actions.create_shortcut.failure_title"),
                        LT("shell.tools.test.toolbox.actions.create_shortcut.unknown_target", ("id", selectedId)));
                    return;
                }
            }

            var shortcutPath = _shellActionService.CreateLauncherShortcutAt(selectedTarget.Directory, LauncherShortcutDisplayName);
            AvaloniaHintBus.Show(
                LT("shell.tools.test.toolbox.actions.create_shortcut.created_hint", ("target", selectedTarget.Title)),
                AvaloniaHintTheme.Success);
            AddActivity(LT("shell.tools.test.toolbox.actions.create_shortcut.title"), shortcutPath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.toolbox.actions.create_shortcut.failure_title"), ex.Message);
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
            return "Unknown";
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

    private string ToolboxUnsupportedMessage => LT("shell.tools.test.toolbox.unsupported");

    private string GetDailyLuckRating(int luckValue)
    {
        return luckValue switch
        {
            100 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r100"),
            >= 95 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r95"),
            >= 90 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r90"),
            >= 60 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r60"),
            >= 40 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r40"),
            >= 30 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r30"),
            >= 10 => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r10"),
            _ => LT("shell.tools.test.toolbox.actions.daily_luck.ratings.r0")
        };
    }

    private string BuildMemoryOptimizePrompt(double totalMemoryGb)
    {
        return totalMemoryGb switch
        {
            >= 32 => LT("shell.tools.test.toolbox.actions.memory_optimize.prompts.r32"),
            >= 16 => LT("shell.tools.test.toolbox.actions.memory_optimize.prompts.r16"),
            >= 6 => LT("shell.tools.test.toolbox.actions.memory_optimize.prompts.r6"),
            >= 2 => LT("shell.tools.test.toolbox.actions.memory_optimize.prompts.r2"),
            > 0 => LT("shell.tools.test.toolbox.actions.memory_optimize.prompts.r0"),
            _ => string.Empty
        };
    }

    private async Task<bool?> ShowToolboxConfirmationAsync(
        string title,
        string message,
        string? confirmText = null,
        bool isDanger = false)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(
                title,
                message,
                confirmText ?? LT("shell.tools.common.confirm"),
                isDanger);
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                LT("shell.tools.common.failure_title", ("title", title)),
                ex.Message);
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
                LT("shell.tools.test.toolbox.actions.create_shortcut.targets.desktop"),
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
                    LT("shell.tools.test.toolbox.actions.create_shortcut.targets.start_menu"),
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
