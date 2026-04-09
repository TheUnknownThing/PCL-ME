using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class AvaloniaRunner
{
    public static ShellAvaloniaRun BuildShellRun(LauncherFrontendShellPlan plan)
    {
        var sidebarLines = plan.Navigation.SidebarEntries.Count == 0
            ? ["Current route has no sidebar entries."]
            : plan.Navigation.SidebarEntries.Select(entry =>
                $"{(entry.IsSelected ? "selected" : "available")} {entry.Title}: {entry.Summary}").ToArray();
        var utilityLines = plan.Navigation.UtilityEntries
            .Where(entry => entry.IsVisible)
            .Select(entry => $"{(entry.IsSelected ? "active" : "available")} {entry.Title}")
            .ToArray();

        return new ShellAvaloniaRun(new AvaloniaTranscript(
            "Frontend Shell Transcript",
            [
                new AvaloniaTranscriptSection("Startup Bootstrap", BuildBootstrapLines(plan.StartupPlan)),
                new AvaloniaTranscriptSection("Frontend Prompt Queue", BuildFrontendPromptLines(plan.Prompts)),
                new AvaloniaTranscriptSection(
                    "Current Surface",
                    BuildCurrentSurfaceLines(plan.Navigation)),
                new AvaloniaTranscriptSection(
                    "Top Navigation",
                    plan.Navigation.TopLevelEntries.Select(entry =>
                        $"{(entry.IsSelected ? "selected" : "available")} {entry.Title}").ToArray()),
                new AvaloniaTranscriptSection("Sidebar", sidebarLines),
                new AvaloniaTranscriptSection(
                    "Utility Surfaces",
                    utilityLines.Length == 0 ? ["No utility surfaces are visible."] : utilityLines)
            ]));
    }

    public static StartupAvaloniaRun BuildStartupRun(StartupAvaloniaPlan plan)
    {
        var sections = new List<AvaloniaTranscriptSection>
        {
            new("Immediate Command", BuildImmediateCommandLines(plan.StartupPlan.ImmediateCommand)),
            new("Bootstrap", BuildBootstrapLines(plan.StartupPlan)),
            new("Consent", BuildConsentLines(plan.Consent))
        };

        if (plan.StartupPlan.EnvironmentWarningPrompt is not null)
        {
            sections.Insert(2, new AvaloniaTranscriptSection(
                "Environment Prompt",
                BuildPromptLines(plan.StartupPlan.EnvironmentWarningPrompt)));
        }

        return new StartupAvaloniaRun(new AvaloniaTranscript("Startup Shell Transcript", sections));
    }

    public static LaunchAvaloniaRun BuildLaunchRun(
        LaunchAvaloniaPlan plan,
        MinecraftLaunchJavaPromptDecision javaPromptDecision,
        AvaloniaJavaDownloadSessionState javaDownloadState)
    {
        var promptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
            plan.JavaWorkflow.MissingJavaPrompt,
            javaPromptDecision);
        var postDownloadSelection = promptOutcome.ActionKind == MinecraftLaunchJavaPromptActionKind.DownloadAndRetrySelection
            ? MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(plan.JavaWorkflow, hasSelectedJava: true)
            : new MinecraftLaunchJavaPostDownloadOutcome(
                MinecraftLaunchJavaPostDownloadActionKind.AbortLaunch,
                plan.JavaWorkflow.NoJavaAvailableHintMessage);

        var sections = new List<AvaloniaTranscriptSection>
        {
            new("Login Execution", BuildLoginLines(plan.LoginPlan)),
            new("Java Selection", BuildJavaSelectionLines(plan, javaPromptDecision, promptOutcome, postDownloadSelection))
        };

        if (plan.JavaRuntimeManifestPlan is not null &&
            plan.JavaRuntimeDownloadWorkflowPlan is not null &&
            plan.JavaRuntimeTransferPlan is not null)
        {
            sections.Add(new AvaloniaTranscriptSection(
                "Java Download Plan",
                BuildJavaDownloadLines(
                    plan.JavaRuntimeIndexRequestUrls,
                    plan.JavaRuntimeManifestPlan,
                    plan.JavaRuntimeDownloadWorkflowPlan,
                    plan.JavaRuntimeTransferPlan,
                    javaDownloadState)));
        }

        if (promptOutcome.ActionKind == MinecraftLaunchJavaPromptActionKind.AbortLaunch)
        {
            sections.Add(new AvaloniaTranscriptSection(
                "Outcome",
                [
                    "Launch stopped before prerun file work.",
                    "The frontend would return to the shell without starting the game process."
                ]));

            return new LaunchAvaloniaRun(
                javaPromptDecision,
                new AvaloniaTranscript($"Launch Shell Transcript ({plan.Scenario})", sections));
        }

        sections.Add(new AvaloniaTranscriptSection("Launch Inputs", BuildLaunchInputLines(plan)));
        sections.Add(new AvaloniaTranscriptSection("Prerun File Work", BuildPrerunLines(plan.PrerunPlan)));
        if (plan.ScriptExportPlan is not null)
        {
            sections.Add(new AvaloniaTranscriptSection("Script Export", BuildScriptExportLines(plan.ScriptExportPlan)));
            sections.Add(new AvaloniaTranscriptSection(
                "Outcome",
                [
                    "Launch stopped after exporting the batch script.",
                    $"Frontend would reveal: {plan.ScriptExportPlan.RevealInShellPath}"
                ]));

            return new LaunchAvaloniaRun(
                javaPromptDecision,
                new AvaloniaTranscript($"Launch Shell Transcript ({plan.Scenario})", sections));
        }

        sections.Add(new AvaloniaTranscriptSection("Session Shell", BuildSessionLines(plan.SessionStartPlan)));
        sections.Add(new AvaloniaTranscriptSection("Post Launch", BuildPostLaunchLines(plan.PostLaunchShell, plan.CompletionNotification)));

        return new LaunchAvaloniaRun(
            javaPromptDecision,
            new AvaloniaTranscript($"Launch Shell Transcript ({plan.Scenario})", sections));
    }

    public static CrashAvaloniaRun BuildCrashRun(
        CrashAvaloniaPlan plan,
        MinecraftCrashOutputPromptActionKind selectedAction)
    {
        var sections = new List<AvaloniaTranscriptSection>
        {
            new("Crash Prompt", BuildCrashPromptLines(plan.OutputPrompt, selectedAction)),
            new("Export Flow", BuildCrashExportLines(plan.ExportPlan, selectedAction))
        };

        return new CrashAvaloniaRun(
            selectedAction,
            new AvaloniaTranscript("Crash Shell Transcript", sections));
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

    private static IReadOnlyList<string> BuildFrontendPromptLines(IReadOnlyList<LauncherFrontendPrompt> prompts)
    {
        if (prompts.Count == 0)
        {
            return ["No frontend prompts are queued."];
        }

        var lines = new List<string>();
        foreach (var prompt in prompts)
        {
            lines.Add($"Prompt: {prompt.Title}");
            lines.Add($"Source: {prompt.Source}");
            lines.Add($"Severity: {prompt.Severity}");
            lines.Add($"Message: {prompt.Message.ReplaceLineEndings(" ")}");
            lines.Add($"Options: {string.Join(" | ", prompt.Options.Select(option => option.Label))}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildCurrentSurfaceLines(LauncherFrontendNavigationView navigation)
    {
        return
        [
            $"Page: {navigation.CurrentPage.Route.Page}",
            $"Kind: {navigation.CurrentPage.Kind}",
            $"Title: {navigation.CurrentPage.Title}",
            $"Summary: {navigation.CurrentPage.Summary}",
            $"Sidebar group: {navigation.CurrentPage.SidebarGroupTitle ?? "none"}",
            $"Sidebar item: {navigation.CurrentPage.SidebarItemTitle ?? "none"}",
            $"Breadcrumbs: {string.Join(" > ", navigation.Breadcrumbs.Select(crumb => crumb.Title))}",
            $"Back target: {navigation.BackTarget?.Label ?? "none"} ({navigation.BackTarget?.Kind.ToString() ?? "none"})",
            $"Back button: {navigation.ShowsBackButton}"
        ];
    }

    private static IReadOnlyList<string> BuildJavaSelectionLines(
        LaunchAvaloniaPlan plan,
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

    private static IReadOnlyList<string> BuildLaunchInputLines(LaunchAvaloniaPlan plan)
    {
        var lines = new List<string>
        {
            $"Resolution: {plan.ResolutionPlan.Width}x{plan.ResolutionPlan.Height}",
            $"Classpath entries: {plan.ClasspathPlan.Entries.Count}",
            $"Natives directory: {plan.NativesDirectory}",
            $"Native alias: {plan.NativePathAliasDirectory ?? "none"}",
            $"Native extraction target: {plan.NativeExtractionDirectory ?? plan.NativesDirectory}",
            $"Native archives: {plan.NativeArchiveCount}",
            $"Replacement tokens: {plan.ReplacementPlan.Values.Count}",
            $"Native search path: {plan.ReplacementPlan.Values.GetValueOrDefault("${natives_directory}", plan.NativesDirectory)}",
            $"Final launch arguments: {plan.ArgumentPlan.FinalArguments}"
        };

        if (plan.ArgumentPlan.ShouldWarnAboutLegacyServerWithOptiFine)
        {
            lines.Add("Legacy server / OptiFine warning would be shown.");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildJavaDownloadLines(
        MinecraftJavaRuntimeRequestUrlPlan indexRequestUrls,
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadWorkflowPlan downloadWorkflowPlan,
        MinecraftJavaRuntimeDownloadTransferPlan transferPlan,
        AvaloniaJavaDownloadSessionState javaDownloadState)
    {
        var selection = manifestPlan.Selection;
        var downloadPlan = downloadWorkflowPlan.DownloadPlan;
        var transitionPlan = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(
            ResolveJavaDownloadSessionState(javaDownloadState),
            downloadPlan.RuntimeBaseDirectory);
        return [
            $"Index sources: {string.Join(" | ", indexRequestUrls.AllUrls)}",
            $"Platform: {selection.PlatformKey}",
            $"Requested component: {selection.RequestedComponent}",
            $"Resolved component: {selection.ComponentKey}",
            $"Version: {selection.VersionName}",
            $"Manifest sources: {string.Join(" | ", manifestPlan.RequestUrls.AllUrls)}",
            $"Runtime directory: {downloadPlan.RuntimeBaseDirectory}",
            $"Planned files: {downloadWorkflowPlan.Files.Count}",
            $"Files to download: {transferPlan.FilesToDownload.Count} ({transferPlan.DownloadBytes} bytes)",
            $"Reused files: {transferPlan.ReusedFiles.Count}",
            $"Session state: {javaDownloadState}",
            $"Cleanup directory: {transitionPlan.CleanupDirectoryPath ?? "none"}",
            $"Refresh Java inventory: {transitionPlan.ShouldRefreshJavaInventory}",
            ..transferPlan.FilesToDownload.Take(3).Select(file =>
                $"Download: {file.RelativePath} ({file.Size} bytes) <= {string.Join(" | ", file.RequestUrls.AllUrls)}"),
            ..transferPlan.ReusedFiles.Take(2).Select(file =>
                $"Reuse: {file.RelativePath}")
        ];
    }

    private static MinecraftJavaRuntimeDownloadSessionState ResolveJavaDownloadSessionState(AvaloniaJavaDownloadSessionState state)
    {
        return state switch
        {
            AvaloniaJavaDownloadSessionState.Finished => MinecraftJavaRuntimeDownloadSessionState.Finished,
            AvaloniaJavaDownloadSessionState.Failed => MinecraftJavaRuntimeDownloadSessionState.Failed,
            AvaloniaJavaDownloadSessionState.Aborted => MinecraftJavaRuntimeDownloadSessionState.Aborted,
            _ => throw new InvalidOperationException($"Unsupported Java download session state '{state}'.")
        };
    }

    private static IReadOnlyList<string> BuildLoginLines(LaunchLoginAvaloniaPlan plan)
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

    private static IReadOnlyList<string> BuildScriptExportLines(MinecraftLaunchScriptExportPlan plan)
    {
        var completionNotification = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest(
                InstanceName: string.Empty,
                Outcome: MinecraftLaunchOutcome.Aborted,
                IsScriptExport: true,
                AbortHint: plan.AbortHint));

        return
        [
            $"Target path: {plan.TargetPath}",
            $"Completion log: {plan.CompletionLogMessage}",
            $"Abort hint: {plan.AbortHint}",
            $"Reveal target: {plan.RevealInShellPath}",
            $"Completion notification: {completionNotification.Kind} - {completionNotification.Message}"
        ];
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
        var response = MinecraftCrashResponseWorkflowService.ResolvePromptResponse(selectedAction);
        var lines = new List<string>
        {
            $"Suggested archive: {plan.SuggestedArchiveName}",
            $"Report directory: {plan.ExportRequest.ReportDirectory}",
            $"Included files: {plan.ExportRequest.SourceFiles.Count}",
            $"Launcher log: {plan.ExportRequest.CurrentLauncherLogFilePath ?? "none"}",
            $"Resolved response: {response.Kind}"
        };

        if (response.Kind == MinecraftCrashPromptResponseKind.ExportReport)
        {
            var saveDialogPlan = MinecraftCrashResponseWorkflowService.BuildExportSaveDialogPlan(
                plan.SuggestedArchiveName);
            var completionPlan = MinecraftCrashResponseWorkflowService.BuildExportCompletionPlan(
                $"<workspace>/output/{plan.SuggestedArchiveName}");
            lines.Add($"Save dialog: {saveDialogPlan.Title}");
            lines.Add($"Save default: {saveDialogPlan.DefaultFileName}");
            lines.Add($"Save filter: {saveDialogPlan.Filter}");
            lines.Add("Frontend should ask for a destination path, then pass the export request to the archive writer.");
            lines.Add($"Default execute-mode export path: <workspace>/output/{plan.SuggestedArchiveName}");
            lines.Add($"Success hint: {completionPlan.HintMessage}");
            lines.Add($"Shell reveal target: {completionPlan.RevealInShellPath}");
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
