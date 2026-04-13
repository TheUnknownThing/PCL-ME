using System.Text;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class SpikeExecutor
{
    public static ShellSpikeExecution ExecuteShell(LauncherFrontendShellPlan plan, string workspaceRoot)
    {
        Directory.CreateDirectory(workspaceRoot);

        var artifacts = new List<SpikeExecutionArtifact>();
        var writtenFiles = new List<string>();

        var promptsPath = Path.Combine(workspaceRoot, "_artifacts", "frontend-prompts.txt");
        WriteTextFile(promptsPath, BuildFrontendPromptText(plan.Prompts));
        artifacts.Add(new SpikeExecutionArtifact("Frontend prompts", promptsPath));
        writtenFiles.Add(promptsPath);

        var surfacePath = Path.Combine(workspaceRoot, "_artifacts", "page-surface.txt");
        WriteTextFile(surfacePath, BuildPageSurfaceText(plan.Navigation));
        artifacts.Add(new SpikeExecutionArtifact("Page surface", surfacePath));
        writtenFiles.Add(surfacePath);

        var navigationPath = Path.Combine(workspaceRoot, "_artifacts", "navigation-view.txt");
        WriteTextFile(navigationPath, BuildNavigationViewText(plan.Navigation));
        artifacts.Add(new SpikeExecutionArtifact("Navigation view", navigationPath));
        writtenFiles.Add(navigationPath);

        var catalogPath = Path.Combine(workspaceRoot, "_artifacts", "navigation-catalog.txt");
        WriteTextFile(catalogPath, BuildNavigationCatalogText(plan.Catalog));
        artifacts.Add(new SpikeExecutionArtifact("Navigation catalog", catalogPath));
        writtenFiles.Add(catalogPath);

        return new ShellSpikeExecution(
            new SpikeExecutionSummary(
                workspaceRoot,
                [workspaceRoot, Path.Combine(workspaceRoot, "_artifacts")],
                writtenFiles,
                [],
                artifacts),
            new SpikeTranscript(
                "Frontend Shell Execution",
                [
                    new SpikeTranscriptSection(
                        "Workspace",
                        [
                            $"Workspace root: {workspaceRoot}",
                            $"Current page: {plan.Navigation.CurrentRoute.Page}",
                            $"Current title: {plan.Navigation.CurrentPageTitle}",
                            $"Breadcrumbs: {string.Join(" > ", plan.Navigation.Breadcrumbs.Select(crumb => crumb.Title))}"
                        ]),
                    new SpikeTranscriptSection(
                        "Artifacts",
                        artifacts.Select(artifact => $"{artifact.Label}: {artifact.Path}").ToArray())
                ]));
    }

    public static StartupSpikeExecution ExecuteStartup(StartupSpikePlan plan, string workspaceRoot)
    {
        Directory.CreateDirectory(workspaceRoot);

        var createdDirectories = new List<string>();
        foreach (var directory in plan.StartupPlan.Bootstrap.DirectoriesToCreate)
        {
            var mappedDirectory = MapSamplePath(directory, workspaceRoot);
            Directory.CreateDirectory(mappedDirectory);
            createdDirectories.Add(mappedDirectory);
        }

        var deletedFiles = new List<string>();
        foreach (var legacyLogPath in plan.StartupPlan.Bootstrap.LegacyLogFilesToDelete)
        {
            var mappedLogPath = MapSamplePath(legacyLogPath, workspaceRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(mappedLogPath)!);
            File.WriteAllText(mappedLogPath, "legacy log placeholder", new UTF8Encoding(false));
            File.Delete(mappedLogPath);
            deletedFiles.Add(mappedLogPath);
        }

        var artifacts = new List<SpikeExecutionArtifact>();
        var writtenFiles = new List<string>();

        var configKeyPath = Path.Combine(workspaceRoot, "_artifacts", "loaded-config-keys.txt");
        WriteTextFile(configKeyPath, string.Join(Environment.NewLine, plan.StartupPlan.Bootstrap.ConfigKeysToLoad));
        artifacts.Add(new SpikeExecutionArtifact("Loaded config keys", configKeyPath));
        writtenFiles.Add(configKeyPath);

        var consentPath = Path.Combine(workspaceRoot, "_artifacts", "startup-prompts.txt");
        WriteTextFile(consentPath, BuildConsentText(plan.Consent));
        artifacts.Add(new SpikeExecutionArtifact("Startup prompts", consentPath));
        writtenFiles.Add(consentPath);

        var sections = new List<SpikeTranscriptSection>
        {
            new("Workspace",
            [
                $"Workspace root: {workspaceRoot}",
                $"Created directories: {createdDirectories.Count}",
                $"Deleted legacy logs: {deletedFiles.Count}"
            ]),
            new("Artifacts",
            artifacts.Select(artifact => $"{artifact.Label}: {artifact.Path}").ToArray())
        };

        if (plan.StartupPlan.EnvironmentWarningPrompt is not null)
        {
            sections.Add(new SpikeTranscriptSection(
                "Environment Prompt",
                BuildStartupPromptLines(plan.StartupPlan.EnvironmentWarningPrompt)));
        }

        return new StartupSpikeExecution(
            new SpikeExecutionSummary(
                workspaceRoot,
                createdDirectories,
                writtenFiles,
                deletedFiles,
                artifacts),
            new SpikeTranscript("Startup Shell Execution", sections));
    }

    public static LaunchSpikeExecution ExecuteLaunch(
        LaunchSpikePlan plan,
        string workspaceRoot,
        MinecraftLaunchJavaPromptDecision javaPromptDecision,
        SpikeJavaDownloadSessionState javaDownloadState)
    {
        Directory.CreateDirectory(workspaceRoot);

        var promptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
            plan.JavaWorkflow.MissingJavaPrompt,
            javaPromptDecision);

        var createdDirectories = new List<string>();
        var writtenFiles = new List<string>();
        var artifacts = new List<SpikeExecutionArtifact>();
        var sections = new List<SpikeTranscriptSection>
        {
            new("Workspace",
            [
                $"Workspace root: {workspaceRoot}",
                $"Java decision: {javaPromptDecision}",
                $"Prompt outcome: {promptOutcome.ActionKind}"
            ])
        };

        var loginRoot = Path.Combine(workspaceRoot, "_artifacts", "login", plan.LoginPlan.Provider.ToString().ToLowerInvariant());
        Directory.CreateDirectory(loginRoot);
        createdDirectories.Add(loginRoot);
        var loginLines = new List<string>
        {
            $"Provider: {plan.LoginPlan.Provider}",
            $"Step count: {plan.LoginPlan.Steps.Count}"
        };

        for (var index = 0; index < plan.LoginPlan.Steps.Count; index++)
        {
            var step = plan.LoginPlan.Steps[index];
            var stepDirectoryName = $"{index + 1:D2}-{SanitizePathSegment(step.Title)}";
            var stepRoot = Path.Combine(loginRoot, stepDirectoryName);
            Directory.CreateDirectory(stepRoot);
            createdDirectories.Add(stepRoot);
            loginLines.Add($"{index + 1:D2}. {step.Title}");

            if (!string.IsNullOrWhiteSpace(step.Method) && !string.IsNullOrWhiteSpace(step.Url))
            {
                var requestSummaryPath = Path.Combine(stepRoot, "request.txt");
                WriteTextFile(
                    requestSummaryPath,
                    BuildLoginRequestText(step));
                writtenFiles.Add(requestSummaryPath);
                artifacts.Add(new SpikeExecutionArtifact($"{step.Title} request", requestSummaryPath));
                loginLines.Add($"Request artifact: {requestSummaryPath}");
            }

            if (!string.IsNullOrWhiteSpace(step.ResponseBody))
            {
                var responsePath = Path.Combine(stepRoot, "response.json");
                WriteTextFile(responsePath, step.ResponseBody);
                writtenFiles.Add(responsePath);
                artifacts.Add(new SpikeExecutionArtifact($"{step.Title} response", responsePath));
                loginLines.Add($"Response artifact: {responsePath}");
            }

            if (step.Notes.Count > 0)
            {
                var notesPath = Path.Combine(stepRoot, "notes.txt");
                WriteTextFile(notesPath, string.Join(Environment.NewLine, step.Notes));
                writtenFiles.Add(notesPath);
                artifacts.Add(new SpikeExecutionArtifact($"{step.Title} notes", notesPath));
            }
        }

        if (plan.LoginPlan.MutationPlan is not null)
        {
            loginLines.Add($"Final mutation: {plan.LoginPlan.MutationPlan.Kind}");
        }

        sections.Add(new SpikeTranscriptSection("Login Execution", loginLines));

        if (promptOutcome.ActionKind == MinecraftLaunchJavaPromptActionKind.AbortLaunch)
        {
            sections.Add(new SpikeTranscriptSection(
                "Outcome",
                [
                    "Launch stopped before prerun file work.",
                    "No launch files were materialized because the Java prompt resolved to abort."
                ]));

            return new LaunchSpikeExecution(
                javaPromptDecision,
                new SpikeExecutionSummary(workspaceRoot, createdDirectories, writtenFiles, [], artifacts),
                new SpikeTranscript($"Launch Shell Execution ({plan.Scenario})", sections));
        }

        if (plan.JavaRuntimeManifestPlan is not null &&
            plan.JavaRuntimeDownloadWorkflowPlan is not null &&
            plan.JavaRuntimeTransferPlan is not null)
        {
            var sessionState = ResolveJavaDownloadSessionState(javaDownloadState);
            var transitionPlan = MinecraftJavaRuntimeDownloadSessionService.ResolveStateTransition(
                sessionState,
                plan.JavaRuntimeDownloadWorkflowPlan.DownloadPlan.RuntimeBaseDirectory);
            var javaDownloadRoot = Path.Combine(workspaceRoot, "_artifacts", "java-download");
            Directory.CreateDirectory(javaDownloadRoot);
            createdDirectories.Add(javaDownloadRoot);

            var javaIndexRequestPath = Path.Combine(javaDownloadRoot, "index-request.txt");
            WriteTextFile(javaIndexRequestPath, BuildUrlPlanText("Java runtime index", plan.JavaRuntimeIndexRequestUrls));
            writtenFiles.Add(javaIndexRequestPath);
            artifacts.Add(new SpikeExecutionArtifact("Java index request", javaIndexRequestPath));

            var javaManifestRequestPath = Path.Combine(javaDownloadRoot, "manifest-request.txt");
            WriteTextFile(javaManifestRequestPath, BuildUrlPlanText("Java runtime manifest", plan.JavaRuntimeManifestPlan.RequestUrls));
            writtenFiles.Add(javaManifestRequestPath);
            artifacts.Add(new SpikeExecutionArtifact("Java manifest request", javaManifestRequestPath));

            var javaDownloadPlanPath = Path.Combine(workspaceRoot, "_artifacts", "java-download-plan.txt");
            WriteTextFile(
                javaDownloadPlanPath,
                BuildJavaDownloadPlanText(
                    plan.JavaRuntimeIndexRequestUrls,
                    plan.JavaRuntimeManifestPlan,
                    plan.JavaRuntimeDownloadWorkflowPlan));
            writtenFiles.Add(javaDownloadPlanPath);
            artifacts.Add(new SpikeExecutionArtifact("Java download plan", javaDownloadPlanPath));

            var runtimeRoot = MapSamplePath(plan.JavaRuntimeDownloadWorkflowPlan.DownloadPlan.RuntimeBaseDirectory, workspaceRoot);
            Directory.CreateDirectory(runtimeRoot);
            createdDirectories.Add(runtimeRoot);

            var javaTransferPlanPath = Path.Combine(workspaceRoot, "_artifacts", "java-download-transfer.txt");
            WriteTextFile(javaTransferPlanPath, BuildJavaDownloadTransferText(plan.JavaRuntimeTransferPlan));
            writtenFiles.Add(javaTransferPlanPath);
            artifacts.Add(new SpikeExecutionArtifact("Java transfer plan", javaTransferPlanPath));

            var javaSessionPath = Path.Combine(workspaceRoot, "_artifacts", "java-download-session.txt");
            WriteTextFile(javaSessionPath, BuildJavaDownloadSessionText(javaDownloadState, transitionPlan));
            writtenFiles.Add(javaSessionPath);
            artifacts.Add(new SpikeExecutionArtifact("Java download session", javaSessionPath));

            var reusedJavaFiles = new List<string>();
            foreach (var filePlan in plan.JavaRuntimeTransferPlan.ReusedFiles)
            {
                var mappedPath = MapSamplePath(filePlan.TargetPath, workspaceRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(mappedPath)!);
                createdDirectories.Add(Path.GetDirectoryName(mappedPath)!);
                WriteTextFile(mappedPath, BuildPreexistingJavaRuntimeFileContent(filePlan));
                writtenFiles.Add(mappedPath);
                reusedJavaFiles.Add(mappedPath);
            }

            var downloadedJavaFiles = new List<string>();
            foreach (var filePlan in plan.JavaRuntimeTransferPlan.FilesToDownload)
            {
                var mappedPath = MapSamplePath(filePlan.TargetPath, workspaceRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(mappedPath)!);
                createdDirectories.Add(Path.GetDirectoryName(mappedPath)!);
                WriteTextFile(mappedPath, BuildJavaRuntimeFileContent(filePlan));
                writtenFiles.Add(mappedPath);
                downloadedJavaFiles.Add(mappedPath);
            }

            if (!string.IsNullOrWhiteSpace(transitionPlan.CleanupDirectoryPath))
            {
                var cleanupPath = MapSamplePath(transitionPlan.CleanupDirectoryPath, workspaceRoot);
                if (Directory.Exists(cleanupPath))
                {
                    Directory.Delete(cleanupPath, recursive: true);
                }
            }

            sections.Add(new SpikeTranscriptSection(
                "Java Download Execution",
                [
                    $"Resolved component: {plan.JavaRuntimeManifestPlan.Selection.ComponentKey}",
                    $"Runtime directory: {runtimeRoot}",
                    $"Planned files: {plan.JavaRuntimeDownloadWorkflowPlan.Files.Count}",
                    $"Downloaded runtime files: {downloadedJavaFiles.Count}",
                    $"Reused runtime files: {reusedJavaFiles.Count}",
                    $"Session state: {javaDownloadState}",
                    $"Cleanup directory: {transitionPlan.CleanupDirectoryPath ?? "none"}",
                    $"Refresh Java inventory: {transitionPlan.ShouldRefreshJavaInventory}",
                    $"Index request artifact: {javaIndexRequestPath}",
                    $"Manifest request artifact: {javaManifestRequestPath}",
                    $"Plan artifact: {javaDownloadPlanPath}",
                    $"Transfer artifact: {javaTransferPlanPath}",
                    $"Session artifact: {javaSessionPath}"
                ]));
        }

        if (plan.PrerunPlan.LauncherProfiles.Path is not null)
        {
            var launcherProfilesPath = MapSamplePath(plan.PrerunPlan.LauncherProfiles.Path, workspaceRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(launcherProfilesPath)!);
            createdDirectories.Add(Path.GetDirectoryName(launcherProfilesPath)!);

            var launcherProfilesJson = plan.PrerunPlan.LauncherProfiles.Workflow.InitialAttempt?.UpdatedProfilesJson ?? "{}";
            File.WriteAllText(launcherProfilesPath, launcherProfilesJson, new UTF8Encoding(false));
            writtenFiles.Add(launcherProfilesPath);
            artifacts.Add(new SpikeExecutionArtifact("launcher_profiles.json", launcherProfilesPath));
        }

        var optionsPath = MapSamplePath(plan.PrerunPlan.Options.TargetFilePath, workspaceRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(optionsPath)!);
        createdDirectories.Add(Path.GetDirectoryName(optionsPath)!);
        SeedOptionsFile(optionsPath);
        ApplyOptionsWrites(optionsPath, plan.PrerunPlan.Options.SyncPlan.Writes);
        writtenFiles.Add(optionsPath);
        artifacts.Add(new SpikeExecutionArtifact("options.txt", optionsPath));

        if (plan.ScriptExportPlan is not null)
        {
            var resolvedScriptExportPath = ResolveLaunchScriptExportPath(workspaceRoot, plan.ScriptExportPlan.TargetPath);
            var scriptExportPlan = MinecraftLaunchShellService.BuildScriptExportPlan(resolvedScriptExportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedScriptExportPath)!);
            createdDirectories.Add(Path.GetDirectoryName(resolvedScriptExportPath)!);
            WriteTextFile(
                resolvedScriptExportPath,
                plan.SessionStartPlan.CustomCommandPlan.BatchScriptContent,
                plan.SessionStartPlan.CustomCommandPlan.UseUtf8Encoding);
            writtenFiles.Add(resolvedScriptExportPath);
            artifacts.Add(new SpikeExecutionArtifact("Exported launch batch script", resolvedScriptExportPath));

            var scriptExportRecordPath = Path.Combine(workspaceRoot, "_artifacts", "launch-script-export.txt");
            WriteTextFile(
                scriptExportRecordPath,
                string.Join(
                    Environment.NewLine,
                    [
                        $"Target path: {scriptExportPlan.TargetPath}",
                        $"Completion log: {scriptExportPlan.CompletionLogMessage}",
                        $"Abort hint: {scriptExportPlan.AbortHint}",
                        $"Reveal target: {scriptExportPlan.RevealInShellPath}"
                    ]));
            writtenFiles.Add(scriptExportRecordPath);
            artifacts.Add(new SpikeExecutionArtifact("Launch script export", scriptExportRecordPath));

            sections.Add(new SpikeTranscriptSection(
                "Artifacts",
                artifacts.Select(artifact => $"{artifact.Label}: {artifact.Path}").ToArray()));
            sections.Add(new SpikeTranscriptSection(
                "Outcome",
                [
                    "Launch stopped after exporting the batch script.",
                    $"Exported batch script: {resolvedScriptExportPath}",
                    $"Reveal target: {scriptExportPlan.RevealInShellPath}"
                ]));

            return new LaunchSpikeExecution(
                javaPromptDecision,
                new SpikeExecutionSummary(workspaceRoot, DistinctPaths(createdDirectories), writtenFiles, [], artifacts),
                new SpikeTranscript($"Launch Shell Execution ({plan.Scenario})", sections));
        }

        var launchScriptPath = Path.Combine(workspaceRoot, "_artifacts", "Launch.bat");
        WriteTextFile(
            launchScriptPath,
            plan.SessionStartPlan.CustomCommandPlan.BatchScriptContent,
            plan.SessionStartPlan.CustomCommandPlan.UseUtf8Encoding);
        writtenFiles.Add(launchScriptPath);
        artifacts.Add(new SpikeExecutionArtifact("Launch batch script", launchScriptPath));

        var startupSummaryPath = Path.Combine(workspaceRoot, "_artifacts", "startup-summary.log");
        WriteTextFile(startupSummaryPath, string.Join(Environment.NewLine, plan.SessionStartPlan.WatcherWorkflowPlan.StartupSummaryLogLines));
        writtenFiles.Add(startupSummaryPath);
        artifacts.Add(new SpikeExecutionArtifact("Startup summary log", startupSummaryPath));

        var processCommandPath = Path.Combine(workspaceRoot, "_artifacts", "process-command.txt");
        WriteTextFile(
            processCommandPath,
            BuildProcessCommandText(
                MinecraftLaunchProcessExecutionService.BuildGameProcessStartRequest(
                    plan.SessionStartPlan.ProcessShellPlan),
                plan.SessionStartPlan.ProcessShellPlan.PriorityKind));
        writtenFiles.Add(processCommandPath);
        artifacts.Add(new SpikeExecutionArtifact("Process command summary", processCommandPath));

        sections.Add(new SpikeTranscriptSection(
            "Artifacts",
            artifacts.Select(artifact => $"{artifact.Label}: {artifact.Path}").ToArray()));
        sections.Add(new SpikeTranscriptSection(
            "Outcome",
            [
                $"Materialized options target: {optionsPath}",
                $"Materialized batch script: {launchScriptPath}",
                $"Process would start: {plan.SessionStartPlan.ProcessShellPlan.FileName}"
            ]));

        return new LaunchSpikeExecution(
            javaPromptDecision,
            new SpikeExecutionSummary(workspaceRoot, DistinctPaths(createdDirectories), writtenFiles, [], artifacts),
            new SpikeTranscript($"Launch Shell Execution ({plan.Scenario})", sections));
    }

    public static CrashSpikeExecution ExecuteCrash(
        CrashSpikePlan plan,
        string workspaceRoot,
        MinecraftCrashOutputPromptActionKind selectedAction,
        string? exportArchivePath)
    {
        Directory.CreateDirectory(workspaceRoot);

        var createdDirectories = new List<string>();
        var writtenFiles = new List<string>();
        var artifacts = new List<SpikeExecutionArtifact>();
        var archivedFileNames = Array.Empty<string>();
        var sections = new List<SpikeTranscriptSection>
        {
            new("Workspace",
            [
                $"Workspace root: {workspaceRoot}",
                $"Selected action: {selectedAction}"
            ])
        };

        var response = MinecraftCrashResponseWorkflowService.ResolvePromptResponse(selectedAction);
        if (response.Kind != MinecraftCrashPromptResponseKind.ExportReport)
        {
            sections.Add(new SpikeTranscriptSection(
                "Outcome",
                [
                    "Crash export was not executed.",
                    $"The selected prompt action resolves to {response.Kind}."
                ]));

            return new CrashSpikeExecution(
                selectedAction,
                new SpikeExecutionSummary(workspaceRoot, createdDirectories, writtenFiles, [], artifacts),
                archivedFileNames,
                new SpikeTranscript("Crash Shell Execution", sections));
        }

        var inputRoot = Path.Combine(workspaceRoot, "input");
        Directory.CreateDirectory(inputRoot);
        createdDirectories.Add(inputRoot);

        var remappedSourceFiles = new List<MinecraftCrashExportFile>();
        foreach (var sourceFile in plan.ExportPlan.ExportRequest.SourceFiles)
        {
            var mappedPath = MapSamplePath(sourceFile.SourcePath, inputRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(mappedPath)!);
            createdDirectories.Add(Path.GetDirectoryName(mappedPath)!);
            File.WriteAllText(
                mappedPath,
                BuildCrashSourceContent(Path.GetFileName(mappedPath), plan.ExportPlan.ExportRequest),
                new UTF8Encoding(false));
            writtenFiles.Add(mappedPath);
            remappedSourceFiles.Add(new MinecraftCrashExportFile(mappedPath));
        }

        var launcherLogPath = plan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath is null
            ? null
            : MapSamplePath(plan.ExportPlan.ExportRequest.CurrentLauncherLogFilePath, inputRoot);
        if (launcherLogPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(launcherLogPath)!);
            createdDirectories.Add(Path.GetDirectoryName(launcherLogPath)!);
            File.WriteAllText(launcherLogPath, "launcher log placeholder", new UTF8Encoding(false));
            writtenFiles.Add(launcherLogPath);
        }

        var archivePath = ResolveCrashArchivePath(workspaceRoot, plan.ExportPlan.SuggestedArchiveName, exportArchivePath);
        var outputRoot = Path.GetDirectoryName(archivePath)!;
        Directory.CreateDirectory(outputRoot);
        createdDirectories.Add(outputRoot);
        var saveDialogPlan = MinecraftCrashResponseWorkflowService.BuildExportSaveDialogPlan(
            plan.ExportPlan.SuggestedArchiveName);

        var exportRequest = plan.ExportPlan.ExportRequest with
        {
            ReportDirectory = Path.Combine(outputRoot, "report"),
            SourceFiles = remappedSourceFiles,
            CurrentLauncherLogFilePath = launcherLogPath,
            UserProfilePath = Path.Combine(workspaceRoot, "Users", "demo")
        };

        var archiveResult = MinecraftCrashExportArchiveService.CreateArchive(
            new MinecraftCrashExportArchiveRequest(
                archivePath,
                exportRequest));
        archivedFileNames = archiveResult.ArchivedFileNames.ToArray();
        writtenFiles.Add(archiveResult.ArchiveFilePath);
        artifacts.Add(new SpikeExecutionArtifact("Crash archive", archiveResult.ArchiveFilePath));

        var pickerDecisionPath = Path.Combine(workspaceRoot, "_artifacts", "crash-export-target.txt");
        var completionPlan = MinecraftCrashResponseWorkflowService.BuildExportCompletionPlan(archiveResult.ArchiveFilePath);
        WriteTextFile(
            pickerDecisionPath,
            string.Join(
                Environment.NewLine,
                [
                    $"Dialog title: {saveDialogPlan.Title}",
                    $"Dialog default: {saveDialogPlan.DefaultFileName}",
                    $"Dialog filter: {saveDialogPlan.Filter}",
                    $"Requested path: {exportArchivePath ?? "default"}",
                    $"Resolved archive path: {archiveResult.ArchiveFilePath}",
                    $"Success hint: {completionPlan.HintMessage}",
                    $"Reveal target: {completionPlan.RevealInShellPath}"
                ]));
        writtenFiles.Add(pickerDecisionPath);
        artifacts.Add(new SpikeExecutionArtifact("Crash export target", pickerDecisionPath));

        sections.Add(new SpikeTranscriptSection(
            "Artifacts",
            [
                $"Crash archive: {archiveResult.ArchiveFilePath}",
                $"Archived files: {string.Join(", ", archiveResult.ArchivedFileNames)}",
                $"Export target record: {pickerDecisionPath}",
                $"Success hint: {completionPlan.HintMessage}",
                $"Shell reveal target: {completionPlan.RevealInShellPath}"
            ]));

        return new CrashSpikeExecution(
            selectedAction,
            new SpikeExecutionSummary(workspaceRoot, DistinctPaths(createdDirectories), writtenFiles, [], artifacts),
            archivedFileNames,
            new SpikeTranscript("Crash Shell Execution", sections));
    }

    private static void ApplyOptionsWrites(string optionsPath, IReadOnlyList<MinecraftLaunchOptionWrite> writes)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(optionsPath))
        {
            foreach (var line in File.ReadAllLines(optionsPath))
            {
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex];
                var value = line[(separatorIndex + 1)..];
                values[key] = value;
            }
        }

        foreach (var write in writes)
        {
            values[write.Key] = write.Value;
        }

        var output = values.Select(pair => $"{pair.Key}:{pair.Value}");
        File.WriteAllLines(optionsPath, output, new UTF8Encoding(false));
    }

    private static void SeedOptionsFile(string optionsPath)
    {
        if (!File.Exists(optionsPath))
        {
            File.WriteAllLines(
                optionsPath,
                ["lang:en_us", "fullscreen:false"],
                new UTF8Encoding(false));
        }
    }

    private static string BuildConsentText(LauncherStartupConsentResult consent)
    {
        var builder = new StringBuilder();
        foreach (var prompt in consent.Prompts)
        {
            builder.AppendLine(prompt.Title);
            builder.AppendLine(prompt.Message.ReplaceLineEndings(" "));
            builder.AppendLine(string.Join(" | ", prompt.Buttons.Select(button => button.Label)));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildNavigationViewText(LauncherFrontendNavigationView navigation)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"CurrentPage={navigation.CurrentRoute.Page}",
                $"CurrentTitle={navigation.CurrentPageTitle}",
                $"CurrentKind={navigation.CurrentPage.Kind}",
                $"CurrentSummary={navigation.CurrentPage.Summary}",
                $"SidebarGroup={navigation.CurrentPage.SidebarGroupTitle ?? "none"}",
                $"SidebarItem={navigation.CurrentPage.SidebarItemTitle ?? "none"}",
                $"Breadcrumbs={string.Join(" > ", navigation.Breadcrumbs.Select(crumb => crumb.Title))}",
                $"BackTarget={navigation.BackTarget?.Label ?? "none"}",
                $"BackTargetKind={navigation.BackTarget?.Kind.ToString() ?? "none"}",
                $"ShowsBackButton={navigation.ShowsBackButton}",
                $"TopLevel={string.Join(" | ", navigation.TopLevelEntries.Select(entry => $"{entry.Title}:{entry.IsSelected}"))}",
                $"Sidebar={string.Join(" | ", navigation.SidebarEntries.Select(entry => $"{entry.Title}:{entry.IsSelected}"))}",
                $"Utilities={string.Join(" | ", navigation.UtilityEntries.Where(entry => entry.IsVisible).Select(entry => $"{entry.Title}:{entry.IsSelected}"))}"
            ]);
    }

    private static string BuildPageSurfaceText(LauncherFrontendNavigationView navigation)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"Page={navigation.CurrentPage.Route.Page}",
                $"SubpageTitle={navigation.CurrentPage.SidebarItemTitle ?? "none"}",
                $"Kind={navigation.CurrentPage.Kind}",
                $"Title={navigation.CurrentPage.Title}",
                $"Summary={navigation.CurrentPage.Summary}",
                $"SidebarGroupTitle={navigation.CurrentPage.SidebarGroupTitle ?? "none"}",
                $"SidebarItemTitle={navigation.CurrentPage.SidebarItemTitle ?? "none"}",
                $"SidebarItemSummary={navigation.CurrentPage.SidebarItemSummary ?? "none"}",
                $"HasSidebar={navigation.CurrentPage.HasSidebar}"
            ]);
    }

    private static string BuildFrontendPromptText(IReadOnlyList<LauncherFrontendPrompt> prompts)
    {
        var builder = new StringBuilder();
        foreach (var prompt in prompts)
        {
            builder.AppendLine(prompt.Id);
            builder.AppendLine(prompt.Source.ToString());
            builder.AppendLine(prompt.Title);
            builder.AppendLine(prompt.Message.ReplaceLineEndings(" "));
            builder.AppendLine(prompt.Severity.ToString());
            builder.AppendLine(string.Join(" | ", prompt.Options.Select(option => option.Label)));
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildNavigationCatalogText(LauncherFrontendNavigationCatalog catalog)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"TopLevelPages={catalog.TopLevelPages.Count}",
                ..catalog.TopLevelPages.Select(page => $"top|{page.Page}|{page.Title}|{page.Summary}"),
                $"SidebarGroups={catalog.SidebarGroups.Count}",
                ..catalog.SidebarGroups.Select(group =>
                    $"sidebar|{group.Page}|{group.Title}|{string.Join(",", group.Items.Select(item => item.Subpage))}"),
                $"SecondaryPages={catalog.SecondaryPages.Count}",
                ..catalog.SecondaryPages.Select(page =>
                    $"secondary|{page.Page}|{page.Title}|{page.Kind}|{page.SidebarGroupPage}")
            ]);
    }

    private static string BuildProcessCommandText(
        PCL.Core.Utils.Processes.ProcessStartRequest request,
        MinecraftLaunchProcessPriorityKind priorityKind)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"FileName={request.FileName}",
                $"Arguments={request.Arguments}",
                $"WorkingDirectory={request.WorkingDirectory}",
                $"Priority={priorityKind}",
                $"RedirectStandardOutput={request.RedirectStandardOutput}",
                $"RedirectStandardError={request.RedirectStandardError}",
                $"PATH={request.EnvironmentVariables?["Path"]}",
                $"APPDATA={request.EnvironmentVariables?["appdata"]}"
            ]);
    }

    private static string BuildCrashSourceContent(string fileName, MinecraftCrashExportRequest request)
    {
        return fileName switch
        {
            "LatestLaunch.bat" => $"echo launching with accessToken {request.CurrentAccessToken} and user profile {request.UserProfilePath}",
            "RawOutput.log" => $"Raw output with token {request.CurrentAccessToken} and uuid {request.CurrentUserUuid}",
            _ => $"Crash sidecar file from {fileName} for {request.UniqueAddress}"
        };
    }

    private static void WriteTextFile(string path, string content, bool useUtf8Bom = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, useUtf8Bom ? new UTF8Encoding(true) : new UTF8Encoding(false));
    }

    private static string BuildLoginRequestText(LaunchLoginSpikeStepPlan step)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{step.Method} {step.Url}");
        if (!string.IsNullOrWhiteSpace(step.ContentType))
        {
            builder.AppendLine($"Content-Type: {step.ContentType}");
        }

        if (!string.IsNullOrWhiteSpace(step.RequestBody))
        {
            builder.AppendLine();
            builder.Append(step.RequestBody);
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildUrlPlanText(string label, MinecraftJavaRuntimeRequestUrlPlan requestUrls)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"{label} sources",
                $"Official={string.Join(" | ", requestUrls.OfficialUrls)}",
                $"Mirror={string.Join(" | ", requestUrls.MirrorUrls)}"
            ]);
    }

    private static string BuildJavaDownloadPlanText(
        MinecraftJavaRuntimeRequestUrlPlan indexRequestUrls,
        MinecraftJavaRuntimeManifestRequestPlan manifestPlan,
        MinecraftJavaRuntimeDownloadWorkflowPlan workflowPlan)
    {
        var selection = manifestPlan.Selection;
        var plan = workflowPlan.DownloadPlan;
        return string.Join(
            Environment.NewLine,
            [
                $"IndexSources={string.Join(" | ", indexRequestUrls.AllUrls)}",
                $"Platform={selection.PlatformKey}",
                $"RequestedComponent={selection.RequestedComponent}",
                $"ResolvedComponent={selection.ComponentKey}",
                $"Version={selection.VersionName}",
                $"ManifestSources={string.Join(" | ", manifestPlan.RequestUrls.AllUrls)}",
                $"RuntimeBaseDirectory={plan.RuntimeBaseDirectory}",
                $"PlannedFiles={workflowPlan.Files.Count}",
                ..workflowPlan.Files.Select(file =>
                    $"{file.RelativePath}|{file.TargetPath}|{file.Size}|{file.Sha1}|{string.Join(" | ", file.RequestUrls.AllUrls)}")
            ]);
    }

    private static string BuildJavaDownloadTransferText(MinecraftJavaRuntimeDownloadTransferPlan transferPlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"RuntimeBaseDirectory={transferPlan.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory}",
                $"PlannedFiles={transferPlan.WorkflowPlan.Files.Count}",
                $"FilesToDownload={transferPlan.FilesToDownload.Count}",
                $"ReusedFiles={transferPlan.ReusedFiles.Count}",
                $"DownloadBytes={transferPlan.DownloadBytes}",
                ..transferPlan.FilesToDownload.Select(file =>
                    $"download|{file.RelativePath}|{file.TargetPath}|{file.Size}|{string.Join(" | ", file.RequestUrls.AllUrls)}"),
                ..transferPlan.ReusedFiles.Select(file =>
                    $"reuse|{file.RelativePath}|{file.TargetPath}|{file.Size}|{string.Join(" | ", file.RequestUrls.AllUrls)}")
            ]);
    }

    private static string BuildJavaDownloadSessionText(
        SpikeJavaDownloadSessionState state,
        MinecraftJavaRuntimeDownloadStateTransitionPlan transitionPlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"State={state}",
                $"CleanupDirectory={transitionPlan.CleanupDirectoryPath ?? "none"}",
                $"CleanupLog={transitionPlan.CleanupLogMessage ?? "none"}",
                $"RefreshJavaInventory={transitionPlan.ShouldRefreshJavaInventory}",
                $"ClearTrackedRuntimeDirectory={transitionPlan.ShouldClearTrackedRuntimeDirectory}"
            ]);
    }

    private static string BuildJavaRuntimeFileContent(MinecraftJavaRuntimeDownloadRequestFilePlan filePlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"downloaded {filePlan.RelativePath}",
                $"size={filePlan.Size}",
                $"sha1={filePlan.Sha1}",
                $"sources={string.Join(" | ", filePlan.RequestUrls.AllUrls)}"
            ]);
    }

    private static string BuildPreexistingJavaRuntimeFileContent(MinecraftJavaRuntimeDownloadRequestFilePlan filePlan)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"reused {filePlan.RelativePath}",
                $"size={filePlan.Size}",
                $"sha1={filePlan.Sha1}",
                $"sources={string.Join(" | ", filePlan.RequestUrls.AllUrls)}"
            ]);
    }

    private static string ResolveCrashArchivePath(string workspaceRoot, string suggestedArchiveName, string? exportArchivePath)
    {
        if (string.IsNullOrWhiteSpace(exportArchivePath))
        {
            return Path.Combine(workspaceRoot, "output", suggestedArchiveName);
        }

        return Path.IsPathRooted(exportArchivePath)
            ? Path.GetFullPath(exportArchivePath)
            : Path.GetFullPath(Path.Combine(workspaceRoot, exportArchivePath));
    }

    private static string ResolveLaunchScriptExportPath(string workspaceRoot, string scriptExportPath)
    {
        return Path.IsPathRooted(scriptExportPath)
            ? Path.GetFullPath(scriptExportPath)
            : Path.GetFullPath(Path.Combine(workspaceRoot, scriptExportPath));
    }

    private static MinecraftJavaRuntimeDownloadSessionState ResolveJavaDownloadSessionState(SpikeJavaDownloadSessionState state)
    {
        return state switch
        {
            SpikeJavaDownloadSessionState.Finished => MinecraftJavaRuntimeDownloadSessionState.Finished,
            SpikeJavaDownloadSessionState.Failed => MinecraftJavaRuntimeDownloadSessionState.Failed,
            SpikeJavaDownloadSessionState.Aborted => MinecraftJavaRuntimeDownloadSessionState.Aborted,
            _ => throw new InvalidOperationException($"Unsupported Java download session state '{state}'.")
        };
    }

    private static string MapSamplePath(string samplePath, string workspaceRoot)
    {
        var normalizedPath = samplePath.Replace('\\', '/');
        if (normalizedPath.Length >= 2 &&
            char.IsLetter(normalizedPath[0]) &&
            normalizedPath[1] == ':')
        {
            normalizedPath = normalizedPath[2..];
        }

        var segments = normalizedPath
            .TrimStart('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        return Path.Combine([workspaceRoot, .. segments]);
    }

    private static string[] DistinctPaths(IEnumerable<string> paths)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (invalidCharacters.Contains(character))
            {
                continue;
            }

            builder.Append(character switch
            {
                ' ' => '-',
                '/' => '-',
                '\\' => '-',
                _ => character
            });
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

    private static IReadOnlyList<string> BuildStartupPromptLines(LauncherStartupPrompt prompt)
    {
        return
        [
            $"Prompt title: {prompt.Title}",
            $"Message: {prompt.Message.ReplaceLineEndings(" ")}",
            $"Buttons: {string.Join(" | ", prompt.Buttons.Select(button => button.Label))}"
        ];
    }
}
