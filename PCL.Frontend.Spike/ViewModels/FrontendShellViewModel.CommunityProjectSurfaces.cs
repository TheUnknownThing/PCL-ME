using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] CommunityProjectKnownLoaders = ["Forge", "NeoForge", "Fabric", "Quilt", "OptiFine", "Iris"];

    private string _selectedCommunityProjectId = string.Empty;
    private LauncherFrontendSubpageKey? _selectedCommunityProjectOriginSubpage;
    private string _selectedCommunityProjectVersionFilter = string.Empty;
    private string _selectedCommunityProjectLoaderFilter = string.Empty;
    private Bitmap? _communityProjectIcon;
    private FrontendCommunityProjectState _communityProjectState = new(
        string.Empty,
        "未选择工程",
        "先从收藏夹中选择一个工程，再查看对应详情。",
        string.Empty,
        "未指定来源",
        null,
        null,
        string.Empty,
        "等待选择",
        "尚未加载",
        0,
        0,
        "未提供兼容信息",
        "未提供标签信息",
        [],
        [],
        string.Empty,
        false);
    private FrontendHomePageMarketState _homePageMarketState = new(
        "主页市场会聚合实时社区资源，方便直接浏览热门内容。",
        [],
        string.Empty,
        false);

    public ObservableCollection<DownloadCatalogActionViewModel> CommunityProjectActionButtons { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> CommunityProjectVersionFilterButtons { get; } = [];

    public ObservableCollection<DownloadCatalogActionViewModel> CommunityProjectLoaderFilterButtons { get; } = [];

    public ObservableCollection<CommunityProjectReleaseGroupViewModel> CommunityProjectReleaseGroups { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> CommunityProjectSections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> HomePageMarketSections { get; } = [];

    public bool ShowCompDetailSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.CompDetail;

    public bool ShowHomePageMarketSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.HomePageMarket;

    public string CommunityProjectTitle => _communityProjectState.Title;

    public string CommunityProjectSummary => _communityProjectState.Summary;

    public string CommunityProjectDescription => _communityProjectState.Description;

    public string CommunityProjectSource => _communityProjectState.Source;

    public Bitmap? CommunityProjectIcon => _communityProjectIcon;

    public bool HasCommunityProjectIcon => CommunityProjectIcon is not null;

    public string CommunityProjectWebsite => _communityProjectState.Website;

    public string CommunityProjectStatus => _communityProjectState.Status;

    public string CommunityProjectUpdatedLabel => _communityProjectState.UpdatedLabel;

    public string CommunityProjectCompatibilitySummary => _communityProjectState.CompatibilitySummary;

    public string CommunityProjectCategorySummary => _communityProjectState.CategorySummary;

    public string CommunityProjectDownloadCountLabel => _communityProjectState.DownloadCount <= 0
        ? "暂无"
        : FormatCompactCount(_communityProjectState.DownloadCount);

    public string CommunityProjectFollowCountLabel => _communityProjectState.FollowCount <= 0
        ? "暂无"
        : FormatCompactCount(_communityProjectState.FollowCount);

    public bool HasCommunityProjectDescription => !string.IsNullOrWhiteSpace(CommunityProjectDescription);

    public string CommunityProjectIntroDescription => !string.IsNullOrWhiteSpace(CommunityProjectSummary)
        ? CommunityProjectSummary
        : CommunityProjectDescription;

    public IReadOnlyList<string> CommunityProjectCategoryTags => BuildCommunityProjectCategoryTags(CommunityProjectCategorySummary);

    public bool HasCommunityProjectCategoryTags => CommunityProjectCategoryTags.Count > 0;

    public string CommunityProjectSourceBadgeText => CommunityProjectSource switch
    {
        "CurseForge" => "CF",
        "Modrinth" => "MR",
        _ => "?"
    };

    public bool HasCommunityProjectActionButtons => CommunityProjectActionButtons.Count > 0;

    public bool ShowCommunityProjectLoadingCard => _isCommunityProjectLoading;

    public bool ShowCommunityProjectContent => !_isCommunityProjectLoading;

    public string CommunityProjectLoadingText
    {
        get => _communityProjectLoadingText;
        private set => SetProperty(ref _communityProjectLoadingText, value);
    }

    public bool ShowCommunityProjectFilterCard => ShowCommunityProjectVersionFilters || ShowCommunityProjectLoaderFilters;

    public bool ShowCommunityProjectVersionFilters => CommunityProjectVersionFilterButtons.Count > 2;

    public bool ShowCommunityProjectLoaderFilters => CommunityProjectLoaderFilterButtons.Count > 2;

    public bool HasCommunityProjectReleaseGroups => CommunityProjectReleaseGroups.Count > 0;

    public bool HasNoCommunityProjectReleaseGroups => !HasCommunityProjectReleaseGroups;

    public bool HasCommunityProjectSections => CommunityProjectSections.Count > 0;

    public bool HasNoCommunityProjectSections => !HasCommunityProjectSections;

    public bool ShowCommunityProjectWarning => _communityProjectState.ShowWarning;

    public string CommunityProjectWarningText => _communityProjectState.WarningText;

    public string HomePageMarketSummary => _homePageMarketState.Summary;

    public bool HasHomePageMarketSections => HomePageMarketSections.Count > 0;

    public bool HasNoHomePageMarketSections => !HasHomePageMarketSections;

    public bool ShowHomePageMarketWarning => _homePageMarketState.ShowWarning;

    public string HomePageMarketWarningText => _homePageMarketState.WarningText;

    public void OpenCommunityProjectDetail(
        string projectId,
        string? projectTitle = null,
        string? initialVersionFilter = null,
        string? initialLoaderFilter = null,
        LauncherFrontendSubpageKey? originSubpage = null)
    {
        _selectedCommunityProjectId = projectId.Trim();
        _selectedCommunityProjectTitleHint = projectTitle?.Trim() ?? string.Empty;
        _selectedCommunityProjectOriginSubpage = originSubpage ?? _selectedCommunityProjectOriginSubpage;
        _selectedCommunityProjectVersionFilter = NormalizeMinecraftVersion(initialVersionFilter) ?? initialVersionFilter?.Trim() ?? string.Empty;
        _selectedCommunityProjectLoaderFilter = initialLoaderFilter?.Trim() ?? string.Empty;
        var activityMessage = string.IsNullOrWhiteSpace(projectTitle)
            ? "已打开资源工程详情。"
            : $"已打开 {projectTitle} 的资源详情。";
        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail)
        {
            RefreshShell(activityMessage);
            return;
        }

        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.CompDetail), activityMessage);
    }

    private void RefreshCompDetailSurface()
    {
        CommunityProjectLoadingText = "正在获取版本列表";
        if (string.IsNullOrWhiteSpace(_selectedCommunityProjectId))
        {
            _communityProjectState = new FrontendCommunityProjectState(
                string.Empty,
                "未选择工程",
                "先从收藏夹或市场条目进入，才能查看对应工程详情。",
                string.Empty,
                "未指定来源",
                null,
                null,
                string.Empty,
                "等待选择",
                "尚未加载",
                0,
                0,
                "未提供兼容信息",
                "未提供标签信息",
                [],
                [],
                "当前详情页没有携带工程标识。",
                true);
            SetCommunityProjectLoading(false);
            ApplyCommunityProjectIcon();
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            return;
        }

        var projectId = _selectedCommunityProjectId;
        var title = string.IsNullOrWhiteSpace(_selectedCommunityProjectTitleHint)
            ? $"项目 {projectId}"
            : _selectedCommunityProjectTitleHint;
        _communityProjectState = new FrontendCommunityProjectState(
            projectId,
            title,
            title,
            string.Empty,
            "正在加载",
            null,
            null,
            string.Empty,
            "正在加载",
            "尚未加载",
            0,
            0,
            "未提供兼容信息",
            "未提供标签信息",
            [],
            [],
            string.Empty,
            false);
        ReplaceItems(CommunityProjectActionButtons, []);
        ReplaceItems(CommunityProjectVersionFilterButtons, []);
        ReplaceItems(CommunityProjectLoaderFilterButtons, []);
        ReplaceItems(CommunityProjectReleaseGroups, []);
        ReplaceItems(CommunityProjectSections, []);
        SetCommunityProjectLoading(true);
        ApplyCommunityProjectIcon();
        RaiseCommunityProjectProperties();

        var refreshVersion = ++_communityProjectRefreshVersion;
        var preferredMinecraftVersion = _instanceComposition.Selection.VanillaVersion;
        var communitySourcePreference = _selectedCommunityDownloadSourceIndex;
        _ = LoadCommunityProjectStateAsync(projectId, preferredMinecraftVersion, communitySourcePreference, refreshVersion);
    }

    private async Task LoadCommunityProjectStateAsync(
        string projectId,
        string preferredMinecraftVersion,
        int communitySourcePreference,
        int refreshVersion)
    {
        var state = await Task.Run(() => FrontendCommunityProjectService.GetProjectState(
            projectId,
            preferredMinecraftVersion,
            communitySourcePreference));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _communityProjectRefreshVersion
                || !string.Equals(projectId, _selectedCommunityProjectId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _communityProjectState = state;
            SetCommunityProjectLoading(false);
            ApplyCommunityProjectIcon();
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
        });
    }

    private void RebuildCommunityProjectSurfaceCollections()
    {
        ReplaceItems(CommunityProjectActionButtons, BuildCommunityProjectActionButtons());

        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        var versionOptions = BuildCommunityProjectVersionOptions(versionGrouping);
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        _selectedCommunityProjectVersionFilter = ResolveCommunityProjectVersionFilter(versionOptions, versionGrouping, preferredVersion);

        var loaderOptions = BuildCommunityProjectLoaderOptions();
        _selectedCommunityProjectLoaderFilter = ResolveCommunityProjectLoaderFilter(loaderOptions);

        ReplaceItems(
            CommunityProjectVersionFilterButtons,
            BuildCommunityProjectFilterButtons(
                versionOptions,
                _selectedCommunityProjectVersionFilter,
                "全部",
                SelectCommunityProjectVersionFilter));
        ReplaceItems(
            CommunityProjectLoaderFilterButtons,
            BuildCommunityProjectFilterButtons(
                loaderOptions,
                _selectedCommunityProjectLoaderFilter,
                "全部",
                SelectCommunityProjectLoaderFilter));
        ReplaceItems(CommunityProjectReleaseGroups, BuildCommunityProjectReleaseGroups(versionGrouping));
        ReplaceItems(
            CommunityProjectSections,
            _communityProjectState.Links.Count == 0
                ? []
                :
                [
                    new DownloadCatalogSectionViewModel(
                        "相关链接",
                        _communityProjectState.Links
                            .Select(entry => new DownloadCatalogEntryViewModel(
                                entry.Title,
                                entry.Info,
                                entry.Meta,
                                entry.ActionText,
                                CreateProjectSectionCommand(entry)))
                            .ToArray())
                ]);
    }

    private IReadOnlyList<DownloadCatalogActionViewModel> BuildCommunityProjectActionButtons()
    {
        var buttons = new List<DownloadCatalogActionViewModel>();
        if (!string.IsNullOrWhiteSpace(CommunityProjectWebsite))
        {
            buttons.Add(new DownloadCatalogActionViewModel(
                CommunityProjectSource,
                PclButtonColorState.Highlight,
                CreateOpenTargetCommand($"打开项目主页: {CommunityProjectTitle}", CommunityProjectWebsite, CommunityProjectWebsite)));
        }
        else
        {
            buttons.Add(new DownloadCatalogActionViewModel(
                CommunityProjectSource,
                PclButtonColorState.Normal,
                CreateIntentCommand($"查看来源: {CommunityProjectTitle}", CommunityProjectSource)));
        }

        buttons.Add(new DownloadCatalogActionViewModel(
            "MC 百科",
            PclButtonColorState.Normal,
            CreateOpenTargetCommand(
                $"打开 MC 百科: {CommunityProjectTitle}",
                BuildCommunityProjectEncyclopediaUrl(CommunityProjectTitle),
                CommunityProjectTitle)));
        buttons.Add(new DownloadCatalogActionViewModel(
            "复制名称",
            PclButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync("复制项目名称", CommunityProjectTitle, "没有可复制的项目名称。"))));
        buttons.Add(new DownloadCatalogActionViewModel(
            "复制链接",
            PclButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                "复制项目链接",
                string.IsNullOrWhiteSpace(CommunityProjectWebsite)
                    ? FrontendCommunityProjectService.CreateCompDetailTarget(_communityProjectState.ProjectId)
                    : CommunityProjectWebsite,
                "当前项目没有可复制的外部链接。"))));
        buttons.Add(new DownloadCatalogActionViewModel(
            "翻译简介",
            PclButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                "翻译简介",
                BuildCommunityProjectDescriptionCopyText(),
                "当前项目没有可供翻译的简介文本。"))));
        buttons.Add(new DownloadCatalogActionViewModel(
            IsCommunityProjectFavorite() ? "已收藏" : "收藏",
            IsCommunityProjectFavorite() ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
            new ActionCommand(ToggleCommunityProjectFavorite)));
        return buttons;
    }

    private static IReadOnlyList<DownloadCatalogActionViewModel> BuildCommunityProjectFilterButtons(
        IReadOnlyList<string> options,
        string selectedValue,
        string allLabel,
        Action<string> applyFilter)
    {
        var buttons = new List<DownloadCatalogActionViewModel>
        {
            new(
                allLabel,
                string.IsNullOrWhiteSpace(selectedValue) ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
                new ActionCommand(() => applyFilter(string.Empty)))
        };
        buttons.AddRange(options.Select(option => new DownloadCatalogActionViewModel(
            option,
            string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase) ? PclButtonColorState.Highlight : PclButtonColorState.Normal,
            new ActionCommand(() => applyFilter(option)))));
        return buttons;
    }

    private static IReadOnlyList<string> BuildCommunityProjectCategoryTags(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary)
            || string.Equals(summary, "未提供标签信息", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return summary
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private IReadOnlyList<CommunityProjectReleaseGroupViewModel> BuildCommunityProjectReleaseGroups((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var showLoaderPrefix = BuildCommunityProjectLoaderOptions().Count > 1;
        var groups = new Dictionary<string, List<FrontendCommunityProjectReleaseEntry>>(StringComparer.OrdinalIgnoreCase);
        var dedupe = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var release in _communityProjectState.Releases)
        {
            var gameVersions = release.GameVersions.Count == 0 ? ["其他"] : release.GameVersions;
            var loaders = release.Loaders.Count == 0 ? [string.Empty] : release.Loaders;

            foreach (var gameVersion in gameVersions)
            {
                var groupedVersion = GetGroupedCommunityProjectVersionName(gameVersion, versionGrouping.GroupByDrop, versionGrouping.FoldOld);
                if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
                    && !string.Equals(groupedVersion, _selectedCommunityProjectVersionFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalizedVersion = NormalizeMinecraftVersion(gameVersion)
                                        ?? (string.IsNullOrWhiteSpace(gameVersion) ? "其他" : gameVersion.Trim());
                foreach (var loader in loaders)
                {
                    if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
                        && !string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var title = string.IsNullOrWhiteSpace(loader) || !showLoaderPrefix
                        ? normalizedVersion
                        : $"{loader} {normalizedVersion}";
                    AddCommunityProjectReleaseGroupEntry(groups, dedupe, title, release);
                }
            }
        }

        var orderedGroups = groups
            .OrderByDescending(pair => GetCommunityProjectGroupPriority(pair.Key))
            .ThenByDescending(pair => ParseVersion(ExtractCommunityProjectGroupVersion(pair.Key)))
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var shouldAutoExpandSingleGroup = orderedGroups.Length == 1;
        return orderedGroups
            .Select(pair => new CommunityProjectReleaseGroupViewModel(
                pair.Key,
                shouldAutoExpandSingleGroup || ShouldExpandCommunityProjectReleaseGroup(pair.Key),
                pair.Value
                    .OrderByDescending(entry => entry.PublishedUnixTime)
                    .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new DownloadCatalogEntryViewModel(
                        entry.Title,
                        entry.Info,
                        entry.Meta,
                        entry.ActionText,
                        entry.IsDirectDownload && !string.IsNullOrWhiteSpace(entry.Target)
                            ? CreateCommunityProjectReleaseDownloadCommand(entry)
                            : string.IsNullOrWhiteSpace(entry.Target)
                                ? CreateIntentCommand(entry.Title, entry.Info)
                                : CreateOpenTargetCommand($"打开文件: {entry.Title}", entry.Target, entry.Target)))
                    .ToArray()))
            .ToArray();
    }

    private bool ShouldExpandCommunityProjectReleaseGroup(string groupTitle)
    {
        if (string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            && string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter))
        {
            return false;
        }

        var matchesVersion = string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            || string.Equals(
                ExtractCommunityProjectGroupVersion(groupTitle),
                _selectedCommunityProjectVersionFilter,
                StringComparison.OrdinalIgnoreCase);
        var matchesLoader = string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
            || string.Equals(
                ExtractCommunityProjectGroupLoader(groupTitle),
                _selectedCommunityProjectLoaderFilter,
                StringComparison.OrdinalIgnoreCase);
        return matchesVersion && matchesLoader;
    }

    private static void AddCommunityProjectReleaseGroupEntry(
        IDictionary<string, List<FrontendCommunityProjectReleaseEntry>> groups,
        IDictionary<string, HashSet<string>> dedupe,
        string title,
        FrontendCommunityProjectReleaseEntry release)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "其他" : title;
        if (!groups.TryGetValue(normalizedTitle, out var entries))
        {
            entries = [];
            groups[normalizedTitle] = entries;
            dedupe[normalizedTitle] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var key = $"{release.Title}|{release.Target}|{release.PublishedUnixTime}";
        if (dedupe[normalizedTitle].Add(key))
        {
            entries.Add(release);
        }
    }

    private (bool GroupByDrop, bool FoldOld) DetermineCommunityProjectVersionGrouping(
        IReadOnlyList<FrontendCommunityProjectReleaseEntry> releases)
    {
        if (releases.Count == 0)
        {
            return (false, false);
        }

        var versions = releases
            .SelectMany(release => release.GameVersions)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var exactCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, false, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (exactCount < 9)
        {
            return (false, false);
        }

        var groupedCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, true, false))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (groupedCount < 9)
        {
            return (true, false);
        }

        var foldedCount = versions
            .Select(version => GetGroupedCommunityProjectVersionName(version, false, true))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (foldedCount < 9)
        {
            return (false, true);
        }

        return (true, true);
    }

    private IReadOnlyList<string> BuildCommunityProjectVersionOptions((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return _communityProjectState.Releases
            .SelectMany(release => release.GameVersions.Count == 0 ? ["其他"] : release.GameVersions)
            .Select(version => GetGroupedCommunityProjectVersionName(version, versionGrouping.GroupByDrop, versionGrouping.FoldOld))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(value => GetCommunityProjectVersionSortPriority(value))
            .ThenByDescending(value => ParseVersion(NormalizeMinecraftVersion(value) ?? value))
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildCommunityProjectLoaderOptions()
    {
        return _communityProjectState.Releases
            .SelectMany(release => release.Loaders)
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(loader => loader, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string ResolveCommunityProjectVersionFilter(
        IReadOnlyList<string> versionOptions,
        (bool GroupByDrop, bool FoldOld) versionGrouping,
        string? preferredVersion)
    {
        if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter))
        {
            if (versionOptions.Any(option => string.Equals(option, _selectedCommunityProjectVersionFilter, StringComparison.OrdinalIgnoreCase)))
            {
                return _selectedCommunityProjectVersionFilter;
            }

            var grouped = GetGroupedCommunityProjectVersionName(
                _selectedCommunityProjectVersionFilter,
                versionGrouping.GroupByDrop,
                versionGrouping.FoldOld);
            if (versionOptions.Any(option => string.Equals(option, grouped, StringComparison.OrdinalIgnoreCase)))
            {
                return grouped;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var groupedPreferred = GetGroupedCommunityProjectVersionName(preferredVersion, versionGrouping.GroupByDrop, versionGrouping.FoldOld);
            if (versionOptions.Any(option => string.Equals(option, groupedPreferred, StringComparison.OrdinalIgnoreCase)))
            {
                return groupedPreferred;
            }
        }

        return string.Empty;
    }

    private string ResolveCommunityProjectLoaderFilter(IReadOnlyList<string> loaderOptions)
    {
        return loaderOptions.Any(option => string.Equals(option, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
            ? _selectedCommunityProjectLoaderFilter
            : string.Empty;
    }

    private void SelectCommunityProjectVersionFilter(string filterValue)
    {
        _selectedCommunityProjectVersionFilter = filterValue;
        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private void SelectCommunityProjectLoaderFilter(string filterValue)
    {
        _selectedCommunityProjectLoaderFilter = filterValue;
        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private ActionCommand CreateProjectSectionCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand(entry.Title, entry.Info)
            : CreateOpenTargetCommand($"打开项目内容: {entry.Title}", entry.Target, entry.Target);
    }

    private ActionCommand CreateCommunityProjectReleaseDownloadCommand(FrontendCommunityProjectReleaseEntry entry)
    {
        return new ActionCommand(() => _ = DownloadCommunityProjectReleaseAsync(entry));
    }

    private async Task DownloadCommunityProjectReleaseAsync(FrontendCommunityProjectReleaseEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            AddActivity($"下载资源文件: {entry.Title}", "当前版本没有可用的下载地址。");
            return;
        }

        var suggestedFileName = SanitizeCommunityProjectReleaseFileName(entry.SuggestedFileName, entry.Title);
        var extension = Path.GetExtension(suggestedFileName);
        var patterns = string.IsNullOrWhiteSpace(extension) ? Array.Empty<string>() : [$"*{extension}"];
        var suggestedStartFolder = ResolveCommunityProjectDownloadStartDirectory();

        string? targetPath;
        try
        {
            targetPath = await _shellActionService.PickSaveFileAsync(
                "选择保存位置",
                suggestedFileName,
                "资源文件",
                suggestedStartFolder,
                patterns);
        }
        catch (Exception ex)
        {
            AddActivity($"选择保存位置失败: {entry.Title}", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            AddActivity($"已取消下载: {entry.Title}", "没有选择保存位置。");
            return;
        }

        TaskCenter.Register(new FrontendManagedFileDownloadTask(
            $"下载资源文件：{Path.GetFileNameWithoutExtension(targetPath)}",
            entry.Target,
            targetPath,
            onStarted: filePath => SpikeHintBus.Show($"开始下载 {Path.GetFileName(filePath)}", SpikeHintTheme.Info),
            onCompleted: filePath => SpikeHintBus.Show($"{Path.GetFileName(filePath)} 下载完成", SpikeHintTheme.Success),
            onFailed: message => SpikeHintBus.Show(message, SpikeHintTheme.Error)));
        AddActivity($"开始下载资源文件: {entry.Title}", targetPath);
    }

    private static string SanitizeCommunityProjectReleaseFileName(string? suggestedFileName, string fallbackTitle)
    {
        var candidate = string.IsNullOrWhiteSpace(suggestedFileName) ? fallbackTitle : suggestedFileName.Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(candidate.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "community-resource-download" : cleaned;
    }

    private async Task CopyCommunityProjectTextAsync(string title, string text, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AddActivity(title, emptyMessage);
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(text);
            AddActivity(title, "已复制到剪贴板。");
        }
        catch (Exception ex)
        {
            AddActivity($"{title} 失败", ex.Message);
        }
    }

    private string BuildCommunityProjectDescriptionCopyText()
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { CommunityProjectSummary, CommunityProjectDescription }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void ToggleCommunityProjectFavorite()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            AddActivity("收藏项目", "当前没有可收藏的项目。");
            return;
        }

        try
        {
            var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
            var raw = provider.Exists("CompFavorites")
                ? SafeReadSharedValue(provider, "CompFavorites", "[]")
                : "[]";
            var root = ParseCommunityProjectFavoriteTargets(raw);
            var target = EnsureCommunityProjectFavoriteTarget(root);
            var favorites = EnsureCommunityProjectFavoriteArray(target);

            var projectId = _communityProjectState.ProjectId;
            var existing = favorites
                .FirstOrDefault(node => string.Equals(GetCommunityProjectFavoriteId(node), projectId, StringComparison.OrdinalIgnoreCase));
            var added = existing is null;
            if (added)
            {
                favorites.Add(projectId);
            }
            else
            {
                favorites.Remove(existing);
            }

            _shellActionService.PersistSharedValue("CompFavorites", root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = false
            }));
            ReloadDownloadComposition();
            RefreshDownloadFavoriteSurface();
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            AddActivity(added ? "加入收藏夹" : "移出收藏夹", CommunityProjectTitle);
        }
        catch (Exception ex)
        {
            AddActivity("收藏项目失败", ex.Message);
        }
    }

    private bool IsCommunityProjectFavorite()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            return false;
        }

        try
        {
            var provider = new JsonFileProvider(_shellActionService.RuntimePaths.SharedConfigPath);
            var raw = provider.Exists("CompFavorites")
                ? SafeReadSharedValue(provider, "CompFavorites", "[]")
                : "[]";
            var root = ParseCommunityProjectFavoriteTargets(raw);
            return root
                .OfType<JsonObject>()
                .SelectMany(node =>
                {
                    var favoritesNode = node["Favs"] as JsonArray ?? node["Favorites"] as JsonArray;
                    return favoritesNode?.Select(GetCommunityProjectFavoriteId) ?? Enumerable.Empty<string?>();
                })
                .Any(value => string.Equals(value, _communityProjectState.ProjectId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static JsonArray ParseCommunityProjectFavoriteTargets(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonNode.Parse(raw);
            if (parsed is JsonArray rootArray)
            {
                if (rootArray.Any(node => node is JsonObject))
                {
                    return rootArray;
                }

                // Migrate the old flat string-array format into the default target format.
                return
                [
                    new JsonObject
                    {
                        ["Name"] = "默认收藏夹",
                        ["Id"] = "default",
                        ["Favs"] = new JsonArray(rootArray.Select(node => node?.DeepClone()).ToArray()),
                        ["Notes"] = new JsonObject()
                    }
                ];
            }
        }
        catch
        {
            // Ignore malformed favorite payloads and rebuild a default structure.
        }

        return [];
    }

    private static JsonObject EnsureCommunityProjectFavoriteTarget(JsonArray root)
    {
        var existing = root.OfType<JsonObject>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new JsonObject
        {
            ["Name"] = "默认收藏夹",
            ["Id"] = "default",
            ["Favs"] = new JsonArray(),
            ["Notes"] = new JsonObject()
        };
        root.Add(created);
        return created;
    }

    private static JsonArray EnsureCommunityProjectFavoriteArray(JsonObject target)
    {
        if (target["Favs"] is JsonArray favorites)
        {
            return favorites;
        }

        if (target["Favorites"] is JsonArray legacyFavorites)
        {
            target["Favs"] = legacyFavorites;
            target.Remove("Favorites");
            return legacyFavorites;
        }

        var created = new JsonArray();
        target["Favs"] = created;
        return created;
    }

    private static string SafeReadSharedValue(JsonFileProvider provider, string key, string fallback)
    {
        try
        {
            return provider.Get<string>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static string? GetCommunityProjectFavoriteId(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCommunityProjectEncyclopediaUrl(string title)
    {
        return $"https://www.mcmod.cn/s?key={Uri.EscapeDataString(title)}";
    }

    private static int GetCommunityProjectVersionSortPriority(string value)
    {
        return value switch
        {
            "其他" => 0,
            "远古版" => 1,
            "快照版" => 2,
            _ => 3
        };
    }

    private int GetCommunityProjectGroupPriority(string groupTitle)
    {
        var version = ExtractCommunityProjectGroupVersion(groupTitle);
        var priority = GetCommunityProjectVersionSortPriority(version) * 10;
        if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            && string.Equals(version, _selectedCommunityProjectVersionFilter, StringComparison.OrdinalIgnoreCase))
        {
            priority += 3;
        }

        var loader = ExtractCommunityProjectGroupLoader(groupTitle);
        if (!string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
            && string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase))
        {
            priority += 2;
        }

        return priority;
    }

    private static string ExtractCommunityProjectGroupVersion(string groupTitle)
    {
        var parts = groupTitle.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && CommunityProjectKnownLoaders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            ? parts[1]
            : groupTitle;
    }

    private static string ExtractCommunityProjectGroupLoader(string groupTitle)
    {
        var parts = groupTitle.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && CommunityProjectKnownLoaders.Contains(parts[0], StringComparer.OrdinalIgnoreCase)
            ? parts[0]
            : string.Empty;
    }

    private static string GetGroupedCommunityProjectVersionName(string? version, bool groupByDrop, bool foldOld)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "其他";
        }

        var trimmed = version.Trim();
        if (trimmed.Contains('w', StringComparison.OrdinalIgnoreCase))
        {
            return "快照版";
        }

        if (!IsCommunityProjectVersionFormat(trimmed))
        {
            return "其他";
        }

        var drop = GetCommunityProjectVersionDrop(trimmed);
        if (drop <= 0)
        {
            return "其他";
        }

        if (foldOld && drop < 120)
        {
            return "远古版";
        }

        if (groupByDrop)
        {
            return GetCommunityProjectDropVersion(drop);
        }

        return NormalizeMinecraftVersion(trimmed) ?? trimmed;
    }

    private static bool IsCommunityProjectVersionFormat(string version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith("1.", StringComparison.Ordinal))
        {
            return true;
        }

        return int.TryParse(normalized.Split('.')[0], out var major) && major > 25;
    }

    private static int GetCommunityProjectVersionDrop(string version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var segments = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !int.TryParse(segments[0], out var major)
            || !int.TryParse(segments[1], out var minor))
        {
            return 0;
        }

        return major == 1 ? minor * 10 : major * 10 + minor;
    }

    private static string GetCommunityProjectDropVersion(int drop)
    {
        return drop >= 250 ? $"{drop / 10}.{drop % 10}" : $"1.{drop / 10}";
    }

    private static Version ParseVersion(string? rawValue)
    {
        return Version.TryParse(rawValue, out var version)
            ? version
            : new Version(0, 0);
    }

    private static string? NormalizeMinecraftVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = Regex.Match(rawValue, @"\d+\.\d+(?:\.\d+)?");
        return match.Success ? match.Value : null;
    }

    private void RefreshHomePageMarketSurface()
    {
        _homePageMarketState = FrontendCommunityProjectService.BuildHomePageMarketState(
            _instanceComposition,
            _selectedCommunityDownloadSourceIndex);

        ReplaceItems(
            HomePageMarketSections,
            _homePageMarketState.Sections.Select(section => new DownloadCatalogSectionViewModel(
                section.Title,
                section.Entries.Select(entry => new DownloadCatalogEntryViewModel(
                    entry.Title,
                    entry.Info,
                    entry.Meta,
                    entry.ActionText,
                    CreateProjectSectionCommand(entry)))
                .ToArray())));

        RaiseHomePageMarketProperties();
    }

    private void RaiseCommunityProjectProperties()
    {
        RaisePropertyChanged(nameof(CommunityProjectTitle));
        RaisePropertyChanged(nameof(CommunityProjectSummary));
        RaisePropertyChanged(nameof(CommunityProjectDescription));
        RaisePropertyChanged(nameof(CommunityProjectSource));
        RaisePropertyChanged(nameof(CommunityProjectIcon));
        RaisePropertyChanged(nameof(HasCommunityProjectIcon));
        RaisePropertyChanged(nameof(CommunityProjectWebsite));
        RaisePropertyChanged(nameof(CommunityProjectStatus));
        RaisePropertyChanged(nameof(CommunityProjectUpdatedLabel));
        RaisePropertyChanged(nameof(CommunityProjectCompatibilitySummary));
        RaisePropertyChanged(nameof(CommunityProjectCategorySummary));
        RaisePropertyChanged(nameof(CommunityProjectDownloadCountLabel));
        RaisePropertyChanged(nameof(CommunityProjectFollowCountLabel));
        RaisePropertyChanged(nameof(HasCommunityProjectDescription));
        RaisePropertyChanged(nameof(CommunityProjectIntroDescription));
        RaisePropertyChanged(nameof(CommunityProjectCategoryTags));
        RaisePropertyChanged(nameof(HasCommunityProjectCategoryTags));
        RaisePropertyChanged(nameof(CommunityProjectSourceBadgeText));
        RaisePropertyChanged(nameof(HasCommunityProjectActionButtons));
        RaisePropertyChanged(nameof(ShowCommunityProjectFilterCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectVersionFilters));
        RaisePropertyChanged(nameof(ShowCommunityProjectLoaderFilters));
        RaisePropertyChanged(nameof(HasCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasNoCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasCommunityProjectSections));
        RaisePropertyChanged(nameof(HasNoCommunityProjectSections));
        RaisePropertyChanged(nameof(ShowCommunityProjectWarning));
        RaisePropertyChanged(nameof(CommunityProjectWarningText));
        RaisePropertyChanged(nameof(ShowCommunityProjectLoadingCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectContent));
        RaisePropertyChanged(nameof(CommunityProjectLoadingText));
    }

    private void ApplyCommunityProjectIcon()
    {
        _communityProjectIcon = LoadCachedBitmapFromPath(_communityProjectState.IconPath)
                                ?? LoadCommunityProjectFallbackIcon();
        _ = EnsureCommunityProjectIconAsync(_communityProjectState.IconUrl);
    }

    private async Task EnsureCommunityProjectIconAsync(string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return;
        }

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

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!string.Equals(iconUrl, _communityProjectState.IconUrl, StringComparison.Ordinal))
            {
                return;
            }

            _communityProjectIcon = bitmap;
            RaisePropertyChanged(nameof(CommunityProjectIcon));
            RaisePropertyChanged(nameof(HasCommunityProjectIcon));
        });
    }

    private Bitmap? LoadCommunityProjectFallbackIcon()
    {
        return _selectedCommunityProjectOriginSubpage switch
        {
            LauncherFrontendSubpageKey.DownloadMod => LoadLauncherBitmap("Images", "Blocks", "CommandBlock.png"),
            LauncherFrontendSubpageKey.DownloadPack => LoadLauncherBitmap("Images", "Blocks", "CommandBlock.png"),
            LauncherFrontendSubpageKey.DownloadDataPack => LoadLauncherBitmap("Images", "Blocks", "RedstoneLampOn.png"),
            LauncherFrontendSubpageKey.DownloadResourcePack => LoadLauncherBitmap("Images", "Blocks", "Grass.png"),
            LauncherFrontendSubpageKey.DownloadShader => LoadLauncherBitmap("Images", "Blocks", "GoldBlock.png"),
            LauncherFrontendSubpageKey.DownloadWorld => LoadLauncherBitmap("Images", "Blocks", "GrassPath.png"),
            _ => LoadLauncherBitmap("Images", "Icons", "NoIcon.png")
        };
    }

    private string? ResolveCommunityProjectDownloadStartDirectory()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            return null;
        }

        var directory = _selectedCommunityProjectOriginSubpage switch
        {
            LauncherFrontendSubpageKey.DownloadResourcePack => ResolveCurrentInstanceResourceDirectory("resourcepacks"),
            LauncherFrontendSubpageKey.DownloadShader => ResolveCurrentInstanceResourceDirectory("shaderpacks"),
            LauncherFrontendSubpageKey.DownloadWorld => Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves"),
            LauncherFrontendSubpageKey.DownloadDataPack => _instanceComposition.Selection.IndieDirectory,
            LauncherFrontendSubpageKey.DownloadPack => _instanceComposition.Selection.InstanceDirectory,
            _ => ResolveCurrentInstanceResourceDirectory("mods")
        };

        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    private void SetCommunityProjectLoading(bool isLoading)
    {
        if (_isCommunityProjectLoading == isLoading)
        {
            return;
        }

        _isCommunityProjectLoading = isLoading;
        RaisePropertyChanged(nameof(ShowCommunityProjectLoadingCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectContent));
    }

    private void RaiseHomePageMarketProperties()
    {
        RaisePropertyChanged(nameof(HomePageMarketSummary));
        RaisePropertyChanged(nameof(HasHomePageMarketSections));
        RaisePropertyChanged(nameof(HasNoHomePageMarketSections));
        RaisePropertyChanged(nameof(ShowHomePageMarketWarning));
        RaisePropertyChanged(nameof(HomePageMarketWarningText));
    }

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}亿",
            >= 10_000 => $"{value / 10_000d:0.#}万",
            _ => value.ToString()
        };
    }
}

