using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public int DownloadFavoriteSelectedCount => _downloadFavoriteSelectedProjectIds.Count;

    public bool HasSelectedDownloadFavorites => DownloadFavoriteSelectedCount > 0;

    public string DownloadFavoriteSelectionText => $"已选择 {DownloadFavoriteSelectedCount} 项";

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

    public ActionCommand SelectAllDownloadFavoritesCommand => new(SelectAllDownloadFavorites);

    public ActionCommand ClearDownloadFavoriteSelectionCommand => new(ClearDownloadFavoriteSelection);

    public ActionCommand RemoveSelectedDownloadFavoritesCommand => new(() => _ = RemoveSelectedDownloadFavoritesAsync());

    public ActionCommand FavoriteSelectedDownloadFavoritesToTargetCommand => new(() => _ = FavoriteSelectedDownloadFavoritesToTargetAsync());

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
            AddActivity("全选收藏", $"{GetSelectedDownloadFavoriteTargetName()} 中没有可选择的项目。");
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
        AddActivity("全选收藏", $"{GetSelectedDownloadFavoriteTargetName()} • 已选中 {visibleEntries.Count} 项。");
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

    private async Task RemoveSelectedDownloadFavoritesAsync()
    {
        if (!HasSelectedDownloadFavorites)
        {
            return;
        }

        var root = LoadDownloadFavoriteTargetRoot();
        var target = GetSelectedDownloadFavoriteTarget(root);
        var targetName = GetDownloadFavoriteTargetName(target);
        var selectedIds = _downloadFavoriteSelectedProjectIds.ToArray();

        bool confirmed;
        try
        {
            confirmed = await _shellActionService.ConfirmAsync(
                "移出收藏夹",
                $"确认从 {targetName} 中移出 {selectedIds.Length} 个项目？",
                "移出",
                isDanger: true);
        }
        catch (Exception ex)
        {
            AddFailureActivity("移出收藏夹失败", ex.Message);
            return;
        }

        if (!confirmed)
        {
            return;
        }

        var removedCount = 0;
        foreach (var projectId in selectedIds)
        {
            if (RemoveProjectFromFavoriteTarget(target, projectId))
            {
                removedCount++;
            }
        }

        if (removedCount == 0)
        {
            AddActivity("移出收藏夹", $"{targetName} 中没有匹配的收藏项目。");
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        ClearDownloadFavoriteSelection();
        AddActivity("移出收藏夹", $"已从 {targetName} 中移出 {removedCount} 个项目。");
    }

    private async Task FavoriteSelectedDownloadFavoritesToTargetAsync()
    {
        if (!HasSelectedDownloadFavorites)
        {
            return;
        }

        var root = LoadDownloadFavoriteTargetRoot();
        var currentTarget = GetSelectedDownloadFavoriteTarget(root);
        var target = await PromptForDownloadFavoriteTargetAsync(
            root,
            "收藏到",
            $"选择一个收藏夹，用于接收这 {DownloadFavoriteSelectedCount} 个项目。",
            excludeTarget: currentTarget);
        if (target is null)
        {
            return;
        }

        var addedCount = 0;
        foreach (var projectId in _downloadFavoriteSelectedProjectIds)
        {
            if (AddProjectToFavoriteTarget(target, projectId))
            {
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            AddActivity("收藏到", $"{GetDownloadFavoriteTargetName(target)} 中已包含所选项目。");
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        AddActivity("收藏到", $"已将 {addedCount} 个项目加入 {GetDownloadFavoriteTargetName(target)}。");
    }

    private async Task<bool> ToggleCommunityProjectFavoriteAsync()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            AddActivity("收藏项目", "当前没有可收藏的项目。");
            return false;
        }

        try
        {
            var root = LoadDownloadFavoriteTargetRoot();
            var target = GetSelectedDownloadFavoriteTarget(root);
            var projectId = _communityProjectState.ProjectId;
            var targetName = GetDownloadFavoriteTargetName(target);
            var added = AddProjectToFavoriteTarget(target, projectId);
            if (!added)
            {
                RemoveProjectFromFavoriteTarget(target, projectId);
            }

            PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            AddActivity(added ? "加入收藏夹" : "移出收藏夹", $"{CommunityProjectTitle} • {targetName}");
            return added;
        }
        catch (Exception ex)
        {
            AddFailureActivity("收藏项目失败", ex.Message);
            return false;
        }
    }

    private async Task FavoriteCurrentCommunityProjectToTargetAsync()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            AddActivity("收藏到", "当前没有可收藏的项目。");
            return;
        }

        try
        {
            var root = LoadDownloadFavoriteTargetRoot();
            var target = await PromptForDownloadFavoriteTargetAsync(
                root,
                "收藏到",
                "选择一个收藏夹来保存当前项目。",
                defaultTarget: GetSelectedDownloadFavoriteTarget(root));
            if (target is null)
            {
                return;
            }

            if (!AddProjectToFavoriteTarget(target, _communityProjectState.ProjectId))
            {
                AddActivity("收藏到", $"{GetDownloadFavoriteTargetName(target)} 中已包含 {CommunityProjectTitle}。");
                return;
            }

            PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            AddActivity("收藏到", $"{CommunityProjectTitle} 已加入 {GetDownloadFavoriteTargetName(target)}。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("收藏到失败", ex.Message);
        }
    }

    private async Task RemoveDownloadFavoriteAsync(string projectId, string title)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return;
        }

        var root = LoadDownloadFavoriteTargetRoot();
        var target = GetSelectedDownloadFavoriteTarget(root);
        if (!RemoveProjectFromFavoriteTarget(target, projectId))
        {
            AddActivity("移出收藏夹", $"{title} 不在 {GetDownloadFavoriteTargetName(target)} 中。");
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        _downloadFavoriteSelectedProjectIds.Remove(projectId);
        RaiseDownloadFavoriteSelectionProperties();
        AddActivity("移出收藏夹", $"{title} 已从 {GetDownloadFavoriteTargetName(target)} 中移出。");
    }

    private FrontendDownloadFavoriteTargetState GetSelectedDownloadFavoriteTargetState()
    {
        if (_downloadComposition.Favorites.Targets.Count == 0)
        {
            return new FrontendDownloadFavoriteTargetState("默认收藏夹", "default", []);
        }

        var index = Math.Clamp(SelectedDownloadFavoriteTargetIndex, 0, _downloadComposition.Favorites.Targets.Count - 1);
        return _downloadComposition.Favorites.Targets[index];
    }

    private string GetSelectedDownloadFavoriteTargetName()
    {
        return GetSelectedDownloadFavoriteTargetState().Name;
    }

    private JsonObject GetSelectedDownloadFavoriteTarget(JsonArray root)
    {
        var targets = root.OfType<JsonObject>().ToArray();
        if (targets.Length == 0)
        {
            return EnsureCommunityProjectFavoriteTarget(root);
        }

        var index = Math.Clamp(SelectedDownloadFavoriteTargetIndex, 0, targets.Length - 1);
        return targets[index];
    }

    private async Task<JsonObject?> PromptForDownloadFavoriteTargetAsync(
        JsonArray root,
        string title,
        string content,
        JsonObject? defaultTarget = null,
        JsonObject? excludeTarget = null)
    {
        var targets = root.OfType<JsonObject>()
            .Where(target => excludeTarget is null || !string.Equals(
                GetDownloadFavoriteTargetId(target),
                GetDownloadFavoriteTargetId(excludeTarget),
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (targets.Length == 0)
        {
            AddActivity(title, "当前没有可用的其他收藏夹。");
            return null;
        }

        var defaultTargetId = defaultTarget is null
            ? GetDownloadFavoriteTargetId(targets[0])
            : GetDownloadFavoriteTargetId(defaultTarget);

        string? selectedTargetId;
        try
        {
            selectedTargetId = await _shellActionService.PromptForChoiceAsync(
                title,
                content,
                targets.Select(target => new PclChoiceDialogOption(
                    GetDownloadFavoriteTargetId(target),
                    GetDownloadFavoriteTargetName(target),
                    $"收藏夹 ID：{GetDownloadFavoriteTargetId(target)}"))
                    .ToArray(),
                defaultTargetId,
                "继续");
        }
        catch (Exception ex)
        {
            AddFailureActivity($"{title}失败", ex.Message);
            return null;
        }

        return selectedTargetId is null
            ? null
            : targets.FirstOrDefault(target => string.Equals(
                GetDownloadFavoriteTargetId(target),
                selectedTargetId,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool AddProjectToFavoriteTarget(JsonObject target, string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return false;
        }

        var favorites = EnsureCommunityProjectFavoriteArray(target);
        var existing = favorites
            .FirstOrDefault(node => string.Equals(GetCommunityProjectFavoriteId(node), projectId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return false;
        }

        favorites.Add(projectId);
        return true;
    }

    private static bool RemoveProjectFromFavoriteTarget(JsonObject target, string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return false;
        }

        var favorites = EnsureCommunityProjectFavoriteArray(target);
        var existing = favorites
            .FirstOrDefault(node => string.Equals(GetCommunityProjectFavoriteId(node), projectId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return false;
        }

        favorites.Remove(existing);
        return true;
    }

    private void QueueDownloadFavoriteIconLoad(InstanceResourceEntryViewModel entry, string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return;
        }

        _ = LoadDownloadFavoriteIconAsync(entry, iconUrl);
    }

    private async Task LoadDownloadFavoriteIconAsync(InstanceResourceEntryViewModel entry, string iconUrl)
    {
        var iconPath = await FrontendCommunityIconCache.EnsureCachedIconAsync(iconUrl);
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        var bitmap = await Task.Run(() => LoadCachedBitmapFromPath(iconPath));
        if (bitmap is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => entry.ApplyIcon(bitmap));
    }
}
