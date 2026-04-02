using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class SpikeRunner
{
    public static StartupSpikeRun BuildStartupRun(StartupSpikePlan plan)
    {
        var sections = new List<SpikeTranscriptSection>
        {
            new("Immediate Command", BuildImmediateCommandLines(plan.StartupPlan.ImmediateCommand)),
            new("Bootstrap", BuildBootstrapLines(plan.StartupPlan)),
            new("Consent", BuildConsentLines(plan.Consent))
        };

        if (plan.StartupPlan.EnvironmentWarningPrompt is not null)
        {
            sections.Insert(2, new SpikeTranscriptSection(
                "Environment Prompt",
                BuildPromptLines(plan.StartupPlan.EnvironmentWarningPrompt)));
        }

        return new StartupSpikeRun(new SpikeTranscript("Startup Shell Transcript", sections));
    }

    public static LaunchSpikeRun BuildLaunchRun(
        LaunchSpikePlan plan,
        MinecraftLaunchJavaPromptDecision javaPromptDecision)
    {
        var promptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
            plan.JavaWorkflow.MissingJavaPrompt,
            javaPromptDecision);
        var postDownloadSelection = promptOutcome.ActionKind == MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection
            ? MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(plan.JavaWorkflow, hasSelectedJava: true)
            : new MinecraftLaunchJavaPostDownloadOutcome(
                MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch,
                plan.JavaWorkflow.NoJavaAvailableHintMessage);

        var sections = new List<SpikeTranscriptSection>
        {
            new("Login Execution", BuildLoginLines(plan.LoginPlan)),
            new("Java Selection", BuildJavaSelectionLines(plan, javaPromptDecision, promptOutcome, postDownloadSelection))
        };

        if (plan.JavaRuntimeSelection is not null && plan.JavaRuntimeDownloadPlan is not null)
        {
            sections.Add(new SpikeTranscriptSection(
                "Java Download Plan",
                BuildJavaDownloadLines(plan.JavaRuntimeSelection, plan.JavaRuntimeDownloadPlan)));
        }

        if (promptOutcome.ActionKind == MinecraftLaunchJavaPromptActionKind.AbortLaunch)
        {
            sections.Add(new SpikeTranscriptSection(
                "Outcome",
                [
                    "Launch stopped before prerun file work.",
                    "The frontend would return to the shell without starting the game process."
                ]));

            return new LaunchSpikeRun(
                javaPromptDecision,
                new SpikeTranscript($"Launch Shell Transcript ({plan.Scenario})", sections));
        }

        sections.Add(new SpikeTranscriptSection("Launch Inputs", BuildLaunchInputLines(plan)));
        sections.Add(new SpikeTranscriptSection("Prerun File Work", BuildPrerunLines(plan.PrerunPlan)));
        sections.Add(new SpikeTranscriptSection("Session Shell", BuildSessionLines(plan.SessionStartPlan)));
        sections.Add(new SpikeTranscriptSection("Post Launch", BuildPostLaunchLines(plan.PostLaunchShell, plan.CompletionNotification)));

        return new LaunchSpikeRun(
            javaPromptDecision,
            new SpikeTranscript($"Launch Shell Transcript ({plan.Scenario})", sections));
    }

    public static CrashSpikeRun BuildCrashRun(
        CrashSpikePlan plan,
        MinecraftCrashOutputPromptActionKind selectedAction)
    {
        var sections = new List<SpikeTranscriptSection>
        {
            new("Crash Prompt", BuildCrashPromptLines(plan.OutputPrompt, selectedAction)),
            new("Export Flow", BuildCrashExportLines(plan.ExportPlan, selectedAction))
        };

        return new CrashSpikeRun(
            selectedAction,
            new SpikeTranscript("Crash Shell Transcript", sections));
    }

    private static IReadOnlyList<string> BuildImmediateCommandLines(LauncherStartupImmediateCommandPlan plan)
    {
        var lines = new List<string> { $"Immediate command: {plan.Kind}" };
        if (!string.IsNullOrWhiteSpace(plan.Argument))
        {
            lines.Add($"Argument: {plan.Argument}");
        }

        if (!string.IsNullOrWhiteSpace(plan.InvalidMessage))
        {
            lines.Add($"Validation: {plan.InvalidMessage}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildBootstrapLines(LauncherStartupWorkflowPlan plan)
    {
        var lines = new List<string>
        {
            $"Create directories: {string.Join(", ", plan.Bootstrap.DirectoriesToCreate)}",
            $"Load config keys: {string.Join(", ", plan.Bootstrap.ConfigKeysToLoad)}",
            $"Delete legacy logs: {string.Join(", ", plan.Bootstrap.LegacyLogFilesToDelete)}",
            $"Default update channel: {plan.Bootstrap.DefaultUpdateChannel}",
            $"Show splash/logo: {plan.Visual.ShouldShowSplashScreen}"
        };

        return lines;
    }

    private static IReadOnlyList<string> BuildConsentLines(LauncherStartupConsentResult consent)
    {
        if (consent.Prompts.Count == 0)
        {
            return ["No startup consent prompts were produced."];
        }

        var lines = new List<string>();
        foreach (var prompt in consent.Prompts)
        {
            lines.Add($"Prompt: {prompt.Title}");
            lines.Add($"Message: {prompt.Message.ReplaceLineEndings(" ")}");
            lines.Add($"Buttons: {string.Join(" | ", prompt.Buttons.Select(button => button.Label))}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildJavaSelectionLines(
        LaunchSpikePlan plan,
        MinecraftLaunchJavaPromptDecision javaPromptDecision,
        MinecraftLaunchJavaPromptOutcome promptOutcome,
        MinecraftLaunchJavaPostDownloadOutcome postDownloadSelection)
    {
        var lines = new List<string>
        {
            plan.JavaWorkflow.RequirementLogMessage,
            $"Initial selection action: {plan.InitialSelection.ActionKind}",
            $"Initial log: {plan.InitialSelection.LogMessage ?? "none"}"
        };

        lines.AddRange(BuildJavaPromptLines(plan.JavaWorkflow.MissingJavaPrompt, javaPromptDecision, promptOutcome));
        lines.Add($"Post-download action: {postDownloadSelection.ActionKind}");
        if (!string.IsNullOrWhiteSpace(postDownloadSelection.HintMessage))
        {
            lines.Add($"Post-download hint: {postDownloadSelection.HintMessage}");
        }

        return lines;
    }

    private static IEnumerable<string> BuildJavaPromptLines(
        MinecraftLaunchJavaPrompt prompt,
        MinecraftLaunchJavaPromptDecision javaPromptDecision,
        MinecraftLaunchJavaPromptOutcome promptOutcome)
    {
        yield return $"Prompt title: {prompt.Title}";
        yield return $"Prompt options: {string.Join(" | ", prompt.Options.Select(option => option.Label))}";
        yield return $"Selected decision: {javaPromptDecision}";
        yield return $"Prompt outcome: {promptOutcome.ActionKind}";
        if (!string.IsNullOrWhiteSpace(promptOutcome.DownloadTarget))
        {
            yield return $"Download target: {promptOutcome.DownloadTarget}";
        }
    }

    private static IReadOnlyList<string> BuildLaunchInputLines(LaunchSpikePlan plan)
    {
        var lines = new List<string>
        {
            $"Resolution: {plan.ResolutionPlan.Width}x{plan.ResolutionPlan.Height}",
            $"Classpath entries: {plan.ClasspathPlan.Entries.Count}",
            $"Natives directory: {plan.NativesDirectory}",
            $"Replacement tokens: {plan.ReplacementPlan.Values.Count}",
            $"Final launch arguments: {plan.ArgumentPlan.FinalArguments}"
        };

        if (plan.ArgumentPlan.ShouldWarnAboutLegacyServerWithOptiFine)
        {
            lines.Add("Legacy server / OptiFine warning would be shown.");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildJavaDownloadLines(
        MinecraftJavaRuntimeSelection selection,
        MinecraftJavaRuntimeDownloadPlan plan)
    {
        return
        [
            $"Platform: {selection.PlatformKey}",
            $"Requested component: {selection.RequestedComponent}",
            $"Resolved component: {selection.ComponentKey}",
            $"Version: {selection.VersionName}",
            $"Manifest: {selection.ManifestUrl}",
            $"Runtime directory: {plan.RuntimeBaseDirectory}",
            $"Planned files: {plan.Files.Count}",
            ..plan.Files.Take(3).Select(file => $"File: {file.RelativePath} ({file.Size} bytes)")
        ];
    }

    private static IReadOnlyList<string> BuildLoginLines(LaunchLoginSpikePlan plan)
    {
        var lines = new List<string>
        {
            $"Provider: {plan.Provider}",
            $"Step count: {plan.Steps.Count}"
        };

        foreach (var step in plan.Steps)
        {
            lines.Add($"{step.Progress:P0} {step.Title}");
            if (!string.IsNullOrWhiteSpace(step.Method) && !string.IsNullOrWhiteSpace(step.Url))
            {
                lines.Add($"Request: {step.Method} {step.Url}");
            }

            foreach (var note in step.Notes)
            {
                lines.Add($"Note: {note}");
            }
        }

        if (plan.MutationPlan is not null)
        {
            lines.Add($"Final mutation: {plan.MutationPlan.Kind}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildPrerunLines(MinecraftLaunchPrerunWorkflowPlan plan)
    {
        var lines = new List<string>
        {
            $"Ensure launcher_profiles exists: {plan.LauncherProfiles.ShouldEnsureFileExists}",
            $"launcher_profiles path: {plan.LauncherProfiles.Path ?? "none"}",
            $"launcher_profiles write: {plan.LauncherProfiles.Workflow.ShouldWrite}",
            $"options target: {plan.Options.TargetFilePath}",
            $"options writes: {string.Join(", ", plan.Options.SyncPlan.Writes.Select(write => $"{write.Key}={write.Value}"))}"
        };

        if (plan.LauncherProfiles.Workflow.InitialAttempt is not null)
        {
            lines.Add($"launcher_profiles success log: {plan.LauncherProfiles.Workflow.InitialAttempt.SuccessLogMessage}");
        }

        if (!string.IsNullOrWhiteSpace(plan.LauncherProfiles.Workflow.RetryLogMessage))
        {
            lines.Add($"launcher_profiles retry log: {plan.LauncherProfiles.Workflow.RetryLogMessage}");
        }

        foreach (var logMessage in plan.Options.SyncPlan.LogMessages)
        {
            lines.Add($"options log: {logMessage}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildSessionLines(MinecraftLaunchSessionStartWorkflowPlan plan)
    {
        var lines = new List<string>();

        foreach (var shellPlan in plan.CustomCommandShellPlans)
        {
            lines.Add($"Custom command: {shellPlan.FileName} {shellPlan.Arguments}");
            lines.Add($"  Wait for exit: {shellPlan.WaitForExit}");
            lines.Add($"  Start log: {shellPlan.StartLogMessage}");
        }

        lines.Add($"Process: {plan.ProcessShellPlan.FileName} {plan.ProcessShellPlan.Arguments}");
        lines.Add($"Working directory: {plan.ProcessShellPlan.WorkingDirectory}");
        lines.Add($"Priority: {plan.ProcessShellPlan.PriorityKind}");
        lines.Add($"Watcher title template: {plan.WatcherWorkflowPlan.RawWindowTitleTemplate}");
        lines.Add($"Realtime log attached: {plan.WatcherWorkflowPlan.ShouldAttachRealtimeLog}");
        lines.Add($"Startup summary lines: {plan.WatcherWorkflowPlan.StartupSummaryLogLines.Count}");

        return lines;
    }

    private static IReadOnlyList<string> BuildPostLaunchLines(
        MinecraftGameShellPlan postLaunchShell,
        MinecraftLaunchNotification completionNotification)
    {
        return
        [
            $"Music action: {postLaunchShell.MusicAction.Kind}",
            $"Video background action: {postLaunchShell.VideoBackgroundAction.Kind}",
            $"Launcher action: {postLaunchShell.LauncherAction.Kind}",
            $"Global launch count increment: {postLaunchShell.GlobalLaunchCountIncrement}",
            $"Instance launch count increment: {postLaunchShell.InstanceLaunchCountIncrement}",
            $"Completion notification: {completionNotification.Kind} - {completionNotification.Message}"
        ];
    }

    private static IReadOnlyList<string> BuildCrashPromptLines(
        MinecraftCrashOutputPrompt prompt,
        MinecraftCrashOutputPromptActionKind selectedAction)
    {
        return
        [
            $"Prompt title: {prompt.Title}",
            $"Message: {prompt.Message}",
            $"Buttons: {string.Join(" | ", prompt.Buttons.Select(button => button.Label))}",
            $"Selected action: {selectedAction}"
        ];
    }

    private static IReadOnlyList<string> BuildCrashExportLines(
        MinecraftCrashExportPlan plan,
        MinecraftCrashOutputPromptActionKind selectedAction)
    {
        var lines = new List<string>
        {
            $"Suggested archive: {plan.SuggestedArchiveName}",
            $"Report directory: {plan.ExportRequest.ReportDirectory}",
            $"Included files: {plan.ExportRequest.SourceFiles.Count}",
            $"Launcher log: {plan.ExportRequest.CurrentLauncherLogFilePath ?? "none"}"
        };

        if (selectedAction == MinecraftCrashOutputPromptActionKind.ExportReport)
        {
            lines.Add("Frontend should ask for a destination path, then pass the export request to the archive writer.");
            lines.Add($"Default execute-mode export path: <workspace>/output/{plan.SuggestedArchiveName}");
        }
        else
        {
            lines.Add("No archive write would be triggered for the selected prompt action.");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildPromptLines(LauncherStartupPrompt prompt)
    {
        return
        [
            $"Prompt title: {prompt.Title}",
            $"Message: {prompt.Message.ReplaceLineEndings(" ")}",
            $"Buttons: {string.Join(" | ", prompt.Buttons.Select(button => button.Label))}"
        ];
    }
}
