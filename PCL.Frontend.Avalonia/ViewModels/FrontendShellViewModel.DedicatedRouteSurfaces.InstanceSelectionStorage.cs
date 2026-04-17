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

internal sealed partial class FrontendShellViewModel
{
    private async Task DeleteInstanceSelectionFolderAsync(InstanceSelectionFolderSnapshot folder)
    {
        if (!folder.IsPersisted)
        {
            AddActivity(
                LT("shell.instance_select.folder_remove.activity"),
                LT("shell.instance_select.folder_remove.not_saved"));
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            LT("shell.instance_select.folder_remove.confirm_title"),
            LT("shell.instance_select.folder_remove.confirm_message", ("path", folder.Directory), ("newline", Environment.NewLine)),
            LT("shell.instance_select.folder_remove.confirm_action"),
            isDanger: false);
        if (!confirmed)
        {
            AddActivity(
                LT("shell.instance_select.folder_remove.activity"),
                LT("shell.instance_select.folder_remove.cancelled"));
            return;
        }

        try
        {
            var runtimePaths = _shellActionService.RuntimePaths;
            var localConfig = runtimePaths.OpenLocalConfigProvider();
            var sharedConfig = runtimePaths.OpenSharedConfigProvider();
            var currentStoredPath = ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw);
            var currentDirectory = ResolveLauncherFolder(currentStoredPath, runtimePaths);
            var configuredFolders = LoadConfiguredInstanceSelectionFolders(sharedConfig, localConfig, runtimePaths)
                .Where(candidate => !string.Equals(candidate.Directory, folder.Directory, GetPathComparison()))
                .ToList();

            PersistInstanceSelectionFolders(configuredFolders, runtimePaths);

            if (!string.Equals(currentDirectory, folder.Directory, GetPathComparison()))
            {
                RefreshInstanceSelectionSurface();
                RefreshInstanceSelectionRouteMetadata();
                AddActivity(
                    LT("shell.instance_select.folder_remove.activity"),
                    LT("shell.instance_select.folder_remove.removed", ("path", folder.Directory)));
                return;
            }

            var fallbackDirectory = ResolveNextInstanceSelectionFolder(configuredFolders, runtimePaths);
            if (string.Equals(fallbackDirectory, folder.Directory, GetPathComparison()))
            {
                RefreshInstanceSelectionSurface();
                RefreshInstanceSelectionRouteMetadata();
                AddActivity(
                    LT("shell.instance_select.folder_remove.activity"),
                    LT("shell.instance_select.folder_remove.still_active", ("path", folder.Directory)));
                return;
            }

            RefreshSelectedLauncherFolderSmoothly(
                StoreLauncherFolderPath(fallbackDirectory, runtimePaths),
                fallbackDirectory,
                LT("shell.instance_select.folder_remove.removed_and_switched", ("removed", folder.Directory), ("target", fallbackDirectory)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.folder_remove.failure"), ex.Message);
        }
    }

    private static string ResolveLauncherFolder(string rawValue, FrontendRuntimePaths runtimePaths)
    {
        return FrontendLauncherPathService.ResolveLauncherFolder(rawValue, runtimePaths);
    }

    private static IReadOnlyList<InstanceSelectionFolderSnapshot> BuildInstanceSelectionFolderSnapshots(
        IKeyValueFileProvider sharedConfig,
        IKeyValueFileProvider localConfig,
        FrontendRuntimePaths runtimePaths,
        string selectedDirectory)
    {
        var folders = LoadConfiguredInstanceSelectionFolders(sharedConfig, localConfig, runtimePaths).ToList();
        var seenDirectories = folders
            .Select(folder => folder.Directory)
            .ToHashSet(GetPathComparer());

        if (seenDirectories.Add(selectedDirectory))
        {
            folders.Insert(0, new InstanceSelectionFolderSnapshot(
                GetInstanceSelectionDirectoryLabel(selectedDirectory),
                selectedDirectory,
                StoreLauncherFolderPath(selectedDirectory, runtimePaths),
                IsPersisted: false));
        }

        return folders;
    }

    private static IReadOnlyList<InstanceSelectionFolderSnapshot> LoadConfiguredInstanceSelectionFolders(
        IKeyValueFileProvider sharedConfig,
        IKeyValueFileProvider localConfig,
        FrontendRuntimePaths runtimePaths)
    {
        var rawFolders = ReadValue(sharedConfig, "LaunchFolders", string.Empty);
        if (string.IsNullOrWhiteSpace(rawFolders))
        {
            rawFolders = ReadValue(localConfig, "LaunchFolders", string.Empty);
        }

        var folders = new List<InstanceSelectionFolderSnapshot>();
        var seenDirectories = new HashSet<string>(GetPathComparer());
        foreach (var rawEntry in rawFolders.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var folder = ParseInstanceSelectionFolderSnapshot(rawEntry, runtimePaths);
            if (folder is null || !seenDirectories.Add(folder.Directory))
            {
                continue;
            }

            folders.Add(folder);
        }

        return folders;
    }

    private static InstanceSelectionFolderSnapshot? ParseInstanceSelectionFolderSnapshot(string rawEntry, FrontendRuntimePaths runtimePaths)
    {
        if (string.IsNullOrWhiteSpace(rawEntry))
        {
            return null;
        }

        var separatorIndex = rawEntry.IndexOf('>');
        var label = separatorIndex > 0
            ? rawEntry[..separatorIndex].Trim()
            : string.Empty;
        var rawPath = separatorIndex >= 0
            ? rawEntry[(separatorIndex + 1)..].Trim()
            : rawEntry.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var directory = ResolveLauncherFolder(rawPath, runtimePaths);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return new InstanceSelectionFolderSnapshot(
            string.IsNullOrWhiteSpace(label) ? GetInstanceSelectionDirectoryLabel(directory) : label,
            directory,
            rawPath,
            IsPersisted: true);
    }

    private static string SerializeInstanceSelectionFolders(
        IReadOnlyList<InstanceSelectionFolderSnapshot> folders,
        FrontendRuntimePaths runtimePaths)
    {
        return string.Join(
            "|",
            folders.Select(folder =>
            {
                var label = string.IsNullOrWhiteSpace(folder.Label)
                    ? GetInstanceSelectionDirectoryLabel(folder.Directory)
                    : folder.Label.Trim();
                var storedPath = StoreLauncherFolderPath(folder.Directory, runtimePaths);
                return $"{label}>{storedPath}";
            }));
    }

    private void PersistInstanceSelectionFolders(
        IReadOnlyList<InstanceSelectionFolderSnapshot> folders,
        FrontendRuntimePaths runtimePaths)
    {
        _shellActionService.PersistSharedValue("LaunchFolders", SerializeInstanceSelectionFolders(folders, runtimePaths));
        _shellActionService.RemoveLocalValues(["LaunchFolders"]);
    }

    private static string ResolveNextInstanceSelectionFolder(
        IReadOnlyList<InstanceSelectionFolderSnapshot> configuredFolders,
        FrontendRuntimePaths runtimePaths)
    {
        if (configuredFolders.Count > 0)
        {
            return configuredFolders[0].Directory;
        }

        return ResolveLauncherFolder(FrontendLauncherPathService.DefaultLauncherFolderRaw, runtimePaths);
    }

    private static string ResolvePickedLauncherFolderPath(string pickedFolderPath)
    {
        var fullPath = Path.GetFullPath(pickedFolderPath);
        if (string.Equals(Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "versions", GetPathComparison())
            && Directory.GetParent(fullPath) is { } parent)
        {
            return parent.FullName;
        }

        if (Directory.Exists(Path.Combine(fullPath, "versions")))
        {
            return fullPath;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(fullPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (Directory.Exists(Path.Combine(childDirectory, "versions")))
            {
                return Path.GetFullPath(childDirectory);
            }
        }

        return fullPath;
    }

    private static string StoreLauncherFolderPath(string directory, FrontendRuntimePaths runtimePaths)
    {
        var fullPath = Path.GetFullPath(directory);
        var executableDirectory = EnsureTrailingSeparator(Path.GetFullPath(runtimePaths.ExecutableDirectory));
        var comparison = GetPathComparison();
        if (fullPath.StartsWith(executableDirectory, comparison))
        {
            var relativePath = fullPath[executableDirectory.Length..]
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(relativePath)
                ? "$"
                : $"${Path.DirectorySeparatorChar}{relativePath}";
        }

        return fullPath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static StringComparer GetPathComparer()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        try
        {
            return provider.Exists(key)
                ? provider.Get<T>(key)
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static string GetInstanceSelectionDirectoryLabel(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        var trimmed = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? trimmed : name;
    }

}

