using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
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
    private void RefreshInstanceResourceEntries()
    {
        InstanceResourceSurfaceTitle = ResolveInstanceResourceSurfaceTitle();
        RaisePropertyChanged(nameof(InstanceResourceLoadingText));
        var sourceEntries = GetCurrentInstanceResourceSourceEntries();
        if (!ShouldRefreshInstanceResourceEntries(sourceEntries))
        {
            return;
        }

        var searchedEntries = sourceEntries
            .Where(entry => MatchesSearch(
                entry.Title,
                entry.Description,
                entry.Summary,
                entry.Meta,
                entry.Authors,
                entry.Version,
                entry.Loader,
                entry.Website,
                InstanceResourceSearchQuery))
            .ToArray();
        var duplicateTitles = searchedEntries
            .GroupBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _instanceResourceIsSearching = !string.IsNullOrWhiteSpace(InstanceResourceSearchQuery);
        _instanceResourceTotalCount = searchedEntries.Length;
        _instanceResourceEnabledCount = searchedEntries.Count(entry => entry.IsEnabled);
        _instanceResourceDisabledCount = searchedEntries.Count(entry => !entry.IsEnabled);
        _instanceResourceDuplicateCount = searchedEntries.Count(entry => duplicateTitles.Contains(entry.Title));

        var visibleEntries = ApplyInstanceResourceSort(ApplyInstanceResourceFilter(searchedEntries, duplicateTitles))
            .ToArray();
        _instanceResourceSelectedPaths.IntersectWith(visibleEntries.Select(entry => entry.Path));

        PopulateInstanceResourceEntries(visibleEntries);
        CaptureInstanceResourceRefreshSnapshot(sourceEntries);

        RaisePropertyChanged(nameof(HasInstanceResourceEntries));
        RaisePropertyChanged(nameof(HasNoInstanceResourceEntries));
        RaisePropertyChanged(nameof(ShowInstanceResourceUnsupportedState));
        RaisePropertyChanged(nameof(ShowInstanceResourceContent));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyState));
        RaisePropertyChanged(nameof(ShowInstanceResourceFilterBar));
        RaisePropertyChanged(nameof(InstanceResourceFilterAllText));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterAllSelected));
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RaisePropertyChanged(nameof(InstanceResourceFilterEnabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceEnabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterEnabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDisabledText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDisabledFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDisabledSelected));
        RaisePropertyChanged(nameof(InstanceResourceFilterDuplicateText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDuplicateFilter));
        RaisePropertyChanged(nameof(IsInstanceResourceFilterDuplicateSelected));
        RaisePropertyChanged(nameof(ShowInstanceResourceEmptyInstallActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceInstanceSelectAction));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
        RaisePropertyChanged(nameof(InstanceResourceEmptyTitle));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDescription));
        RaisePropertyChanged(nameof(InstanceResourceEmptyDownloadButtonText));
        RaiseInstanceResourceSelectionProperties();
    }

    private void PopulateInstanceResourceEntries(IReadOnlyList<FrontendInstanceResourceEntry> visibleEntries)
    {
        var state = GetCurrentInstanceResourceSurfaceState();
        ReplaceItems(
            state.Entries,
            visibleEntries.Select(CreateInstanceResourceEntry));
    }

    private bool ShouldRefreshInstanceResourceEntries(IReadOnlyList<FrontendInstanceResourceEntry> sourceEntries)
    {
        var state = GetCurrentInstanceResourceSurfaceState();
        if (!state.HasRefreshSnapshot)
        {
            return true;
        }

        return !ReferenceEquals(state.RefreshSourceEntries, sourceEntries)
               || !string.Equals(state.RefreshSearchQuery, InstanceResourceSearchQuery, StringComparison.Ordinal)
               || state.RefreshFilter != _instanceResourceFilter
               || state.RefreshSortMethod != _instanceResourceSortMethod;
    }

    private void CaptureInstanceResourceRefreshSnapshot(IReadOnlyList<FrontendInstanceResourceEntry> sourceEntries)
    {
        var state = GetCurrentInstanceResourceSurfaceState();
        state.HasRefreshSnapshot = true;
        state.RefreshSourceEntries = sourceEntries;
        state.RefreshSearchQuery = InstanceResourceSearchQuery;
        state.RefreshFilter = _instanceResourceFilter;
        state.RefreshSortMethod = _instanceResourceSortMethod;
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceFilter(
        IReadOnlyList<FrontendInstanceResourceEntry> entries,
        ISet<string> duplicateTitles)
    {
        if (!ShowInstanceResourceFilterBar)
        {
            return entries;
        }

        return _instanceResourceFilter switch
        {
            InstanceResourceFilter.Enabled => entries.Where(entry => entry.IsEnabled),
            InstanceResourceFilter.Disabled => entries.Where(entry => !entry.IsEnabled),
            InstanceResourceFilter.Duplicate => entries.Where(entry => duplicateTitles.Contains(entry.Title)),
            _ => entries
        };
    }

    private IEnumerable<FrontendInstanceResourceEntry> ApplyInstanceResourceSort(IEnumerable<FrontendInstanceResourceEntry> entries)
    {
        return _instanceResourceSortMethod switch
        {
            InstanceResourceSortMethod.FileName => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.CreateTime => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => GetPathCreationTimeUtc(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            InstanceResourceSortMethod.FileSize => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenByDescending(entry => IsDirectoryPath(entry.Path) ? 0L : GetFileSize(entry.Path))
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase),
            _ => entries
                .OrderBy(entry => IsDirectoryPath(entry.Path) ? 0 : 1)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        };
    }

    private IReadOnlyList<FrontendInstanceResourceEntry> GetCurrentInstanceResourceSourceEntries()
    {
        return _currentRoute.Subpage switch
        {
            LauncherFrontendSubpageKey.VersionMod => GetCachedModSourceEntries(),
            LauncherFrontendSubpageKey.VersionModDisabled => _instanceComposition.DisabledMods.Entries,
            LauncherFrontendSubpageKey.VersionResourcePack => _instanceComposition.ResourcePacks.Entries,
            LauncherFrontendSubpageKey.VersionShader => _instanceComposition.Shaders.Entries,
            LauncherFrontendSubpageKey.VersionSchematic => _instanceComposition.Schematics.Entries,
            _ => _instanceComposition.Mods.Entries
        };
    }

    private IReadOnlyList<FrontendInstanceResourceEntry> GetCachedModSourceEntries()
    {
        var state = GetCurrentInstanceResourceSurfaceState();
        if (state.CachedModSourceEntries is not null
            && ReferenceEquals(state.CachedEnabledModEntries, _instanceComposition.Mods.Entries)
            && ReferenceEquals(state.CachedDisabledModEntries, _instanceComposition.DisabledMods.Entries))
        {
            return state.CachedModSourceEntries;
        }

        state.CachedEnabledModEntries = _instanceComposition.Mods.Entries;
        state.CachedDisabledModEntries = _instanceComposition.DisabledMods.Entries;
        state.CachedModSourceEntries = _instanceComposition.Mods.Entries
            .Concat(_instanceComposition.DisabledMods.Entries)
            .ToArray();
        return state.CachedModSourceEntries;
    }

    private void SetInstanceResourceFilter(InstanceResourceFilter filter)
    {
        if (_instanceResourceFilter == filter)
        {
            return;
        }

        _instanceResourceFilter = filter;
        RefreshInstanceResourceEntries();
    }

    internal void SetInstanceResourceFileNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileName);

    internal void SetInstanceResourceNameSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.ResourceName);

    internal void SetInstanceResourceCreateTimeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.CreateTime);

    internal void SetInstanceResourceFileSizeSort() => SetInstanceResourceSortMethod(InstanceResourceSortMethod.FileSize);

    private void SetInstanceResourceSortMethod(InstanceResourceSortMethod target)
    {
        if (_instanceResourceSortMethod == target)
        {
            return;
        }

        _instanceResourceSortMethod = target;
        RaisePropertyChanged(nameof(InstanceResourceSortText));
        RefreshInstanceResourceEntries();
    }

    private void RaiseInstanceResourceSelectionProperties()
    {
        RaisePropertyChanged(nameof(InstanceResourceSelectedCount));
        RaisePropertyChanged(nameof(HasSelectedInstanceResources));
        RaisePropertyChanged(nameof(InstanceResourceSelectionText));
        RaisePropertyChanged(nameof(ShowInstanceResourceDefaultActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceBatchActions));
        RaisePropertyChanged(nameof(ShowInstanceResourceToggleActions));
        RaisePropertyChanged(nameof(CanSelectAllInstanceResources));
        RaisePropertyChanged(nameof(CanEnableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDisableSelectedInstanceResources));
        RaisePropertyChanged(nameof(CanDeleteSelectedInstanceResources));
    }

    private bool IsInstanceResourceToggleSupported()
    {
        return _instanceComposition.Selection.IsModable
            && (_currentRoute.Subpage == LauncherFrontendSubpageKey.VersionMod
                || _currentRoute.Subpage == LauncherFrontendSubpageKey.VersionModDisabled);
    }

    private IReadOnlyList<InstanceResourceEntryViewModel> GetSelectedInstanceResourceEntries()
    {
        return InstanceResourceEntries
            .Where(entry => entry.IsSelected)
            .ToArray();
    }

    private void HandleInstanceResourceSelectionChanged(string path, bool isSelected)
    {
        if (_suppressInstanceResourceSelectionChanged)
        {
            return;
        }

        if (isSelected)
        {
            _instanceResourceSelectedPaths.Add(path);
        }
        else
        {
            _instanceResourceSelectedPaths.Remove(path);
        }

        RaiseInstanceResourceSelectionProperties();
    }

    private void SelectAllInstanceResources()
    {
        var activityTitle = SD("instance.content.resource.actions.select_all");
        if (InstanceResourceEntries.Count == 0)
        {
            AddActivity(activityTitle, SD("instance.content.resource.messages.list_empty", ("surface_title", InstanceResourceSurfaceTitle)));
            return;
        }

        if (!CanSelectAllInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = true;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        foreach (var entry in InstanceResourceEntries)
        {
            _instanceResourceSelectedPaths.Add(entry.Path);
        }

        RaiseInstanceResourceSelectionProperties();
        AddActivity(
            activityTitle,
            SD(
                "instance.content.resource.messages.selected_all",
                ("surface_title", InstanceResourceSurfaceTitle),
                ("count", InstanceResourceEntries.Count)));
    }

    private void ClearInstanceResourceSelection()
    {
        if (!HasSelectedInstanceResources)
        {
            return;
        }

        _suppressInstanceResourceSelectionChanged = true;
        try
        {
            foreach (var entry in InstanceResourceEntries)
            {
                entry.IsSelected = false;
            }
        }
        finally
        {
            _suppressInstanceResourceSelectionChanged = false;
        }

        _instanceResourceSelectedPaths.Clear();
        RaiseInstanceResourceSelectionProperties();
    }

    private InstanceResourceEntryViewModel CreateInstanceResourceEntry(FrontendInstanceResourceEntry entry)
    {
        var display = IsInstanceResourceToggleSupported()
            ? FrontendGameManagementService.ResolveLocalModDisplay(entry, SelectedModLocalNameStyleIndex)
            : new FrontendLocalModDisplay(entry.Title, entry.Summary);
        var icon = LoadInstanceResourceBitmap(entry);
        var detailCommand = new ActionCommand(() => _ = ShowInstanceResourceDetailsAsync(entry));
        var websiteCommand = string.IsNullOrWhiteSpace(entry.Website)
            ? null
            : CreateOpenTargetCommand(
                SD(
                    "instance.content.resource.activities.open_homepage",
                    ("surface_title", InstanceResourceSurfaceTitle),
                    ("entry_title", entry.Title)),
                entry.Website,
                entry.Website);
        var openCommand = new ActionCommand(() =>
            OpenInstanceTarget(
                SD("instance.content.resource.tooltips.open_file_location"),
                entry.Path,
                SD("instance.content.resource.messages.entry_missing", ("surface_title", InstanceResourceSurfaceTitle))));
        var toggleCommand = IsInstanceResourceToggleSupported()
            ? new ActionCommand(() => _ = SetInstanceResourceEntriesEnabledAsync(
                new[] { (Title: entry.Title, Path: entry.Path, IsEnabledState: entry.IsEnabled) },
                !entry.IsEnabled,
                SD("instance.content.resource.messages.no_toggleable_entries")))
            : null;
        var deleteCommand = new ActionCommand(() => _ = DeleteInstanceResourcesAsync(
            new[] { (Title: entry.Title, Path: entry.Path) },
            SD("instance.content.resource.messages.no_deletable_entries")));

        var viewModel = new InstanceResourceEntryViewModel(
            icon,
            display.Title,
            LocalizeResourceSummary(display.Summary),
            LocalizeResourceMeta(entry.Meta),
            entry.Path,
            openCommand,
            actionToolTip: SD("instance.content.resource.tooltips.open_file_location"),
            isEnabled: entry.IsEnabled,
            description: entry.Description,
            website: entry.Website,
            showSelection: true,
            isSelected: _instanceResourceSelectedPaths.Contains(entry.Path),
            selectionChanged: isSelected => HandleInstanceResourceSelectionChanged(entry.Path, isSelected),
            infoCommand: detailCommand,
            websiteCommand: websiteCommand,
            openCommand: openCommand,
            toggleCommand: toggleCommand,
            deleteCommand: deleteCommand,
            infoToolTip: SD("instance.content.resource.tooltips.details"),
            websiteToolTip: SD("instance.content.resource.tooltips.website"),
            openToolTip: SD("instance.content.resource.tooltips.open_file_location"),
            enableToolTip: SD("instance.content.resource.tooltips.enable"),
            disableToolTip: SD("instance.content.resource.tooltips.disable"),
            deleteToolTip: SD("instance.content.resource.tooltips.delete"),
            disabledTagText: SD("instance.content.resource.tags.disabled"));

        return viewModel;
    }

    private async Task ShowInstanceResourceDetailsAsync(FrontendInstanceResourceEntry entry)
    {
        var lines = new List<string>
        {
            $"{SD("instance.content.resource.details.fields.name")}: {entry.Title}",
            $"{SD("instance.content.resource.details.fields.status")}: {(entry.IsEnabled ? SD("instance.content.resource.tooltips.enable") : SD("instance.content.resource.tooltips.disable"))}"
        };

        if (!string.IsNullOrWhiteSpace(entry.Meta))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.type")}: {entry.Meta}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Loader))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.loader")}: {entry.Loader}");
        }

        if (!string.IsNullOrWhiteSpace(entry.DownloadSource))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.download_source")}: {entry.DownloadSource}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Version))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.version")}: {entry.Version}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Authors))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.authors")}: {entry.Authors}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Description))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.description")}: {entry.Description}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Website))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.website")}: {entry.Website}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            lines.Add($"{SD("instance.content.resource.details.fields.summary")}: {entry.Summary}");
        }

        lines.Add($"{SD("instance.content.resource.details.fields.path")}: {entry.Path}");

        try
        {
            if (Directory.Exists(entry.Path))
            {
                var directoryInfo = new DirectoryInfo(entry.Path);
                lines.Add($"{SD("instance.content.resource.details.fields.item_kind")}: {SD("instance.content.resource.meta.folder")}");
                lines.Add($"{SD("instance.content.resource.details.fields.create_time")}: {directoryInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"{SD("instance.content.resource.details.fields.modify_time")}: {directoryInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
            else if (File.Exists(entry.Path))
            {
                var fileInfo = new FileInfo(entry.Path);
                lines.Add($"{SD("instance.content.resource.details.fields.file_size")}: {FormatInstanceResourceFileSize(fileInfo.Length)}");
                lines.Add($"{SD("instance.content.resource.details.fields.create_time")}: {fileInfo.CreationTime:yyyy/MM/dd HH:mm:ss}");
                lines.Add($"{SD("instance.content.resource.details.fields.modify_time")}: {fileInfo.LastWriteTime:yyyy/MM/dd HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            lines.Add(SD("instance.content.resource.messages.details_read_failed", ("error", ex.Message)));
        }

        var result = await ShowToolboxConfirmationAsync(
            SD("instance.content.resource.activities.view_details", ("surface_title", InstanceResourceSurfaceTitle)),
            string.Join(Environment.NewLine, lines),
            T("common.actions.close"));
        if (result is not null)
        {
            AddActivity(SD("instance.content.resource.activities.view_details", ("surface_title", InstanceResourceSurfaceTitle)), entry.Title);
        }
    }

    private Bitmap? LoadInstanceResourceBitmap(FrontendInstanceResourceEntry entry)
    {
        if (entry.IconBytes is not null && entry.IconBytes.Length > 0)
        {
            try
            {
                using var stream = new MemoryStream(entry.IconBytes, writable: false);
                return new Bitmap(stream);
            }
            catch
            {
            }
        }

        return LoadLauncherBitmap("Images", "Blocks", entry.IconName);
    }

    private static string FormatInstanceResourceFileSize(long bytes)
    {
        string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.##} {units[unitIndex]}";
    }

    private string GetInstanceResourceSortName(InstanceResourceSortMethod method)
    {
        return method switch
        {
            InstanceResourceSortMethod.FileName => SD("instance.content.sort.file_name"),
            InstanceResourceSortMethod.CreateTime => SD("instance.content.sort.added_time"),
            InstanceResourceSortMethod.FileSize => SD("instance.content.sort.file_size"),
            _ => SD("instance.content.sort.resource_name")
        };
    }

    private static DateTime GetPathCreationTimeUtc(string path)
    {
        try
        {
            return IsDirectoryPath(path) ? Directory.GetCreationTimeUtc(path) : File.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch
        {
            return 0L;
        }
    }

    private static bool IsDirectoryPath(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

}
