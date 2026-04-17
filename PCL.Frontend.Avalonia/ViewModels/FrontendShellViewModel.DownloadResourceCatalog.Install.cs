using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InstallDownloadResourceModPack()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource) || _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack)
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.no_surface"));
            return;
        }

        var visibleEntry = DownloadResourceEntries.FirstOrDefault();
        if (visibleEntry is null)
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.no_filtered_result"));
            return;
        }

        if (!_downloadResourceRuntimeStates.TryGetValue(_currentRoute.Subpage, out var resourceState))
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.runtime_state_missing"));
            return;
        }

        var sourceEntry = resourceState.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Title, visibleEntry.Title, StringComparison.Ordinal)
            && string.Equals(entry.Source, visibleEntry.Source, StringComparison.Ordinal));
        if (sourceEntry is null || string.IsNullOrWhiteSpace(sourceEntry.TargetPath) || !Directory.Exists(sourceEntry.TargetPath))
        {
            AddActivity(T("download.resource.activities.install_pack"), T("download.resource.install_pack.target_missing"));
            return;
        }

        try
        {
            var launcherFolder = ResolveDownloadLauncherFolder();
            var versionsDirectory = Path.Combine(launcherFolder, "versions");
            Directory.CreateDirectory(versionsDirectory);

            var targetName = string.IsNullOrWhiteSpace(DownloadInstallName)
                ? visibleEntry.Title
                : DownloadInstallName.Trim();
            var targetDirectory = GetUniqueInstallDirectoryPath(Path.Combine(
                versionsDirectory,
                SanitizeInstallDirectoryName(targetName)));

            CopyDirectory(sourceEntry.TargetPath, targetDirectory);

            var summaryDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "download-installs");
            Directory.CreateDirectory(summaryDirectory);
            var summaryPath = Path.Combine(summaryDirectory, $"{Path.GetFileName(targetDirectory)}.txt");
            File.WriteAllText(
                summaryPath,
                string.Join(Environment.NewLine,
                [
                    $"Time: {DateTime.Now:yyyy/MM/dd HH:mm:ss}",
                    $"Source modpack: {visibleEntry.Title}",
                    $"Source directory: {sourceEntry.TargetPath}",
                    $"Target directory: {targetDirectory}",
                    $"Current install name: {DownloadInstallName}"
                ]),
                new UTF8Encoding(false));

            ReloadDownloadComposition();
            InitializeDownloadInstallSurface();
            RefreshDownloadResourceSurface();
            RaisePropertyChanged(nameof(DownloadInstallName));
            OpenInstanceTarget(T("download.resource.activities.install_pack"), targetDirectory, T("download.resource.install_pack.open_target_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.resource.activities.install_pack_failed"), ex.Message);
        }
    }
    private string ResolveDownloadLauncherFolder()
    {
        var provider = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
        var rawValue = FrontendLauncherPathService.DefaultLauncherFolderRaw;

        if (provider.Exists("LaunchFolderSelect"))
        {
            try
            {
                rawValue = provider.Get<string>("LaunchFolderSelect");
            }
            catch
            {
                rawValue = FrontendLauncherPathService.DefaultLauncherFolderRaw;
            }
        }

        return FrontendLauncherPathService.ResolveLauncherFolder(rawValue, _shellActionService.RuntimePaths);
    }

    private static string SanitizeInstallDirectoryName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "ImportedModPack" : cleaned;
    }

    private static string GetUniqueInstallDirectoryPath(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return directoryPath;
        }

        for (var suffix = 1; ; suffix++)
        {
            var candidate = $"{directoryPath}-{suffix}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
