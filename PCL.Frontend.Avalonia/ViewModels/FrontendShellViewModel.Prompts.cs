using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Tasks;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;
using System.Runtime.InteropServices;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void HandleI18nChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _updateStatus = FrontendSetupUpdateStatusService.Relocalize(_updateStatus, _i18n);
            RefreshSetupLocalizationState();
            RaiseSectionBLocalizedProperties();
            _promptCatalog[AvaloniaPromptLaneKind.Startup] = LauncherFrontendPromptService
                .BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent)
                .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Startup, prompt))
                .ToList();
            EnsureLaunchPromptLane();
            EnsureCrashPromptLane();
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
            RaiseLaunchProfileSurfaceProperties();
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
            RefreshShellCore(activityMessage: null, addActivity: false);
            RaiseLaunchSessionProperties();
            RaiseGameLogSurfaceProperties();
            RefreshSectionDI18nSurfaces();

            if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                RaiseActiveSetupSurfaceProperties();
            }
        });
    }

    private Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>> BuildPromptCatalog(string scenario)
    {
        var startupPrompts = LauncherFrontendPromptService.BuildStartupPromptQueue(_startupPlan.StartupPlan, _startupPlan.Consent);

        return new Dictionary<AvaloniaPromptLaneKind, List<PromptCardViewModel>>
        {
            [AvaloniaPromptLaneKind.Startup] = startupPrompts.Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Startup, prompt)).ToList(),
            [AvaloniaPromptLaneKind.Launch] = [],
            [AvaloniaPromptLaneKind.Crash] = []
        };
    }

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

    private PromptCardViewModel CreatePromptCard(AvaloniaPromptLaneKind lane, LauncherFrontendPrompt prompt)
    {
        return new PromptCardViewModel(
            lane,
            prompt.Id,
            _i18n.T(prompt.Title),
            _i18n.T(prompt.Message),
            prompt.Source.ToString(),
            prompt.Severity.ToString(),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushRedLight", "#D33232")
                : FrontendThemeResourceResolver.GetBrush("ColorBrush2", "#0B5BCB"),
            prompt.Severity == LauncherFrontendPromptSeverity.Warning
                ? FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticDangerBackground", "#FFF1EA")
                : FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBackground", "#EDF5FF"),
            prompt.Options.Select((option, index) => new PromptOptionViewModel(
                _i18n.T(option.Label),
                string.Empty,
                ResolvePromptOptionColorType(prompt.Severity, index, prompt.Options.Count),
                new ActionCommand(() => _ = ApplyPromptOptionAsync(lane, prompt.Id, option)))).ToList());
    }

    private static PclButtonColorState ResolvePromptOptionColorType(
        LauncherFrontendPromptSeverity severity,
        int index,
        int optionCount)
    {
        if (index != 0)
        {
            return PclButtonColorState.Normal;
        }

        if (severity == LauncherFrontendPromptSeverity.Warning)
        {
            return PclButtonColorState.Red;
        }

        return PclButtonColorState.Highlight;
    }

    private async Task ApplyPromptOptionAsync(AvaloniaPromptLaneKind lane, string promptId, LauncherFrontendPromptOption option)
    {
        var commandSummary = option.Commands.Count == 0
            ? T("shell.prompts.commands.none_attached")
            : string.Join(" • ", option.Commands.Select(DescribePromptCommand));
        AddActivity(T("shell.prompts.activities.action.title", ("label", _i18n.T(option.Label))), commandSummary);

        if (option.ClosesPrompt && IsPromptOverlayVisible)
        {
            SetPromptOverlayOpen(false);
            await Task.Delay(PclModalMotion.ExitDuration);
        }

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(lane, command);
        }

        if (option.ClosesPrompt)
        {
            if (lane == AvaloniaPromptLaneKind.Launch)
            {
                _dismissedLaunchPromptIds.Add(promptId);
            }

            _promptCatalog[lane].RemoveAll(prompt => prompt.Id == promptId);
            RebuildPromptLanes();
            SyncPromptLaneState();
            SelectPromptLane(_selectedPromptLane, updateActivity: false);
            if (HasActivePrompts)
            {
                SetPromptOverlayOpen(true);
            }
            else
            {
                SetPromptOverlayOpen(false);
            }

            AddActivity(
                T("shell.prompts.activities.closed.title"),
                T("shell.prompts.activities.closed.body", ("prompt_id", promptId), ("lane", lane.ToString())));

            if (lane == AvaloniaPromptLaneKind.Launch &&
                _pendingLaunchAfterPrompt &&
                !_isLaunchBlockedByPrompt &&
                _promptCatalog[AvaloniaPromptLaneKind.Launch].Count == 0)
            {
                _pendingLaunchAfterPrompt = false;
                _ = StartLaunchAsync();
            }
        }
    }

    private void ExecutePromptCommand(
        AvaloniaPromptLaneKind lane,
        LauncherFrontendPromptCommand command)
    {
        switch (command.Kind)
        {
            case LauncherFrontendPromptCommandKind.ViewGameLog:
                OpenCrashLogFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.OpenInstanceSettings:
                NavigateTo(
                    new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup),
                    T("shell.prompts.activities.navigate.instance_settings"));
                break;
            case LauncherFrontendPromptCommandKind.ExportCrashReport:
                ExportCrashReportFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.DownloadJavaRuntime:
                _ = DownloadJavaRuntimeFromPromptAsync();
                break;
            case LauncherFrontendPromptCommandKind.OpenUrl:
                OpenExternalTarget(command.Value, T("shell.prompts.external_open.success.url"));
                break;
            case LauncherFrontendPromptCommandKind.AppendLaunchArgument:
                AppendPromptLaunchArgument(command.Value);
                break;
            case LauncherFrontendPromptCommandKind.AcceptConsent:
                AcceptPromptConsent();
                break;
            case LauncherFrontendPromptCommandKind.RejectConsent:
                AddActivity(T("shell.prompts.activities.reject_consent.title"), T("shell.prompts.activities.reject_consent.body"));
                break;
            case LauncherFrontendPromptCommandKind.ContinueFlow:
                ContinuePromptFlow(lane);
                break;
            case LauncherFrontendPromptCommandKind.AbortLaunch:
                AbortLaunchFromPrompt();
                break;
            case LauncherFrontendPromptCommandKind.PersistSetting:
                PersistPromptSetting(command.Value);
                break;
            case LauncherFrontendPromptCommandKind.PersistInstanceJavaCompatibilityIgnored:
                PersistJavaCompatibilityOverride();
                break;
            case LauncherFrontendPromptCommandKind.IgnoreJavaCompatibilityOnce:
                IgnoreJavaCompatibilityWarningOnce();
                break;
            case LauncherFrontendPromptCommandKind.ClosePrompt:
                AddActivity(T("shell.prompts.activities.close_prompt.title"), T("shell.prompts.activities.close_prompt.body"));
                break;
            case LauncherFrontendPromptCommandKind.ExitLauncher:
                AddActivity(T("shell.prompts.activities.exit_launcher.title"), T("shell.prompts.activities.exit_launcher.body"));
                _shellActionService.ExitLauncher();
                break;
            default:
                AddActivity(T("shell.prompts.activities.unhandled_command.title"), command.Kind.ToString());
                break;
        }
    }

    private void AddActivity(string title, string body)
    {
        ActivityEntries.Insert(0, new ActivityItemViewModel(DateTime.Now.ToString("HH:mm:ss"), title, body));
        while (ActivityEntries.Count > 12)
        {
            ActivityEntries.RemoveAt(ActivityEntries.Count - 1);
        }

        RaisePropertyChanged(nameof(HasActivityEntries));
    }

    private void AddFailureActivity(string title, string body)
    {
        AddActivity(title, body);
        AvaloniaHintBus.Show(ComposeFailureHintMessage(title, body), AvaloniaHintTheme.Error);
    }

    private string ComposeFailureHintMessage(string title, string body)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        var normalizedBody = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return string.IsNullOrWhiteSpace(normalizedTitle) ? T("shell.prompts.activities.failure_default") : normalizedTitle;
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle) ||
            normalizedBody.StartsWith(normalizedTitle, StringComparison.Ordinal))
        {
            return normalizedBody;
        }

        return $"{normalizedTitle}: {normalizedBody}";
    }

    private void TogglePromptOverlay()
    {
        if (HasPromptOverlayInlineDialog)
        {
            return;
        }

        SetPromptOverlayOpen(!IsPromptOverlayVisible);
    }

    private void ToggleLaunchMigrationCard()
    {
        IsLaunchMigrationExpanded = !IsLaunchMigrationExpanded;
    }

    private void ToggleLaunchNewsCard()
    {
        IsLaunchNewsExpanded = !IsLaunchNewsExpanded;
    }

    private void SetPromptOverlayOpen(bool isOpen)
    {
        if (_isPromptOverlayOpen == isOpen)
        {
            RaisePropertyChanged(nameof(IsPromptOverlayVisible));
            NotifyTopLevelNavigationInteractionChanged();
            return;
        }

        _isPromptOverlayOpen = isOpen;
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
        NotifyTopLevelNavigationInteractionChanged();
    }

    private string DescribePromptOption(LauncherFrontendPromptOption option)
    {
        return option.Commands.Count == 0
            ? T("shell.prompts.commands.none")
            : string.Join(", ", option.Commands.Select(DescribePromptCommand));
    }

    private string DescribePromptCommand(LauncherFrontendPromptCommand command)
    {
        return command.Kind switch
        {
            LauncherFrontendPromptCommandKind.ContinueFlow => T("shell.prompts.commands.continue_flow"),
            LauncherFrontendPromptCommandKind.AcceptConsent => T("shell.prompts.commands.accept_consent"),
            LauncherFrontendPromptCommandKind.RejectConsent => T("shell.prompts.commands.reject_consent"),
            LauncherFrontendPromptCommandKind.OpenUrl => T("shell.prompts.commands.open_url", ("value", command.Value ?? T("shell.prompts.commands.not_available"))),
            LauncherFrontendPromptCommandKind.ExitLauncher => T("shell.prompts.commands.exit_launcher"),
            LauncherFrontendPromptCommandKind.AbortLaunch => T("shell.prompts.commands.abort_launch"),
            LauncherFrontendPromptCommandKind.AppendLaunchArgument => T("shell.prompts.commands.append_launch_argument", ("value", command.Value ?? T("shell.prompts.commands.not_available"))),
            LauncherFrontendPromptCommandKind.PersistSetting => T("shell.prompts.commands.persist_setting", ("value", command.Value ?? T("shell.prompts.commands.not_available"))),
            LauncherFrontendPromptCommandKind.DownloadJavaRuntime => T("shell.prompts.commands.download_java_runtime", ("value", command.Value ?? T("shell.prompts.commands.not_available"))),
            LauncherFrontendPromptCommandKind.PersistInstanceJavaCompatibilityIgnored => T("shell.prompts.commands.persist_java_override"),
            LauncherFrontendPromptCommandKind.IgnoreJavaCompatibilityOnce => T("shell.prompts.commands.ignore_java_once"),
            LauncherFrontendPromptCommandKind.ClosePrompt => T("shell.prompts.commands.close_prompt"),
            LauncherFrontendPromptCommandKind.ViewGameLog => T("shell.prompts.commands.view_game_log"),
            LauncherFrontendPromptCommandKind.OpenInstanceSettings => T("shell.prompts.commands.open_instance_settings"),
            LauncherFrontendPromptCommandKind.ExportCrashReport => T("shell.prompts.commands.export_crash_report"),
            _ => command.Kind.ToString()
        };
    }

    private async Task HandleLaunchRequestedAsync()
    {
        if (_isLaunchInProgress)
        {
            AddActivity(T("shell.prompts.activities.launch_in_progress.title"), T("shell.prompts.activities.launch_in_progress.body"));
            return;
        }

        await AwaitLatestSelectedInstanceRefreshAsync();
        RefreshLaunchState();

        if (_isLaunchBlockedByPrompt)
        {
            AddActivity(T("shell.prompts.activities.launch_blocked.title"), T("shell.prompts.activities.launch_blocked.body"));
            return;
        }

        if (!_launchComposition.PrecheckResult.IsSuccess)
        {
            AddFailureActivity(T("shell.prompts.activities.precheck_failed.title"), GetLaunchPrecheckFailureMessage());
            return;
        }

        _dismissedLaunchPromptIds.Clear();
        EnsureLaunchPromptLane();
        if (_promptCatalog[AvaloniaPromptLaneKind.Launch].Count > 0)
        {
            _pendingLaunchAfterPrompt = true;
            RebuildPromptLanes();
            SetPromptOverlayOpen(true);
            SelectPromptLane(AvaloniaPromptLaneKind.Launch, updateActivity: false);
            AddActivity(
                T("shell.prompts.activities.pending_launch_prompts.title"),
                T("shell.prompts.activities.pending_launch_prompts.body", ("instance", LaunchVersionSubtitle), ("count", _promptCatalog[AvaloniaPromptLaneKind.Launch].Count)));
            return;
        }

        await StartLaunchAsync();
    }

    private void AcceptPromptConsent()
    {
        _shellActionService.AcceptLauncherEula();
        UpdateStartupConsentRequest(request => request with { HasAcceptedEula = true });
        AddActivity(T("shell.prompts.activities.accept_consent.title"), T("shell.prompts.activities.accept_consent.body"));
    }

    private void ContinuePromptFlow(AvaloniaPromptLaneKind lane)
    {
        if (lane == AvaloniaPromptLaneKind.Launch)
        {
            _isLaunchBlockedByPrompt = false;
            AddActivity(
                T("shell.prompts.activities.continue_launch.title"),
                _pendingLaunchAfterPrompt
                    ? T("shell.prompts.activities.continue_launch.body_pending")
                    : T("shell.prompts.activities.continue_launch.body_ready"));
            return;
        }

        AddActivity(T("shell.prompts.activities.continue_flow.title"), T("shell.prompts.activities.continue_flow.body"));
    }

    private void AbortLaunchFromPrompt()
    {
        _isLaunchBlockedByPrompt = false;
        _ignoreJavaCompatibilityWarningOnce = false;
        _pendingLaunchAfterPrompt = false;
        AddActivity(T("shell.prompts.activities.abort_launch.title"), T("shell.prompts.activities.abort_launch.body"));
    }

    private void PersistPromptSetting(string? rawValue)
    {
        _shellActionService.DisableNonAsciiGamePathWarning();
        var updatedPrecheckRequest = _launchComposition.PrecheckRequest with
        {
            IsNonAsciiPathWarningDisabled = true
        };
        _launchComposition = _launchComposition with
        {
            PrecheckRequest = updatedPrecheckRequest,
            PrecheckResult = MinecraftLaunchPrecheckService.Evaluate(updatedPrecheckRequest)
        };
        RaiseLaunchCompositionProperties();
        AddActivity(
            T("shell.prompts.activities.persist_setting.title"),
            string.IsNullOrWhiteSpace(rawValue)
                ? T("shell.prompts.activities.persist_setting.body_non_ascii_disabled")
                : T("shell.prompts.activities.persist_setting.body_value", ("value", rawValue)));
    }

    private void PersistJavaCompatibilityOverride()
    {
        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        if (string.IsNullOrWhiteSpace(instanceDirectory))
        {
            AddFailureActivity(T("shell.prompts.activities.persist_java_override_failed.title"), T("shell.prompts.activities.persist_java_override_failed.body"));
            return;
        }

        _shellActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceJava", true);
        IgnoreInstanceJavaCompatibilityWarning = true;
        RefreshLaunchState();
        AddActivity(T("shell.prompts.activities.persist_java_override.title"), T("shell.prompts.activities.persist_java_override.body"));
    }

    private void IgnoreJavaCompatibilityWarningOnce()
    {
        _ignoreJavaCompatibilityWarningOnce = true;
        AddActivity(T("shell.prompts.activities.ignore_java_once.title"), T("shell.prompts.activities.ignore_java_once.body"));
    }

    private void AppendPromptLaunchArgument(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            AddFailureActivity(T("shell.prompts.activities.append_argument_failed.title"), T("shell.prompts.activities.append_argument_failed.body"));
            return;
        }

        var currentArguments = LaunchGameArguments.Trim();
        var argumentTokens = currentArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (argumentTokens.Contains(argument, StringComparer.Ordinal))
        {
            AddActivity(T("shell.prompts.activities.argument_exists.title"), argument);
            return;
        }

        LaunchGameArguments = string.IsNullOrWhiteSpace(currentArguments)
            ? argument
            : $"{currentArguments} {argument}";
        AddActivity(T("shell.prompts.activities.argument_appended.title"), LaunchGameArguments);
    }

    private void OpenCrashLogFromPrompt()
    {
        var logPath = _shellActionService.MaterializeCrashLog(_activeCrashPlan);
        NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.GameLog), T("shell.prompts.activities.navigate.game_log"));
        RefreshGameLogSurface();
        AddActivity(T("shell.prompts.activities.open_log.title"), T("shell.prompts.activities.open_log.body", ("path", logPath)));
    }

    private void ExportCrashReportFromPrompt()
    {
        var exportResult = _shellActionService.ExportCrashReport(_activeCrashPlan);
        OpenExternalTarget(exportResult.ArchivePath, T("shell.prompts.external_open.success.crash_report_exported"));
        AddActivity(
            T("shell.prompts.activities.crash_report_exported.title"),
            T("shell.prompts.activities.crash_report_exported.body", ("path", exportResult.ArchivePath), ("count", exportResult.ArchivedFileCount)));
    }

    private async Task DownloadJavaRuntimeFromPromptAsync()
    {
        if (_launchComposition.JavaRuntimeManifestPlan is null || _launchComposition.JavaRuntimeTransferPlan is null)
        {
            AddFailureActivity(T("shell.prompts.activities.java_runtime_prepare_failed.title"), T("shell.prompts.activities.java_runtime_prepare_failed.body_missing_plan"));
            return;
        }

        AddActivity(T("shell.prompts.activities.java_runtime_preparing.title"), T("shell.prompts.activities.java_runtime_preparing.body"));

        try
        {
            var installResult = await Task.Run(() => _shellActionService.MaterializeJavaRuntime(
                _launchComposition.JavaRuntimeManifestPlan,
                _launchComposition.JavaRuntimeTransferPlan));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var installedRuntime = FrontendJavaInventoryService.TryResolveRuntime(
                    _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory),
                    isEnabled: true,
                    fallbackDisplayName: T("shell.prompts.java.runtime_name", ("version", installResult.VersionName)));
                _launchComposition = _launchComposition with
                {
                    SelectedJavaRuntime = new FrontendJavaRuntimeSummary(
                        installedRuntime?.ExecutablePath ?? _shellActionService.GetJavaExecutablePath(installResult.RuntimeDirectory),
                        installedRuntime?.DisplayName ?? T("shell.prompts.java.runtime_name", ("version", installResult.VersionName)),
                        installedRuntime?.MajorVersion ?? _launchComposition.JavaWorkflow.RecommendedMajorVersion,
                        IsEnabled: installedRuntime?.IsEnabled ?? true,
                        Is64Bit: installedRuntime?.Is64Bit ?? Environment.Is64BitOperatingSystem,
                        Architecture: installedRuntime?.Architecture),
                    JavaWarningMessage = null
                };
                RaiseLaunchCompositionProperties();
                RegisterMaterializedJavaRuntime(installResult);
                _isLaunchBlockedByPrompt = false;
                _pendingLaunchAfterPrompt = false;
                NavigateTo(new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupJava), T("shell.prompts.activities.navigate.java_settings"));
                AddActivity(
                    T("shell.prompts.activities.java_runtime_ready.title"),
                    T("shell.prompts.activities.java_runtime_ready.body", ("path", installResult.RuntimeDirectory), ("downloaded_count", installResult.DownloadedFileCount), ("reused_count", installResult.ReusedFileCount)));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddFailureActivity(T("shell.prompts.activities.java_runtime_prepare_failed.title"), ex.Message);
            });
        }
    }

    private void RegisterMaterializedJavaRuntime(FrontendJavaRuntimeInstallResult installResult)
    {
        var key = $"downloaded-{Path.GetFileName(installResult.RuntimeDirectory)}";
        var tags = new List<string> { T("shell.prompts.java.tags.bit64"), T("shell.prompts.java.tags.prompt_download") };
        if (installResult.ReusedFileCount > 0)
        {
            tags.Add(T("shell.prompts.java.tags.reused_count", ("count", installResult.ReusedFileCount)));
        }

        var newEntry = CreateJavaRuntimeEntry(
            key,
            T("shell.prompts.java.runtime_name", ("version", installResult.VersionName)),
            installResult.RuntimeDirectory,
            tags,
            isEnabled: true);

        var existingIndex = -1;
        for (var index = 0; index < JavaRuntimeEntries.Count; index++)
        {
            if (JavaRuntimeEntries[index].Key == key)
            {
                existingIndex = index;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            JavaRuntimeEntries[existingIndex] = newEntry;
        }
        else
        {
            JavaRuntimeEntries.Add(newEntry);
        }

        RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
        SelectJavaRuntime(key);
    }

    private void OpenExternalTarget(string? target, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity(T("shell.prompts.external_open.failure.title"), T("shell.prompts.external_open.failure.missing_target"));
            return;
        }

        if (_shellActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity(T("shell.prompts.external_open.success.title"), T("shell.prompts.external_open.success.body", ("message", successMessage), ("target", target)));
            return;
        }

        AddFailureActivity(T("shell.prompts.external_open.failure.title"), T("shell.prompts.external_open.failure.body", ("target", target), ("error", error ?? T("shell.prompts.external_open.failure.unknown_error"))));
    }

    private void UpdateStartupConsentRequest(Func<LauncherStartupConsentRequest, LauncherStartupConsentRequest> updater)
    {
        var updatedRequest = updater(_shellComposition.StartupConsentRequest);
        var updatedConsent = LauncherStartupConsentService.Evaluate(updatedRequest);

        _shellComposition = _shellComposition with
        {
            StartupConsentRequest = updatedRequest,
            StartupConsentResult = updatedConsent
        };
        _startupPlan = _startupPlan with
        {
            Consent = updatedConsent
        };
    }

    private async Task StartLaunchAsync()
    {
        try
        {
            var launchCancellation = new CancellationTokenSource();
            _launchSessionCancellation = launchCancellation;
            ShowLaunchDialog();
            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                T("launch.dialog.state.running.initializing"),
                0d,
                showDownload: false,
                isError: false);

            _isLaunchInProgress = true;
            _showLaunchLog = true;
            ClearLaunchLogBuffer();
            RaiseLaunchSessionProperties();
            RefreshGameLogSurface();

            await EnsureSelectedLaunchProfileReadyForLaunchAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            await RefreshLaunchCompositionAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            if (_launchComposition.SelectedJavaRuntime is null)
            {
                throw new InvalidOperationException(T("launch.status.errors.java_missing"));
            }

            if (!string.IsNullOrWhiteSpace(_launchComposition.JavaWarningMessage))
            {
                AppendLaunchLogLine(_launchComposition.JavaWarningMessage);
                AddActivity(T("launch.status.activities.java_check_ignored"), _launchComposition.JavaWarningMessage);
            }

            foreach (var line in _launchComposition.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines)
            {
                AppendLaunchLogLine(line);
            }

            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection
                    ? T("launch.status.steps.prepare_arguments")
                    : T("launch.dialog.stages.verify_instance"),
                DisableInstanceFileValidation || !_instanceComposition.Selection.HasSelection ? 0.18d : 0.06d,
                showDownload: false,
                isError: false);

            await EnsureLaunchFilesAsync(launchCancellation.Token);
            launchCancellation.Token.ThrowIfCancellationRequested();

            SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                T("launch.status.steps.write_launch_script"),
                0.88d,
                showDownload: false,
                isError: false);

            var startResult = _shellActionService.StartLaunchSession(
                _launchComposition,
                _instanceComposition.Selection.InstanceDirectory);
            _activeLaunchProcess = startResult.Process;
            AppendLaunchLogLine(_launchComposition.SessionStartPlan.ProcessShellPlan.StartedLogMessage);
            AddActivity(T("launch.status.activities.game_process_started"), T("launch.status.messages.game_process_started", ("instance_name", LaunchVersionSubtitle), ("pid", startResult.Process.Id)));
            if (_currentRoute.Page != LauncherFrontendPageKey.Launch)
            {
                NavigateTo(
                    new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                    T("launch.status.messages.returned_to_launch_page"),
                    RouteNavigationBehavior.Reset);
            }

            AvaloniaHintBus.Show(T("launch.status.hints.launch_succeeded"), AvaloniaHintTheme.Success);
            HideLaunchDialog();
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();

            _ = MonitorLaunchSessionAsync(startResult);
        }
        catch (OperationCanceledException)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine(T("launch.status.logs.canceled"));
            AddActivity(T("launch.status.activities.canceled"), T("launch.status.messages.canceled"));
            SetLaunchDialogStoppedState(T("launch.status.stopped.canceled_title"), T("launch.status.messages.canceled"), isError: false);
        }
        catch (Exception ex)
        {
            _isLaunchInProgress = false;
            RaiseLaunchSessionProperties();
            AppendLaunchLogLine(T("launch.status.logs.failed", ("message", ex.Message)));
            AddFailureActivity(T("launch.status.activities.failed"), ex.Message);
            SetLaunchDialogStoppedState(T("launch.status.stopped.failed_title"), ex.Message, isError: true);
        }
        finally
        {
            _launchSessionCancellation?.Dispose();
            _launchSessionCancellation = null;
            RaiseLaunchSessionProperties();
        }
    }

    private async Task RefreshLaunchCompositionAsync(CancellationToken cancellationToken)
    {
        SetLaunchDialogRunningState(
            T("launch.dialog.state.running.title"),
            T("launch.status.steps.read_configuration"),
            0.02d,
            showDownload: false,
            isError: false);

        var ignoreJavaCompatibilityWarningOnce = _ignoreJavaCompatibilityWarningOnce;
        _ignoreJavaCompatibilityWarningOnce = false;
        var launchComposition = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return FrontendLaunchCompositionService.Compose(
                _options,
                _shellActionService.RuntimePaths,
                ignoreJavaCompatibilityWarningOnce);
        }, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
    }

    private async Task EnsureSelectedLaunchProfileReadyForLaunchAsync(CancellationToken cancellationToken)
    {
        var result = await RefreshSelectedLaunchProfileCoreAsync(
            cancellationToken,
            forceRefresh: false,
            onStatusChanged: stage => SetLaunchDialogRunningState(
                T("launch.dialog.state.running.title"),
                stage,
                0.03d,
                showDownload: false,
                isError: false));
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.WasChecked)
        {
            return;
        }

        if (result.ShouldInvalidateAvatarCache)
        {
            InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
        }

        AppendLaunchLogLine(result.Message);
        AddActivity(T("launch.status.activities.prelaunch_account_check"), result.Message);
    }

    private async Task EnsureLaunchFilesAsync(CancellationToken cancellationToken)
    {
        if (!_instanceComposition.Selection.HasSelection || DisableInstanceFileValidation)
        {
            return;
        }

        AppendLaunchLogLine(T("launch.status.logs.verifying_instance_files"));
        AppendLaunchLogLine(T("launch.status.logs.task_manager_progress"));

        try
        {
            var repairResult = await ExecuteManagedInstanceRepairAsync(
                T("launch.status.tasks.verify_instance_files", ("instance_name", _instanceComposition.Selection.InstanceName)),
                new FrontendInstanceRepairRequest(
                    _instanceComposition.Selection.LauncherDirectory,
                    _instanceComposition.Selection.InstanceDirectory,
                    _instanceComposition.Selection.InstanceName,
                    ForceCoreRefresh: false),
                ApplyLaunchRepairProgress,
                cancellationToken);
            var completionMessage = T("launch.status.messages.instance_verification_completed", ("downloaded_count", repairResult.DownloadedFiles.Count), ("reused_count", repairResult.ReusedFiles.Count));
            AppendLaunchLogLine(completionMessage);

            if (repairResult.DownloadedFiles.Count > 0)
            {
                AddActivity(T("launch.status.activities.instance_verification"), completionMessage);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(T("launch.status.errors.instance_verification_failed", ("message", ex.Message)), ex);
        }
    }

    private async Task MonitorLaunchSessionAsync(FrontendLaunchStartResult startResult)
    {
        try
        {
            startResult.Process.EnableRaisingEvents = true;
            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(args.Data))
                    {
                        AppendLaunchLogLine(args.Data);
                        File.AppendAllText(startResult.RawOutputLogPath, args.Data + Environment.NewLine, new System.Text.UTF8Encoding(false));
                    }
                };
            }

            if (startResult.Process.StartInfo.RedirectStandardOutput)
            {
                startResult.Process.BeginOutputReadLine();
            }

            if (startResult.Process.StartInfo.RedirectStandardError)
            {
                startResult.Process.BeginErrorReadLine();
            }

            await startResult.Process.WaitForExitAsync();
            _shellActionService.ApplyWatcherStopShellPlan(_launchComposition);
            AppendLaunchLogLine(T("shell.prompts.launch_logs.process_exited", ("exit_code", startResult.Process.ExitCode)));
            AddActivity(T("shell.prompts.activities.game_process_ended.title"), T("shell.prompts.activities.game_process_ended.body", ("instance", LaunchVersionSubtitle), ("exit_code", startResult.Process.ExitCode)));
            if (startResult.Process.ExitCode != 0 && !_launchProcessTerminationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() => ShowCrashPromptForLaunchFailure(startResult));
            }
        }
        catch (Exception ex)
        {
            AppendLaunchLogLine(T("shell.prompts.launch_logs.monitor_exception", ("message", ex.Message)));
            AddActivity(T("shell.prompts.activities.monitor_exception.title"), ex.Message);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _activeLaunchProcess = null;
                _launchProcessTerminationRequested = false;
                _isLaunchInProgress = false;
                RaiseLaunchSessionProperties();
                RefreshLaunchState();
                RefreshGameLogSurface();
                if (IsLaunchDialogVisible)
                {
                    HideLaunchDialog();
                }
            });
        }
    }

    private void RefreshLaunchState()
    {
        ReloadSetupComposition();
        ReloadInstanceComposition();
        ApplyLaunchComposition(
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths),
            normalizeLaunchProfileSurface: true);
    }

    private async Task RefreshLaunchProfileCompositionAsync()
    {
        var refreshVersion = Interlocked.Increment(ref _launchProfileCompositionRefreshVersion);
        var launchComposition = await Task.Run(() =>
            FrontendLaunchCompositionService.Compose(_options, _shellActionService.RuntimePaths));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (refreshVersion != _launchProfileCompositionRefreshVersion)
            {
                return;
            }

            ApplyLaunchComposition(launchComposition, normalizeLaunchProfileSurface: true);
        });
    }

    private void ApplyLaunchComposition(FrontendLaunchComposition composition, bool normalizeLaunchProfileSurface)
    {
        _launchComposition = composition;
        ClearOptimisticLaunchInstanceName(raiseProperties: false);
        if (normalizeLaunchProfileSurface)
        {
            NormalizeLaunchProfileSurface();
        }

        var launchPromptContextKey = BuildLaunchPromptContextKey(_launchComposition, _instanceComposition.Selection.InstanceDirectory);
        if (!string.Equals(_launchPromptContextKey, launchPromptContextKey, StringComparison.Ordinal))
        {
            _dismissedLaunchPromptIds.Clear();
            _launchPromptContextKey = launchPromptContextKey;
        }

        RaiseLaunchCompositionProperties();
        ScheduleLaunchAvatarRefresh();
    }

    private void AppendLaunchLogLine(string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AppendLaunchLogEntry(line);
            RaiseGameLogSurfaceProperties();
            if (!_showLaunchLog)
            {
                _showLaunchLog = true;
                RaisePropertyChanged(nameof(ShowLaunchLog));
            }
        });
    }

    private void ShowLaunchCompletionNotification()
    {
        var message = _i18n.T(_launchComposition.CompletionNotification.Message);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        switch (_launchComposition.CompletionNotification.Kind)
        {
            case MinecraftLaunchNotificationKind.Info:
                AvaloniaHintBus.Show(message, AvaloniaHintTheme.Info);
                break;
            case MinecraftLaunchNotificationKind.Finish:
                AvaloniaHintBus.Show(message, AvaloniaHintTheme.Success);
                break;
        }
    }

    private void RaiseLaunchSessionProperties()
    {
        RaisePropertyChanged(nameof(LaunchButtonTitle));
        RaisePropertyChanged(nameof(ShowLaunchLog));
        RaisePropertyChanged(nameof(LaunchLogText));
        RaisePropertyChanged(nameof(LaunchMigrationLines));
        _launchCommand.NotifyCanExecuteChanged();
        _cancelLaunchCommand.NotifyCanExecuteChanged();
        RaiseLaunchDialogProperties();
    }

    private string GetLaunchPrecheckFailureMessage()
    {
        return _launchComposition.PrecheckResult.Failure is { } failure
            ? _i18n.T(failure.ToLocalizedText())
            : _i18n.T("launch.precheck.failures.unknown");
    }

    private void EnsureLaunchPromptLane()
    {
        var launchPrompts = LauncherFrontendPromptService.BuildLaunchPromptQueue(
            _launchComposition.PrecheckResult,
            _launchComposition.SupportPrompt,
            _launchComposition.JavaCompatibilityPrompt,
            GetPendingJavaPrompt());
        _promptCatalog[AvaloniaPromptLaneKind.Launch] = launchPrompts
            .Where(prompt => !_dismissedLaunchPromptIds.Contains(prompt.Id))
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Launch, prompt))
            .ToList();
    }

    private static string BuildLaunchPromptContextKey(
        FrontendLaunchComposition launchComposition,
        string? instanceDirectory)
    {
        return string.Join(
            "|",
            instanceDirectory ?? string.Empty,
            launchComposition.InstanceName,
            launchComposition.SelectedProfile.Kind,
            launchComposition.SelectedProfile.UserName);
    }

    private void EnsureCrashPromptLane()
    {
        var crashPrompts = LauncherFrontendPromptService.BuildCrashPromptQueue(_activeCrashPlan.OutputPrompt);
        _promptCatalog[AvaloniaPromptLaneKind.Crash] = crashPrompts
            .Select(prompt => CreatePromptCard(AvaloniaPromptLaneKind.Crash, prompt))
            .ToList();
    }

    private void ShowCrashPromptForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        _activeCrashPlan = BuildCrashPlanForLaunchFailure(startResult);
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity(T("shell.prompts.activities.crash_prompt_shown.title"), T("shell.prompts.activities.crash_prompt_shown.body"));
    }

    private CrashAvaloniaPlan BuildCrashPlanForLaunchFailure(FrontendLaunchStartResult startResult)
    {
        var exportRequest = new MinecraftCrashExportPlanRequest(
            Timestamp: DateTime.Now,
            ReportDirectory: Path.Combine(
                _shellActionService.RuntimePaths.FrontendTempDirectory,
                "CrashReport",
                DateTime.Now.ToString("yyyy-MM-dd")),
            LauncherVersionName: "frontend-avalonia",
            UniqueAddress: _instanceComposition.Selection.InstanceDirectory ??
                           _launchComposition.InstancePath,
            SourceFilePaths:
            [
                startResult.LaunchScriptPath,
                startResult.RawOutputLogPath
            ],
            AdditionalSourceFilePaths:
            [
                startResult.SessionSummaryPath,
                Path.Combine(_launchComposition.InstancePath, "logs", "latest.log")
            ],
            CurrentLauncherLogFilePath: _shellActionService.RuntimePaths.ResolveCurrentLauncherLogFilePath(),
            Environment: GetHostEnvironmentSnapshot(),
            CurrentAccessToken: _launchComposition.SelectedProfile.AccessToken,
            CurrentUserUuid: _launchComposition.SelectedProfile.Uuid,
            UserProfilePath: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var analysisResult = MinecraftCrashAnalysisService.Analyze(new MinecraftCrashAnalysisRequest(
            BuildAnalysisSourcePaths(exportRequest),
            exportRequest.CurrentLauncherLogFilePath));
        var resultText = analysisResult.HasKnownReason
            ? analysisResult.ResultText
            : $"{analysisResult.ResultText}{Environment.NewLine}{Environment.NewLine}{T("shell.prompts.crash.details_header")}{Environment.NewLine}{BuildLaunchFailureMessage(startResult)}";
        var outputPrompt = MinecraftCrashWorkflowService.BuildOutputPrompt(new MinecraftCrashOutputPromptRequest(
            resultText,
            IsManualAnalysis: false,
            HasDirectFile: analysisResult.HasDirectFile,
            CanOpenModLoaderSettings: true,
            HasModLoaderVersionMismatch: analysisResult.HasModLoaderVersionMismatch));
        var exportPlan = MinecraftCrashExportWorkflowService.CreatePlan(exportRequest);

        return new CrashAvaloniaPlan(outputPrompt, exportPlan);
    }

    private static IReadOnlyList<string> BuildAnalysisSourcePaths(MinecraftCrashExportPlanRequest request)
    {
        return request.SourceFilePaths
            .Concat(request.AdditionalSourceFilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string BuildLaunchFailureMessage(FrontendLaunchStartResult startResult)
    {
        var details = new List<string>
        {
            T("shell.prompts.crash.launch_failure.exit_code", ("exit_code", startResult.Process.ExitCode))
        };

        var lastOutputLine = TryReadLastMeaningfulLine(startResult.RawOutputLogPath);
        if (!string.IsNullOrWhiteSpace(lastOutputLine))
        {
            details.Add(lastOutputLine);
        }

        details.Add(T("shell.prompts.crash.launch_failure.raw_output_log", ("path", startResult.RawOutputLogPath)));
        return string.Join(Environment.NewLine, details);
    }

    private static string? TryReadLastMeaningfulLine(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadLines(path)
                .Reverse()
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return null;
        }
    }

    private static SystemEnvironmentSnapshot GetHostEnvironmentSnapshot()
    {
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var totalPhysicalMemoryBytes = availableMemory > 0
            ? (ulong)availableMemory
            : 8UL * 1024UL * 1024UL * 1024UL;

        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            totalPhysicalMemoryBytes,
            GetHostCpuName(),
            []);
    }

    private static string GetHostCpuName()
    {
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
               Environment.GetEnvironmentVariable("HOSTTYPE") ??
               RuntimeInformation.ProcessArchitecture.ToString();
    }

    private void TriggerCrashPromptTest()
    {
        EnsureCrashPromptLane();
        RebuildPromptLanes();
        SetPromptOverlayOpen(true);
        SelectPromptLane(AvaloniaPromptLaneKind.Crash, updateActivity: false);
        AddActivity(T("shell.prompts.activities.crash_test_triggered.title"), T("shell.prompts.activities.crash_test_triggered.body"));
    }

    private LauncherFrontendPageContent BuildPageContent(LauncherFrontendShellPlan shellPlan)
    {
        return FrontendShellLocalizationService.BuildPageContent(
            shellPlan,
            _currentNavigation ?? FrontendShellLocalizationService.LocalizeNavigationView(shellPlan.Navigation, _i18n),
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
        return _launchComposition.SelectedJavaRuntime is null
            ? _launchComposition.JavaWorkflow.MissingJavaPrompt
            : null;
    }

    private void RaiseLaunchCompositionProperties()
    {
        RaisePropertyChanged(nameof(LaunchAvatarImage));
        RaisePropertyChanged(nameof(LaunchUserName));
        RaisePropertyChanged(nameof(LaunchAuthLabel));
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
