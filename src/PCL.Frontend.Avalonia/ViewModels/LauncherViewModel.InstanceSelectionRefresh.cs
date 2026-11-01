using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private int _instanceSelectionRefreshVersion;

    private string ReadRememberedLaunchInstanceName(YamlFileProvider localConfig)
    {
        return ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
    }

    private void RefreshSelectedLauncherFolderSmoothly(
        string storedFolderPath,
        string launcherDirectory,
        string activityMessage)
    {
        _launcherActionService.PersistLocalValue("LaunchFolderSelect", storedFolderPath);
        _launcherActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
        SetOptimisticLaunchInstanceName(null);
        RefreshInstanceSelectionSurface();
        RefreshInstanceSelectionRouteMetadata();

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity(LT("shell.instance_select.activities.switch_directory"), activityMessage);
        QueueSelectedInstanceStateRefresh(refreshVersion, suppressInstanceSelectionSurfaceRefresh: true);
    }

    private void RefreshSelectedInstanceSmoothly(string instanceName)
    {
        _launcherActionService.PersistLocalValue("LaunchInstanceSelect", instanceName);
        ApplyOptimisticInstanceSelection(instanceName);
        SetOptimisticLaunchInstanceName(instanceName);

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity(
            LT("shell.instance_select.activities.switch_instance"),
            LT("shell.instance_select.activities.switch_instance_detail", ("name", instanceName)));
        QueueSelectedInstanceStateRefresh(refreshVersion);
    }

    private void QueueSelectedInstanceStateRefresh(
        int refreshVersion,
        bool suppressInstanceSelectionSurfaceRefresh = false)
    {
        SetInstanceResourceLoading(ShouldShowInstanceResourceLoadingForRoute(_currentRoute));
        _selectedInstanceRefreshTask = RefreshSelectedInstanceStateAsync(
            refreshVersion,
            suppressInstanceSelectionSurfaceRefresh);
    }

    private async Task AwaitLatestSelectedInstanceRefreshAsync()
    {
        while (true)
        {
            var refreshTask = _selectedInstanceRefreshTask;
            await refreshTask;

            if (ReferenceEquals(refreshTask, _selectedInstanceRefreshTask))
            {
                return;
            }
        }
    }

    private void SetOptimisticLaunchInstanceName(string? instanceName, bool raiseProperties = true)
    {
        var hasSelectedInstance = !string.IsNullOrWhiteSpace(instanceName);
        var normalizedInstanceName = string.IsNullOrWhiteSpace(instanceName)
            ? LT("shell.instance_select.route.values.none_selected")
            : instanceName.Trim();
        var previousDisplayName = GetDisplayedLaunchInstanceName();
        var previousHasSelection = HasSelectedInstance;
        _optimisticLaunchInstanceName = normalizedInstanceName;
        _hasOptimisticLaunchInstanceName = true;
        _optimisticHasSelectedInstance = hasSelectedInstance;

        if (raiseProperties
            && (!string.Equals(previousDisplayName, GetDisplayedLaunchInstanceName(), StringComparison.Ordinal)
                || previousHasSelection != HasSelectedInstance))
        {
            RaiseLaunchInstanceDisplayProperties();
        }
    }

    private void ClearOptimisticLaunchInstanceName(bool raiseProperties = true)
    {
        if (!_hasOptimisticLaunchInstanceName)
        {
            return;
        }

        var previousDisplayName = GetDisplayedLaunchInstanceName();
        var previousHasSelection = HasSelectedInstance;
        _optimisticLaunchInstanceName = string.Empty;
        _hasOptimisticLaunchInstanceName = false;
        _optimisticHasSelectedInstance = false;

        if (raiseProperties
            && (!string.Equals(previousDisplayName, GetDisplayedLaunchInstanceName(), StringComparison.Ordinal)
                || previousHasSelection != HasSelectedInstance))
        {
            RaiseLaunchInstanceDisplayProperties();
        }
    }

    private string GetDisplayedLaunchInstanceName()
    {
        return _hasOptimisticLaunchInstanceName
            ? _optimisticLaunchInstanceName
            : _launchComposition.InstanceName;
    }

    private void RaiseLaunchInstanceDisplayProperties()
    {
        RaisePropertyChanged(nameof(HasSelectedInstance));
        RaisePropertyChanged(nameof(ShowLaunchVersionSetupButton));
        RaisePropertyChanged(nameof(LaunchVersionSelectButtonColumnSpan));
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(LaunchWelcomeBanner));
        RaisePropertyChanged(nameof(LaunchNewsTitle));
        RaisePropertyChanged(nameof(LaunchNewsBadgeText));
    }

    private async Task RefreshSelectedInstanceStateAsync(
        int refreshVersion,
        bool suppressInstanceSelectionSurfaceRefresh)
    {
        try
        {
            await Task.Yield();

            var runtimePaths = _launcherActionService.RuntimePaths;
            var options = _options;
            var loadMode = ResolveInstanceCompositionLoadMode(_currentRoute);
            var refreshedState = await Task.Run(() => new DeferredInstanceSelectionRefreshState(
                FrontendInstanceCompositionService.Compose(runtimePaths, loadMode, _i18n),
                FrontendLaunchCompositionService.Compose(options, runtimePaths, i18n: _i18n)));

            if (refreshVersion != Volatile.Read(ref _instanceSelectionRefreshVersion))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (refreshVersion != Volatile.Read(ref _instanceSelectionRefreshVersion))
                {
                    return;
                }

                _instanceComposition = refreshedState.InstanceComposition;
                _instanceCompositionLoadMode = loadMode;
                SetInstanceResourceLoading(false);
                ReloadToolsComposition();
                ReloadVersionSavesComposition();
                ReloadDownloadComposition(includeRemoteState: _downloadCompositionHasRemoteState);

                var handledSurfaceRefresh = false;
                if (IsCurrentStandardRightPane(StandardRightPaneKind.DownloadResource))
                {
                    RefreshDownloadResourceFiltersForSelectedInstance();
                    handledSurfaceRefresh = true;
                }

                if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail)
                {
                    ApplyCurrentInstanceCommunityProjectFilters();
                    handledSurfaceRefresh = true;
                }

                if (!handledSurfaceRefresh)
                {
                    if (suppressInstanceSelectionSurfaceRefresh
                        && IsCurrentStandardRightPane(StandardRightPaneKind.InstanceSelection))
                    {
                        RefreshInstanceSelectionRouteMetadata();
                    }
                    else
                    {
                        RefreshActiveRightPaneSurface();
                    }
                }

                ApplyLaunchComposition(refreshedState.LaunchComposition, normalizeLaunchProfileSurface: true);
            });
        }
        catch (Exception ex)
        {
            if (refreshVersion != Volatile.Read(ref _instanceSelectionRefreshVersion))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetInstanceResourceLoading(false);
                AddFailureActivity(LT("shell.instance_select.activities.refresh_failure"), ex.Message);
            });
        }
    }

    private void ApplyOptimisticInstanceSelection(string instanceName)
    {
        var optimisticEntries = InstanceSelectionEntries
            .Select(entry => CloneInstanceSelectionEntry(entry, instanceName))
            .ToArray();
        var optimisticGroups = InstanceSelectionGroups
            .Select(group =>
                new InstanceSelectionGroupViewModel(
                    group.Title,
                    group.HeaderText,
                    group.Entries.Select(entry => CloneInstanceSelectionEntry(entry, instanceName)).ToArray(),
                    group.IsExpanded))
            .ToArray();

        ReplaceItems(
            InstanceSelectionEntries,
            optimisticEntries);

        ReplaceItems(
            InstanceSelectionGroups,
            optimisticGroups);
    }

    private static InstanceSelectEntryViewModel CloneInstanceSelectionEntry(
        InstanceSelectEntryViewModel entry,
        string selectedInstanceName)
    {
        return new InstanceSelectEntryViewModel(
            entry.Title,
            entry.Subtitle,
            entry.Detail,
            entry.Tags,
            string.Equals(entry.Title, selectedInstanceName, StringComparison.OrdinalIgnoreCase),
            entry.IsFavorite,
            entry.Icon,
            entry.SelectText,
            entry.FavoriteToolTip,
            entry.OpenFolderToolTip,
            entry.DeleteToolTip,
            entry.SettingsToolTip,
            entry.SelectCommand,
            entry.OpenSettingsCommand,
            entry.ToggleFavoriteCommand,
            entry.OpenFolderCommand,
            entry.DeleteCommand);
    }

    private sealed record DeferredInstanceSelectionRefreshState(
        FrontendInstanceComposition InstanceComposition,
        FrontendLaunchComposition LaunchComposition);
}
