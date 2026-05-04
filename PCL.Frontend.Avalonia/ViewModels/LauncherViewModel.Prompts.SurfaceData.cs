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

internal sealed partial class LauncherViewModel
{
    private void HandleI18nChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            ApplyLaunchComposition(
                FrontendLaunchCompositionService.Compose(_options, _launcherActionService.RuntimePaths, i18n: _i18n),
                normalizeLaunchProfileSurface: false);
            var hadCrashPrompts = _promptCatalog.TryGetValue(AvaloniaPromptLaneKind.Crash, out var crashPrompts) &&
                                  crashPrompts.Count > 0;

            _updateStatus = FrontendSetupUpdateStatusService.Relocalize(_updateStatus, _i18n);
            RefreshSetupLocalizationState();
            RaiseSectionBLocalizedProperties();
            EnsureStartupPromptLane();
            EnsureLaunchPromptLane();
            if (hadCrashPrompts)
            {
                EnsureCrashPromptLane();
            }
            else
            {
                _activeCrashPlan = FrontendCrashCompositionService.CreateDeferredPlan(_launcherActionService.RuntimePaths, _i18n);
                _promptCatalog[AvaloniaPromptLaneKind.Crash] = [];
            }
            RebuildPromptLanes();
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false, raiseCollectionState: false);
            ReloadDownloadComposition(includeRemoteState: _downloadCompositionHasRemoteState);
            RefreshDownloadInstallSurfaceState();
            RefreshDownloadResourceSurface();
            RefreshDownloadFavoriteSurface();
            if (_currentRoute.Page == LauncherFrontendPageKey.CompDetail)
            {
                RefreshCompDetailSurface();
            }

            RefreshLaunchProfileEntries();
            RaiseLaunchCompositionProperties();
            RaiseSectionAI18nProperties();
            RaisePropertyChanged(nameof(HomepagePresetOptions));
            RaisePropertyChanged(nameof(MemorySummaryUsageHeaderText));
            RaisePropertyChanged(nameof(MemorySummaryAllocationPrefixText));
            RaisePropertyChanged(nameof(CustomRamAllocationLabel));
            RaisePropertyChanged(nameof(UsedRamLabel));
            RaisePropertyChanged(nameof(TotalRamLabel));
            RaisePropertyChanged(nameof(AllocatedRamLabel));
            RaisePropertyChanged(nameof(InstanceCustomRamAllocationLabel));
            RaisePropertyChanged(nameof(InstanceUsedRamLabel));
            RaisePropertyChanged(nameof(InstanceTotalRamLabel));
            RaisePropertyChanged(nameof(InstanceAllocatedRamLabel));
            RaiseDownloadFavoriteSelectionProperties();
            RaiseLaunchDialogProperties();
            RefreshToolsTestLocalization();
            ReloadHelpState();
            RefreshCurrentDedicatedGenericRouteSurface();
            RefreshInstanceSelectionRouteMetadata();
            RefreshLauncherStateCore(activityMessage: null, addActivity: false);
            RaiseLaunchSessionProperties();
            RaiseGameLogSurfaceProperties();
            RefreshSectionDI18nSurfaces();

            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                RaiseActiveSetupSurfaceProperties();
            }
        });
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendPlan shellPlan)
    {
        return LauncherLocalizationService.BuildPageContent(
            shellPlan,
            _currentNavigation ?? LauncherLocalizationService.LocalizeNavigationView(shellPlan.Navigation, _i18n),
            BuildPromptLaneSummaries(),
            BuildLaunchSurfaceData(),
            BuildCrashSurfaceData(),
            _i18n);
    }

    private LauncherFrontendPromptLaneSummary[] BuildPromptLaneSummaries()
    {
        return PromptLanes
            .Select(lane => new LauncherFrontendPromptLaneSummary(
                lane.Kind.ToString().ToLowerInvariant(),
                lane.Title,
                lane.Summary,
                lane.Count,
                lane.IsSelected))
            .ToArray();
    }

    private LauncherFrontendLaunchSurfaceData BuildLaunchSurfaceData()
    {
        return new LauncherFrontendLaunchSurfaceData(
            _launchComposition.Scenario,
            LaunchAuthLabel,
            GetLaunchProfileIdentityLabel(),
            _launchComposition.SelectedProfile.Kind == MinecraftLaunchProfileKind.None ? 0 : 1,
            GetLaunchJavaRuntimeLabel(),
            _launchComposition.JavaWarningMessage,
            GetPendingJavaPrompt()?.DownloadTarget,
            $"{_launchComposition.ResolutionPlan.Width} x {_launchComposition.ResolutionPlan.Height}",
            _launchComposition.ClasspathPlan.Entries.Count,
            _launchComposition.ReplacementPlan.Values.Count,
            _launchComposition.NativesDirectory,
            _launchComposition.PrerunPlan.Options.TargetFilePath,
            _launchComposition.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite,
            false,
            null,
            _i18n.T(_launchComposition.CompletionNotification.Message));
    }

    private string GetLaunchJavaRuntimeLabel()
    {
        if (_launchComposition.SelectedJavaRuntime is not null)
        {
            return _launchComposition.SelectedJavaRuntime.DisplayName;
        }

        return _launchComposition.JavaWorkflow.RecommendedComponent is null
            ? T("shell.prompts.java.runtime_name", ("version", _launchComposition.JavaWorkflow.RecommendedMajorVersion))
            : T("shell.prompts.java.component_runtime_name", ("component", _launchComposition.JavaWorkflow.RecommendedComponent), ("version", _launchComposition.JavaWorkflow.RecommendedMajorVersion));
    }

    private MinecraftLaunchJavaPrompt? GetPendingJavaPrompt()
    {
        return _launchComposition.SelectedJavaRuntime is null && _launchComposition.JavaRuntimeInstallPlan is not null
            ? _launchComposition.JavaWorkflow.MissingJavaPrompt
            : null;
    }

    private void RaiseLaunchCompositionProperties()
    {
        RaisePropertyChanged(nameof(LaunchAvatarImage));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
        RaisePropertyChanged(nameof(ShowLaunch32BitJavaWarning));
        RaisePropertyChanged(nameof(CanRefreshLaunchProfile));
        RaisePropertyChanged(nameof(HasSelectedLaunchProfile));
        RaisePropertyChanged(nameof(ShowLaunchProfileSetupActions));
        RaisePropertyChanged(nameof(LaunchProfileHint));
        RaisePropertyChanged(nameof(LaunchProfileDescription));
        RaiseLaunchProfileSurfaceProperties();
        RaisePropertyChanged(nameof(LaunchVersionSubtitle));
        RaisePropertyChanged(nameof(LaunchWelcomeBanner));
        RaisePropertyChanged(nameof(LaunchNewsTitle));
        RaisePropertyChanged(nameof(LaunchNewsBadgeText));
        RaisePropertyChanged(nameof(LaunchNewsSectionTitle));
        RaisePropertyChanged(nameof(LaunchAnnouncementHeader));
        RaisePropertyChanged(nameof(LaunchAnnouncementPrimaryText));
        RaisePropertyChanged(nameof(LaunchAnnouncementSecondaryText));
        RaisePropertyChanged(nameof(ShowLaunchAnnouncement));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _refreshLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    private LauncherFrontendCrashSurfaceData BuildCrashSurfaceData()
    {
        return new LauncherFrontendCrashSurfaceData(
            _activeCrashPlan.ExportPlan.SuggestedArchiveName,
            _activeCrashPlan.ExportPlan.ExportRequest.SourceFiles.Count,
            !string.IsNullOrWhiteSpace(_activeCrashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath),
            _activeCrashPlan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath);
    }
}
