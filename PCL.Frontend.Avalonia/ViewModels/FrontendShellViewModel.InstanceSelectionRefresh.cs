using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private int _instanceSelectionRefreshVersion;

    private void RefreshSelectedLauncherFolderSmoothly(
        string storedFolderPath,
        string launcherDirectory,
        string activityMessage)
    {
        _shellActionService.PersistLocalValue("LaunchFolderSelect", storedFolderPath);

        var localConfig = new YamlFileProvider(_shellActionService.RuntimePaths.LocalConfigPath);
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(selectedInstanceName) &&
            !Directory.Exists(Path.Combine(launcherDirectory, "versions", selectedInstanceName)))
        {
            _shellActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
        }

        RefreshInstanceSelectionSurface();
        RefreshInstanceSelectionRouteMetadata();

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity("切换实例目录", activityMessage);
        _ = RefreshSelectedInstanceStateAsync(refreshVersion);
    }

    private void RefreshSelectedInstanceSmoothly(string instanceName)
    {
        _shellActionService.PersistLocalValue("LaunchInstanceSelect", instanceName);
        ApplyOptimisticInstanceSelection(instanceName);

        var refreshVersion = Interlocked.Increment(ref _instanceSelectionRefreshVersion);
        AddActivity("切换启动实例", $"已切换到 {instanceName}，正在后台刷新实例状态。");
        _ = RefreshSelectedInstanceStateAsync(refreshVersion);
    }

    private async Task RefreshSelectedInstanceStateAsync(int refreshVersion)
    {
        try
        {
            await Task.Yield();

            var runtimePaths = _shellActionService.RuntimePaths;
            var options = _options;
            var refreshedState = await Task.Run(() => new DeferredInstanceSelectionRefreshState(
                FrontendInstanceCompositionService.Compose(runtimePaths),
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
                _launchComposition = refreshedState.LaunchComposition;
                NormalizeLaunchProfileSurface();

                var launchPromptContextKey = BuildLaunchPromptContextKey(
                    _launchComposition,
                    _instanceComposition.Selection.InstanceDirectory);
                if (!string.Equals(_launchPromptContextKey, launchPromptContextKey, StringComparison.Ordinal))
                {
                    _dismissedLaunchPromptIds.Clear();
                    _launchPromptContextKey = launchPromptContextKey;
                }

                RaiseLaunchCompositionProperties();
            });
        }
        catch (Exception ex)
        {
            if (refreshVersion != Volatile.Read(ref _instanceSelectionRefreshVersion))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => AddActivity("刷新实例状态失败", ex.Message));
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
