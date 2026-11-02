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

    private FrontendDownloadFavoriteTargetState GetSelectedDownloadFavoriteTargetState()
    {
        if (_downloadComposition.Favorites.Targets.Count == 0)
        {
            return new FrontendDownloadFavoriteTargetState(T("download.favorites.targets.default_name"), "default", []);
        }

        var index = Math.Clamp(SelectedDownloadFavoriteTargetIndex, 0, _downloadComposition.Favorites.Targets.Count - 1);
        return _downloadComposition.Favorites.Targets[index];
    }

    private string GetSelectedDownloadFavoriteTargetName()
    {
        return GetSelectedDownloadFavoriteTargetState().Name;
    }

    private async Task<DownloadFavoriteInstallTargetSnapshot?> PromptForDownloadFavoriteInstallTargetAsync(
        IReadOnlyList<FrontendDownloadCatalogEntry> selectedEntries)
    {
        var selectedCount = selectedEntries.Count;
        var includesDatapacks = selectedEntries.Any(entry => entry.OriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack);
        var instances = LoadAvailableDownloadTargetInstances();
        if (instances.Count == 0)
        {
            AddActivity(T("download.favorites.activities.batch_install"), T("download.favorites.batch_install.no_instances"));
            return null;
        }

        string? selectedId;
        try
        {
            selectedId = await _launcherActionService.PromptForChoiceAsync(
                T("download.favorites.batch_install.target.title"),
                T("download.favorites.batch_install.target.body", ("count", selectedCount)),
                instances.Select(entry => new PclChoiceDialogOption(
                    entry.Name,
                    entry.Name,
                    entry.Subtitle))
                    .ToArray(),
                _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : instances[0].Name,
                T("download.favorites.batch_install.target.confirm"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.batch_install.target.failed"), ex.Message);
            return null;
        }

        var instanceSnapshot = string.IsNullOrWhiteSpace(selectedId)
            ? null
            : instances.FirstOrDefault(entry => string.Equals(entry.Name, selectedId, StringComparison.OrdinalIgnoreCase));
        if (instanceSnapshot is null)
        {
            return null;
        }

        if (!includesDatapacks)
        {
            return new DownloadFavoriteInstallTargetSnapshot(instanceSnapshot, null);
        }

        var targetComposition = ComposeInstanceForSaveSelection(instanceSnapshot.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            AddActivity(
                T("download.favorites.activities.batch_install"),
                T("download.favorites.batch_install.target.datapack_instance_unavailable", ("instance_name", instanceSnapshot.Name)));
            return null;
        }

        var datapackSaveSelection = await PromptForDownloadFavoriteDatapackSaveTargetAsync(targetComposition, instanceSnapshot);
        if (datapackSaveSelection is null)
        {
            return null;
        }

        return new DownloadFavoriteInstallTargetSnapshot(instanceSnapshot, datapackSaveSelection);
    }

    private async Task<FrontendVersionSaveSelectionState?> PromptForDownloadFavoriteDatapackSaveTargetAsync(
        FrontendInstanceComposition targetComposition,
        InstanceSelectionSnapshot instanceSnapshot)
    {
        var saves = targetComposition.World.Entries;
        if (saves.Count == 0)
        {
            AddActivity(
                T("download.favorites.activities.batch_install"),
                T("download.favorites.batch_install.target.datapack_no_saves", ("instance_name", instanceSnapshot.Name)));
            return null;
        }

        var defaultSavePath = string.Equals(instanceSnapshot.Name, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase)
                              && _versionSavesComposition.Selection.HasSelection
            ? _versionSavesComposition.Selection.SavePath
            : saves[0].Path;

        string? selectedSavePath;
        try
        {
            selectedSavePath = await _launcherActionService.PromptForChoiceAsync(
                T("download.favorites.batch_install.target.datapack_save_title"),
                T("download.favorites.batch_install.target.datapack_save_message", ("instance_name", instanceSnapshot.Name)),
                saves.Select(entry => new PclChoiceDialogOption(
                    entry.Path,
                    entry.Title,
                    entry.Summary))
                    .ToArray(),
                defaultSavePath,
                T("download.favorites.batch_install.target.datapack_save_confirm"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.batch_install.target.datapack_save_failed"), ex.Message);
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedSavePath))
        {
            return null;
        }

        var selection = FrontendVersionSavesCompositionService.Compose(targetComposition, selectedSavePath).Selection;
        if (!selection.HasSelection)
        {
            AddActivity(
                T("download.favorites.activities.batch_install"),
                T("download.favorites.batch_install.target.datapack_directory_unresolved"));
            return null;
        }

        return selection;
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
            AddActivity(title, T("download.favorites.targets.none_available"));
            return null;
        }

        var defaultTargetId = defaultTarget is null
            ? GetDownloadFavoriteTargetId(targets[0])
            : GetDownloadFavoriteTargetId(defaultTarget);

        string? selectedTargetId;
        try
        {
            selectedTargetId = await _launcherActionService.PromptForChoiceAsync(
                title,
                content,
                targets.Select(target => new PclChoiceDialogOption(
                    GetDownloadFavoriteTargetId(target),
                    GetDownloadFavoriteTargetName(target),
                    T("download.favorites.targets.option.id", ("target_id", GetDownloadFavoriteTargetId(target)))))
                    .ToArray(),
                defaultTargetId,
                T("common.actions.continue"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.prompt_failed", ("title", title)), ex.Message);
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

}
