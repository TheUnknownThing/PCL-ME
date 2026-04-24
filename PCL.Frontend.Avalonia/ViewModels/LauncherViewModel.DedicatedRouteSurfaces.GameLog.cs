using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void RefreshGameLogSurface()
    {
        var runtimePaths = _launcherActionService.RuntimePaths;
        var preferredLauncherLogPath = runtimePaths.ResolveCurrentLauncherLogFilePath();
        var candidateFiles = FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                runtimePaths.LauncherAppDataDirectory,
                _launcherActionService.PlatformAdapter)
            .Concat(
                FrontendLauncherPathService.EnumerateLatestLaunchScriptPaths(
                    runtimePaths.DataDirectory,
                    _launcherActionService.PlatformAdapter))
            .Concat(
            [
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "latest.log"),
                preferredLauncherLogPath ?? Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log", "PCL.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "RawOutput.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "latest.log"),
                Path.Combine(runtimePaths.DataDirectory, "Log", "PCL.log")
            ]);
        var recentFiles = candidateFiles
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.LauncherAppDataDirectory, "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.DataDirectory, "Log")))
            .Concat(EnumerateLogDirectoryFiles(Path.Combine(runtimePaths.FrontendArtifactDirectory, "crash-logs")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(10)
            .ToArray();

        _gameLogRecentFileCount = recentFiles.Length;
        _gameLogLatestUpdateLabel = recentFiles.FirstOrDefault() is { } latest
            ? latest.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            : LT("shell.game_log.latest_update.none");

        ReplaceItems(
            GameLogFileEntries,
            recentFiles.Select(file =>
                new SimpleListEntryViewModel(
                    file.Name,
                    $"{file.DirectoryName} • {file.LastWriteTime:yyyy-MM-dd HH:mm}",
                    CreateOpenTargetCommand(
                        LT("shell.game_log.actions.open_file", ("name", file.Name)),
                        file.FullName,
                        file.FullName))));

        RaiseGameLogSurfaceProperties();
        RaisePropertyChanged(nameof(HasGameLogFiles));
        RaisePropertyChanged(nameof(HasNoGameLogFiles));
        RefreshDynamicUtilityEntries();
    }

    private void ClearGameLogSurface()
    {
        ClearLaunchLogBuffer();
        RaiseGameLogSurfaceProperties();
        AddActivity(LT("shell.game_log.actions.clear_cache"), LT("shell.game_log.clear_cache.completed"));
    }

    private void RaiseGameLogSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowGameLogLiveOutput));
        RaisePropertyChanged(nameof(ShowGameLogEmptyState));
        RaisePropertyChanged(nameof(GameLogRefreshButtonText));
        RaisePropertyChanged(nameof(GameLogExportButtonText));
        RaisePropertyChanged(nameof(GameLogOpenDirectoryButtonText));
        RaisePropertyChanged(nameof(GameLogClearCacheButtonText));
        RaisePropertyChanged(nameof(GameLogLiveLineCountLabel));
        RaisePropertyChanged(nameof(GameLogRecentFilesLabel));
        RaisePropertyChanged(nameof(GameLogCurrentSessionOutputTitle));
        RaisePropertyChanged(nameof(GameLogRecentGeneratedFilesTitle));
        RaisePropertyChanged(nameof(GameLogEmptyTitle));
        RaisePropertyChanged(nameof(GameLogEmptyDescription));
        RaisePropertyChanged(nameof(GameLogLiveLineCount));
        RaisePropertyChanged(nameof(GameLogRecentFileCount));
        RaisePropertyChanged(nameof(GameLogLatestUpdateLabel));
    }

    private static IEnumerable<string> EnumerateLogDirectoryFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".command", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase));
    }

}

