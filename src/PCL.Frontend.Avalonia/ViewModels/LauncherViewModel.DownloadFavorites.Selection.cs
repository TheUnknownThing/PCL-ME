using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{

    public int DownloadFavoriteSelectedCount => _downloadFavoriteSelectedProjectIds.Count;

    public bool HasSelectedDownloadFavorites => DownloadFavoriteSelectedCount > 0;

    public string DownloadFavoriteSelectionText => T("download.favorites.selection.summary", ("count", DownloadFavoriteSelectedCount));

    public bool ShowDownloadFavoriteDefaultActions => !HasSelectedDownloadFavorites;

    public bool ShowDownloadFavoriteBatchActions => HasSelectedDownloadFavorites;

    public bool CanSelectAllDownloadFavorites
    {
        get
        {
            var visibleEntries = GetVisibleDownloadFavoriteEntries();
            return visibleEntries.Count > 0 && DownloadFavoriteSelectedCount < visibleEntries.Count;
        }
    }

    public bool CanRemoveSelectedDownloadFavorites => HasSelectedDownloadFavorites;

    public bool CanFavoriteSelectedDownloadFavoritesToTarget => HasSelectedDownloadFavorites;

    public bool CanShareSelectedDownloadFavorites => HasSelectedDownloadFavorites;

    public bool CanBatchInstallSelectedDownloadFavorites => HasSelectedDownloadFavorites;

    public ActionCommand SelectAllDownloadFavoritesCommand => new(SelectAllDownloadFavorites);

    public ActionCommand ClearDownloadFavoriteSelectionCommand => new(ClearDownloadFavoriteSelection);

    public ActionCommand RemoveSelectedDownloadFavoritesCommand => new(() => _ = RemoveSelectedDownloadFavoritesAsync());

    public ActionCommand FavoriteSelectedDownloadFavoritesToTargetCommand => new(() => _ = FavoriteSelectedDownloadFavoritesToTargetAsync());

    public ActionCommand ShareSelectedDownloadFavoritesCommand => new(() => _ = ShareSelectedDownloadFavoritesAsync());

    public ActionCommand BatchInstallSelectedDownloadFavoritesCommand => new(() => _ = BatchInstallSelectedDownloadFavoritesAsync());

    private IReadOnlyList<InstanceResourceEntryViewModel> GetVisibleDownloadFavoriteEntries()
    {
        return DownloadFavoriteSections
            .SelectMany(section => section.Items)
            .ToArray();
    }

    private void RaiseDownloadFavoriteSelectionProperties()
    {
        RaisePropertyChanged(nameof(DownloadFavoriteSelectedCount));
        RaisePropertyChanged(nameof(HasSelectedDownloadFavorites));
        RaisePropertyChanged(nameof(DownloadFavoriteSelectionText));
        RaisePropertyChanged(nameof(ShowDownloadFavoriteDefaultActions));
        RaisePropertyChanged(nameof(ShowDownloadFavoriteBatchActions));
        RaisePropertyChanged(nameof(CanSelectAllDownloadFavorites));
        RaisePropertyChanged(nameof(CanRemoveSelectedDownloadFavorites));
        RaisePropertyChanged(nameof(CanFavoriteSelectedDownloadFavoritesToTarget));
        RaisePropertyChanged(nameof(CanShareSelectedDownloadFavorites));
        RaisePropertyChanged(nameof(CanBatchInstallSelectedDownloadFavorites));
    }

    private void HandleDownloadFavoriteSelectionChanged(string projectId, bool isSelected)
    {
        if (_suppressDownloadFavoriteSelectionChanged || string.IsNullOrWhiteSpace(projectId))
        {
            return;
        }

        if (isSelected)
        {
            _downloadFavoriteSelectedProjectIds.Add(projectId);
        }
        else
        {
            _downloadFavoriteSelectedProjectIds.Remove(projectId);
        }

        RaiseDownloadFavoriteSelectionProperties();
    }

    private void SyncDownloadFavoriteSelectionTarget(string targetId)
    {
        if (string.Equals(_downloadFavoriteSelectionTargetId, targetId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _downloadFavoriteSelectionTargetId = targetId;
        if (_downloadFavoriteSelectedProjectIds.Count == 0)
        {
            return;
        }

        _downloadFavoriteSelectedProjectIds.Clear();
        RaiseDownloadFavoriteSelectionProperties();
    }

    private void SelectAllDownloadFavorites()
    {
        var visibleEntries = GetVisibleDownloadFavoriteEntries();
        if (visibleEntries.Count == 0)
        {
            AddActivity(
                T("download.favorites.activities.select_all"),
                T("download.favorites.select_all.empty", ("target_name", GetSelectedDownloadFavoriteTargetName())));
            return;
        }

        if (!CanSelectAllDownloadFavorites)
        {
            return;
        }

        _suppressDownloadFavoriteSelectionChanged = true;
        try
        {
            foreach (var entry in visibleEntries)
            {
                entry.IsSelected = true;
            }
        }
        finally
        {
            _suppressDownloadFavoriteSelectionChanged = false;
        }

        _downloadFavoriteSelectedProjectIds.Clear();
        foreach (var entry in visibleEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Path))
            {
                _downloadFavoriteSelectedProjectIds.Add(entry.Path);
            }
        }

        RaiseDownloadFavoriteSelectionProperties();
        AddActivity(
            T("download.favorites.activities.select_all"),
            T("download.favorites.select_all.completed", ("target_name", GetSelectedDownloadFavoriteTargetName()), ("count", visibleEntries.Count)));
    }

    private void ClearDownloadFavoriteSelection()
    {
        if (!HasSelectedDownloadFavorites)
        {
            return;
        }

        _suppressDownloadFavoriteSelectionChanged = true;
        try
        {
            foreach (var entry in GetVisibleDownloadFavoriteEntries())
            {
                entry.IsSelected = false;
            }
        }
        finally
        {
            _suppressDownloadFavoriteSelectionChanged = false;
        }

        _downloadFavoriteSelectedProjectIds.Clear();
        RaiseDownloadFavoriteSelectionProperties();
    }

    private IReadOnlyList<FrontendDownloadCatalogEntry> GetSelectedDownloadFavoriteCatalogEntries()
    {
        var target = GetSelectedDownloadFavoriteTargetState();
        return target.Sections
            .SelectMany(section => section.Entries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Identity)
                            && _downloadFavoriteSelectedProjectIds.Contains(entry.Identity))
            .GroupBy(entry => entry.Identity!, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

}
