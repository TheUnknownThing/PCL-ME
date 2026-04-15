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
            AddActivity("分享所选", "分享了个寂寞啊！");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(JsonSerializer.Serialize(selectedIds));
            ClearDownloadFavoriteSelection();
            AddActivity("分享所选", $"已复制 {selectedIds.Length} 个收藏项目的分享代码。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("分享所选失败", ex.Message);
        }
    }

    private async Task BatchInstallSelectedDownloadFavoritesAsync()
    {
        var selectedEntries = GetSelectedDownloadFavoriteCatalogEntries();
        if (selectedEntries.Count == 0)
        {
            AddActivity("批量安装收藏", "当前没有选中的收藏项目。");
            return;
        }

        var targetSnapshot = await PromptForDownloadFavoriteInstallTargetAsync(selectedEntries);
        if (targetSnapshot is null)
        {
            return;
        }

        AvaloniaHintBus.Show($"正在分析 {selectedEntries.Count} 个收藏项目…", AvaloniaHintTheme.Info);
        AddActivity("批量安装收藏", $"正在为 {targetSnapshot.DisplayName} 分析 {selectedEntries.Count} 个收藏项目。");
        CommunityProjectInstallBuildResult result;
        try
        {
            result = await Task.Run(() => BuildDownloadFavoriteBatchInstallResult(selectedEntries, targetSnapshot));
        }
        catch (Exception ex)
        {
            AddFailureActivity("批量安装收藏失败", ex.Message);
            return;
        }

        var confirmed = await ConfirmDownloadFavoriteBatchInstallAsync(targetSnapshot, result);
        if (!confirmed)
        {
            AddActivity("批量安装收藏", "已取消加入批量安装任务。");
            return;
        }

        AvaloniaHintBus.Show("批量安装已开始，正在加入任务中心…", AvaloniaHintTheme.Info);
        AddActivity("批量安装收藏", $"{targetSnapshot.DisplayName} • 正在把分析完成的资源加入任务中心。");
        foreach (var plan in result.Plans)
        {
            RegisterDownloadFavoriteBatchInstallTask(plan);
        }

        var summaryParts = new List<string>();
        if (result.Plans.Count > 0)
        {
            summaryParts.Add($"已加入 {result.Plans.Count} 个安装任务");
            var dependencyCount = result.Plans.Count(plan => plan.IsDependency);
            if (dependencyCount > 0)
            {
                summaryParts.Add($"包含 {dependencyCount} 个依赖");
            }
        }

        if (result.Skipped.Count > 0)
        {
            summaryParts.Add($"跳过 {result.Skipped.Count} 个项目");
        }

        AddActivity(
            "批量安装收藏",
            $"{targetSnapshot.DisplayName} • {(summaryParts.Count == 0 ? "没有可执行的安装任务。" : string.Join("，", summaryParts))}");

        foreach (var skipped in result.Skipped.Take(5))
        {
            AddActivity("批量安装收藏", skipped);
        }
    }

    private async Task<bool> ConfirmDownloadFavoriteBatchInstallAsync(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(
                "确认批量安装",
                BuildDownloadFavoriteBatchInstallConfirmationMessage(targetSnapshot, result),
                "开始安装");
        }
        catch (Exception ex)
        {
            AddFailureActivity("批量安装确认失败", ex.Message);
            return false;
        }
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
            return new FrontendDownloadFavoriteTargetState("默认收藏夹", "default", []);
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
            AddActivity("批量安装收藏", "当前没有可安装的实例。");
            return null;
        }

        string? selectedId;
        try
        {
            selectedId = await _shellActionService.PromptForChoiceAsync(
                "选择安装目标版本",
                includesDatapacks
                    ? $"批量安装会根据目标实例的 Minecraft 版本与加载器，为这 {selectedCount} 个收藏选择推荐版本。数据包会在下一步继续选择目标存档。"
                    : $"批量安装会根据目标实例的 Minecraft 版本与加载器，为这 {selectedCount} 个收藏选择推荐版本。",
                instances.Select(entry => new PclChoiceDialogOption(
                    entry.Name,
                    entry.Name,
                    entry.Subtitle))
                    .ToArray(),
                _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : instances[0].Name,
                "开始安装");
        }
        catch (Exception ex)
        {
            AddFailureActivity("选择安装目标失败", ex.Message);
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
            AddActivity("批量安装收藏", $"{instanceSnapshot.Name} 当前不可用，无法选择数据包存档。");
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
            AddActivity("批量安装收藏", $"{instanceSnapshot.Name} 当前没有可用的存档，无法安装数据包。");
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
                "选择数据包安装存档",
                $"数据包需要安装到具体存档的 datapacks 文件夹中。请选择 {instanceSnapshot.Name} 中的目标存档。",
                saves.Select(entry => new PclChoiceDialogOption(
                    entry.Path,
                    entry.Title,
                    entry.Summary))
                    .ToArray(),
                defaultSavePath,
                "开始安装");
        }
        catch (Exception ex)
        {
            AddFailureActivity("选择数据包存档失败", ex.Message);
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedSavePath))
        {
            return null;
        }

        var selection = FrontendVersionSavesCompositionService.Compose(targetComposition, selectedSavePath).Selection;
        if (!selection.HasSelection)
        {
            AddActivity("批量安装收藏", "未能解析所选存档的数据包目录。");
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

    private CommunityProjectInstallBuildResult BuildDownloadFavoriteBatchInstallResult(
        IReadOnlyList<FrontendDownloadCatalogEntry> selectedEntries,
        DownloadFavoriteInstallTargetSnapshot targetSnapshot)
    {
        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, targetSnapshot.Instance.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            return new CommunityProjectInstallBuildResult([], [$"{targetSnapshot.Instance.Name} 当前不可用，无法执行批量安装。"]);
        }

        var roots = new List<CommunityProjectInstallRootRequest>();
        var skipped = new List<string>();
        foreach (var favorite in selectedEntries)
        {
            if (string.IsNullOrWhiteSpace(favorite.Identity))
            {
                skipped.Add($"已跳过 {favorite.Title}：缺少项目标识。");
                continue;
            }

            if (favorite.OriginSubpage is null)
            {
                skipped.Add($"已跳过 {favorite.Title}：无法识别资源类型。");
                continue;
            }

            if (favorite.OriginSubpage == LauncherFrontendSubpageKey.DownloadPack)
            {
                skipped.Add($"已跳过 {favorite.Title}：整合包暂不支持收藏夹批量安装。");
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
        RegisterCommunityProjectInstallTask(plan, "批量安装收藏");
    }

    private void RegisterCommunityProjectInstallTask(CommunityProjectInstallPlan plan, string activityTitle)
    {
        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            $"{activityTitle}：{plan.Title}",
            plan.SourceUrl,
            plan.TargetPath,
            ResolveDownloadRequestTimeout(),
            onStarted: _ => AvaloniaHintBus.Show($"开始安装到 {plan.TargetName}", AvaloniaHintTheme.Info),
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

                    AddActivity(activityTitle, $"{plan.Title} -> {installedPath}");
                    AvaloniaHintBus.Show($"{plan.Title} 已安装到 {plan.TargetName}", AvaloniaHintTheme.Success);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() => AddFailureActivity($"{activityTitle}失败", $"{plan.Title} • {message}"));
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
            return new CommunityProjectInstallBuildResult([], ["目标实例当前不可用。"]);
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
                skipped.Add($"已跳过 {request.Title}：缺少项目标识。");
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
                    skipped.Add($"已跳过 {projectTitle}：没有找到适合 {installTargetName} 的可安装版本。");
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
                            skipped.Add($"已跳过 {projectTitle}：必需依赖 {dependency.Title} 没有可安装版本。");
                            resolvedResults[requestKey] = false;
                            return false;
                        }
                    }
                }

                var targetDirectory = ResolveCommunityProjectInstallDirectory(targetComposition.Selection, request.Route, datapackSaveSelection);
                if (string.IsNullOrWhiteSpace(targetDirectory))
                {
                    skipped.Add($"已跳过 {projectTitle}：{installTargetName} 没有可用的安装目录。");
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
                    skipped.Add($"已跳过 {projectTitle}：{installTargetName} 中已安装相同或更新的版本。");
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
                skipped.Add($"已跳过 {request.Title}：{ex.Message}");
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

    private static string BuildDownloadFavoriteBatchInstallConfirmationMessage(
        DownloadFavoriteInstallTargetSnapshot targetSnapshot,
        CommunityProjectInstallBuildResult result)
    {
        return BuildCommunityProjectInstallConfirmationMessage(targetSnapshot.DisplayName, result);
    }

    private static string BuildCommunityProjectInstallConfirmationMessage(
        string targetName,
        CommunityProjectInstallBuildResult result)
    {
        var lines = new List<string>
        {
            $"安装目标：{targetName}",
            $"准备安装：{result.Plans.Count} 项"
        };

        if (result.Plans.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("将安装的版本：");
            foreach (var plan in result.Plans)
            {
                var releaseText = string.IsNullOrWhiteSpace(plan.ReleaseSummary)
                    ? plan.ReleaseTitle
                    : $"{plan.ReleaseTitle} | {plan.ReleaseSummary}";
                lines.Add(plan.IsDependency
                    ? $"• {plan.Title} -> {releaseText}（依赖）"
                    : $"• {plan.Title} -> {releaseText}");
            }
        }

        if (result.Skipped.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("未安装 / 缺少兼容版本：");
            foreach (var skipped in result.Skipped)
            {
                lines.Add($"• {skipped}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("确认后会把以上资源加入任务中心。");
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
