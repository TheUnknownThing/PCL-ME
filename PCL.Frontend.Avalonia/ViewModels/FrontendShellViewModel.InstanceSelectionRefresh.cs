using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
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
        _shellActionService.PersistLocalValue("LaunchFolderSelect", storedFolderPath);

        var localConfig = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
        var selectedInstanceName = ReadRememberedLaunchInstanceName(localConfig);
        SetOptimisticLaunchInstanceName(selectedInstanceName);
        RefreshInstanceSelectionSurface();
        RefreshInstanceSelectionRouteMetadata();

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity("切换实例目录", activityMessage);
        QueueSelectedInstanceStateRefresh(refreshVersion, suppressInstanceSelectionSurfaceRefresh: true);
    }

    private void RefreshSelectedInstanceSmoothly(string instanceName)
    {
        _shellActionService.PersistLocalValue("LaunchInstanceSelect", instanceName);
        ApplyOptimisticInstanceSelection(instanceName);
        SetOptimisticLaunchInstanceName(instanceName);

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity("切换启动实例", $"已切换到 {instanceName}，正在后台刷新实例状态。");
        QueueSelectedInstanceStateRefresh(refreshVersion);
    }

    private void QueueSelectedInstanceStateRefresh(
        int refreshVersion,
        bool suppressInstanceSelectionSurfaceRefresh = false)
    {
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
        var normalizedInstanceName = string.IsNullOrWhiteSpace(instanceName)
            ? "未选择实例"
            : instanceName.Trim();
        var previousDisplayName = GetDisplayedLaunchInstanceName();
        _optimisticLaunchInstanceName = normalizedInstanceName;
        _hasOptimisticLaunchInstanceName = true;

        if (raiseProperties && !string.Equals(previousDisplayName, GetDisplayedLaunchInstanceName(), StringComparison.Ordinal))
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
        _optimisticLaunchInstanceName = string.Empty;
        _hasOptimisticLaunchInstanceName = false;

        if (raiseProperties && !string.Equals(previousDisplayName, GetDisplayedLaunchInstanceName(), StringComparison.Ordinal))
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

            var runtimePaths = _shellActionService.RuntimePaths;
            var options = _options;
            var loadMode = ResolveInstanceCompositionLoadMode(_currentRoute);
            var refreshedState = await Task.Run(() => new DeferredInstanceSelectionRefreshState(
                FrontendInstanceCompositionService.Compose(runtimePaths, loadMode),
                FrontendLaunchCompositionService.Compose(options, runtimePaths)));

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
                ReloadToolsComposition();
                ReloadVersionSavesComposition();
                ReloadDownloadComposition(includeRemoteState: _downloadCompositionHasRemoteState);

                var handledSurfaceRefresh = false;
                if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
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
                        && IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceSelection))
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

            await Dispatcher.UIThread.InvokeAsync(() => AddFailureActivity("刷新实例状态失败", ex.Message));
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
