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
            confirmed = await _launcherActionService.ConfirmAsync(
                T("download.favorites.remove.confirmation.title"),
                T("download.favorites.remove.confirmation.message", ("target_name", targetName), ("count", selectedIds.Length)),
                T("download.favorites.actions.remove"),
                isDanger: true);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.activities.remove_failed"), ex.Message);
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
            AddActivity(T("download.favorites.activities.remove"), T("download.favorites.remove.no_matches", ("target_name", targetName)));
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        ClearDownloadFavoriteSelection();
        AddActivity(T("download.favorites.activities.remove"), T("download.favorites.remove.completed", ("target_name", targetName), ("count", removedCount)));
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
            T("download.favorites.activities.favorite_to"),
            T("download.favorites.favorite_to.selection_prompt", ("count", DownloadFavoriteSelectedCount)),
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
            AddActivity(T("download.favorites.activities.favorite_to"), T("download.favorites.favorite_to.already_contains_selected", ("target_name", GetDownloadFavoriteTargetName(target))));
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        AddActivity(T("download.favorites.activities.favorite_to"), T("download.favorites.favorite_to.completed", ("count", addedCount), ("target_name", GetDownloadFavoriteTargetName(target))));
    }

    private async Task ShareSelectedDownloadFavoritesAsync()
    {
        var selectedIds = GetSelectedDownloadFavoriteCatalogEntries()
            .Select(entry => entry.Identity)
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selectedIds.Length == 0)
        {
            AddActivity(T("download.favorites.activities.share_selected"), T("download.favorites.share_selected.empty"));
            return;
        }

        try
        {
            await _launcherActionService.SetClipboardTextAsync(JsonSerializer.Serialize(selectedIds));
            ClearDownloadFavoriteSelection();
            AddActivity(T("download.favorites.activities.share_selected"), T("download.favorites.share_selected.completed", ("count", selectedIds.Length)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.activities.share_selected_failed"), ex.Message);
        }
    }

    private async Task BatchInstallSelectedDownloadFavoritesAsync()
    {
        var selectedEntries = GetSelectedDownloadFavoriteCatalogEntries();
        if (selectedEntries.Count == 0)
        {
            AddActivity(T("download.favorites.activities.batch_install"), T("download.favorites.batch_install.empty_selection"));
            return;
        }

        var targetSnapshot = await PromptForDownloadFavoriteInstallTargetAsync(selectedEntries);
        if (targetSnapshot is null)
        {
            return;
        }

        AvaloniaHintBus.Show(T("download.favorites.batch_install.analyzing_hint", ("count", selectedEntries.Count)), AvaloniaHintTheme.Info);
        AddActivity(T("download.favorites.activities.batch_install"), T("download.favorites.batch_install.analyzing_activity", ("target_name", targetSnapshot.DisplayName), ("count", selectedEntries.Count)));
        CommunityProjectInstallBuildResult result;
        try
        {
            result = await Task.Run(() => BuildDownloadFavoriteBatchInstallResult(selectedEntries, targetSnapshot));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.activities.batch_install_failed"), ex.Message);
            return;
        }

        var confirmed = await ConfirmDownloadFavoriteBatchInstallAsync(targetSnapshot, result);
        if (!confirmed)
        {
            AddActivity(T("download.favorites.activities.batch_install"), T("download.favorites.batch_install.canceled"));
            return;
        }

        AvaloniaHintBus.Show(T("download.favorites.batch_install.started_hint"), AvaloniaHintTheme.Info);
        AddActivity(T("download.favorites.activities.batch_install"), T("download.favorites.batch_install.started_activity", ("target_name", targetSnapshot.DisplayName)));
        foreach (var plan in result.Plans)
        {
            RegisterDownloadFavoriteBatchInstallTask(plan);
        }

        var summaryParts = new List<string>();
        if (result.Plans.Count > 0)
        {
            summaryParts.Add(T("download.favorites.batch_install.summary.tasks", ("count", result.Plans.Count)));
            var dependencyCount = result.Plans.Count(plan => plan.IsDependency);
            if (dependencyCount > 0)
            {
                summaryParts.Add(T("download.favorites.batch_install.summary.dependencies", ("count", dependencyCount)));
            }
        }

        if (result.Skipped.Count > 0)
        {
            summaryParts.Add(T("download.favorites.batch_install.summary.skipped", ("count", result.Skipped.Count)));
        }

        AddActivity(
            T("download.favorites.activities.batch_install"),
            T(
                "download.favorites.batch_install.summary.completed",
                ("target_name", targetSnapshot.DisplayName),
                ("summary", summaryParts.Count == 0 ? T("download.favorites.batch_install.summary.none") : string.Join(T("common.punctuation.comma"), summaryParts))));

        foreach (var skipped in result.Skipped.Take(5))
        {
            AddActivity(T("download.favorites.activities.batch_install"), skipped);
        }
    }

    private async Task<bool> ToggleCommunityProjectFavoriteAsync()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            AddActivity(T("download.favorites.activities.favorite"), T("download.favorites.favorite.none_selected"));
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
            AddActivity(
                added ? T("download.favorites.activities.favorite_added") : T("download.favorites.activities.favorite_removed"),
                T("download.favorites.favorite.changed", ("title", CommunityProjectTitle), ("target_name", targetName)));
            return added;
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.activities.favorite_failed"), ex.Message);
            return false;
        }
    }

    private async Task FavoriteCurrentCommunityProjectToTargetAsync()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            AddActivity(T("download.favorites.activities.favorite_to"), T("download.favorites.favorite_to.none_selected"));
            return;
        }

        try
        {
            var root = LoadDownloadFavoriteTargetRoot();
            var target = await PromptForDownloadFavoriteTargetAsync(
                root,
                T("download.favorites.activities.favorite_to"),
                T("download.favorites.favorite_to.title"),
                defaultTarget: GetSelectedDownloadFavoriteTarget(root));
            if (target is null)
            {
                return;
            }

            if (!AddProjectToFavoriteTarget(target, _communityProjectState.ProjectId))
            {
                AddActivity(
                    T("download.favorites.activities.favorite_to"),
                    T("download.favorites.favorite_to.already_contains", ("target_name", GetDownloadFavoriteTargetName(target)), ("title", CommunityProjectTitle)));
                return;
            }

            PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            AddActivity(
                T("download.favorites.activities.favorite_to"),
                T("download.favorites.favorite_to.completed", ("title", CommunityProjectTitle), ("target_name", GetDownloadFavoriteTargetName(target))));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.activities.favorite_to_failed"), ex.Message);
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
            AddActivity(
                T("download.favorites.activities.remove_from_target"),
                T("download.favorites.remove_from_target.not_in_target", ("title", title), ("target_name", GetDownloadFavoriteTargetName(target))));
            return;
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        _downloadFavoriteSelectedProjectIds.Remove(projectId);
        RaiseDownloadFavoriteSelectionProperties();
        AddActivity(
            T("download.favorites.activities.remove_from_target"),
            T("download.favorites.remove_from_target.completed", ("title", title), ("target_name", GetDownloadFavoriteTargetName(target))));
    }

}
