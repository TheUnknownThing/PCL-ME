using System.IO.Compression;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void ExportLauncherLogs(bool includeAllLogs)
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity(LT("shell.game_log.actions.export"), LT("shell.game_log.export.missing_directory"));
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length == 0)
        {
            AddActivity(LT("shell.game_log.actions.export"), LT("shell.game_log.export.empty_directory"));
            return;
        }

        var selectedFiles = includeAllLogs ? logFiles : logFiles.Take(1).ToArray();
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "log-exports");
        Directory.CreateDirectory(exportDirectory);

        var archiveName = includeAllLogs ? "launcher-logs-all.zip" : "launcher-log-latest.zip";
        var archivePath = GetUniqueArchivePath(Path.Combine(exportDirectory, archiveName));

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in selectedFiles)
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        AddActivity(
            includeAllLogs ? LT("shell.game_log.export.export_all_activity") : LT("shell.game_log.actions.export"),
            archivePath);
    }

    private void OpenLauncherLogDirectory()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        Directory.CreateDirectory(logDirectory);
        if (_shellActionService.TryOpenExternalTarget(logDirectory, out var error))
        {
            AddActivity(LT("shell.game_log.actions.open_directory"), logDirectory);
        }
        else
        {
            AddFailureActivity(LT("shell.game_log.open_directory_failure"), error ?? logDirectory);
        }
    }

    private void CleanLauncherLogs()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity(
                LT("setup.log.activities.clean_history"),
                LT("setup.log.activities.directory_missing"));
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length <= 1)
        {
            AddActivity(
                LT("setup.log.activities.clean_history"),
                LT("setup.log.activities.nothing_to_clean"));
            return;
        }

        var removedCount = 0;
        foreach (var file in logFiles.Skip(1))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore individual failures and continue clearing other archived logs.
            }
        }

        ReloadSetupComposition();
        AddActivity(
            LT("setup.log.activities.clean_history"),
            removedCount == 0
                ? LT("setup.log.activities.clean_failed")
                : LT("setup.log.activities.cleaned_count", ("count", removedCount)));
    }

    private void ExportSettingsSnapshot()
    {
        try
        {
            var exportDirectory = FrontendSettingsSnapshotWorkflowService.CreateSnapshot(_shellActionService.RuntimePaths);
            AddActivity(LT("setup.launcher_misc.activities.export_settings"), exportDirectory);
            if (!_shellActionService.TryOpenExternalTarget(exportDirectory, out var error))
            {
                AddFailureActivity(LT("setup.launcher_misc.activities.export_settings"), error ?? exportDirectory);
            }
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.launcher_misc.activities.export_settings"), ex.Message);
        }
    }

    private async Task ImportSettingsAsync()
    {
        string? sourceDirectory;
        var settingsDirectory = GetSettingsSnapshotRootDirectory();

        try
        {
            sourceDirectory = await _shellActionService.PickFolderAsync(
                LT("setup.launcher_misc.activities.import_settings_pick_title"),
                settingsDirectory);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.launcher_misc.activities.import_settings_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            AddActivity(
                LT("setup.launcher_misc.activities.import_settings"),
                LT("setup.launcher_misc.activities.import_settings_cancelled"));
            return;
        }

        try
        {
            FrontendSettingsSnapshotWorkflowService.RestoreSnapshot(_shellActionService.RuntimePaths, sourceDirectory);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.launcher_misc.activities.import_settings_failed"), ex.Message);
            return;
        }

        _ = _i18n.ReloadLocaleFromSettings();
        ReloadSetupComposition();

        AddActivity(
            LT("setup.launcher_misc.activities.import_settings"),
            LT("setup.launcher_misc.activities.import_settings_completed", ("path", sourceDirectory)));
    }

    private void OpenSettingsSnapshotFolder()
    {
        var settingsDirectory = GetSettingsSnapshotRootDirectory();
        Directory.CreateDirectory(settingsDirectory);
        if (_shellActionService.TryOpenExternalTarget(settingsDirectory, out var error))
        {
            AddActivity(LT("setup.launcher_misc.activities.open_settings_folder"), settingsDirectory);
        }
        else
        {
            AddFailureActivity(LT("setup.launcher_misc.activities.open_settings_folder_failed"), error ?? settingsDirectory);
        }
    }

    private string GetSettingsSnapshotRootDirectory()
    {
        return FrontendSettingsSnapshotWorkflowService.GetSnapshotRootDirectory(_shellActionService.RuntimePaths);
    }

    private static string GetUniqueArchivePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
