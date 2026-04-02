using System.Text;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Rendering;

internal static class SpikeTextRenderer
{
    public static string Render(object payload)
    {
        return payload switch
        {
            StartupSpikePlan startupPlan => RenderStartupPlan(startupPlan),
            LaunchSpikePlan launchPlan => RenderLaunchPlan(launchPlan),
            CrashSpikePlan crashPlan => RenderCrashPlan(crashPlan),
            SpikePlanBundle bundle => RenderPlanBundle(bundle),
            StartupSpikeRun startupRun => RenderTranscript(startupRun.Transcript),
            LaunchSpikeRun launchRun => RenderTranscript(launchRun.Transcript),
            CrashSpikeRun crashRun => RenderTranscript(crashRun.Transcript),
            SpikeRunBundle runBundle => RenderRunBundle(runBundle),
            StartupSpikeExecution startupExecution => RenderTranscript(startupExecution.Transcript),
            LaunchSpikeExecution launchExecution => RenderTranscript(launchExecution.Transcript),
            CrashSpikeExecution crashExecution => RenderTranscript(crashExecution.Transcript),
            SpikeExecutionBundle executionBundle => RenderExecutionBundle(executionBundle),
            _ => payload.ToString() ?? string.Empty
        };
    }

    private static string RenderPlanBundle(SpikePlanBundle bundle)
    {
        var builder = new StringBuilder();
        builder.AppendLine(RenderStartupPlan(bundle.Startup));
        builder.AppendLine();
        builder.AppendLine(RenderLaunchPlan(bundle.Launch));
        builder.AppendLine();
        builder.Append(RenderCrashPlan(bundle.Crash));
        return builder.ToString();
    }

    private static string RenderRunBundle(SpikeRunBundle bundle)
    {
        var builder = new StringBuilder();
        builder.AppendLine(RenderTranscript(bundle.Startup.Transcript));
        builder.AppendLine();
        builder.AppendLine(RenderTranscript(bundle.Launch.Transcript));
        builder.AppendLine();
        builder.Append(RenderTranscript(bundle.Crash.Transcript));
        return builder.ToString();
    }

    private static string RenderExecutionBundle(SpikeExecutionBundle bundle)
    {
        var builder = new StringBuilder();
        builder.AppendLine(RenderTranscript(bundle.Startup.Transcript));
        builder.AppendLine();
        builder.AppendLine(RenderTranscript(bundle.Launch.Transcript));
        builder.AppendLine();
        builder.Append(RenderTranscript(bundle.Crash.Transcript));
        return builder.ToString();
    }

    private static string RenderStartupPlan(StartupSpikePlan plan)
    {
        var sections = new List<SpikeTranscriptSection>
        {
            new("Immediate Command",
            [
                $"Kind: {plan.StartupPlan.ImmediateCommand.Kind}",
                $"Argument: {plan.StartupPlan.ImmediateCommand.Argument ?? "none"}"
            ]),
            new("Bootstrap",
            [
                $"Create directories: {string.Join(", ", plan.StartupPlan.Bootstrap.DirectoriesToCreate)}",
                $"Load config keys: {string.Join(", ", plan.StartupPlan.Bootstrap.ConfigKeysToLoad)}",
                $"Delete legacy logs: {string.Join(", ", plan.StartupPlan.Bootstrap.LegacyLogFilesToDelete)}",
                $"Default update channel: {plan.StartupPlan.Bootstrap.DefaultUpdateChannel}",
                $"Show splash/logo: {plan.StartupPlan.Visual.ShouldShowSplashScreen}"
            ]),
            new("Consent",
            [
                $"Consent prompt count: {plan.Consent.Prompts.Count}",
                ..plan.Consent.Prompts.Select(prompt => $"Prompt: {prompt.Title}")
            ])
        };

        if (plan.StartupPlan.EnvironmentWarningPrompt is not null)
        {
            sections.Insert(2, new SpikeTranscriptSection(
                "Environment Prompt",
                BuildStartupPromptLines(plan.StartupPlan.EnvironmentWarningPrompt)));
        }

        return RenderTranscript(new SpikeTranscript("Startup Plan", sections));
    }

