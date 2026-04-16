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

internal sealed partial class FrontendShellViewModel
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
            await _shellActionService.SetClipboardTextAsync(JsonSerializer.Serialize(selectedIds));
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

    private async Task<bool> ConfirmDownloadFavoriteBatchInstallAsync(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(
                T("download.favorites.batch_install.confirmation.title"),
                BuildDownloadFavoriteBatchInstallConfirmationMessage(targetSnapshot, result),
                T("download.favorites.batch_install.confirmation.confirm"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.batch_install.confirmation.failed"), ex.Message);
            return false;
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
            selectedId = await _shellActionService.PromptForChoiceAsync(
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

        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, instanceSnapshot.Name);
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
            selectedSavePath = await _shellActionService.PromptForChoiceAsync(
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
            selectedTargetId = await _shellActionService.PromptForChoiceAsync(
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

    private CommunityProjectInstallBuildResult BuildDownloadFavoriteBatchInstallResult(
        IReadOnlyList<FrontendDownloadCatalogEntry> selectedEntries,
        DownloadFavoriteInstallTargetSnapshot targetSnapshot)
    {
        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, targetSnapshot.Instance.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            return new CommunityProjectInstallBuildResult([], [T("download.favorites.batch_install.target_unavailable", ("target_name", targetSnapshot.Instance.Name))]);
        }

        var roots = new List<CommunityProjectInstallRootRequest>();
        var skipped = new List<string>();
        foreach (var favorite in selectedEntries)
        {
            if (string.IsNullOrWhiteSpace(favorite.Identity))
            {
                skipped.Add(T("download.favorites.batch_install.skip.missing_project_id", ("title", favorite.Title)));
                continue;
            }

            if (favorite.OriginSubpage is null)
            {
                skipped.Add(T("download.favorites.batch_install.skip.unknown_type", ("title", favorite.Title)));
                continue;
            }

            if (favorite.OriginSubpage == LauncherFrontendSubpageKey.DownloadPack)
            {
                skipped.Add(T("download.favorites.batch_install.skip.pack_unsupported", ("title", favorite.Title)));
                continue;
            }

            roots.Add(new CommunityProjectInstallRootRequest(
                favorite.Identity,
                favorite.Title,
                favorite.OriginSubpage.Value));
        }

        var buildResult = BuildCommunityProjectInstallBuildResult(
            roots,
            targetComposition,
            targetSnapshot.Instance.LoaderLabel,
            includeDependencies: true,
            datapackSaveSelection: targetSnapshot.DatapackSaveSelection);
        return new CommunityProjectInstallBuildResult(
            buildResult.Plans,
            skipped.Concat(buildResult.Skipped).ToArray());
    }

    private void RegisterDownloadFavoriteBatchInstallTask(CommunityProjectInstallPlan plan)
    {
        RegisterCommunityProjectInstallTask(plan, T("download.favorites.batch_install.task_title"));
    }

    private void RegisterCommunityProjectInstallTask(CommunityProjectInstallPlan plan, string activityTitle)
    {
        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            T("download.favorites.batch_install.task_display", ("activity_title", activityTitle), ("title", plan.Title)),
            plan.SourceUrl,
            plan.TargetPath,
            ResolveDownloadRequestTimeout(),
            onStarted: _ => AvaloniaHintBus.Show(T("download.favorites.batch_install.task_started", ("instance_name", plan.InstanceName)), AvaloniaHintTheme.Info),
            onCompleted: downloadedPath =>
            {
                var installedPath = FinalizeCommunityProjectInstalledArtifact(plan.Route, downloadedPath, plan.ReplacedPath);
                Dispatcher.UIThread.Post(() =>
                {
                    CleanupReplacedDownloadFavoriteResource(plan.ReplacedPath);
                    if (plan.IsCurrentInstanceTarget)
                    {
                        ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
                        if (plan.Route == LauncherFrontendSubpageKey.DownloadDataPack)
                        {
                            ReloadVersionSavesComposition();
                        }
                    }

                    AddActivity(activityTitle, T("download.favorites.batch_install.task_completed", ("title", plan.Title), ("path", installedPath)));
                    AvaloniaHintBus.Show(T("download.favorites.batch_install.task_completed_hint", ("title", plan.Title), ("instance_name", plan.InstanceName)), AvaloniaHintTheme.Success);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() => AddFailureActivity(T("download.favorites.batch_install.task_failed", ("activity_title", activityTitle)), T("download.favorites.batch_install.task_failed_body", ("title", plan.Title), ("message", message))));
            }));
    }

    private CommunityProjectInstallBuildResult BuildCommunityProjectInstallBuildResult(
        IReadOnlyList<CommunityProjectInstallRootRequest> roots,
        FrontendInstanceComposition targetComposition,
        string? preferredLoader,
        bool includeDependencies,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (!targetComposition.Selection.HasSelection)
        {
            return new CommunityProjectInstallBuildResult([], [T("download.favorites.batch_install.target_unavailable")]);
        }

        var plans = new List<CommunityProjectInstallPlan>();
        var skipped = new List<string>();
        var resolvedResults = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var resolvingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var preferredVersion = targetComposition.Selection.VanillaVersion;

        foreach (var root in roots)
        {
            ResolveInstallRoot(root, isDependency: false);
        }

        return new CommunityProjectInstallBuildResult(plans, skipped);

        bool ResolveInstallRoot(CommunityProjectInstallRootRequest request, bool isDependency)
        {
            if (string.IsNullOrWhiteSpace(request.ProjectId))
            {
                skipped.Add(T("download.favorites.batch_install.skip.missing_project_id", ("title", request.Title)));
                return false;
            }

            var requestKey = $"{request.Route}:{request.ProjectId}";
            if (resolvedResults.TryGetValue(requestKey, out var resolved))
            {
                return resolved;
            }

            if (!resolvingKeys.Add(requestKey))
            {
                return true;
            }

            try
            {
                var projectState = request.ProjectState ?? FrontendCommunityProjectService.GetProjectState(
                    request.ProjectId,
                    preferredVersion,
                    _selectedCommunityDownloadSourceIndex);
                var projectTitle = string.IsNullOrWhiteSpace(projectState.Title) ? request.Title : projectState.Title;
                var projectAliases = BuildCommunityProjectInstallAliases(
                    request.Route,
                    projectTitle,
                    projectState.Website,
                    filePath: request.Release?.SuggestedFileName);
                if (projectAliases.Any(resolvedAliases.Contains))
                {
                    resolvedResults[requestKey] = true;
                    return true;
                }

                var release = request.Release ?? SelectPreferredCommunityProjectReleaseForTarget(
                    projectState.Releases.Where(entry => entry.IsDirectDownload && !string.IsNullOrWhiteSpace(entry.Target)),
                    preferredVersion,
                    preferredLoader,
                    request.Route);
                var installTargetName = ResolveCommunityProjectInstallTargetName(
                    targetComposition.Selection.InstanceName,
                    request.Route,
                    datapackSaveSelection);
                if (release is null || string.IsNullOrWhiteSpace(release.Target))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.no_version", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedResults[requestKey] = false;
                    return false;
                }

                if (includeDependencies && request.Route == LauncherFrontendSubpageKey.DownloadMod)
                {
                    foreach (var dependency in release.Dependencies.Where(ShouldAutoInstallCommunityProjectDependency))
                    {
                        var dependencyResolved = ResolveInstallRoot(
                            new CommunityProjectInstallRootRequest(
                                dependency.ProjectId,
                                dependency.Title,
                                request.Route),
                            isDependency: true);
                        if (!dependencyResolved && dependency.Kind == FrontendCommunityProjectDependencyKind.Required)
                        {
                            skipped.Add(T("download.favorites.batch_install.skip.missing_dependency", ("title", projectTitle), ("dependency_title", dependency.Title)));
                            resolvedResults[requestKey] = false;
                            return false;
                        }
                    }
                }

                var targetDirectory = ResolveCommunityProjectInstallDirectory(targetComposition.Selection, request.Route, datapackSaveSelection);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.no_install_dir", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedResults[requestKey] = false;
                    return false;
                }

                Directory.CreateDirectory(targetDirectory);
                var targetFileName = FrontendGameManagementService.ResolveCommunityResourceFileName(
                    projectTitle,
                    release.SuggestedFileName,
                    release.Title,
                    SelectedFileNameFormatIndex);
                targetFileName = NormalizeCommunityProjectInstallArtifactFileName(request.Route, targetFileName);
                var installed = FindInstalledCommunityProjectResource(targetComposition, request.Route, projectTitle, projectState, datapackSaveSelection);
                if (request.Route != LauncherFrontendSubpageKey.DownloadWorld
                    && installed is not null
                    && !ShouldInstallFavoriteResourceUpdate(installed, targetFileName, release))
                {
                    skipped.Add(T("download.favorites.batch_install.skip.already_installed", ("title", projectTitle), ("instance_name", targetComposition.Selection.InstanceName)));
                    resolvedAliases.UnionWith(projectAliases);

                    resolvedResults[requestKey] = true;
                    return true;
                }

                var targetPath = ResolveCommunityProjectInstallTargetPath(
                    targetDirectory,
                    targetFileName,
                    request.Route,
                    installed,
                    plans);
                plans.Add(new CommunityProjectInstallPlan(
                    projectAliases,
                    string.IsNullOrWhiteSpace(projectState.ProjectId) ? request.ProjectId : projectState.ProjectId,
                    projectTitle,
                    release.Title,
                    string.IsNullOrWhiteSpace(release.Meta) ? release.Info : release.Meta,
                    release.Target!,
                    targetPath,
                    targetComposition.Selection.InstanceName,
                    installTargetName,
                    request.Route,
                    installed is not null && !string.Equals(installed.Path, targetPath, StringComparison.OrdinalIgnoreCase)
                        ? installed.Path
                        : null,
                    string.Equals(targetComposition.Selection.InstanceName, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase),
                    isDependency));
                resolvedAliases.UnionWith(projectAliases);
                resolvedResults[requestKey] = true;
                return true;
            }
            catch (Exception ex)
            {
                skipped.Add(T("download.favorites.batch_install.skip.exception", ("title", request.Title), ("message", ex.Message)));
                resolvedResults[requestKey] = false;
                return false;
            }
            finally
            {
                resolvingKeys.Remove(requestKey);
            }
        }
    }

    private static void CleanupReplacedDownloadFavoriteResource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Keep the newly installed file even if cleanup of the old version fails.
        }
    }

    private static FrontendCommunityProjectReleaseEntry? SelectPreferredCommunityProjectReleaseForTarget(
        IEnumerable<FrontendCommunityProjectReleaseEntry> releases,
        string? preferredVersion,
        string? preferredLoader,
        LauncherFrontendSubpageKey? originSubpage)
    {
        return releases
            .Where(release => IsCompatibleCommunityProjectInstallRelease(
                release,
                preferredVersion,
                preferredLoader,
                originSubpage))
            .OrderByDescending(release => ReleaseMatchesExactInstanceVersion(release, NormalizeMinecraftVersion(preferredVersion)))
            .ThenByDescending(release => ReleaseMatchesExactInstanceLoader(release, preferredLoader))
            .ThenByDescending(release => release.PublishedUnixTime)
            .ThenBy(release => release.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private static string? ResolveCommunityProjectInstallDirectory(
        FrontendInstanceSelectionState selection,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (!selection.HasSelection)
        {
            return null;
        }

        return route switch
        {
            LauncherFrontendSubpageKey.DownloadResourcePack => Path.Combine(selection.IndieDirectory, "resourcepacks"),
            LauncherFrontendSubpageKey.DownloadShader => Path.Combine(selection.IndieDirectory, "shaderpacks"),
            LauncherFrontendSubpageKey.DownloadWorld => Path.Combine(selection.IndieDirectory, "saves"),
            LauncherFrontendSubpageKey.DownloadDataPack => datapackSaveSelection?.HasSelection == true ? datapackSaveSelection.DatapackDirectory : null,
            _ => Path.Combine(selection.IndieDirectory, "mods")
        };
    }

    private static InstalledFavoriteResource? FindInstalledFavoriteResource(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        FrontendDownloadCatalogEntry favorite,
        FrontendCommunityProjectState projectState,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        return FindInstalledCommunityProjectResource(composition, route, favorite.Title, projectState, datapackSaveSelection);
    }

    private static InstalledFavoriteResource? FindInstalledCommunityProjectResource(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        string title,
        FrontendCommunityProjectState projectState,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        if (route == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return null;
        }

        var installedResources = GetInstalledFavoriteResources(composition, route, datapackSaveSelection);
        var projectAliases = BuildCommunityProjectInstallAliases(route, title, projectState.Website);
        if (!string.IsNullOrWhiteSpace(projectState.Website))
        {
            var websiteMatch = installedResources.FirstOrDefault(entry =>
                string.Equals(entry.Website, projectState.Website, StringComparison.OrdinalIgnoreCase));
            if (websiteMatch is not null)
            {
                return websiteMatch;
            }
        }

        return installedResources.FirstOrDefault(entry =>
            string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase)
            || entry.InstallAliases.Any(projectAliases.Contains));
    }

    private static IReadOnlyList<InstalledFavoriteResource> GetInstalledFavoriteResources(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection = null)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => composition.Mods.Entries
                .Concat(composition.DisabledMods.Entries)
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadResourcePack => composition.ResourcePacks.Entries
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadShader => composition.Shaders.Entries
                .Select(entry => new InstalledFavoriteResource(
                    entry.Title,
                    entry.Path,
                    entry.Version,
                    entry.Website,
                    BuildCommunityProjectInstallAliases(route, entry.Title, entry.Website, entry.Identity, entry.Path)))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadDataPack => datapackSaveSelection?.HasSelection == true
                ? EnumerateDirectoryInstallArtifacts(datapackSaveSelection.DatapackDirectory)
                : [],
            _ => []
        };
    }

    private static string ResolveCommunityProjectInstallTargetName(
        string instanceName,
        LauncherFrontendSubpageKey route,
        FrontendVersionSaveSelectionState? datapackSaveSelection)
    {
        if (route == LauncherFrontendSubpageKey.DownloadDataPack && datapackSaveSelection?.HasSelection == true)
        {
            return $"{instanceName} • {datapackSaveSelection.SaveName}";
        }

        return instanceName;
    }

    private static InstalledFavoriteResource[] EnumerateDirectoryInstallArtifacts(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new InstalledFavoriteResource(
                Path.GetFileNameWithoutExtension(path),
                path,
                string.Empty,
                string.Empty,
                BuildCommunityProjectInstallAliases(route: LauncherFrontendSubpageKey.DownloadDataPack, title: Path.GetFileNameWithoutExtension(path), website: null, filePath: path)));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new InstalledFavoriteResource(
                Path.GetFileName(path),
                path,
                string.Empty,
                string.Empty,
                BuildCommunityProjectInstallAliases(route: LauncherFrontendSubpageKey.DownloadDataPack, title: Path.GetFileName(path), website: null, filePath: path)));
        return files.Concat(folders).ToArray();
    }

    private static bool ShouldInstallFavoriteResourceUpdate(
        InstalledFavoriteResource existing,
        string targetFileName,
        FrontendCommunityProjectReleaseEntry release)
    {
        var existingFileName = Path.GetFileName(existing.Path);
        if (string.Equals(existingFileName, targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var existingVersion = GetComparableFavoriteVersion(existing.Version, existingFileName, existing.Title);
        var releaseVersion = GetComparableFavoriteVersion(
            release.Title,
            release.SuggestedFileName,
            release.Info,
            release.Meta);
        if (existingVersion > new Version(0, 0) && releaseVersion > new Version(0, 0))
        {
            return releaseVersion > existingVersion;
        }

        return true;
    }

    private static Version GetComparableFavoriteVersion(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(candidate, @"\d+(?:\.\d+){1,3}");
            if (match.Success && Version.TryParse(match.Value, out var parsed))
            {
                return parsed;
            }
        }

        return new Version(0, 0);
    }

    private string BuildDownloadFavoriteBatchInstallConfirmationMessage(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        return BuildCommunityProjectInstallConfirmationMessage(targetSnapshot.DisplayName, result);
    }

    private string BuildCommunityProjectInstallConfirmationMessage(
        string instanceName,
        CommunityProjectInstallBuildResult result)
    {
        var lines = new List<string>
        {
            T("download.favorites.batch_install.confirmation.instance", ("instance_name", instanceName)),
            T("download.favorites.batch_install.confirmation.count", ("count", result.Plans.Count))
        };

        if (result.Plans.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(T("download.favorites.batch_install.confirmation.plans"));
            foreach (var plan in result.Plans)
            {
                var releaseText = string.IsNullOrWhiteSpace(plan.ReleaseSummary)
                    ? plan.ReleaseTitle
                    : $"{plan.ReleaseTitle} | {plan.ReleaseSummary}";
                lines.Add(plan.IsDependency
                    ? T("download.favorites.batch_install.confirmation.plan_dependency", ("title", plan.Title), ("release_text", releaseText))
                    : T("download.favorites.batch_install.confirmation.plan", ("title", plan.Title), ("release_text", releaseText)));
            }
        }

        if (result.Skipped.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(T("download.favorites.batch_install.confirmation.skipped"));
            foreach (var skipped in result.Skipped)
            {
                lines.Add(T("download.favorites.batch_install.confirmation.skipped_item", ("value", skipped)));
            }
        }

        lines.Add(string.Empty);
        lines.Add(T("download.favorites.batch_install.confirmation.footer"));
        return string.Join(Environment.NewLine, lines);
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

    private static bool ShouldAutoInstallCommunityProjectDependency(FrontendCommunityProjectDependencyEntry dependency)
    {
        return dependency.Kind is FrontendCommunityProjectDependencyKind.Required
            or FrontendCommunityProjectDependencyKind.Tool
            or FrontendCommunityProjectDependencyKind.Include;
    }

    private static string ResolveCommunityProjectInstallTargetPath(
        string targetDirectory,
        string targetFileName,
        LauncherFrontendSubpageKey route,
        InstalledFavoriteResource? installed,
        IEnumerable<CommunityProjectInstallPlan> existingPlans)
    {
        if (route == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return GetUniqueChildPath(targetDirectory, targetFileName);
        }

        var preferredPath = installed is not null
                            && string.Equals(Path.GetFileName(installed.Path), targetFileName, StringComparison.OrdinalIgnoreCase)
            ? installed.Path
            : Path.Combine(targetDirectory, targetFileName);
        var isOccupied = existingPlans.Any(plan => string.Equals(plan.TargetPath, preferredPath, StringComparison.OrdinalIgnoreCase))
                         || (installed is null && (File.Exists(preferredPath) || Directory.Exists(preferredPath)));
        return isOccupied ? GetUniqueChildPath(targetDirectory, targetFileName) : preferredPath;
    }

    private static HashSet<string> BuildCommunityProjectInstallAliases(
        LauncherFrontendSubpageKey route,
        string? title,
        string? website,
        string? identity = null,
        string? filePath = null)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(identity));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(title));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(ExtractCommunityProjectWebsiteSlug(website)));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(ExtractCommunityProjectStemAlias(filePath)));
        AddCommunityProjectInstallAlias(aliases, route, NormalizeCommunityProjectAlias(filePath is null ? null : Path.GetFileNameWithoutExtension(filePath)));
        return aliases;
    }

    private static string ExtractCommunityProjectWebsiteSlug(string? website)
    {
        if (string.IsNullOrWhiteSpace(website)
            || !Uri.TryCreate(website, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Segments
            .Select(segment => segment.Trim('/'))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .LastOrDefault(segment =>
                !segment.Equals("mod", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("plugin", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("project", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("mc-mods", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("texture-packs", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("resource-packs", StringComparison.OrdinalIgnoreCase)
                && !segment.Equals("shader-packs", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    private static string NormalizeCommunityProjectAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\p{L}\p{Nd}]+", " ");
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token is not (
                "mod" or "mods" or "api" or "library" or "lib" or "shader" or "shaders" or "resource" or "resources" or
                "resourcepack" or "resourcepacks" or "resource-pack" or "resource-packs" or
                "pack" or "packs" or "plugin" or "plugins" or
                "fabric" or "forge" or "neoforge" or "quilt"))
            .ToArray();
        return tokens.Length == 0 ? string.Empty : string.Concat(tokens);
    }

    private static string ExtractCommunityProjectStemAlias(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        var stem = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return string.Empty;
        }

        var tokens = System.Text.RegularExpressions.Regex
            .Split(stem, @"[^A-Za-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
        if (tokens.Length == 0)
        {
            return string.Empty;
        }

        var kept = new List<string>();
        foreach (var token in tokens)
        {
            if (LooksLikeVersionToken(token))
            {
                break;
            }

            kept.Add(token);
        }

        return kept.Count == 0 ? stem : string.Join(' ', kept);
    }

    private static bool LooksLikeVersionToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+$"))
        {
            return true;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d+(?:[A-Za-z]?\d+)*(?:v\d+)?$");
    }

    private static void AddCommunityProjectInstallAlias(
        ISet<string> aliases,
        LauncherFrontendSubpageKey route,
        string alias)
    {
        if (!string.IsNullOrWhiteSpace(alias))
        {
            aliases.Add($"{route}:{alias}");
        }
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private sealed record CommunityProjectInstallPlan(
        IReadOnlyCollection<string> InstallAliases,
        string ProjectId,
        string Title,
        string ReleaseTitle,
        string ReleaseSummary,
        string SourceUrl,
        string TargetPath,
        string InstanceName,
        string TargetName,
        LauncherFrontendSubpageKey Route,
        string? ReplacedPath,
        bool IsCurrentInstanceTarget,
        bool IsDependency);

    private sealed record CommunityProjectInstallBuildResult(
        IReadOnlyList<CommunityProjectInstallPlan> Plans,
        IReadOnlyList<string> Skipped);

    private sealed record CommunityProjectInstallRootRequest(
        string ProjectId,
        string Title,
        LauncherFrontendSubpageKey Route,
        FrontendCommunityProjectState? ProjectState = null,
        FrontendCommunityProjectReleaseEntry? Release = null);

    private sealed record DownloadFavoriteInstallTargetSnapshot(
        InstanceSelectionSnapshot Instance,
        FrontendVersionSaveSelectionState? DatapackSaveSelection)
    {
        public string DisplayName => DatapackSaveSelection?.HasSelection == true
            ? $"{Instance.Name} • {DatapackSaveSelection.SaveName}"
            : Instance.Name;
    }

    private sealed record InstalledFavoriteResource(
        string Title,
        string Path,
        string Version,
        string Website,
        IReadOnlyCollection<string> InstallAliases);
}
