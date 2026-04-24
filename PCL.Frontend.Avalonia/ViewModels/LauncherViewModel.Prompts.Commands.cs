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

        if (option.ClosesPrompt)
        {
            if (lane == AvaloniaPromptLaneKind.Startup)
            {
                _dismissedStartupPromptIds.Add(promptId);
            }
            else if (lane == AvaloniaPromptLaneKind.Launch)
            {
                _dismissedLaunchPromptIds.Add(promptId);
            }
        }

        foreach (var command in option.Commands)
        {
            ExecutePromptCommand(lane, command);
        }

        if (option.ClosesPrompt)
        {
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
                _ = StartLaunchAsync(resumeAfterPrompt: true);
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
                _launcherActionService.ExitLauncher();
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

    private void AcceptPromptConsent()
    {
        _launcherActionService.AcceptLauncherEula();
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
        _launcherActionService.DisableNonAsciiGamePathWarning();
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

        _launcherActionService.PersistInstanceValue(instanceDirectory, "VersionAdvanceJava", true);
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

    private void OpenExternalTarget(string? target, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            AddFailureActivity(T("shell.prompts.external_open.failure.title"), T("shell.prompts.external_open.failure.missing_target"));
            return;
        }

        if (_launcherActionService.TryOpenExternalTarget(target, out var error))
        {
            AddActivity(T("shell.prompts.external_open.success.title"), T("shell.prompts.external_open.success.body", ("message", successMessage), ("target", target)));
            return;
        }

        AddFailureActivity(T("shell.prompts.external_open.failure.title"), T("shell.prompts.external_open.failure.body", ("target", target), ("error", error ?? T("shell.prompts.external_open.failure.unknown_error"))));
    }

    private void UpdateStartupConsentRequest(Func<LauncherStartupConsentRequest, LauncherStartupConsentRequest> updater)
    {
        var updatedRequest = updater(_launcherComposition.StartupConsentRequest);
        var updatedConsent = LauncherStartupConsentService.Evaluate(updatedRequest);

        _launcherComposition = _launcherComposition with
        {
            StartupConsentRequest = updatedRequest,
            StartupConsentResult = updatedConsent
        };
        _startupPlan = _startupPlan with
        {
            Consent = updatedConsent
        };

        EnsureStartupPromptLane();
        RebuildPromptLanes();
        SyncPromptLaneState();
        SelectPromptLane(_selectedPromptLane, updateActivity: false);
        RaisePropertyChanged(nameof(IsPromptOverlayVisible));
    }
}
