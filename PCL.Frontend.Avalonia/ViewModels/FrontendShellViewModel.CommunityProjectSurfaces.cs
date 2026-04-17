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

    private string _selectedCommunityProjectId = string.Empty;

    private LauncherFrontendSubpageKey? _selectedCommunityProjectOriginSubpage;

    private string _selectedCommunityProjectVersionFilter = string.Empty;

    private string _selectedCommunityProjectLoaderFilter = string.Empty;

    private string _selectedCommunityProjectInstallMode = CommunityProjectInstallModeCurrentOnlyValue;

    private readonly List<CommunityProjectNavigationState> _communityProjectNavigationStack = [];

    private Bitmap? _communityProjectIcon;

    private FrontendCommunityProjectState _communityProjectState = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        null,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        string.Empty,
        string.Empty,
        [],
        [],
        string.Empty,
        false);

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

    public string CommunityProjectStatus => LocalizeCommunityProjectStatus(_communityProjectState.Status);

    public string CommunityProjectUpdatedLabel => LocalizeCommunityProjectUpdatedLabel(_communityProjectState.UpdatedLabel);

    public string CommunityProjectCompatibilitySummary => LocalizeCommunityProjectCompatibilitySummary(_communityProjectState.CompatibilitySummary);

    public string CommunityProjectCategorySummary => LocalizeCommunityProjectCategorySummary(_communityProjectState.CategorySummary);

    public string CommunityProjectDownloadCountLabel => _communityProjectState.DownloadCount <= 0
        ? T("resource_detail.values.none")
        : FormatCompactCount(_communityProjectState.DownloadCount);

    public string CommunityProjectFollowCountLabel => _communityProjectState.FollowCount <= 0
        ? T("resource_detail.values.none")
        : FormatCompactCount(_communityProjectState.FollowCount);

    public bool HasCommunityProjectDescription => !string.IsNullOrWhiteSpace(CommunityProjectDescription);

    public string CommunityProjectIntroDescription => !string.IsNullOrWhiteSpace(CommunityProjectSummary)
        ? CommunityProjectSummary
        : CommunityProjectDescription;

    public IReadOnlyList<string> CommunityProjectCategoryTags => BuildCommunityProjectCategoryTags(_communityProjectState.CategorySummary);

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
                    : T("resource_detail.current_instance.none_selected");
            }

            return _instanceComposition.Selection.HasSelection
                ? _instanceComposition.Selection.InstanceName
                : T("resource_detail.current_instance.none_selected");
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
                    return T("resource_detail.current_instance.summary_none_selected_save");
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
                return T("resource_detail.current_instance.summary_none_selected");
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

    public string CommunityProjectInstallSuggestionTitle => GetSuggestedCommunityProjectInstallRelease()?.Title ?? T("resource_detail.suggested_release.none");

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
                parts.Add(T("resource_detail.suggested_release.target_save", ("save_name", _versionSavesComposition.Selection.SaveName)));
            }

            var localizedInfo = LocalizeCommunityProjectReleaseInfo(release.Info);
            if (!string.IsNullOrWhiteSpace(localizedInfo))
            {
                parts.Add(localizedInfo);
            }

            var localizedMeta = LocalizeCommunityProjectReleaseMeta(release.Meta);
            if (!string.IsNullOrWhiteSpace(localizedMeta))
            {
                parts.Add(localizedMeta);
            }

            return string.Join(" • ", parts);
        }
    }

    public ActionCommand InstallCommunityProjectToCurrentInstanceCommand => new(() => _ = InstallCommunityProjectToCurrentInstanceAsync());

    public ActionCommand ExecuteCommunityProjectInstallSuggestionCommand => new(() => _ = InstallCommunityProjectToCurrentInstanceAsync());

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectInstallModeOptions =>
        (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
            ? CommunityProjectModInstallModeOptions
            : CommunityProjectSingleInstallModeOptions)
        .Select(option => new DownloadResourceFilterOptionViewModel(
            T(option.Label),
            option.FilterValue,
            option.IsHeader))
        .ToArray();

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
        ? T("resource_detail.dependencies.title")
        : T("resource_detail.dependencies.title_for_release", ("release_title", _communityProjectDependencyReleaseTitle));

    public bool ShowCommunityProjectDependencyCard => _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
        && HasCommunityProjectDependencySections;

    public bool HasCommunityProjectSections => CommunityProjectSections.Count > 0;

    public bool HasNoCommunityProjectSections => !HasCommunityProjectSections;

    public bool ShowCommunityProjectWarning => _communityProjectState.ShowWarning;

    public string CommunityProjectWarningText => LocalizeCommunityProjectWarningText(_communityProjectState.WarningText);

    private string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => T("resource_detail.counts.hundred_million", ("value", $"{value / 100_000_000d:0.#}")),
            >= 10_000 => T("resource_detail.counts.ten_thousand", ("value", $"{value / 10_000d:0.#}")),
            _ => value.ToString()
        };
    }

}
