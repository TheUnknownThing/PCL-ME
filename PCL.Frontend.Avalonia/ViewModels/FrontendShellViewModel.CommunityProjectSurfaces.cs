using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
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
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] CommunityProjectKnownLoaders = ["Forge", "NeoForge", "Fabric", "Quilt", "OptiFine", "Iris"];
    private static readonly string[] CommunityProjectReservedInstanceNames =
    [
        "CON", "PRN", "AUX", "CLOCK$", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "LPT¹", "LPT²", "LPT³"
    ];
    private static readonly FrontendIcon CommunityProjectLinkIcon = new(
        "M607.934444 417.856853c-6.179746-6.1777-12.766768-11.746532-19.554358-16.910135l-0.01228 0.011256c-6.986111-6.719028-16.47216-10.857279-26.930349-10.857279-21.464871 0-38.864146 17.400299-38.864146 38.864146 0 9.497305 3.411703 18.196431 9.071609 24.947182l-0.001023 0c0.001023 0.001023 0.00307 0.00307 0.005117 0.004093 2.718925 3.242857 5.953595 6.03853 9.585309 8.251941 3.664459 3.021823 7.261381 5.997598 10.624988 9.361205l3.203972 3.204995c40.279379 40.229237 28.254507 109.539812-12.024871 149.820214L371.157763 796.383956c-40.278355 40.229237-105.761766 40.229237-146.042167 0l-3.229554-3.231601c-40.281425-40.278355-40.281425-105.809861 0-145.991002l75.93546-75.909877c9.742898-7.733125 15.997346-19.668968 15.997346-33.072233 0-23.312962-18.898419-42.211381-42.211381-42.211381-8.797363 0-16.963347 2.693342-23.725354 7.297197-0.021489-0.045025-0.044002-0.088004-0.066515-0.134053l-0.809435 0.757247c-2.989077 2.148943-5.691629 4.669346-8.025791 7.510044l-78.913281 73.841775c-74.178443 74.229608-74.178443 195.632609 0 269.758863l3.203972 3.202948c74.178443 74.127278 195.529255 74.127278 269.707698 0l171.829484-171.880649c74.076112-74.17435 80.357166-191.184297 6.282077-265.311575L607.934444 417.856853z M855.61957 165.804257l-3.203972-3.203972c-74.17742-74.178443-195.528232-74.178443-269.706675 0L410.87944 334.479911c-74.178443 74.178443-78.263481 181.296089-4.085038 255.522628l3.152806 3.104711c3.368724 3.367701 6.865361 6.54302 10.434653 9.588379 2.583848 2.885723 5.618974 5.355985 8.992815 7.309476 0.025583 0.020466 0.052189 0.041956 0.077771 0.062422l0.011256-0.010233c5.377474 3.092431 11.608386 4.870938 18.257829 4.870938 20.263509 0 36.68962-16.428158 36.68962-36.68962 0-5.719258-1.309832-11.132548-3.645017-15.95846l0 0c-4.850471-10.891048-13.930267-17.521049-20.210297-23.802102l-3.15383-3.102664c-40.278355-40.278355-24.982998-98.79612 15.295358-139.074476l171.930791-171.830507c40.179095-40.280402 105.685018-40.280402 145.965419 0l3.206018 3.152806c40.279379 40.281425 40.279379 105.838513 0 146.06775l-75.686796 75.737962c-10.296507 7.628748-16.97358 19.865443-16.97358 33.662681 0 23.12365 18.745946 41.87062 41.87062 41.87062 8.048303 0 15.563464-2.275833 21.944801-6.211469 0.048095 0.081864 0.093121 0.157589 0.141216 0.240477l1.173732-1.083681c3.616364-2.421142 6.828522-5.393847 9.529027-8.792247l79.766718-73.603345C929.798013 361.334535 929.798013 239.981676 855.61957 165.804257z",
        0.85);
    private static readonly FrontendIcon CommunityProjectCopyIcon = new(
        "M394.666667 106.666667h448a74.666667 74.666667 0 0 1 74.666666 74.666666v448a74.666667 74.666667 0 0 1-74.666666 74.666667H394.666667a74.666667 74.666667 0 0 1-74.666667-74.666667V181.333333a74.666667 74.666667 0 0 1 74.666667-74.666666z m0 64a10.666667 10.666667 0 0 0-10.666667 10.666666v448a10.666667 10.666667 0 0 0 10.666667 10.666667h448a10.666667 10.666667 0 0 0 10.666666-10.666667V181.333333a10.666667 10.666667 0 0 0-10.666666-10.666666H394.666667z m245.333333 597.333333a32 32 0 0 1 64 0v74.666667a74.666667 74.666667 0 0 1-74.666667 74.666666H181.333333a74.666667 74.666667 0 0 1-74.666666-74.666666V394.666667a74.666667 74.666667 0 0 1 74.666666-74.666667h74.666667a32 32 0 0 1 0 64h-74.666667a10.666667 10.666667 0 0 0-10.666666 10.666667v448a10.666667 10.666667 0 0 0 10.666666 10.666666h448a10.666667 10.666667 0 0 0 10.666667-10.666666v-74.666667z",
        0.85);
    private static readonly FrontendIcon CommunityProjectTranslateIcon = new(
        "M213.333333 640v85.333333a85.333333 85.333333 0 0 0 78.933334 85.12L298.666667 810.666667h128v85.333333H298.666667a170.666667 170.666667 0 0 1-170.666667-170.666667v-85.333333h85.333333z m554.666667-213.333333l187.733333 469.333333h-91.946666l-51.242667-128h-174.506667l-51.157333 128h-91.904L682.666667 426.666667h85.333333z m-42.666667 123.093333L672.128 682.666667h106.325333L725.333333 549.76zM341.333333 85.333333v85.333334h170.666667v298.666666H341.333333v128H256v-128H85.333333V170.666667h170.666667V85.333333h85.333333z m384 42.666667a170.666667 170.666667 0 0 1 170.666667 170.666667v85.333333h-85.333333V298.666667a85.333333 85.333333 0 0 0-85.333334-85.333334h-128V128h128zM256 256H170.666667v128h85.333333V256z m170.666667 0H341.333333v128h85.333334V256z",
        0.82);
    private static readonly FrontendIcon CommunityProjectFavoriteOutlineIcon = new(
        "M512 896a42.666667 42.666667 0 0 1-30.293333-12.373333l-331.52-331.946667a224.426667 224.426667 0 0 1 0-315.733333 223.573333 223.573333 0 0 1 315.733333 0L512 282.026667l46.08-46.08a223.573333 223.573333 0 0 1 315.733333 0 224.426667 224.426667 0 0 1 0 315.733333l-331.52 331.946667A42.666667 42.666667 0 0 1 512 896zM308.053333 256a136.533333 136.533333 0 0 0-97.28 40.106667 138.24 138.24 0 0 0 0 194.986666L512 792.746667l301.226667-301.653334a138.24 138.24 0 0 0 0-194.986666 141.653333 141.653333 0 0 0-194.56 0l-76.373334 76.8a42.666667 42.666667 0 0 1-60.586666 0L405.333333 296.106667A136.533333 136.533333 0 0 0 308.053333 256z",
        0.82);
    private static readonly FrontendIcon CommunityProjectFavoriteFilledIcon = new(
        "M700.856 155.543c-74.769 0-144.295 72.696-190.046 127.26-45.737-54.576-115.247-127.26-190.056-127.26-134.79 0-244.443 105.78-244.443 235.799 0 77.57 39.278 131.988 70.845 175.713C238.908 694.053 469.62 852.094 479.39 858.757c9.41 6.414 20.424 9.629 31.401 9.629 11.006 0 21.998-3.215 31.398-9.63 9.782-6.662 240.514-164.703 332.238-291.701 31.587-43.724 70.874-98.143 70.874-175.713-0.001-130.02-109.656-235.8-244.445-235.8z m0 0",
        0.82);

    private string _selectedCommunityProjectId = string.Empty;
    private LauncherFrontendSubpageKey? _selectedCommunityProjectOriginSubpage;
    private string _selectedCommunityProjectVersionFilter = string.Empty;
    private string _selectedCommunityProjectLoaderFilter = string.Empty;
    private string _selectedCommunityProjectInstallMode = CommunityProjectInstallModeCurrentOnlyValue;
    private readonly List<CommunityProjectNavigationState> _communityProjectNavigationStack = [];
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
    private const string CommunityProjectInstallModeCurrentOnlyValue = "current-only";
    private const string CommunityProjectInstallModeWithDependenciesValue = "with-dependencies";
    private static readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectModInstallModeOptions =
    [
        new DownloadResourceFilterOptionViewModel("仅安装当前模组", CommunityProjectInstallModeCurrentOnlyValue),
        new DownloadResourceFilterOptionViewModel("安装当前模组和缺失依赖", CommunityProjectInstallModeWithDependenciesValue)
    ];
    private static readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectSingleInstallModeOptions =
    [
        new DownloadResourceFilterOptionViewModel("仅安装当前资源", CommunityProjectInstallModeCurrentOnlyValue)
    ];

    public ObservableCollection<CommunityProjectActionButtonViewModel> CommunityProjectActionButtons { get; } = [];

    public ObservableCollection<CommunityProjectFilterButtonViewModel> CommunityProjectVersionFilterButtons { get; } = [];

    public ObservableCollection<CommunityProjectFilterButtonViewModel> CommunityProjectLoaderFilterButtons { get; } = [];

    public ObservableCollection<CommunityProjectReleaseGroupViewModel> CommunityProjectReleaseGroups { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> CommunityProjectDependencySections { get; } = [];

    public ObservableCollection<DownloadCatalogSectionViewModel> CommunityProjectSections { get; } = [];

    public bool ShowCompDetailSurface => IsStandardShellRoute && _currentRoute.Page == LauncherFrontendPageKey.CompDetail;

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

    public string CommunityProjectCurrentInstanceName
    {
        get
        {
            if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack)
            {
                return _versionSavesComposition.Selection.HasSelection
                    ? _versionSavesComposition.Selection.SaveName
                    : "未选择存档";
            }

            return _instanceComposition.Selection.HasSelection
                ? _instanceComposition.Selection.InstanceName
                : "未选择实例";
        }
    }

    public string CommunityProjectCurrentInstanceSummary
    {
        get
        {
            if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack)
            {
                if (!_versionSavesComposition.Selection.HasSelection)
                {
                    return "下载页当前还没有选中存档，无法直接安装数据包。请先在存档页打开目标存档详情。";
                }

                var datapackParts = new List<string> { _versionSavesComposition.Selection.InstanceName };
                if (!string.IsNullOrWhiteSpace(_instanceComposition.Selection.VanillaVersion))
                {
                    datapackParts.Add($"Minecraft {_instanceComposition.Selection.VanillaVersion}");
                }

                datapackParts.Add(_versionSavesComposition.Selection.SavePath);
                return string.Join(" • ", datapackParts);
            }

            if (!_instanceComposition.Selection.HasSelection)
            {
                return "下载页当前还没有选中实例，无法直接安装到实例。";
            }

            var parts = new List<string> { CommunityProjectCurrentInstanceName };
            if (!string.IsNullOrWhiteSpace(_instanceComposition.Selection.VanillaVersion))
            {
                parts.Add($"Minecraft {_instanceComposition.Selection.VanillaVersion}");
            }

            var loader = ResolveSelectedInstanceLoaderLabel();
            if (!string.IsNullOrWhiteSpace(loader))
            {
                parts.Add(loader);
            }

            return string.Join(" • ", parts);
        }
    }

    public bool ShowCommunityProjectInstallSuggestionCard => CanInstallCommunityProjectToCurrentInstance();

    public string CommunityProjectInstallSuggestionTitle => GetSuggestedCommunityProjectInstallRelease()?.Title ?? "当前没有可安装的版本";

    public string CommunityProjectInstallSuggestionSummary
    {
        get
        {
            var release = GetSuggestedCommunityProjectInstallRelease();
            if (release is null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack
                && _versionSavesComposition.Selection.HasSelection)
            {
                parts.Add($"目标存档：{_versionSavesComposition.Selection.SaveName}");
            }

            if (!string.IsNullOrWhiteSpace(release.Info))
            {
                parts.Add(release.Info);
            }

            if (!string.IsNullOrWhiteSpace(release.Meta))
            {
                parts.Add(release.Meta);
            }

            return string.Join(" • ", parts);
        }
    }

    public ActionCommand InstallCommunityProjectToCurrentInstanceCommand => new(() => _ = InstallCommunityProjectToCurrentInstanceAsync());

    public ActionCommand ExecuteCommunityProjectInstallSuggestionCommand => new(() => _ = InstallCommunityProjectToCurrentInstanceAsync());

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectInstallModeOptions =>
        _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
            ? CommunityProjectModInstallModeOptions
            : CommunityProjectSingleInstallModeOptions;

    public DownloadResourceFilterOptionViewModel? SelectedCommunityProjectInstallModeOption
    {
        get => CommunityProjectInstallModeOptions.FirstOrDefault(option =>
                   string.Equals(option.FilterValue, _selectedCommunityProjectInstallMode, StringComparison.OrdinalIgnoreCase))
               ?? CommunityProjectInstallModeOptions.FirstOrDefault();
        set
        {
            var nextValue = value?.FilterValue ?? CommunityProjectInstallModeOptions.FirstOrDefault()?.FilterValue ?? CommunityProjectInstallModeCurrentOnlyValue;
            if (SetProperty(ref _selectedCommunityProjectInstallMode, nextValue, nameof(SelectedCommunityProjectInstallModeOption)))
            {
                RaisePropertyChanged(nameof(CommunityProjectInstallModeOptions));
            }
        }
    }

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

    public bool HasCommunityProjectDependencySections => CommunityProjectDependencySections.Count > 0;

    public string CommunityProjectDependencyCardTitle => string.IsNullOrWhiteSpace(_communityProjectDependencyReleaseTitle)
        ? "当前版本依赖"
        : $"{_communityProjectDependencyReleaseTitle} 的依赖";

    public bool ShowCommunityProjectDependencyCard => _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
        && HasCommunityProjectDependencySections;

    public bool HasCommunityProjectSections => CommunityProjectSections.Count > 0;

    public bool HasNoCommunityProjectSections => !HasCommunityProjectSections;

    public bool ShowCommunityProjectWarning => _communityProjectState.ShowWarning;

    public string CommunityProjectWarningText => _communityProjectState.WarningText;

    public void OpenCommunityProjectDetail(
        string projectId,
        string? projectTitle = null,
        string? initialVersionFilter = null,
        string? initialLoaderFilter = null,
        LauncherFrontendSubpageKey? originSubpage = null)
    {
        var normalizedProjectId = projectId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProjectId))
        {
            return;
        }

        if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
            && ShouldPushCommunityProjectNavigationState(normalizedProjectId))
        {
            _communityProjectNavigationStack.Add(CreateCommunityProjectNavigationState());
        }
        else if (_currentRoute.Page != LauncherFrontendPageKey.CompDetail)
        {
            _communityProjectNavigationStack.Clear();
        }

        var targetOriginSubpage = originSubpage ?? _selectedCommunityProjectOriginSubpage;
        var shouldSyncFiltersWithInstance = ShouldAutoSyncCommunityProjectFiltersWithInstance(targetOriginSubpage);
        ApplyCommunityProjectNavigationState(new CommunityProjectNavigationState(
            normalizedProjectId,
            projectTitle?.Trim() ?? string.Empty,
            targetOriginSubpage,
            shouldSyncFiltersWithInstance
                ? NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion)
                    ?? NormalizeMinecraftVersion(initialVersionFilter)
                    ?? initialVersionFilter?.Trim()
                    ?? string.Empty
                : NormalizeMinecraftVersion(initialVersionFilter) ?? initialVersionFilter?.Trim() ?? string.Empty,
            shouldSyncFiltersWithInstance
                ? ResolveSelectedInstanceLoaderLabel()
                    ?? initialLoaderFilter?.Trim()
                    ?? string.Empty
                : initialLoaderFilter?.Trim() ?? string.Empty));
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

    private bool TryNavigateBackWithinCommunityProjectDetail()
    {
        if (_currentRoute.Page != LauncherFrontendPageKey.CompDetail || _communityProjectNavigationStack.Count == 0)
        {
            return false;
        }

        var state = _communityProjectNavigationStack[^1];
        _communityProjectNavigationStack.RemoveAt(_communityProjectNavigationStack.Count - 1);
        ApplyCommunityProjectNavigationState(state);
        RefreshShell($"已返回到 {state.TitleHintOrProjectId}。");
        return true;
    }

    private void ResetCommunityProjectNavigationStack()
    {
        _communityProjectNavigationStack.Clear();
    }

    private bool HasCommunityProjectNavigationHistory => _communityProjectNavigationStack.Count > 0;

    private bool ShouldPushCommunityProjectNavigationState(string nextProjectId)
    {
        return !string.IsNullOrWhiteSpace(_selectedCommunityProjectId)
               && !string.Equals(_selectedCommunityProjectId, nextProjectId, StringComparison.OrdinalIgnoreCase);
    }

    private CommunityProjectNavigationState CreateCommunityProjectNavigationState()
    {
        return new CommunityProjectNavigationState(
            _selectedCommunityProjectId,
            _selectedCommunityProjectTitleHint,
            _selectedCommunityProjectOriginSubpage,
            _selectedCommunityProjectVersionFilter,
            _selectedCommunityProjectLoaderFilter);
    }

    private void ApplyCommunityProjectNavigationState(CommunityProjectNavigationState state)
    {
        _selectedCommunityProjectId = state.ProjectId;
        _selectedCommunityProjectTitleHint = state.TitleHint;
        _selectedCommunityProjectOriginSubpage = state.OriginSubpage;
        _selectedCommunityProjectVersionFilter = state.VersionFilter;
        _selectedCommunityProjectLoaderFilter = state.LoaderFilter;
    }

    private void RefreshCompDetailSurface()
    {
        CommunityProjectLoadingText = "正在获取版本列表";
        if (string.IsNullOrWhiteSpace(_selectedCommunityProjectId))
        {
            _communityProjectState = new FrontendCommunityProjectState(
                string.Empty,
                "未选择工程",
                "先从收藏夹或资源条目进入，才能查看对应工程详情。",
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
        ReplaceItems(CommunityProjectDependencySections, []);
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
        RebuildCommunityProjectDependencySections(versionGrouping);
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

    private IReadOnlyList<CommunityProjectActionButtonViewModel> BuildCommunityProjectActionButtons()
    {
        var isFavorite = IsCommunityProjectFavorite();
        var favoriteIcon = isFavorite
            ? FrontendIconCatalog.FavoriteFilled
            : FrontendIconCatalog.FavoriteOutline;
        var buttons = new List<CommunityProjectActionButtonViewModel>();
        if (!string.IsNullOrWhiteSpace(CommunityProjectWebsite))
        {
            buttons.Add(new CommunityProjectActionButtonViewModel(
                CommunityProjectSource,
                CommunityProjectLinkIcon.Data,
                CommunityProjectLinkIcon.Scale,
                PclIconTextButtonColorState.Normal,
                CreateOpenTargetCommand($"打开项目主页: {CommunityProjectTitle}", CommunityProjectWebsite, CommunityProjectWebsite)));
        }
        else
        {
            buttons.Add(new CommunityProjectActionButtonViewModel(
                CommunityProjectSource,
                CommunityProjectLinkIcon.Data,
                CommunityProjectLinkIcon.Scale,
                PclIconTextButtonColorState.Normal,
                CreateIntentCommand($"查看来源: {CommunityProjectTitle}", CommunityProjectSource)));
        }

        buttons.Add(new CommunityProjectActionButtonViewModel(
            "MC 百科",
            CommunityProjectLinkIcon.Data,
            CommunityProjectLinkIcon.Scale,
            PclIconTextButtonColorState.Normal,
            CreateOpenTargetCommand(
                $"打开 MC 百科: {CommunityProjectTitle}",
                BuildCommunityProjectEncyclopediaUrl(CommunityProjectTitle),
                CommunityProjectTitle)));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            "复制名称",
            CommunityProjectCopyIcon.Data,
            CommunityProjectCopyIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync("复制项目名称", CommunityProjectTitle, "没有可复制的项目名称。"))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            "复制链接",
            CommunityProjectCopyIcon.Data,
            CommunityProjectCopyIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                "复制项目链接",
                string.IsNullOrWhiteSpace(CommunityProjectWebsite)
                    ? FrontendCommunityProjectService.CreateCompDetailTarget(_communityProjectState.ProjectId)
                    : CommunityProjectWebsite,
                "当前项目没有可复制的外部链接。"))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            "翻译简介",
            CommunityProjectTranslateIcon.Data,
            CommunityProjectTranslateIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                "翻译简介",
                BuildCommunityProjectDescriptionCopyText(),
                "当前项目没有可供翻译的简介文本。"))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            "收藏到",
            FrontendIconCatalog.FolderAdd.Data,
            FrontendIconCatalog.FolderAdd.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = FavoriteCurrentCommunityProjectToTargetAsync())));
        buttons.Add(CreateCommunityProjectFavoriteActionButton());
        return buttons;
    }

    private CommunityProjectActionButtonViewModel CreateCommunityProjectFavoriteActionButton()
    {
        var isFavorite = IsCommunityProjectFavorite();
        var icon = isFavorite ? CommunityProjectFavoriteFilledIcon : CommunityProjectFavoriteOutlineIcon;
        return new CommunityProjectActionButtonViewModel(
            "收藏",
            icon.Data,
            icon.Scale,
            isFavorite ? PclIconTextButtonColorState.Highlight : PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = ToggleCommunityProjectFavoriteAsync()));
    }

    private static IReadOnlyList<CommunityProjectFilterButtonViewModel> BuildCommunityProjectFilterButtons(
        IReadOnlyList<string> options,
        string selectedValue,
        string allLabel,
        Action<string> applyFilter)
    {
        var buttons = new List<CommunityProjectFilterButtonViewModel>
        {
            new(
                allLabel,
                string.IsNullOrWhiteSpace(selectedValue),
                new ActionCommand(() => applyFilter(string.Empty)))
        };
        buttons.AddRange(options.Select(option => new CommunityProjectFilterButtonViewModel(
            option,
            string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase),
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
            var visibleLoaders = FrontendLoaderVisibilityService.FilterVisibleLoaders(release.Loaders, IgnoreQuiltLoader);
            var loaders = visibleLoaders.Count == 0 ? [string.Empty] : visibleLoaders;

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
        var shouldAutoExpandFirstGroup = shouldAutoExpandSingleGroup
            || !string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            || !string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter);
        return orderedGroups
            .Select((pair, index) => new CommunityProjectReleaseGroupViewModel(
                pair.Key,
                shouldAutoExpandFirstGroup && index == 0,
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
                                : CreateOpenTargetCommand($"打开文件: {entry.Title}", entry.Target, entry.Target),
                        GetCommunityProjectReleaseChannelIcon(entry.Channel)))
                    .ToArray()))
            .ToArray();
    }

    private void RebuildCommunityProjectDependencySections((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var release = GetCurrentCommunityProjectRelease(versionGrouping);
        _communityProjectDependencyReleaseTitle = release?.Title ?? string.Empty;
        var visibleDependencies = release?.Dependencies
            .Where(entry => entry.Kind != FrontendCommunityProjectDependencyKind.Embedded)
            .ToArray();
        if (release is null || visibleDependencies is null || visibleDependencies.Length == 0)
        {
            ReplaceItems(CommunityProjectDependencySections, []);
            return;
        }

        var fallbackDependencyIcon = GetCommunityProjectDependencyIcon();
        var sections = visibleDependencies
            .GroupBy(entry => entry.Kind)
            .OrderBy(group => GetCommunityProjectDependencyPriority(group.Key))
            .Select(group => new DownloadCatalogSectionViewModel(
                GetCommunityProjectDependencyGroupTitle(group.Key),
                group.OrderBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
                    .Select(entry => new DownloadCatalogEntryViewModel(
                        entry.Title,
                        entry.Summary,
                        entry.Meta,
                        "查看详情",
                        CreateCommunityProjectDependencyCommand(entry),
                        LoadCachedBitmapFromPath(entry.IconPath) ?? fallbackDependencyIcon,
                        entry.IconUrl))
                    .ToArray()))
            .ToArray();
        ReplaceItems(CommunityProjectDependencySections, sections);
        QueueCommunityProjectDependencyIconLoad(sections);
    }

    private Bitmap? GetCommunityProjectReleaseChannelIcon(FrontendCommunityProjectReleaseChannel channel)
    {
        return channel switch
        {
            FrontendCommunityProjectReleaseChannel.Alpha => LoadLauncherBitmap("Images", "Icons", "A.png"),
            FrontendCommunityProjectReleaseChannel.Beta => LoadLauncherBitmap("Images", "Icons", "B.png"),
            _ => LoadLauncherBitmap("Images", "Icons", "R.png")
        };
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
        return FrontendLoaderVisibilityService.FilterVisibleLoaders(
                _communityProjectState.Releases.SelectMany(release => release.Loaders),
                IgnoreQuiltLoader)
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

    private ActionCommand CreateCommunityProjectDependencyCommand(FrontendCommunityProjectDependencyEntry entry)
    {
        return FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId)
            ? new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title))
            : string.IsNullOrWhiteSpace(entry.Target)
                ? CreateIntentCommand(entry.Title, entry.Summary)
                : CreateOpenTargetCommand($"打开依赖项目: {entry.Title}", entry.Target, entry.Target);
    }

    private ActionCommand CreateCommunityProjectReleaseDownloadCommand(FrontendCommunityProjectReleaseEntry entry)
    {
        return new ActionCommand(() => _ = DownloadCommunityProjectReleaseAsync(entry));
    }

    private async Task DownloadCommunityProjectReleaseAsync(FrontendCommunityProjectReleaseEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.Target))
            {
                AddActivity($"下载资源文件: {entry.Title}", "当前版本没有可用的下载地址。");
                return;
            }

            if (ShouldAutoInstallCommunityProjectRelease(entry))
            {
                await DownloadAndInstallCommunityProjectReleaseAsync(entry);
                return;
            }

            var suggestedFileName = ResolveCommunityProjectReleaseFileName(entry, CommunityProjectTitle);
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
                AddFailureActivity($"选择保存位置失败: {entry.Title}", ex.Message);
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
                ResolveDownloadRequestTimeout(),
                _shellActionService.GetDownloadTransferOptions(),
                onStarted: filePath => AvaloniaHintBus.Show($"开始下载 {Path.GetFileName(filePath)}", AvaloniaHintTheme.Info),
                onCompleted: filePath => AvaloniaHintBus.Show($"{Path.GetFileName(filePath)} 下载完成", AvaloniaHintTheme.Success),
                onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error)));
            AddActivity($"开始下载资源文件: {entry.Title}", targetPath);
        }
        catch (Exception ex)
        {
            AddFailureActivity($"下载资源文件失败: {entry.Title}", ex.Message);
        }
    }

    private bool CanInstallCommunityProjectToCurrentInstance()
    {
        if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack
            && ResolveCurrentDatapackInstallSelection() is null)
        {
            return false;
        }

        return _instanceComposition.Selection.HasSelection
               && _selectedCommunityProjectOriginSubpage != LauncherFrontendSubpageKey.DownloadPack
               && _selectedCommunityProjectOriginSubpage is not null
               && TryGetCommunityProjectInstallRelease(out _);
    }

    private FrontendVersionSaveSelectionState? ResolveCurrentDatapackInstallSelection()
    {
        return _versionSavesComposition.Selection.HasSelection
            ? _versionSavesComposition.Selection
            : null;
    }

    private string GetCommunityProjectInstallActivityTitle()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack
            ? "安装到当前存档"
            : "安装到当前实例";
    }

    private FrontendCommunityProjectReleaseEntry? GetSuggestedCommunityProjectInstallRelease()
    {
        return TryGetCommunityProjectInstallRelease(out var release) ? release : null;
    }

    private async Task InstallCommunityProjectToCurrentInstanceAsync()
    {
        var activityTitle = GetCommunityProjectInstallActivityTitle();
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, "当前未选择实例。");
            return;
        }

        if (!TryGetCommunityProjectInstallRelease(out var entry))
        {
            AddActivity(activityTitle, "当前筛选条件下没有可直接安装的版本文件。");
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            AddActivity(activityTitle, "当前版本没有可用的下载地址。");
            return;
        }

        try
        {
            var includeDependencies = ShouldInstallCommunityProjectMissingDependencies();
            var route = _selectedCommunityProjectOriginSubpage;
            if (route is null)
            {
                AddActivity(activityTitle, "当前页面没有可识别的资源类型。");
                return;
            }

            var datapackSaveSelection = route == LauncherFrontendSubpageKey.DownloadDataPack
                ? ResolveCurrentDatapackInstallSelection()
                : null;
            if (route == LauncherFrontendSubpageKey.DownloadDataPack && datapackSaveSelection is null)
            {
                AddActivity(activityTitle, "当前未选择存档，无法直接安装数据包。");
                return;
            }

            AvaloniaHintBus.Show("正在分析建议安装版本…", AvaloniaHintTheme.Info);
            AddActivity(activityTitle, $"{CommunityProjectCurrentInstanceName} • 正在分析 {CommunityProjectTitle} 的安装计划。");
            var result = await Task.Run(() => BuildCommunityProjectInstallBuildResult(
                [
                    new CommunityProjectInstallRootRequest(
                        _selectedCommunityProjectId,
                        CommunityProjectTitle,
                        route.Value,
                        _communityProjectState,
                        entry)
                ],
                _instanceComposition,
                ResolveSelectedInstanceLoaderLabel(),
                includeDependencies,
                datapackSaveSelection));

            if (includeDependencies)
            {
                var confirmed = await ConfirmCommunityProjectInstallWithDependenciesAsync(result);
                if (!confirmed)
                {
                    AddActivity(activityTitle, "已取消安装当前模组和缺失依赖。");
                    return;
                }
            }

            if (result.Plans.Count == 0)
            {
                AddActivity(activityTitle, result.Skipped.Count == 0
                    ? "没有需要加入任务中心的安装项。"
                    : string.Join("；", result.Skipped.Take(3)));
                return;
            }

            AvaloniaHintBus.Show("安装任务已开始，正在加入任务中心…", AvaloniaHintTheme.Info);
            foreach (var plan in result.Plans)
            {
                RegisterCommunityProjectInstallTask(plan, activityTitle);
            }

            var summaryParts = new List<string> { $"已加入 {result.Plans.Count} 个安装任务" };
            if (includeDependencies)
            {
                var dependencyCount = result.Plans.Count(plan => plan.IsDependency);
                if (dependencyCount > 0)
                {
                    summaryParts.Add($"包含 {dependencyCount} 个缺失依赖");
                }
            }

            if (result.Skipped.Count > 0)
            {
                summaryParts.Add($"跳过 {result.Skipped.Count} 项");
            }

            AddActivity(activityTitle, $"{CommunityProjectCurrentInstanceName} • {string.Join("，", summaryParts)}。");
            foreach (var skipped in result.Skipped.Take(5))
            {
                AddActivity(activityTitle, skipped);
            }
        }
        catch (Exception ex)
        {
            AddFailureActivity($"{activityTitle}失败", ex.Message);
        }
    }

    private async Task<bool> ConfirmCommunityProjectInstallWithDependenciesAsync(CommunityProjectInstallBuildResult result)
    {
        try
        {
            return await _shellActionService.ConfirmAsync(
                "确认安装依赖",
                BuildCommunityProjectInstallConfirmationMessage(CommunityProjectCurrentInstanceName, result),
                "开始安装");
        }
        catch (Exception ex)
        {
            AddFailureActivity("安装确认失败", ex.Message);
            return false;
        }
    }

    private bool ShouldAutoInstallCommunityProjectRelease(FrontendCommunityProjectReleaseEntry entry)
    {
        if (!entry.IsDirectDownload || !IsCommunityProjectModpack())
        {
            return false;
        }

        var extension = ResolveCommunityProjectReleaseExtension(entry);
        return extension is ".mrpack" or ".zip";
    }

    private bool IsCommunityProjectModpack()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadPack
               || _communityProjectState.Website.Contains("/modpacks/", StringComparison.OrdinalIgnoreCase)
               || _communityProjectState.Website.Contains("/modpack/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadAndInstallCommunityProjectReleaseAsync(FrontendCommunityProjectReleaseEntry entry)
    {
        var launcherDirectory = ResolveDownloadLauncherFolder();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        Directory.CreateDirectory(versionsDirectory);

        string? instanceName;
        try
        {
            instanceName = await PromptForCommunityProjectInstanceNameAsync(versionsDirectory);
        }
        catch (Exception ex)
        {
            AddFailureActivity("输入实例名称失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            AddActivity($"已取消安装整合包: {entry.Title}", "没有输入实例名称。");
            return;
        }

        var extension = ResolveCommunityProjectReleaseExtension(entry);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mrpack";
        }

        var targetDirectory = Path.Combine(versionsDirectory, instanceName);
        var archivePath = Path.Combine(targetDirectory, $"原始整合包{extension}");
        var description = string.IsNullOrWhiteSpace(_communityProjectState.Summary)
            ? _communityProjectState.Description
            : _communityProjectState.Summary;
        var taskTitle = $"{_communityProjectState.Source} 整合包安装：{instanceName}";

        TaskCenter.Register(new FrontendManagedModpackInstallTask(
            taskTitle,
            new FrontendModpackInstallRequest(
                entry.Target!,
                null,
                archivePath,
                launcherDirectory,
                _selectedDownloadSourceIndex,
                instanceName,
                targetDirectory,
                _selectedCommunityProjectId,
                _communityProjectState.Source,
                _communityProjectState.IconPath,
                description,
                _selectedCommunityDownloadSourceIndex),
            ResolveDownloadRequestTimeout(),
            _shellActionService.GetDownloadTransferOptions(),
            onStarted: filePath =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaHintBus.Show($"开始下载并安装 {Path.GetFileName(filePath)}", AvaloniaHintTheme.Info);
                });
            },
            onCompleted: result =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    HandleCommunityProjectModpackInstalled(result);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddFailureActivity($"整合包安装失败: {entry.Title}", message);
                });
            }));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            $"{taskTitle} 已加入任务中心。");
    }

    private async Task SelectCommunityProjectInstanceAsync()
    {
        var instances = LoadAvailableDownloadTargetInstances();
        if (instances.Count == 0)
        {
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect),
                "下载页未发现可用实例，已转到实例选择页。");
            return;
        }

        string? selectedId;
        try
        {
            selectedId = await _shellActionService.PromptForChoiceAsync(
                "选择实例",
                "根据选择实例筛选推荐模组版本，支持直接安装到该实例。",
                instances.Select(entry => new PclChoiceDialogOption(
                    entry.Name,
                    entry.Name,
                    entry.Subtitle)).ToArray(),
                _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : null,
                "切换实例");
        }
        catch (Exception ex)
        {
            AddFailureActivity("选择实例失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        if (_instanceComposition.Selection.HasSelection
            && string.Equals(_instanceComposition.Selection.InstanceName, selectedId, StringComparison.OrdinalIgnoreCase))
        {
            AddActivity("选择实例", $"{selectedId} 已经是当前实例。");
            return;
        }

        RefreshSelectedInstanceSmoothly(selectedId);
    }

    private async Task<string?> PromptForCommunityProjectInstanceNameAsync(string versionsDirectory, string? suggestion = null)
    {
        suggestion ??= BuildCommunityProjectInstanceNameSuggestion();
        while (true)
        {
            var input = await _shellActionService.PromptForTextAsync(
                "输入实例名称",
                "整合包会安装到当前启动目录的 versions 文件夹中。",
                suggestion,
                "开始安装");
            if (input is null)
            {
                return null;
            }

            var trimmed = input.Trim();
            var validationError = ValidateCommunityProjectInstanceName(trimmed, versionsDirectory);
            if (string.IsNullOrWhiteSpace(validationError))
            {
                return trimmed;
            }

            var retry = await _shellActionService.ConfirmAsync(
                "实例名称不可用",
                validationError,
                "重新输入");
            if (!retry)
            {
                return null;
            }

            suggestion = trimmed;
        }
    }

    private string BuildCommunityProjectInstanceNameSuggestion()
    {
        var title = _communityProjectState.Title
            .Replace(".zip", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".rar", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".mrpack", string.Empty, StringComparison.OrdinalIgnoreCase);
        return SanitizeInstallDirectoryName(title);
    }

    private static string? ValidateCommunityProjectInstanceName(string value, string versionsDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "实例名称不能为空。";
        }

        if (value.StartsWith(' ') || value.EndsWith(' '))
        {
            return "实例名称不能以空格开头或结尾。";
        }

        if (value.Length > 100)
        {
            return "实例名称不能超过 100 个字符。";
        }

        if (value.EndsWith(".", StringComparison.Ordinal))
        {
            return "实例名称不能以小数点结尾。";
        }

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('!') || value.Contains(';'))
        {
            return "实例名称包含不受支持的字符。";
        }

        if (CommunityProjectReservedInstanceNames.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return "实例名称不能使用系统保留名称。";
        }

        if (Regex.IsMatch(value, ".{2,}~\\d", RegexOptions.CultureInvariant))
        {
            return "实例名称不能包含这一特殊格式。";
        }

        if (Directory.Exists(versionsDirectory) && Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase)))
        {
            return "不可与现有实例重名。";
        }

        return null;
    }

    private void HandleCommunityProjectModpackInstalled(FrontendModpackInstallResult result)
    {
        if (AutoSelectNewInstance)
        {
            _shellActionService.PersistLocalValue("LaunchInstanceSelect", result.InstanceName);
        }

        RefreshLaunchState();
        ReloadDownloadComposition();
        AddActivity("整合包安装完成", $"{result.InstanceName} • {result.TargetDirectory}");
        AvaloniaHintBus.Show($"{result.InstanceName} 安装完成", AvaloniaHintTheme.Success);
    }

    private string ResolveCommunityProjectReleaseExtension(FrontendCommunityProjectReleaseEntry entry)
    {
        var suggestedFileName = ResolveCommunityProjectReleaseFileName(entry, _communityProjectState.Title);
        var extension = Path.GetExtension(suggestedFileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        if (Uri.TryCreate(entry.Target, UriKind.Absolute, out var uri))
        {
            extension = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension.ToLowerInvariant();
            }
        }

        return string.Empty;
    }

    private string? ResolveSelectedInstanceLoaderLabel()
    {
        if (!_instanceComposition.Selection.HasSelection
            || string.IsNullOrWhiteSpace(_instanceComposition.Selection.InstanceDirectory))
        {
            return null;
        }

        return BuildInstanceSelectionSnapshot(
            _instanceComposition.Selection.InstanceDirectory,
            _instanceComposition.Selection.InstanceName)?.LoaderLabel;
    }

    private static bool ShouldAutoSyncCommunityProjectFiltersWithInstance(LauncherFrontendSubpageKey? originSubpage)
    {
        return originSubpage != LauncherFrontendSubpageKey.DownloadPack;
    }

    private IReadOnlyList<InstanceSelectionSnapshot> LoadAvailableDownloadTargetInstances()
    {
        var runtimePaths = _shellActionService.RuntimePaths;
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstance = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(directory => BuildInstanceSelectionSnapshot(directory, selectedInstance))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .OrderByDescending(snapshot => snapshot.IsSelected)
            .ThenByDescending(snapshot => snapshot.IsStarred)
            .ThenBy(snapshot => snapshot.IsBroken)
            .ThenBy(snapshot => snapshot.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private FrontendCommunityProjectReleaseEntry? GetCurrentCommunityProjectRelease((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return SelectPreferredCommunityProjectRelease(GetVisibleCommunityProjectReleases(versionGrouping));
    }

    private bool TryGetCommunityProjectInstallRelease(out FrontendCommunityProjectReleaseEntry entry)
    {
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        var preferredLoader = ResolveSelectedInstanceLoaderLabel();
        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        entry = SelectPreferredCommunityProjectRelease(
            GetVisibleCommunityProjectReleases(versionGrouping)
                .Where(release => release.IsDirectDownload
                                  && !string.IsNullOrWhiteSpace(release.Target)
                                  && IsCompatibleCommunityProjectInstallRelease(
                                      release,
                                      preferredVersion,
                                      preferredLoader,
                                      _selectedCommunityProjectOriginSubpage)))!;
        return entry is not null;
    }

    private FrontendCommunityProjectReleaseEntry? SelectPreferredCommunityProjectRelease(
        IEnumerable<FrontendCommunityProjectReleaseEntry> releases)
    {
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        var preferredLoader = ResolveSelectedInstanceLoaderLabel();

        return releases
            .OrderByDescending(release => ReleaseMatchesExactInstanceVersion(release, preferredVersion))
            .ThenByDescending(release => ReleaseMatchesExactInstanceLoader(release, preferredLoader))
            .ThenByDescending(release => release.PublishedUnixTime)
            .ThenBy(release => release.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private static bool ReleaseMatchesExactInstanceVersion(FrontendCommunityProjectReleaseEntry release, string? preferredVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            return false;
        }

        return release.GameVersions.Any(version =>
            string.Equals(NormalizeMinecraftVersion(version) ?? version, preferredVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReleaseMatchesExactInstanceLoader(FrontendCommunityProjectReleaseEntry release, string? preferredLoader)
    {
        if (string.IsNullOrWhiteSpace(preferredLoader))
        {
            return false;
        }

        return release.Loaders.Any(loader => string.Equals(loader, preferredLoader, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCompatibleCommunityProjectInstallRelease(
        FrontendCommunityProjectReleaseEntry release,
        string? preferredVersion,
        string? preferredLoader,
        LauncherFrontendSubpageKey? originSubpage)
    {
        if (!ReleaseMatchesExactInstanceVersion(release, NormalizeMinecraftVersion(preferredVersion)))
        {
            return false;
        }

        if (!RequiresCommunityProjectInstallLoader(originSubpage))
        {
            return true;
        }

        return ReleaseMatchesExactInstanceLoader(release, preferredLoader);
    }

    private static bool RequiresCommunityProjectInstallLoader(LauncherFrontendSubpageKey? originSubpage)
    {
        return originSubpage is LauncherFrontendSubpageKey.DownloadMod
            or LauncherFrontendSubpageKey.DownloadShader;
    }

    private IEnumerable<FrontendCommunityProjectReleaseEntry> GetVisibleCommunityProjectReleases((bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        return _communityProjectState.Releases.Where(release => MatchesCommunityProjectReleaseFilters(release, versionGrouping));
    }

    private bool MatchesCommunityProjectReleaseFilters(
        FrontendCommunityProjectReleaseEntry release,
        (bool GroupByDrop, bool FoldOld) versionGrouping)
    {
        var versions = release.GameVersions.Count == 0 ? ["其他"] : release.GameVersions;
        var loaders = release.Loaders.Count == 0 ? [string.Empty] : release.Loaders;

        var matchesVersion = string.IsNullOrWhiteSpace(_selectedCommunityProjectVersionFilter)
            || versions.Any(version => string.Equals(
                GetGroupedCommunityProjectVersionName(version, versionGrouping.GroupByDrop, versionGrouping.FoldOld),
                _selectedCommunityProjectVersionFilter,
                StringComparison.OrdinalIgnoreCase));
        var matchesLoader = string.IsNullOrWhiteSpace(_selectedCommunityProjectLoaderFilter)
            || loaders.Any(loader => string.Equals(loader, _selectedCommunityProjectLoaderFilter, StringComparison.OrdinalIgnoreCase));
        return matchesVersion && matchesLoader;
    }

    private static int GetCommunityProjectDependencyPriority(FrontendCommunityProjectDependencyKind kind)
    {
        return kind switch
        {
            FrontendCommunityProjectDependencyKind.Required => 0,
            FrontendCommunityProjectDependencyKind.Tool => 1,
            FrontendCommunityProjectDependencyKind.Include => 2,
            FrontendCommunityProjectDependencyKind.Optional => 3,
            FrontendCommunityProjectDependencyKind.Embedded => 4,
            FrontendCommunityProjectDependencyKind.Incompatible => 5,
            _ => 6
        };
    }

    private static string GetCommunityProjectDependencyGroupTitle(FrontendCommunityProjectDependencyKind kind)
    {
        return kind switch
        {
            FrontendCommunityProjectDependencyKind.Required => "必需依赖",
            FrontendCommunityProjectDependencyKind.Tool => "工具依赖",
            FrontendCommunityProjectDependencyKind.Include => "内含依赖",
            FrontendCommunityProjectDependencyKind.Optional => "可选依赖",
            FrontendCommunityProjectDependencyKind.Embedded => "已嵌入依赖",
            FrontendCommunityProjectDependencyKind.Incompatible => "不兼容项目",
            _ => "其他依赖"
        };
    }

    private void ApplyCurrentInstanceCommunityProjectFilters()
    {
        if (!ShouldAutoSyncCommunityProjectFiltersWithInstance(_selectedCommunityProjectOriginSubpage))
        {
            RebuildCommunityProjectSurfaceCollections();
            RaiseCommunityProjectProperties();
            return;
        }

        if (_communityProjectState.Releases.Count == 0)
        {
            RaiseCommunityProjectProperties();
            return;
        }

        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        var versionOptions = BuildCommunityProjectVersionOptions(versionGrouping);
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            var groupedPreferredVersion = GetGroupedCommunityProjectVersionName(
                preferredVersion,
                versionGrouping.GroupByDrop,
                versionGrouping.FoldOld);
            _selectedCommunityProjectVersionFilter = versionOptions.Any(option =>
                string.Equals(option, groupedPreferredVersion, StringComparison.OrdinalIgnoreCase))
                ? groupedPreferredVersion
                : string.Empty;
        }
        else
        {
            _selectedCommunityProjectVersionFilter = string.Empty;
        }

        var loaderOptions = BuildCommunityProjectLoaderOptions();
        var preferredLoader = ResolveSelectedInstanceLoaderLabel();
        _selectedCommunityProjectLoaderFilter = !string.IsNullOrWhiteSpace(preferredLoader)
            && loaderOptions.Any(option => string.Equals(option, preferredLoader, StringComparison.OrdinalIgnoreCase))
            ? preferredLoader
            : string.Empty;

        RebuildCommunityProjectSurfaceCollections();
        RaiseCommunityProjectProperties();
    }

    private Bitmap? GetCommunityProjectDependencyIcon()
    {
        return _selectedCommunityProjectOriginSubpage switch
        {
            LauncherFrontendSubpageKey.DownloadDataPack => LoadLauncherBitmap("Images", "Blocks", "RedstoneLampOn.png"),
            LauncherFrontendSubpageKey.DownloadResourcePack => LoadLauncherBitmap("Images", "Blocks", "Grass.png"),
            LauncherFrontendSubpageKey.DownloadShader => LoadLauncherBitmap("Images", "Blocks", "GoldBlock.png"),
            LauncherFrontendSubpageKey.DownloadWorld => LoadLauncherBitmap("Images", "Blocks", "GrassPath.png"),
            _ => LoadLauncherBitmap("Images", "Blocks", "CommandBlock.png")
        };
    }

    private void QueueCommunityProjectDependencyIconLoad(IEnumerable<DownloadCatalogSectionViewModel> sections)
    {
        foreach (var entry in sections.SelectMany(section => section.Items))
        {
            if (!entry.TryBeginIconLoad())
            {
                continue;
            }

            _ = LoadCommunityProjectDependencyIconAsync(entry);
        }
    }

    private async Task LoadCommunityProjectDependencyIconAsync(DownloadCatalogEntryViewModel entry)
    {
        var iconPath = await FrontendCommunityIconCache.EnsureCachedIconAsync(entry.IconUrl);
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

    private bool ShouldInstallCommunityProjectMissingDependencies()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
               && string.Equals(
                   SelectedCommunityProjectInstallModeOption?.FilterValue,
                   CommunityProjectInstallModeWithDependenciesValue,
                   StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveCommunityProjectReleaseFileName(FrontendCommunityProjectReleaseEntry entry, string? projectTitle = null)
    {
        var fileName = FrontendGameManagementService.ResolveCommunityResourceFileName(
            projectTitle,
            entry.SuggestedFileName,
            entry.Title,
            SelectedFileNameFormatIndex);
        return NormalizeCommunityProjectInstallArtifactFileName(_selectedCommunityProjectOriginSubpage, fileName);
    }

    private static string NormalizeCommunityProjectInstallArtifactFileName(
        LauncherFrontendSubpageKey? route,
        string fileName)
    {
        if (route != LauncherFrontendSubpageKey.DownloadDataPack)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return $"{fileName}.zip";
        }

        if (string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(fileName, ".zip");
        }

        return fileName;
    }

    private static string FinalizeCommunityProjectInstalledArtifact(
        LauncherFrontendSubpageKey? originSubpage,
        string downloadedPath,
        string? replacedPath = null)
    {
        if (originSubpage == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return FrontendWorldArchiveInstallService.ExtractInstalledWorldArchive(downloadedPath);
        }

        if (originSubpage == LauncherFrontendSubpageKey.DownloadDataPack)
        {
            return FrontendDatapackArchiveInstallService.ExtractInstalledDatapackArchive(downloadedPath, replacedPath);
        }

        return downloadedPath;
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
            AddFailureActivity($"{title} 失败", ex.Message);
        }
    }

    private string BuildCommunityProjectDescriptionCopyText()
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { CommunityProjectSummary, CommunityProjectDescription }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private bool IsCommunityProjectFavorite()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            return false;
        }

        try
        {
            var provider = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
            var raw = provider.Exists("CompFavorites")
                ? SafeReadSharedValue(provider, "CompFavorites", "[]")
                : "[]";
            var root = ParseCommunityProjectFavoriteTargets(raw);
            var target = GetSelectedDownloadFavoriteTarget(root);
            return EnsureCommunityProjectFavoriteArray(target)
                .Select(GetCommunityProjectFavoriteId)
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
        // Version filters are applied before ordering, so boosting the exact filter value here
        // would incorrectly push 1.21 above newer patch groups like 1.21.11.
        var priority = GetCommunityProjectVersionSortPriority(version) * 10;
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
        RaisePropertyChanged(nameof(CommunityProjectCurrentInstanceName));
        RaisePropertyChanged(nameof(CommunityProjectCurrentInstanceSummary));
        RaisePropertyChanged(nameof(ShowCommunityProjectInstallSuggestionCard));
        RaisePropertyChanged(nameof(CommunityProjectInstallSuggestionTitle));
        RaisePropertyChanged(nameof(CommunityProjectInstallSuggestionSummary));
        RaisePropertyChanged(nameof(CommunityProjectInstallModeOptions));
        RaisePropertyChanged(nameof(SelectedCommunityProjectInstallModeOption));
        RaisePropertyChanged(nameof(HasCommunityProjectActionButtons));
        RaisePropertyChanged(nameof(ShowCommunityProjectFilterCard));
        RaisePropertyChanged(nameof(ShowCommunityProjectVersionFilters));
        RaisePropertyChanged(nameof(ShowCommunityProjectLoaderFilters));
        RaisePropertyChanged(nameof(HasCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasNoCommunityProjectReleaseGroups));
        RaisePropertyChanged(nameof(HasCommunityProjectDependencySections));
        RaisePropertyChanged(nameof(CommunityProjectDependencyCardTitle));
        RaisePropertyChanged(nameof(ShowCommunityProjectDependencyCard));
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
            LauncherFrontendSubpageKey.DownloadDataPack => _versionSavesComposition.Selection.HasSelection
                ? _versionSavesComposition.Selection.DatapackDirectory
                : Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves"),
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

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}亿",
            >= 10_000 => $"{value / 10_000d:0.#}万",
            _ => value.ToString()
        };
    }

    private TimeSpan ResolveDownloadRequestTimeout()
    {
        var seconds = Math.Clamp((int)Math.Round(DownloadTimeoutSeconds), 1, 60);
        return TimeSpan.FromSeconds(seconds);
    }

    private string _communityProjectDependencyReleaseTitle = string.Empty;

    private sealed record CommunityProjectNavigationState(
        string ProjectId,
        string TitleHint,
        LauncherFrontendSubpageKey? OriginSubpage,
        string VersionFilter,
        string LoaderFilter)
    {
        public string TitleHintOrProjectId => string.IsNullOrWhiteSpace(TitleHint) ? ProjectId : TitleHint;
    }
}

internal sealed class FrontendManagedFileDownloadTask(
    string title,
    string sourceUrl,
    string targetPath,
    TimeSpan requestTimeout,
    FrontendDownloadTransferOptions? downloadOptions = null,
    Action<string>? onStarted = null,
    Action<string>? onCompleted = null,
    Action<string>? onFailed = null,
    string? userAgent = null) : ITask, ITaskProgressive, ITaskProgressStatus, ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", 1, null);

    public string Title { get; } = title;

    public TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        StateChanged(TaskState.Running, "正在取消下载…");
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var token = linkedCts.Token;
        StateChanged(TaskState.Waiting, "已加入任务中心");

        try
        {
            using var client = CreateDownloadHttpClient(requestTimeout, userAgent);
            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
                ? new FrontendDownloadSpeedLimiter(speedLimit)
                : null;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using (var sourceStream = await response.Content.ReadAsStreamAsync(token))
            {
                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
                var lastReportedBytes = 0L;
                var lastReportedAt = Environment.TickCount64;
                StateChanged(TaskState.Running, "正在下载文件…");
                onStarted?.Invoke(targetPath);

                await FrontendDownloadTransferService.CopyToPathAsync(
                    sourceStream,
                    targetPath,
                    totalRead =>
                    {
                        var progress = contentLength > 0
                            ? Math.Clamp(totalRead / (double)contentLength, 0d, 1d)
                            : 0d;
                        ProgressChanged(progress);

                        var now = Environment.TickCount64;
                        if (now - lastReportedAt < 250)
                        {
                            return;
                        }

                        var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                        var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                        PublishProgressStatus(progress, speed);
                        lastReportedAt = now;
                        lastReportedBytes = totalRead;
                        StateChanged(TaskState.Running, $"正在下载 {Path.GetFileName(targetPath)}…");
                    },
                    speedLimiter,
                    token);
            }

            ProgressChanged(1d);
            PublishProgressStatus(1d, 0d, 0);
            StateChanged(TaskState.Success, $"已保存到 {targetPath}");
            onCompleted?.Invoke(targetPath);
        }
        catch (OperationCanceledException)
        {
            CleanupPartialDownload();
            PublishProgressStatus(0d, 0d);
            StateChanged(TaskState.Canceled, "下载已取消");
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} 下载已取消");
            throw;
        }
        catch (Exception ex)
        {
            CleanupPartialDownload();
            PublishProgressStatus(0d, 0d);
            StateChanged(TaskState.Failed, ex.Message);
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} 下载失败");
            throw;
        }
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout, string? userAgent)
    {
        var safeTimeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(8) : timeout;
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            safeTimeout,
            userAgent,
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
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

    private void PublishProgressStatus(double progress, double speedBytesPerSecond, int? remainingFileCount = 1)
    {
        _progressStatus = new TaskProgressStatusSnapshot(
            $"{Math.Round(progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            $"{FormatBytes(speedBytesPerSecond)}/s",
            remainingFileCount,
            null);
        ProgressStatusChanged(_progressStatus);
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
