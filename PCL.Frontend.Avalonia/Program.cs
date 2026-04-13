using System.Text.Json;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Rendering;
using PCL.Frontend.Avalonia.Serialization;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Workflows.Inspection;

public partial class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            AvaloniaDesktopHost.Run(CreateDefaultAppOptions());
            return;
        }

        var parseResult = AvaloniaCommandParser.Parse(args);
        if (parseResult.ShowHelp)
        {
            Console.WriteLine(AvaloniaCommandParser.GetUsageText());
            return;
        }

        if (parseResult.ErrorMessage is not null || parseResult.Options is null)
        {
            Console.Error.WriteLine(parseResult.ErrorMessage ?? "Unknown error.");
            Console.WriteLine();
            Console.WriteLine(AvaloniaCommandParser.GetUsageText());
            Environment.ExitCode = 1;
            return;
        }

        var options = parseResult.Options;
        if (options.Command == AvaloniaCommandKind.App)
        {
            AvaloniaDesktopHost.Run(options);
            return;
        }

        var payload = BuildPayload(options);

        if (options.Format == AvaloniaOutputFormat.Text)
        {
            Console.WriteLine(AvaloniaTextRenderer.Render(payload));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(payload, CreateJsonOptions()));
        }
    }

    private static AvaloniaCommandOptions CreateDefaultAppOptions()
    {
        return new AvaloniaCommandOptions(
            AvaloniaCommandKind.App,
            Scenario: "modern-fabric",
            Mode: AvaloniaOutputMode.Plan,
            Format: AvaloniaOutputFormat.Json,
            UseHostEnvironment: false,
            JavaPromptDecision: PCL.Core.Minecraft.Launch.MinecraftLaunchJavaPromptDecision.Download,
            JavaDownloadState: AvaloniaJavaDownloadSessionState.Finished,
            CrashAction: PCL.Core.Minecraft.MinecraftCrashOutputPromptActionKind.ExportReport,
            ForceCjkFontWarning: false,
            SaveBatchPath: null,
            WorkspaceRoot: null,
            InputRoot: null,
            ExportArchivePath: null);
    }

    private static object BuildPayload(AvaloniaCommandOptions options)
    {
        return options.Mode switch
        {
            AvaloniaOutputMode.Plan => BuildPlanPayload(options),
            AvaloniaOutputMode.Run => BuildRunPayload(options),
            AvaloniaOutputMode.Execute => BuildExecutePayload(options),
            _ => throw new InvalidOperationException($"Unsupported mode '{options.Mode}'.")
        };
    }

    private static object BuildPlanPayload(AvaloniaCommandOptions options)
    {
        var shellInputs = AvaloniaInputResolver.ResolveShellInputs(options);
        var startupInputs = AvaloniaInputResolver.ResolveStartupInputs(options);
        var launchInputs = AvaloniaInputResolver.ResolveLaunchInputs(options);
        var crashInputs = AvaloniaInputResolver.ResolveCrashInputs(options);

        return options.Command switch
        {
            AvaloniaCommandKind.Startup => AvaloniaSampleFactory.BuildStartupPlan(startupInputs),
            AvaloniaCommandKind.Shell => AvaloniaSampleFactory.BuildShellPlan(shellInputs),
            AvaloniaCommandKind.Launch => AvaloniaSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
            AvaloniaCommandKind.Crash => BuildCrashPlan(options, crashInputs),
            AvaloniaCommandKind.All => new AvaloniaPlanBundle(
                AvaloniaSampleFactory.BuildStartupPlan(startupInputs),
                AvaloniaSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
                BuildCrashPlan(options, crashInputs)),
            _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
        };
    }

    private static object BuildRunPayload(AvaloniaCommandOptions options)
    {
        var shellInputs = AvaloniaInputResolver.ResolveShellInputs(options);
        var startupInputs = AvaloniaInputResolver.ResolveStartupInputs(options);
        var launchInputs = AvaloniaInputResolver.ResolveLaunchInputs(options);
        var crashInputs = AvaloniaInputResolver.ResolveCrashInputs(options);

        return options.Command switch
        {
            AvaloniaCommandKind.Startup => AvaloniaRunner.BuildStartupRun(AvaloniaSampleFactory.BuildStartupPlan(startupInputs)),
            AvaloniaCommandKind.Shell => AvaloniaRunner.BuildShellRun(AvaloniaSampleFactory.BuildShellPlan(shellInputs)),
            AvaloniaCommandKind.Launch => AvaloniaRunner.BuildLaunchRun(
                AvaloniaSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
                options.JavaPromptDecision,
                options.JavaDownloadState),
            AvaloniaCommandKind.Crash => AvaloniaRunner.BuildCrashRun(
                BuildCrashPlan(options, crashInputs),
                options.CrashAction),
            AvaloniaCommandKind.All => new AvaloniaRunBundle(
                AvaloniaRunner.BuildStartupRun(AvaloniaSampleFactory.BuildStartupPlan(startupInputs)),
                AvaloniaRunner.BuildLaunchRun(
                    AvaloniaSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
                    options.JavaPromptDecision,
                    options.JavaDownloadState),
                AvaloniaRunner.BuildCrashRun(BuildCrashPlan(options, crashInputs), options.CrashAction)),
            _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
        };
    }

    private static object BuildExecutePayload(AvaloniaCommandOptions options)
    {
        var workspaceRoot = ResolveWorkspaceRoot(options.WorkspaceRoot);
        var shellInputs = AvaloniaInputResolver.ResolveShellInputs(options);
        var startupInputs = AvaloniaInputResolver.ResolveStartupInputs(options);
        var launchInputs = AvaloniaInputResolver.ResolveLaunchInputs(options);
        var crashInputs = AvaloniaInputResolver.ResolveCrashInputs(options);

        return options.Command switch
        {
            AvaloniaCommandKind.Startup => CreateStartupExecution(startupInputs, workspaceRoot),
            AvaloniaCommandKind.Shell => CreateShellExecution(shellInputs, workspaceRoot),
            AvaloniaCommandKind.Launch => CreateLaunchExecution(
                launchInputs,
                workspaceRoot,
                options.JavaPromptDecision,
                options.JavaDownloadState,
                options.SaveBatchPath),
            AvaloniaCommandKind.Crash => CreateCrashExecution(crashInputs, workspaceRoot, options.CrashAction, options.ExportArchivePath),
            AvaloniaCommandKind.All => new AvaloniaExecutionBundle(
                CreateStartupExecution(startupInputs, Path.Combine(workspaceRoot, "startup")),
                CreateLaunchExecution(
                    launchInputs,
                    Path.Combine(workspaceRoot, "launch"),
                    options.JavaPromptDecision,
                    options.JavaDownloadState,
                    options.SaveBatchPath),
                CreateCrashExecution(crashInputs, Path.Combine(workspaceRoot, "crash"), options.CrashAction, options.ExportArchivePath)),
            _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
        };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return AvaloniaJson.CreateOptions();
    }

    private static string ResolveWorkspaceRoot(string? requestedWorkspaceRoot)
    {
        if (!string.IsNullOrWhiteSpace(requestedWorkspaceRoot))
        {
            return Path.GetFullPath(requestedWorkspaceRoot);
        }

        var workspaceId = Guid.NewGuid().ToString("N")[..8];
        var directoryName = $"avalonia-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{workspaceId}";
        return Path.Combine(Path.GetTempPath(), "PCL.Frontend.Avalonia", directoryName);
    }

    private static StartupAvaloniaExecution CreateStartupExecution(StartupAvaloniaInputs inputs, string workspaceRoot)
    {
        var execution = AvaloniaExecutor.ExecuteStartup(
            AvaloniaSampleFactory.BuildStartupPlan(inputs),
            workspaceRoot);
        var inputArtifact = AvaloniaInputStore.SaveStartupInputs(execution.Execution.WorkspaceRoot, inputs);
        return AvaloniaExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
    }

    private static ShellAvaloniaExecution CreateShellExecution(ShellAvaloniaInputs inputs, string workspaceRoot)
    {
        var execution = AvaloniaExecutor.ExecuteShell(
            AvaloniaSampleFactory.BuildShellPlan(inputs),
            workspaceRoot);
        var inputArtifact = AvaloniaInputStore.SaveShellInputs(execution.Execution.WorkspaceRoot, inputs);
        return AvaloniaExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
    }

    private static LaunchAvaloniaExecution CreateLaunchExecution(
        LaunchAvaloniaInputs inputs,
        string workspaceRoot,
        PCL.Core.Minecraft.Launch.MinecraftLaunchJavaPromptDecision javaPromptDecision,
        AvaloniaJavaDownloadSessionState javaDownloadState,
        string? saveBatchPath)
    {
        var execution = AvaloniaExecutor.ExecuteLaunch(
            AvaloniaSampleFactory.BuildLaunchPlan(inputs, saveBatchPath),
            workspaceRoot,
            javaPromptDecision,
            javaDownloadState);
        var inputArtifact = AvaloniaInputStore.SaveLaunchInputs(execution.Execution.WorkspaceRoot, inputs);
        return AvaloniaExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
    }

    private static CrashAvaloniaExecution CreateCrashExecution(
        CrashAvaloniaInputs inputs,
        string workspaceRoot,
        PCL.Core.Minecraft.MinecraftCrashOutputPromptActionKind crashAction,
        string? exportArchivePath)
    {
        var execution = AvaloniaExecutor.ExecuteCrash(
            BuildCrashPlan(inputs),
            workspaceRoot,
            crashAction,
            exportArchivePath);
        var inputArtifact = AvaloniaInputStore.SaveCrashInputs(execution.Execution.WorkspaceRoot, inputs);
        return AvaloniaExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
    }

    private static CrashAvaloniaPlan BuildCrashPlan(AvaloniaCommandOptions options, CrashAvaloniaInputs inputs)
    {
        return options.UseHostEnvironment || options.InputRoot is not null
            ? BuildCrashPlan(inputs)
            : AvaloniaSampleFactory.BuildCrashPlan(inputs);
    }

    private static CrashAvaloniaPlan BuildCrashPlan(CrashAvaloniaInputs inputs)
    {
        return FrontendInspectionCrashCompositionService.Compose(inputs);
    }
}
