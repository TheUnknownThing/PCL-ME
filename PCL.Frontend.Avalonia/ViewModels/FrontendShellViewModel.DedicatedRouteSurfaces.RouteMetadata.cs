using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InitializeStepOneSurfaces()
    {
        TaskCenter.Tasks.CollectionChanged += OnTaskCenterCollectionChanged;
        EnsureTaskManagerRefreshTimer();
        SyncTaskSubscriptions();
    }

    private void RefreshCurrentDedicatedGenericRouteSurface()
    {
        switch (_currentRoute.Page)
        {
            case LauncherFrontendPageKey.InstanceSelect:
                RefreshInstanceSelectionSurface();
                break;
            case LauncherFrontendPageKey.TaskManager:
                RefreshTaskManagerSurface();
                break;
            case LauncherFrontendPageKey.GameLog:
                RefreshGameLogSurface();
                break;
            case LauncherFrontendPageKey.CompDetail:
                RefreshCompDetailSurface();
                break;
            case LauncherFrontendPageKey.HelpDetail:
                RefreshHelpDetailSurface();
                break;
        }
    }

    private bool TryBuildDedicatedGenericRouteMetadata(out DedicatedGenericRouteMetadata metadata)
    {
        switch (_currentRoute.Page)
        {
            case LauncherFrontendPageKey.InstanceSelect:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.navigation.pages.instance_select.title"),
                    LT("shell.instance_select.route.description"),
                    [
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.launch_directory"),
                            string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory)
                                ? LT("shell.instance_select.route.values.unresolved")
                                : _instanceSelectionLauncherDirectory),
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.selected_instance"),
                            _instanceComposition.Selection.HasSelection
                                ? _instanceComposition.Selection.InstanceName
                                : LT("shell.instance_select.route.values.none_selected")),
                        new LauncherFrontendPageFact(
                            LT("shell.instance_select.route.facts.result_count"),
                            $"{InstanceSelectionEntries.Count} / {_instanceSelectionTotalCount}")
                    ]);
                return true;
            case LauncherFrontendPageKey.TaskManager:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.navigation.pages.task_manager.title"),
                    LT("shell.task_manager.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.waiting"), TaskManagerWaitingCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.running"), TaskManagerRunningCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.finished"), TaskManagerFinishedCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.task_manager.route.facts.failed"), TaskManagerFailedCount.ToString())
                    ]);
                return true;
            case LauncherFrontendPageKey.GameLog:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.navigation.pages.game_log.title"),
                    LT("shell.game_log.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.live_lines"), GameLogLiveLineCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.recent_files"), GameLogRecentFileCount.ToString()),
                        new LauncherFrontendPageFact(LT("shell.game_log.route.facts.latest_update"), GameLogLatestUpdateLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.CompDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    LT("shell.comp_detail.route.eyebrow"),
                    LT("shell.comp_detail.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.source"), CommunityProjectSource),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.status"), CommunityProjectStatus),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.updated"), CommunityProjectUpdatedLabel),
                        new LauncherFrontendPageFact(LT("shell.comp_detail.route.facts.downloads"), CommunityProjectDownloadCountLabel)
                    ]);
                return true;
            case LauncherFrontendPageKey.HelpDetail:
                metadata = new DedicatedGenericRouteMetadata(
                    HelpDetailTitle,
                    LT("shell.help_detail.route.description"),
                    [
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.source"), HelpDetailSource),
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.lines"), HelpDetailSections.Sum(section => section.Lines.Count).ToString()),
                        new LauncherFrontendPageFact(LT("shell.help_detail.route.facts.actions"), HelpDetailSections.Sum(section => section.Actions.Count).ToString())
                    ]);
                return true;
            default:
                metadata = null!;
                return false;
        }
    }

    private void RefreshInstanceSelectionRouteMetadata()
    {
        if (_currentRoute.Page != LauncherFrontendPageKey.InstanceSelect)
        {
            return;
        }

        if (TryBuildDedicatedGenericRouteMetadata(out var metadata))
        {
            Eyebrow = metadata.Eyebrow;
            Description = metadata.Description;
            ReplaceSurfaceFactsIfChanged(metadata.Facts);
            ReplaceSurfaceSectionsIfChanged([]);
        }
    }
}

