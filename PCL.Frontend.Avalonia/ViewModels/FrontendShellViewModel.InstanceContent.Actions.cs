using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private async Task PasteInstanceWorldClipboardAsync()
    {
        var activityTitle = SD("instance.content.world.actions.paste_clipboard");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.world.messages.no_instance_selected"));
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        var sourcePaths = ParseClipboardPaths(clipboardText)
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourcePaths.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.world.messages.no_importable_paths"));
            return;
        }

        var savesDirectory = Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves");
        Directory.CreateDirectory(savesDirectory);

        var importedTargets = new List<string>();
        foreach (var sourcePath in sourcePaths)
        {
            var targetPath = GetUniqueChildPath(savesDirectory, Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            if (Directory.Exists(sourcePath))
            {
                CopyDirectory(sourcePath, targetPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(sourcePath, targetPath, overwrite: false);
            }

            importedTargets.Add(targetPath);
        }

        ReloadInstanceComposition();
        AddActivity(activityTitle, string.Join(Environment.NewLine, importedTargets));
    }

    private async Task InstallInstanceResourceFromFileAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.install_from_file");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var (typeName, patterns) = ResolveInstanceResourcePickerOptions();

        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                SD("instance.content.resource.dialogs.install_from_file.title", ("surface_title", InstanceResourceSurfaceTitle)),
                typeName,
                patterns);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.install_from_file_canceled"));
            return;
        }

        var targetDirectory = GetCurrentInstanceResourceDirectory();
        Directory.CreateDirectory(targetDirectory);
        var targetPath = GetUniqueChildPath(targetDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, targetPath, overwrite: false);

        ReloadInstanceComposition();
        AddActivity(activityTitle, $"{sourcePath} -> {targetPath}");
    }

    private void DownloadInstanceResource()
    {
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Download, ResolveInstanceDownloadSubpage()),
            SD("instance.content.resource.messages.open_download_route", ("surface_title", InstanceResourceSurfaceTitle)));
    }

    private void RefreshInstanceResources()
    {
        var activityTitle = SD("common.actions.refresh");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity(activityTitle, InstanceResourceSurfaceTitle);
        QueueSelectedInstanceStateRefresh(refreshVersion);
    }

    private async Task SetSelectedInstanceResourcesEnabledAsync(bool isEnabled)
    {
        var activityTitle = SD(isEnabled ? "instance.content.resource.actions.enable" : "instance.content.resource.actions.disable");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        if (!IsInstanceResourceToggleSupported())
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.toggle_not_supported", ("surface_title", InstanceResourceSurfaceTitle)));
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabledState))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_selected"));
            return;
        }

        await SetInstanceResourceEntriesEnabledAsync(selectedEntries, isEnabled, SD("instance.content.resource.messages.no_selected"));
    }

    private Task SetInstanceResourceEntriesEnabledAsync(
        IReadOnlyList<(string Title, string Path, bool IsEnabledState)> entries,
        bool isEnabled,
        string emptyMessage)
    {
        var activityTitle = SD(isEnabled ? "instance.content.resource.actions.enable" : "instance.content.resource.actions.disable");
        if (entries.Count == 0)
        {
            AddActivity(activityTitle, emptyMessage);
            return Task.CompletedTask;
        }

        var candidates = entries
            .Where(entry => entry.IsEnabledState != isEnabled)
            .ToArray();
        if (candidates.Length == 0)
        {
            AddActivity(activityTitle, SD(isEnabled ? "instance.content.resource.messages.already_enabled" : "instance.content.resource.messages.already_disabled"));
            return Task.CompletedTask;
        }

        var succeededEntries = new List<string>();
        var movedEntries = new List<(string OldPath, string NewPath, bool IsEnabled)>();
        var failedEntries = new List<string>();

        foreach (var entry in candidates)
        {
            try
            {
                var newPath = SetInstanceResourceEnabled(entry.Path, isEnabled);
                succeededEntries.Add(entry.Title);
                movedEntries.Add((entry.Path, newPath, isEnabled));
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ApplyInstanceResourceToggleResults(movedEntries);
        if (succeededEntries.Count > 0)
        {
            AddActivity(
                activityTitle,
                failedEntries.Count == 0
                    ? SD(
                        isEnabled ? "instance.content.resource.messages.enabled_completed" : "instance.content.resource.messages.disabled_completed",
                        ("count", succeededEntries.Count),
                        ("titles", string.Join(SD("common.punctuation.comma"), succeededEntries)))
                    : SD(
                        isEnabled ? "instance.content.resource.messages.enabled_partial" : "instance.content.resource.messages.disabled_partial",
                        ("count", succeededEntries.Count),
                        ("failed_count", failedEntries.Count)));
        }

        if (failedEntries.Count > 0)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), string.Join(Environment.NewLine, failedEntries));
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedInstanceResourcesAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.delete");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var selectedEntries = GetSelectedInstanceResourceEntries()
            .Select(entry => (Title: entry.Title, Path: entry.Path))
            .ToArray();
        if (selectedEntries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_selected"));
            return;
        }

        await DeleteInstanceResourcesAsync(selectedEntries, SD("instance.content.resource.messages.no_selected"));
    }

    private async Task DeleteInstanceResourcesAsync(
        IReadOnlyList<(string Title, string Path)> entries,
        string emptyMessage)
    {
        var activityTitle = SD("instance.content.resource.actions.delete");
        if (entries.Count == 0)
        {
            AddActivity(activityTitle, emptyMessage);
            return;
        }

        var itemDescription = string.Join(Environment.NewLine, entries.Take(8).Select(entry => $"- {entry.Title}"));
        if (entries.Count > 8)
        {
            itemDescription = $"{itemDescription}{Environment.NewLine}{SD("instance.content.resource.dialogs.delete.extra_items", ("count", entries.Count - 8))}";
        }

        var confirmed = await ShowToolboxConfirmationAsync(
            SD("instance.content.resource.dialogs.delete.title"),
            $"{SD("instance.content.resource.dialogs.delete.message", ("count", entries.Count), ("surface_title", InstanceResourceSurfaceTitle))}{Environment.NewLine}{Environment.NewLine}{itemDescription}",
            SD("instance.content.resource.dialogs.delete.confirm"),
            isDanger: true);
        if (confirmed != true)
        {
            if (confirmed == false)
            {
                AddActivity(activityTitle, SD("instance.content.resource.messages.delete_canceled"));
            }

            return;
        }

        var trashDirectory = ResolveInstanceResourceTrashDirectory();
        Directory.CreateDirectory(trashDirectory);

        var succeededEntries = new List<string>();
        var deletedPaths = new List<string>();
        var failedEntries = new List<string>();
        foreach (var entry in entries)
        {
            try
            {
                MoveInstanceResourceToTrash(entry.Path, trashDirectory);
                succeededEntries.Add(entry.Title);
                deletedPaths.Add(entry.Path);
            }
            catch (Exception ex)
            {
                failedEntries.Add($"{entry.Title}: {ex.Message}");
            }
        }

        ApplyInstanceResourceDeleteResults(deletedPaths);
        if (succeededEntries.Count > 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.deleted_completed", ("count", succeededEntries.Count)));
        }

        if (failedEntries.Count > 0)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), string.Join(Environment.NewLine, failedEntries));
        }
    }

    private void ExportInstanceResourceInfo()
    {
        var activityTitle = SD("instance.content.resource.actions.export_info");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var entries = GetCurrentInstanceResourceState().Entries;
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "instance-resources");
        Directory.CreateDirectory(exportDirectory);
        var outputPath = Path.Combine(
            exportDirectory,
            $"{_instanceComposition.Selection.InstanceName}-{ResolveInstanceResourceExportSlug()}-info.txt");
        var lines = entries.Count == 0
            ? [SD("instance.content.resource.messages.list_empty", ("surface_title", InstanceResourceSurfaceTitle))]
            : entries.Select(entry => $"{entry.Title} | {entry.Meta} | {entry.Summary} | {entry.Path}").ToArray();
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));
        OpenInstanceTarget(activityTitle, outputPath, SD("instance.content.resource.messages.export_missing"));
    }

    private string SetInstanceResourceEnabled(string path, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The resource path is empty.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The resource file does not exist.", path);
        }

        if (isEnabled)
        {
            var enabledFileName = Path.GetFileName(path);
            if (enabledFileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".disabled".Length];
            }
            else if (enabledFileName.EndsWith(".old", StringComparison.OrdinalIgnoreCase))
            {
                enabledFileName = enabledFileName[..^".old".Length];
            }

            var enabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, enabledFileName);
            File.Move(path, enabledPath);
            return enabledPath;
        }

        var disabledFileName = $"{Path.GetFileName(path)}.disabled";
        var disabledPath = GetUniqueChildPath(Path.GetDirectoryName(path)!, disabledFileName);
        File.Move(path, disabledPath);
        return disabledPath;
    }

    private void ApplyInstanceResourceToggleResults(IReadOnlyList<(string OldPath, string NewPath, bool IsEnabled)> movedEntries)
    {
        if (movedEntries.Count == 0)
        {
            return;
        }

        var enabledEntries = _instanceComposition.Mods.Entries.ToList();
        var disabledEntries = _instanceComposition.DisabledMods.Entries.ToList();

        foreach (var move in movedEntries)
        {
            var sourceEntries = move.IsEnabled ? disabledEntries : enabledEntries;
            var targetEntries = move.IsEnabled ? enabledEntries : disabledEntries;
            var entry = RemoveInstanceResourceEntry(sourceEntries, move.OldPath)
                        ?? RemoveInstanceResourceEntry(targetEntries, move.OldPath);
            if (entry is null)
            {
                continue;
            }

            targetEntries.Add(entry with
            {
                Path = move.NewPath,
                IsEnabled = move.IsEnabled
            });

            if (_instanceResourceSelectedPaths.Remove(move.OldPath))
            {
                _instanceResourceSelectedPaths.Add(move.NewPath);
            }
        }

        _instanceComposition = _instanceComposition with
        {
            Mods = new FrontendInstanceResourceState(SortInstanceResourceEntriesByFileName(enabledEntries)),
            DisabledMods = new FrontendInstanceResourceState(SortInstanceResourceEntriesByFileName(disabledEntries))
        };

        RefreshInstanceResourceEntries();
    }

    private static FrontendInstanceResourceEntry? RemoveInstanceResourceEntry(
        IList<FrontendInstanceResourceEntry> entries,
        string path)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entry = entries[i];
            entries.RemoveAt(i);
            return entry;
        }

        return null;
    }

    private static FrontendInstanceResourceEntry[] SortInstanceResourceEntriesByFileName(
        IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        return entries
            .OrderBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ApplyInstanceResourceDeleteResults(IReadOnlyCollection<string> deletedPaths)
    {
        if (deletedPaths.Count == 0)
        {
            return;
        }

        var deletedPathSet = new HashSet<string>(deletedPaths, StringComparer.OrdinalIgnoreCase);
        _instanceResourceSelectedPaths.ExceptWith(deletedPathSet);

        _instanceComposition = _instanceComposition with
        {
            Mods = RemoveInstanceResourceEntries(_instanceComposition.Mods, deletedPathSet),
            DisabledMods = RemoveInstanceResourceEntries(_instanceComposition.DisabledMods, deletedPathSet),
            ResourcePacks = RemoveInstanceResourceEntries(_instanceComposition.ResourcePacks, deletedPathSet),
            Shaders = RemoveInstanceResourceEntries(_instanceComposition.Shaders, deletedPathSet),
            Schematics = RemoveInstanceResourceEntries(_instanceComposition.Schematics, deletedPathSet)
        };

        RefreshInstanceResourceEntries();
    }

    private static FrontendInstanceResourceState RemoveInstanceResourceEntries(
        FrontendInstanceResourceState state,
        ISet<string> deletedPaths)
    {
        return new FrontendInstanceResourceState(
            state.Entries
                .Where(entry => !deletedPaths.Contains(entry.Path))
                .ToArray());
    }

    private void CheckInstanceMods() => _ = CheckInstanceModsAsync();

    private async Task CheckInstanceModsAsync()
    {
        var activityTitle = SD("instance.content.resource.actions.check_mods");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.no_instance_selected"));
            return;
        }

        var enabledMods = _instanceComposition.Mods.Entries;
        var disabledMods = _instanceComposition.DisabledMods.Entries;
        var duplicateGroups = enabledMods
            .Concat(disabledMods)
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lines = new List<string>
        {
            $"{SD("instance.content.resource.check_mods.instance")}: {_instanceComposition.Selection.InstanceName}",
            $"{SD("instance.content.resource.check_mods.enabled_mods")}: {enabledMods.Count}",
            $"{SD("instance.content.resource.check_mods.disabled_mods")}: {disabledMods.Count}",
            $"{SD("instance.content.resource.check_mods.duplicate_names")}: {duplicateGroups.Length}",
            string.Empty
        };

        if (duplicateGroups.Length == 0)
        {
            lines.Add(SD("instance.content.resource.check_mods.no_duplicates"));
        }
        else
        {
            lines.Add(SD("instance.content.resource.check_mods.duplicate_group_title"));
            foreach (var group in duplicateGroups)
            {
                lines.Add($"- {group.Key}");
                foreach (var entry in group)
                {
                    lines.Add($"  {entry.Meta} | {entry.Summary} | {entry.Path}");
                }
            }
        }

        var result = await ShowToolboxConfirmationAsync(activityTitle, string.Join(Environment.NewLine, lines));
        if (result is null)
        {
            return;
        }

        AddActivity(
            activityTitle,
            SD(
                "instance.content.resource.messages.checked_mods_summary",
                ("instance_name", _instanceComposition.Selection.InstanceName),
                ("enabled_count", enabledMods.Count),
                ("disabled_count", disabledMods.Count),
                ("duplicate_count", duplicateGroups.Length)));
    }

    private FrontendInstanceResourceState GetCurrentInstanceResourceState()
    {
        return new FrontendInstanceResourceState(GetCurrentInstanceResourceSourceEntries());
    }

    private string ResolveInstanceResourceSurfaceTitle()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "Mod",
            LauncherFrontendSubpageKey.VersionModDisabled => "Mod",
            LauncherFrontendSubpageKey.VersionResourcePack => SD("instance.content.resource.kind.resource_pack"),
            LauncherFrontendSubpageKey.VersionShader => SD("instance.content.resource.kind.shader"),
            LauncherFrontendSubpageKey.VersionSchematic => SD("instance.content.resource.kind.schematic_file"),
            _ => SD("instance.content.resource.kind.resource")
        };
    }

    private string GetCurrentInstanceResourceDirectory()
    {
        var folderName = _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "mods"
        };

        return ResolveCurrentInstanceResourceDirectory(folderName);
    }

    private (string TypeName, string[] Patterns) ResolveInstanceResourcePickerOptions()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => (SD("instance.content.resource.dialogs.install_from_file.file_types.resource_pack"), ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionShader => (SD("instance.content.resource.dialogs.install_from_file.file_types.shader"), ["*.zip", "*.rar"]),
            LauncherFrontendSubpageKey.VersionSchematic => (SD("instance.content.resource.dialogs.install_from_file.file_types.schematic"), ["*.litematic", "*.schem", "*.schematic", "*.nbt"]),
            _ => (SD("instance.content.resource.dialogs.install_from_file.file_types.mod"), ["*.jar", "*.disabled", "*.old"])
        };
    }

    private LauncherFrontendSubpageKey ResolveInstanceDownloadSubpage()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionResourcePack => LauncherFrontendSubpageKey.DownloadResourcePack,
            LauncherFrontendSubpageKey.VersionShader => LauncherFrontendSubpageKey.DownloadShader,
            LauncherFrontendSubpageKey.VersionSchematic => LauncherFrontendSubpageKey.DownloadMod,
            _ => LauncherFrontendSubpageKey.DownloadMod
        };
    }

    private string ResolveInstanceResourceExportSlug()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => "mods",
            LauncherFrontendSubpageKey.VersionModDisabled => "mods",
            LauncherFrontendSubpageKey.VersionResourcePack => "resourcepacks",
            LauncherFrontendSubpageKey.VersionShader => "shaderpacks",
            LauncherFrontendSubpageKey.VersionSchematic => "schematics",
            _ => "resources"
        };
    }

    private string ResolveInstanceResourceTrashDirectory()
    {
        return Path.Combine(
            _instanceComposition.Selection.LauncherDirectory,
            ".pcl-trash",
            "resources",
            ResolveInstanceResourceExportSlug());
    }

    private static void MoveInstanceResourceToTrash(string sourcePath, string trashDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException("The resource path is empty.");
        }

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("The resource entry does not exist.", sourcePath);
        }

        Directory.CreateDirectory(trashDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetPath = GetUniqueChildPath(
            trashDirectory,
            $"{timestamp}-{Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}");

        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
            return;
        }

        File.Move(sourcePath, targetPath);
    }

    private static IEnumerable<string> ParseClipboardPaths(string? clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return [];
        }

        return clipboardText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().Trim('"'))
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string GetUniqueChildPath(string directory, string fileOrFolderName)
    {
        var candidate = Path.Combine(directory, fileOrFolderName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileOrFolderName);
        var extension = Path.GetExtension(fileOrFolderName);
        var suffix = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{baseName}-{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(file));
            File.Copy(file, targetPath, overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
        }
    }

    private static bool MatchesSearch(params string[] values)
    {
        if (values.Length == 0)
        {
            return true;
        }

        var query = values[^1];
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return values
            .Take(values.Length - 1)
            .Any(value => !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
