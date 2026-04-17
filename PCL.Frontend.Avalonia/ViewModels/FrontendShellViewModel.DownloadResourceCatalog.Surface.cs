using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public string DownloadResourceSurfaceTitle
    {
        get => _downloadResourceSurfaceTitle;
        private set => SetProperty(ref _downloadResourceSurfaceTitle, value);
    }

    public string DownloadResourceSearchWatermark => _currentRoute.Subpage switch
    {
        LauncherFrontendSubpageKey.DownloadMod => T("download.resource.search.mod"),
        LauncherFrontendSubpageKey.DownloadPack => T("download.resource.search.pack"),
        LauncherFrontendSubpageKey.DownloadDataPack => T("download.resource.search.data_pack"),
        LauncherFrontendSubpageKey.DownloadResourcePack => T("download.resource.search.resource_pack"),
        LauncherFrontendSubpageKey.DownloadShader => T("download.resource.search.shader"),
        LauncherFrontendSubpageKey.DownloadWorld => T("download.resource.search.world"),
        _ => T("download.resource.search.default")
    };

    public string DownloadResourceCurrentInstanceTitle
    {
        get
        {
            if (_currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadDataPack)
            {
                return _versionSavesComposition.Selection.HasSelection
                    ? _versionSavesComposition.Selection.SaveName
                    : T("download.resource.current_instance.none_selected");
            }

            return _instanceComposition.Selection.HasSelection
                ? _instanceComposition.Selection.InstanceName
                : T("download.resource.current_instance.none_selected");
        }
    }

    public string DownloadResourceCurrentInstanceCardTitle => _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadDataPack
        ? T("download.resource.current_instance.title_save")
        : T("download.resource.current_instance.title");

    public string DownloadResourceCurrentInstanceSummary
    {
        get
        {
            if (_currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadDataPack)
            {
                if (!_versionSavesComposition.Selection.HasSelection)
                {
                    return T("download.resource.current_instance.summary_none_selected_save");
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
                return T("download.resource.current_instance.summary_none_selected");
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_instanceComposition.Selection.VanillaVersion))
            {
                parts.Add($"Minecraft {_instanceComposition.Selection.VanillaVersion}");
            }

            var loader = ResolveSelectedInstanceLoaderLabel();
            if (!string.IsNullOrWhiteSpace(loader))
            {
                parts.Add(loader);
            }

            parts.Add(T("download.resource.current_instance.summary_suffix"));
            return string.Join(" • ", parts);
        }
    }

    private string GetLocalizedDownloadResourceSurfaceName(LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => T("download.resource.search.mod"),
            LauncherFrontendSubpageKey.DownloadPack => T("download.resource.search.pack"),
            LauncherFrontendSubpageKey.DownloadDataPack => T("download.resource.search.data_pack"),
            LauncherFrontendSubpageKey.DownloadResourcePack => T("download.resource.search.resource_pack"),
            LauncherFrontendSubpageKey.DownloadShader => T("download.resource.search.shader"),
            LauncherFrontendSubpageKey.DownloadWorld => T("download.resource.search.world"),
            _ => T("download.resource.search.default")
        };
    }

    public bool ShowDownloadResourceCurrentInstanceCard => _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack;

    public bool ShowDownloadResourceLoaderFilter => _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadWorld;

    public string DownloadResourceCurrentInstanceActionText => _currentRoute.Subpage == LauncherFrontendSubpageKey.DownloadDataPack
        ? T("download.resource.current_instance.actions.switch_save")
        : T("download.resource.current_instance.actions.switch");

    public ActionCommand SelectDownloadResourceInstanceCommand => new(() => _ = OpenDownloadResourceTargetContextAsync());

    public string DownloadResourceLoadingText
    {
        get => _downloadResourceLoadingText;
        private set => SetProperty(ref _downloadResourceLoadingText, value);
    }

    public string DownloadResourceEmptyStateText
    {
        get => _downloadResourceEmptyStateText;
        private set => SetProperty(ref _downloadResourceEmptyStateText, value);
    }

    public string DownloadResourceEmptyStateHintText
    {
        get => _downloadResourceEmptyStateHintText;
        private set
        {
            if (SetProperty(ref _downloadResourceEmptyStateHintText, value))
            {
                RaisePropertyChanged(nameof(ShowDownloadResourceEmptyStateHint));
            }
        }
    }

    public bool ShowDownloadResourceEmptyStateHint => !string.IsNullOrWhiteSpace(DownloadResourceEmptyStateHintText);

    public string DownloadResourceHintText
    {
        get => _downloadResourceHintText;
        private set => SetProperty(ref _downloadResourceHintText, value);
    }

    public bool ShowDownloadResourceHint
    {
        get => _showDownloadResourceHint;
        private set => SetProperty(ref _showDownloadResourceHint, value);
    }

    public bool ShowDownloadResourceInstallModPackAction
    {
        get => _showDownloadResourceInstallModPackAction;
        private set => SetProperty(ref _showDownloadResourceInstallModPackAction, value);
    }
    private void RefreshDownloadResourceSurface()
    {
        DownloadResourceSurfaceTitle = string.Empty;
        DownloadResourceLoadingText = string.Empty;
        DownloadResourceEmptyStateText = T("download.resource.empty.default");
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = false;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceTotalEntryCount = 0;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions = [];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceVersionOptions = BuildDefaultDownloadResourceVersionOptions();
        _downloadResourceLoaderOptions = BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _allDownloadResourceEntries = [];
        ReplaceItems(DownloadResourceEntries, []);
        SetDownloadResourceLoading(false);

        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
        {
            RaisePropertyChanged(nameof(DownloadResourceSearchWatermark));
            RaisePropertyChanged(nameof(DownloadResourceSourceOptions));
            RaisePropertyChanged(nameof(DownloadResourceTagOptions));
            RaisePropertyChanged(nameof(DownloadResourceLoaderOptions));
            RaisePropertyChanged(nameof(ShowDownloadResourceCurrentInstanceCard));
            RaisePropertyChanged(nameof(ShowDownloadResourceLoaderFilter));
            RaisePropertyChanged(nameof(HasDownloadResourceEntries));
            RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
            RaisePropertyChanged(nameof(ShowDownloadResourceLoadingCard));
            RaisePropertyChanged(nameof(ShowDownloadResourceContent));
            RaisePropertyChanged(nameof(DownloadResourcePageLabel));
            RaisePropertyChanged(nameof(ShowDownloadResourcePagination));
            NotifyDownloadResourcePageCommandState();
            return;
        }

        var (showInstallModPackAction, useShaderLoaderOptions) = GetDownloadResourceSurfaceDescriptor(_currentRoute.Subpage);
        var surfaceTitle = GetLocalizedDownloadResourceSurfaceName(_currentRoute.Subpage);
        DownloadResourceSurfaceTitle = T("download.resource.surface.title", ("surface_name", surfaceTitle));
        DownloadResourceLoadingText = T("download.resource.surface.loading", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateText = T("download.resource.surface.empty", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = string.Empty;
        ShowDownloadResourceHint = false;
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = true;
        _downloadResourceSourceOptions =
        [
            CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
            CreateDownloadResourceFilterOption("CurseForge", "CurseForge"),
            CreateDownloadResourceFilterOption("Modrinth", "Modrinth")
        ];
        _downloadResourceTagOptions = BuildFallbackDownloadResourceTagOptions();
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceVersionOptions = BuildDefaultDownloadResourceVersionOptions(
            ShouldAutoSyncDownloadResourceFiltersWithInstance()
                ? ResolveSelectedDownloadResourceVersionFilter()
                : null);
        _downloadResourceLoaderOptions = useShaderLoaderOptions
            ? BuildDefaultShaderLoaderOptions()
            : BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _downloadResourceRuntimeStates.Remove(_currentRoute.Subpage);
        ResetDownloadResourceFilterState();
        RaiseDownloadResourceFilterState();
        SetDownloadResourceLoading(true);
        RaisePropertyChanged(nameof(HasDownloadResourceEntries));
        RaisePropertyChanged(nameof(HasNoDownloadResourceEntries));
        ScheduleDownloadResourceRefresh(immediate: true, resetPage: true);
    }

    private void ConfigureDownloadResourceSurface(
        string surfaceTitle,
        bool supportsModrinth,
        bool showInstallModPackAction,
        bool useShaderLoader,
        IReadOnlyList<DownloadResourceFilterOptionViewModel> tagOptions,
        IReadOnlyList<DownloadResourceEntryViewModel> entries)
    {
        DownloadResourceSurfaceTitle = T("download.resource.surface.title", ("surface_name", surfaceTitle));
        DownloadResourceLoadingText = T("download.resource.surface.loading", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateText = T("download.resource.surface.empty", ("surface_name", surfaceTitle));
        DownloadResourceEmptyStateHintText = string.Empty;
        DownloadResourceHintText = T("download.resource.hints.modrinth_unavailable");
        ShowDownloadResourceInstallModPackAction = showInstallModPackAction;
        _downloadResourceSupportsModrinth = supportsModrinth;
        _downloadResourceHasMoreEntries = false;
        _downloadResourceSourceOptions = supportsModrinth
            ? [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption("CurseForge", "CurseForge"),
                CreateDownloadResourceFilterOption("Modrinth", "Modrinth")
            ]
            : [
                CreateDownloadResourceFilterOption(T("common.filters.all"), string.Empty),
                CreateDownloadResourceFilterOption("CurseForge", "CurseForge")
            ];
        _downloadResourceTagOptions = tagOptions;
        _downloadResourceSortOptions = BuildDownloadResourceSortOptions();
        _downloadResourceLoaderOptions = useShaderLoader
            ? [
                CreateDownloadResourceFilterOption(T("common.filters.any"), string.Empty),
                CreateDownloadResourceFilterOption("vanilla_compatible", "vanilla_compatible"),
                CreateDownloadResourceFilterOption("Iris", "Iris"),
                CreateDownloadResourceFilterOption("OptiFine", "OptiFine")
            ]
            : BuildDefaultResourceLoaderOptions(IgnoreQuiltLoader);
        _allDownloadResourceEntries = entries;
        ResetDownloadResourceFilterState();
    }
    private static (bool ShowInstallModPackAction, bool UseShaderLoaderOptions) GetDownloadResourceSurfaceDescriptor(
        LauncherFrontendSubpageKey route)
    {
        return route switch
        {
            LauncherFrontendSubpageKey.DownloadMod => (false, false),
            LauncherFrontendSubpageKey.DownloadPack => (true, false),
            LauncherFrontendSubpageKey.DownloadDataPack => (false, false),
            LauncherFrontendSubpageKey.DownloadResourcePack => (false, false),
            LauncherFrontendSubpageKey.DownloadShader => (false, true),
            LauncherFrontendSubpageKey.DownloadWorld => (false, false),
            _ => (false, false)
        };
    }
    private async Task OpenDownloadResourceTargetContextAsync()
    {
        if (_currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadDataPack)
        {
            await SelectCommunityProjectInstanceAsync();
            return;
        }

        await SwitchDownloadResourceDatapackSaveAsync();
    }

    private async Task SwitchDownloadResourceDatapackSaveAsync()
    {
        var activityTitle = T("download.resource.current_instance.activities.switch_save");
        var instances = LoadAvailableDownloadTargetInstances();
        if (instances.Count == 0)
        {
            AddActivity(activityTitle, T("download.resource.current_instance.messages.no_instances_available"));
            return;
        }

        string? selectedInstanceId;
        try
        {
            selectedInstanceId = await _shellActionService.PromptForChoiceAsync(
                T("download.resource.current_instance.dialogs.select_instance.title"),
                T("download.resource.current_instance.dialogs.select_instance.message"),
                instances.Select(entry => new PclChoiceDialogOption(
                    entry.Name,
                    entry.Name,
                    entry.Subtitle))
                    .ToArray(),
                _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : instances[0].Name,
                T("common.actions.continue"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.resource.current_instance.dialogs.select_instance.failed"), ex.Message);
            return;
        }

        var selectedInstance = string.IsNullOrWhiteSpace(selectedInstanceId)
            ? null
            : instances.FirstOrDefault(entry => string.Equals(entry.Name, selectedInstanceId, StringComparison.OrdinalIgnoreCase));
        if (selectedInstance is null)
        {
            return;
        }

        var targetComposition = FrontendInstanceCompositionService.Compose(_shellActionService.RuntimePaths, selectedInstance.Name);
        if (!targetComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, T("download.resource.current_instance.messages.instance_unavailable", ("instance_name", selectedInstance.Name)));
            return;
        }

        var saves = targetComposition.World.Entries;
        if (saves.Count == 0)
        {
            AddActivity(activityTitle, T("download.resource.current_instance.messages.no_saves_available", ("instance_name", selectedInstance.Name)));
            return;
        }

        var defaultSavePath = string.Equals(selectedInstance.Name, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase)
                              && _versionSavesComposition.Selection.HasSelection
            ? _versionSavesComposition.Selection.SavePath
            : saves[0].Path;

        string? selectedSavePath;
        try
        {
            selectedSavePath = await _shellActionService.PromptForChoiceAsync(
                T("download.resource.current_instance.dialogs.select_save.title"),
                T("download.resource.current_instance.dialogs.select_save.message", ("instance_name", selectedInstance.Name)),
                saves.Select(entry => new PclChoiceDialogOption(
                    entry.Path,
                    entry.Title,
                    entry.Summary))
                    .ToArray(),
                defaultSavePath,
                T("download.resource.current_instance.actions.switch_save"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.resource.current_instance.dialogs.select_save.failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedSavePath))
        {
            return;
        }

        var selectedSave = saves.FirstOrDefault(entry => string.Equals(entry.Path, selectedSavePath, StringComparison.OrdinalIgnoreCase));
        if (selectedSave is null)
        {
            AddActivity(activityTitle, T("download.resource.current_instance.messages.save_not_found"));
            return;
        }

        var isSameInstance = string.Equals(selectedInstance.Name, _instanceComposition.Selection.InstanceName, StringComparison.OrdinalIgnoreCase);
        var isSameSave = string.Equals(selectedSave.Path, _versionSavesComposition.Selection.SavePath, StringComparison.OrdinalIgnoreCase);
        if (isSameInstance && _versionSavesComposition.Selection.HasSelection && isSameSave)
        {
            AddActivity(activityTitle, T("download.resource.current_instance.messages.save_already_selected", ("instance_name", selectedInstance.Name), ("save_name", selectedSave.Title)));
            return;
        }

        if (!isSameInstance)
        {
            RefreshSelectedInstanceSmoothly(selectedInstance.Name);
            await AwaitLatestSelectedInstanceRefreshAsync();
        }

        _selectedVersionSavePath = selectedSave.Path;
        ReloadVersionSavesComposition();
        AddActivity(activityTitle, $"{selectedInstance.Name} • {selectedSave.Title}");
    }

    private bool ShouldAutoSyncDownloadResourceFiltersWithInstance()
    {
        return _currentRoute.Subpage != LauncherFrontendSubpageKey.DownloadPack;
    }
}