    private static string RenderLaunchPlan(LaunchSpikePlan plan)
    {
        return RenderTranscript(new SpikeTranscript(
            $"Launch Plan ({plan.Scenario})",
            [
                new SpikeTranscriptSection(
                    "Login",
                    [
                        $"Provider: {plan.LoginPlan.Provider}",
                        $"Request or shell steps: {plan.LoginPlan.Steps.Count}",
                        $"Final mutation: {plan.LoginPlan.MutationPlan?.Kind.ToString() ?? "none"}"
                    ]),
                new SpikeTranscriptSection(
                    "Java Workflow",
                    [
                        plan.JavaWorkflow.RequirementLogMessage,
                        $"Initial selection: {plan.InitialSelection.ActionKind}",
                        $"Accepted prompt outcome: {plan.AcceptedPromptOutcome.ActionKind}",
                        $"Post-download selection: {plan.PostDownloadSelection.ActionKind}"
                    ]),
                new SpikeTranscriptSection(
                    "Launch Composition",
                    [
                        $"Resolution: {plan.ResolutionPlan.Width}x{plan.ResolutionPlan.Height}",
                        $"Classpath entries: {plan.ClasspathPlan.Entries.Count}",
                        $"Natives directory: {plan.NativesDirectory}",
                        $"Replacement tokens: {plan.ReplacementPlan.Values.Count}",
                        $"Final arguments: {plan.ArgumentPlan.FinalArguments}"
                    ]),
                new SpikeTranscriptSection(
                    "Prerun",
                    [
                        $"launcher_profiles path: {plan.PrerunPlan.LauncherProfiles.Path ?? "none"}",
                        $"launcher_profiles write: {plan.PrerunPlan.LauncherProfiles.Workflow.ShouldWrite}",
                        $"options target: {plan.PrerunPlan.Options.TargetFilePath}",
                        $"options writes: {plan.PrerunPlan.Options.SyncPlan.Writes.Count}"
                    ]),
                new SpikeTranscriptSection(
                    "Session",
                    [
                        $"Custom command shells: {plan.SessionStartPlan.CustomCommandShellPlans.Count}",
                        $"Process: {plan.SessionStartPlan.ProcessShellPlan.FileName}",
                        $"Realtime log: {plan.SessionStartPlan.WatcherWorkflowPlan.ShouldAttachRealtimeLog}",
                        $"Watcher summary lines: {plan.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines.Count}"
                    ]),
                new SpikeTranscriptSection(
                    "Post Launch",
                    [
                        $"Launcher action: {plan.PostLaunchShell.LauncherAction.Kind}",
                        $"Music action: {plan.PostLaunchShell.MusicAction.Kind}",
                        $"Completion notification: {plan.CompletionNotification.Message}"
                    ])
            ]));
    }

    private static string RenderCrashPlan(CrashSpikePlan plan)
    {
        return RenderTranscript(new SpikeTranscript(
            "Crash Plan",
            [
                new SpikeTranscriptSection(
                    "Prompt",
                    [
                        $"Title: {plan.OutputPrompt.Title}",
                        $"Buttons: {string.Join(" | ", plan.OutputPrompt.Buttons.Select(button => button.Label))}"
                    ]),
                new SpikeTranscriptSection(
                    "Export",
                    [
                        $"Suggested archive: {plan.ExportPlan.SuggestedArchiveName}",
                        $"Report directory: {plan.ExportPlan.ExportRequest.ReportDirectory}",
                        $"Included files: {plan.ExportPlan.ExportRequest.SourceFiles.Count}"
                    ])
            ]));
    }

    private static string RenderTranscript(SpikeTranscript transcript)
    {
        var builder = new StringBuilder();
        builder.AppendLine(transcript.Title);

        foreach (var section in transcript.Sections)
        {
            builder.AppendLine();
            builder.AppendLine($"[{section.Heading}]");
            foreach (var line in section.Lines)
            {
                builder.AppendLine($"- {line}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> BuildStartupPromptLines(LauncherStartupPrompt prompt)
    {
        return
        [
            $"Title: {prompt.Title}",
            $"Message: {prompt.Message.ReplaceLineEndings(" ")}",
            $"Buttons: {string.Join(" | ", prompt.Buttons.Select(button => button.Label))}"
        ];
    }
}
