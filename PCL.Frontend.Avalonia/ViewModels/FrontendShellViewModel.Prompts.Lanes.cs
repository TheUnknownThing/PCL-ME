using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using System.Runtime.InteropServices;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void InitializePromptLanes()
    {
        RebuildPromptLanes();
        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane);

        if (_promptCatalog[AvaloniaPromptLaneKind.Startup].Count > 0)
        {
            SetPromptOverlayOpen(true);
        }
    }

    private void SelectPromptLane(AvaloniaPromptLaneKind lane, bool updateActivity = true, bool raiseCollectionState = true)
    {
        if (_promptCatalog[lane].Count == 0)
        {
            var firstAvailableLane = GetFirstAvailablePromptLane();
            if (firstAvailableLane is null)
            {
                _selectedPromptLane = lane;
                ReplaceItems(ActivePrompts, []);
                RaisePropertyChanged(nameof(HasActivePrompts));
                RaisePropertyChanged(nameof(HasNoActivePrompts));
                RaisePropertyChanged(nameof(CurrentPrompt));
                RaisePropertyChanged(nameof(HasCurrentPrompt));
                RaisePromptOverlayPresentationProperties();
                return;
            }

            lane = firstAvailableLane.Value;
        }

        _selectedPromptLane = lane;
        SyncPromptLaneState();
        ReplaceItems(ActivePrompts, _promptCatalog[lane]);
        RaisePropertyChanged(nameof(HasActivePrompts));
        RaisePropertyChanged(nameof(HasNoActivePrompts));
        RaisePropertyChanged(nameof(CurrentPrompt));
        RaisePropertyChanged(nameof(HasCurrentPrompt));
        RaisePromptOverlayPresentationProperties();

        var selectedLane = ResolvePromptLaneViewModel(lane);
        var (laneTitle, laneSummary) = selectedLane is null
            ? GetPromptLaneMetadata(lane)
            : (selectedLane.Title, selectedLane.Summary);
        var laneCount = selectedLane?.Count ?? _promptCatalog[lane].Count;
        PromptInboxTitle = _i18n.T(
            "shell.prompts.inbox.title",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["lane"] = laneTitle
            });
        PromptInboxSummary = laneSummary;
        PromptEmptyState = _i18n.T(
            "shell.prompts.inbox.empty",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["lane"] = laneTitle
            });
        var pageContent = BuildPageContent(BuildShellPlan());
        ReplaceSurfaceFactsIfChanged(pageContent.Facts);
        ReplaceSurfaceSectionsIfChanged(pageContent.Sections);
        if (raiseCollectionState)
        {
            RaiseCollectionStateProperties();
        }

        if (updateActivity)
        {
            AddActivity(
                T("shell.prompts.activities.switch_lane.title"),
                T("shell.prompts.activities.switch_lane.body", ("lane", laneTitle), ("count", laneCount)));
        }
    }

    private PromptLaneViewModel? ResolvePromptLaneViewModel(AvaloniaPromptLaneKind lane)
    {
        var selectedLane = PromptLanes.FirstOrDefault(item => item.Kind == lane);
        if (selectedLane is not null || _promptCatalog[lane].Count == 0)
        {
            return selectedLane;
        }

        RebuildPromptLanes();
        SyncPromptLaneState();
        return PromptLanes.FirstOrDefault(item => item.Kind == lane);
    }

    private void SyncPromptLaneState()
    {
        foreach (var lane in PromptLanes)
        {
            lane.Count = _promptCatalog[lane.Kind].Count;
            lane.IsSelected = lane.Kind == _selectedPromptLane;
        }
    }

    private void RebuildPromptLanes()
    {
        var visibleLanes = new[]
            {
                AvaloniaPromptLaneKind.Startup,
                AvaloniaPromptLaneKind.Launch,
                AvaloniaPromptLaneKind.Crash
            }
            .Where(kind => _promptCatalog[kind].Count > 0)
            .Select(CreatePromptLane)
            .ToArray();

        ReplaceItems(PromptLanes, visibleLanes);
    }

    private PromptLaneViewModel CreatePromptLane(AvaloniaPromptLaneKind lane)
    {
        var (title, summary) = GetPromptLaneMetadata(lane);
        return new PromptLaneViewModel(
            lane,
            title,
            summary,
            new ActionCommand(() => SelectPromptLane(lane)));
    }

    private (string Title, string Summary) GetPromptLaneMetadata(AvaloniaPromptLaneKind lane)
    {
        return lane switch
        {
            AvaloniaPromptLaneKind.Startup => (
                _i18n.T("shell.prompts.lanes.startup.title"),
                _i18n.T("shell.prompts.lanes.startup.summary")),
            AvaloniaPromptLaneKind.Launch => (
                _i18n.T("shell.prompts.lanes.launch.title"),
                _i18n.T("shell.prompts.lanes.launch.summary")),
            AvaloniaPromptLaneKind.Crash => (
                _i18n.T("shell.prompts.lanes.crash.title"),
                _i18n.T("shell.prompts.lanes.crash.summary")),
            _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown prompt lane.")
        };
    }

    private AvaloniaPromptLaneKind? GetFirstAvailablePromptLane()
    {
        foreach (var lane in new[] { AvaloniaPromptLaneKind.Startup, AvaloniaPromptLaneKind.Launch, AvaloniaPromptLaneKind.Crash })
        {
            if (_promptCatalog[lane].Count > 0)
            {
                return lane;
            }
        }

        return null;
    }
}