internal sealed class FrontendManagedFileDownloadTask(
    string title,
    string sourceUrl,
    string targetPath,
    Action<string>? onStarted = null,
    Action<string>? onCompleted = null,
    Action<string>? onFailed = null) : ITask, ITaskProgressive, ITaskTelemetry, ITaskCancelable
{
    private static readonly HttpClient DownloadHttpClient = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    })
    {
        Timeout = TimeSpan.FromMinutes(15)
    };

    private readonly CancellationTokenSource _cancellation = new();
    private TaskTelemetrySnapshot _telemetry = new("0%", "0 B/s", 1, null);

    public string Title { get; } = title;

    public TaskTelemetrySnapshot Telemetry => _telemetry;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskTelemetryEvent TelemetryChanged = delegate { };

    public void Cancel()
    {
        _cancellation.Cancel();
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var token = linkedCts.Token;
        StateChanged(TaskState.Waiting, "已加入任务中心");

        try
        {
            using var response = await DownloadHttpClient.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var sourceStream = await response.Content.ReadAsStreamAsync(token);
            await using var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var contentLength = response.Content.Headers.ContentLength;
            var buffer = new byte[81920];
            var totalRead = 0L;
            var lastReportedBytes = 0L;
            var lastReportedAt = Environment.TickCount64;
            StateChanged(TaskState.Running, "正在下载文件…");
            onStarted?.Invoke(targetPath);

            while (true)
            {
                var read = await sourceStream.ReadAsync(buffer, token);
                if (read <= 0)
                {
                    break;
                }

                await targetStream.WriteAsync(buffer.AsMemory(0, read), token);
                totalRead += read;

                var totalLength = contentLength.GetValueOrDefault();
                var progress = totalLength > 0
                    ? Math.Clamp(totalRead / (double)totalLength, 0d, 1d)
                    : 0d;
                ProgressChanged(progress);

                var now = Environment.TickCount64;
                if (now - lastReportedAt >= 250)
                {
                    var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                    var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                    PublishTelemetry(progress, speed);
                    lastReportedAt = now;
                    lastReportedBytes = totalRead;
                    StateChanged(TaskState.Running, $"正在下载 {Path.GetFileName(targetPath)}…");
                }
            }

            await targetStream.FlushAsync(token);
            ProgressChanged(1d);
            PublishTelemetry(1d, 0d, 0);
            StateChanged(TaskState.Success, $"已保存到 {targetPath}");
            onCompleted?.Invoke(targetPath);
        }
        catch (OperationCanceledException)
        {
            CleanupPartialDownload();
            PublishTelemetry(0d, 0d);
            StateChanged(TaskState.Canceled, "下载已取消");
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} 下载已取消");
            throw;
        }
        catch (Exception ex)
        {
            CleanupPartialDownload();
            PublishTelemetry(0d, 0d);
            StateChanged(TaskState.Failed, ex.Message);
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} 下载失败");
            throw;
        }
    }

    private void CleanupPartialDownload()
    {
        try
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private void PublishTelemetry(double progress, double speedBytesPerSecond, int? remainingFileCount = 1)
    {
        _telemetry = new TaskTelemetrySnapshot(
            $"{Math.Round(progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            $"{FormatBytes(speedBytesPerSecond)}/s",
            remainingFileCount,
            null);
        TelemetryChanged(_telemetry);
    }

    private static string FormatBytes(double value)
    {
        var absolute = Math.Max(value, 0d);
        return absolute switch
        {
            >= 1024d * 1024d * 1024d => $"{absolute / (1024d * 1024d * 1024d):0.##} GB",
            >= 1024d * 1024d => $"{absolute / (1024d * 1024d):0.##} MB",
            >= 1024d => $"{absolute / 1024d:0.##} KB",
            _ => $"{absolute:0} B"
        };
    }
}
