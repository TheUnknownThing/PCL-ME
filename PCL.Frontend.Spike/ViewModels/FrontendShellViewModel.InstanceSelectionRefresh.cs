using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private int _instanceSelectionRefreshVersion;

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
        ReplaceItems(
            InstanceSelectionEntries,
            InstanceSelectionEntries.Select(entry => CloneInstanceSelectionEntry(entry, instanceName)));

        ReplaceItems(
            InstanceSelectionGroups,
            InstanceSelectionGroups.Select(group =>
                new InstanceSelectionGroupViewModel(
                    group.Title,
                    group.Entries.Select(entry => CloneInstanceSelectionEntry(entry, instanceName)).ToArray(),
                    group.IsExpanded)));
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
            entry.Icon,
            entry.SelectCommand,
            entry.OpenCommand,
            entry.OpenFolderCommand);
    }

    private sealed record DeferredInstanceSelectionRefreshState(
        FrontendInstanceComposition InstanceComposition,
        FrontendLaunchComposition LaunchComposition);
}
