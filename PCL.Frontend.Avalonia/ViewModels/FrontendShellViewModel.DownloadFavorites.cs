using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

    public bool CanBatchInstallSelectedDownloadFavorites => HasSelectedDownloadFavorites;

    public ActionCommand SelectAllDownloadFavoritesCommand => new(SelectAllDownloadFavorites);

    public ActionCommand ClearDownloadFavoriteSelectionCommand => new(ClearDownloadFavoriteSelection);

    public ActionCommand RemoveSelectedDownloadFavoritesCommand => new(() => _ = RemoveSelectedDownloadFavoritesAsync());

    public ActionCommand FavoriteSelectedDownloadFavoritesToTargetCommand => new(() => _ = FavoriteSelectedDownloadFavoritesToTargetAsync());

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

    private async Task BatchInstallSelectedDownloadFavoritesAsync()
    {
        var selectedEntries = GetSelectedDownloadFavoriteCatalogEntries();
        if (selectedEntries.Count == 0)
        {
            AddActivity("批量安装收藏", "当前没有选中的收藏项目。");
            return;
        }

        var targetSnapshot = await PromptForDownloadFavoriteInstallTargetAsync(selectedEntries.Count);
        if (targetSnapshot is null)
        {
            return;
        }

        AddActivity("批量安装收藏", $"正在为 {targetSnapshot.Name} 分析 {selectedEntries.Count} 个收藏项目。");
        DownloadFavoriteBatchInstallBuildResult result;
        try
        {
            result = await Task.Run(() => BuildDownloadFavoriteBatchInstallResult(selectedEntries, targetSnapshot));
        }
        catch (Exception ex)
        {
            AddFailureActivity("批量安装收藏失败", ex.Message);
            return;
        }

        foreach (var plan in result.Plans)
        {
            RegisterDownloadFavoriteBatchInstallTask(plan);
        }

        var summaryParts = new List<string>();
        if (result.Plans.Count > 0)
        {
            summaryParts.Add($"已加入 {result.Plans.Count} 个安装任务");
        }

        if (result.Skipped.Count > 0)
        {
            summaryParts.Add($"跳过 {result.Skipped.Count} 个项目");
        }

        AddActivity(
            "批量安装收藏",
            $"{targetSnapshot.Name} • {(summaryParts.Count == 0 ? "没有可执行的安装任务。" : string.Join("，", summaryParts))}");

        foreach (var skipped in result.Skipped.Take(5))
        {
            AddActivity("批量安装收藏", skipped);
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

    private async Task<InstanceSelectionSnapshot?> PromptForDownloadFavoriteInstallTargetAsync(int selectedCount)
    {
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
                $"批量安装会根据目标实例的 Minecraft 版本与加载器，为这 {selectedCount} 个收藏选择推荐版本。",
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

        return string.IsNullOrWhiteSpace(selectedId)
            ? null
            : instances.FirstOrDefault(entry => string.Equals(entry.Name, selectedId, StringComparison.OrdinalIgnoreCase));
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

    private DownloadFavoriteBatchInstallBuildResult BuildDownloadFavoriteBatchInstallResult(
        IReadOnlyList<FrontendDownloadCatalogEntry> selectedEntries,
        InstanceSelectionSnapshot targetSnapshot)
    {
        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, targetSnapshot.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            return new DownloadFavoriteBatchInstallBuildResult([], [$"{targetSnapshot.Name} 当前不可用，无法执行批量安装。"]);
        }

        var plans = new List<DownloadFavoriteBatchInstallPlan>();
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

            var projectState = FrontendCommunityProjectService.GetProjectState(
                favorite.Identity,
                targetComposition.Selection.VanillaVersion,
                _selectedCommunityDownloadSourceIndex);
            var release = SelectPreferredCommunityProjectReleaseForTarget(
                projectState.Releases.Where(entry => entry.IsDirectDownload && !string.IsNullOrWhiteSpace(entry.Target)),
                targetComposition.Selection.VanillaVersion,
                targetSnapshot.LoaderLabel);
            if (release is null || string.IsNullOrWhiteSpace(release.Target))
            {
                skipped.Add($"已跳过 {favorite.Title}：没有找到适合 {targetSnapshot.Name} 的可安装版本。");
                continue;
            }

            var targetDirectory = ResolveCommunityProjectInstallDirectory(targetComposition.Selection, favorite.OriginSubpage.Value);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                skipped.Add($"已跳过 {favorite.Title}：{targetSnapshot.Name} 没有可用的安装目录。");
                continue;
            }

            Directory.CreateDirectory(targetDirectory);
            var targetFileName = SanitizeCommunityProjectReleaseFileName(release.SuggestedFileName, release.Title);
            var installed = FindInstalledFavoriteResource(targetComposition, favorite.OriginSubpage.Value, favorite, projectState);
            if (favorite.OriginSubpage != LauncherFrontendSubpageKey.DownloadWorld
                && installed is not null
                && !ShouldInstallFavoriteResourceUpdate(installed, targetFileName, release))
            {
                skipped.Add($"已跳过 {favorite.Title}：{targetSnapshot.Name} 中已安装相同或更新的版本。");
                continue;
            }

            var targetPath = favorite.OriginSubpage == LauncherFrontendSubpageKey.DownloadWorld
                ? GetUniqueChildPath(targetDirectory, targetFileName)
                : Path.Combine(targetDirectory, targetFileName);

            plans.Add(new DownloadFavoriteBatchInstallPlan(
                favorite.Title,
                release.Target!,
                targetPath,
                targetSnapshot.Name,
                favorite.OriginSubpage.Value,
                installed is not null && !string.Equals(installed.Path, targetPath, StringComparison.OrdinalIgnoreCase)
                    ? installed.Path
                    : null,
                string.Equals(targetSnapshot.Name, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase)));
        }

        return new DownloadFavoriteBatchInstallBuildResult(plans, skipped);
    }

    private void RegisterDownloadFavoriteBatchInstallTask(DownloadFavoriteBatchInstallPlan plan)
    {
        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            $"收藏批量安装：{plan.Title}",
            plan.SourceUrl,
            plan.TargetPath,
            ResolveDownloadRequestTimeout(),
            onStarted: _ => AvaloniaHintBus.Show($"开始安装到 {plan.InstanceName}", AvaloniaHintTheme.Info),
            onCompleted: _ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    CleanupReplacedDownloadFavoriteResource(plan.ReplacedPath);
                    if (plan.IsCurrentInstanceTarget)
                    {
                        ReloadInstanceComposition(reloadDependentCompositions: false, initializeAllSurfaces: false);
                    }

                    AddActivity("批量安装收藏", $"{plan.Title} -> {plan.TargetPath}");
                    AvaloniaHintBus.Show($"{plan.Title} 已安装到 {plan.InstanceName}", AvaloniaHintTheme.Success);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() => AddFailureActivity("批量安装收藏失败", $"{plan.Title} • {message}"));
            }));
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
        string? preferredLoader)
    {
        return releases
            .OrderByDescending(release => ReleaseMatchesExactInstanceVersion(release, NormalizeMinecraftVersion(preferredVersion)))
            .ThenByDescending(release => ReleaseMatchesExactInstanceLoader(release, preferredLoader))
            .ThenByDescending(release => release.PublishedUnixTime)
            .ThenBy(release => release.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private static string? ResolveCommunityProjectInstallDirectory(
        FrontendInstanceSelectionState selection,
        LauncherFrontendSubpageKey route)
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
            LauncherFrontendSubpageKey.DownloadDataPack => selection.IndieDirectory,
            _ => Path.Combine(selection.IndieDirectory, "mods")
        };
    }

    private static InstalledFavoriteResource? FindInstalledFavoriteResource(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route,
        FrontendDownloadCatalogEntry favorite,
        FrontendCommunityProjectState projectState)
    {
        if (route == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return null;
        }

        var installedResources = GetInstalledFavoriteResources(composition, route);
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
            string.Equals(entry.Title, favorite.Title, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<InstalledFavoriteResource> GetInstalledFavoriteResources(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => composition.Mods.Entries
                .Concat(composition.DisabledMods.Entries)
                .Select(entry => new InstalledFavoriteResource(entry.Title, entry.Path, entry.Version, entry.Website))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadResourcePack => composition.ResourcePacks.Entries
                .Select(entry => new InstalledFavoriteResource(entry.Title, entry.Path, entry.Version, entry.Website))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadShader => composition.Shaders.Entries
                .Select(entry => new InstalledFavoriteResource(entry.Title, entry.Path, entry.Version, entry.Website))
                .ToArray(),
            LauncherFrontendSubpageKey.DownloadDataPack => EnumerateDirectoryInstallArtifacts(composition.Selection.IndieDirectory),
            _ => []
        };
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
                string.Empty));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new InstalledFavoriteResource(
                Path.GetFileName(path),
                path,
                string.Empty,
                string.Empty));
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

    private sealed record DownloadFavoriteBatchInstallPlan(
        string Title,
        string SourceUrl,
        string TargetPath,
        string InstanceName,
        LauncherFrontendSubpageKey Route,
        string? ReplacedPath,
        bool IsCurrentInstanceTarget);

    private sealed record DownloadFavoriteBatchInstallBuildResult(
        IReadOnlyList<DownloadFavoriteBatchInstallPlan> Plans,
        IReadOnlyList<string> Skipped);

    private sealed record InstalledFavoriteResource(
        string Title,
        string Path,
        string Version,
        string Website);
}
