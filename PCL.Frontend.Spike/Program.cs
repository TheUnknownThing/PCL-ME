using System.Text.Json;
using PCL.Frontend.Spike.Desktop;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Rendering;
using PCL.Frontend.Spike.Serialization;
using PCL.Frontend.Spike.Workflows;
using PCL.Frontend.Spike.Workflows.Inspection;

var parseResult = SpikeCommandParser.Parse(args);
if (parseResult.ShowHelp)
{
    Console.WriteLine(SpikeCommandParser.GetUsageText());
    return;
}

if (parseResult.ErrorMessage is not null || parseResult.Options is null)
{
    Console.Error.WriteLine(parseResult.ErrorMessage ?? "Unknown error.");
    Console.WriteLine();
    Console.WriteLine(SpikeCommandParser.GetUsageText());
    Environment.ExitCode = 1;
    return;
}

var options = parseResult.Options;
if (options.Command == SpikeCommandKind.App)
{
    SpikeDesktopHost.Run(options);
    return;
}

var payload = BuildPayload(options);

if (options.Format == SpikeOutputFormat.Text)
{
    Console.WriteLine(SpikeTextRenderer.Render(payload));
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(payload, CreateJsonOptions()));
}

static object BuildPayload(SpikeCommandOptions options)
{
    return options.Mode switch
    {
        SpikeOutputMode.Plan => BuildPlanPayload(options),
        SpikeOutputMode.Run => BuildRunPayload(options),
        SpikeOutputMode.Execute => BuildExecutePayload(options),
        _ => throw new InvalidOperationException($"Unsupported mode '{options.Mode}'.")
    };
}

static object BuildPlanPayload(SpikeCommandOptions options)
{
    var shellInputs = SpikeInputResolver.ResolveShellInputs(options);
    var startupInputs = SpikeInputResolver.ResolveStartupInputs(options);
    var launchInputs = SpikeInputResolver.ResolveLaunchInputs(options);
    var crashInputs = SpikeInputResolver.ResolveCrashInputs(options);

    return options.Command switch
    {
        SpikeCommandKind.Startup => SpikeSampleFactory.BuildStartupPlan(startupInputs),
        SpikeCommandKind.Shell => SpikeSampleFactory.BuildShellPlan(shellInputs),
        SpikeCommandKind.Launch => SpikeSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
        SpikeCommandKind.Crash => SpikeSampleFactory.BuildCrashPlan(crashInputs),
        SpikeCommandKind.All => new SpikePlanBundle(
            SpikeSampleFactory.BuildStartupPlan(startupInputs),
            SpikeSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
            SpikeSampleFactory.BuildCrashPlan(crashInputs)),
        _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
    };
}

static object BuildRunPayload(SpikeCommandOptions options)
{
    var shellInputs = SpikeInputResolver.ResolveShellInputs(options);
    var startupInputs = SpikeInputResolver.ResolveStartupInputs(options);
    var launchInputs = SpikeInputResolver.ResolveLaunchInputs(options);
    var crashInputs = SpikeInputResolver.ResolveCrashInputs(options);

    return options.Command switch
    {
        SpikeCommandKind.Startup => SpikeRunner.BuildStartupRun(SpikeSampleFactory.BuildStartupPlan(startupInputs)),
        SpikeCommandKind.Shell => SpikeRunner.BuildShellRun(SpikeSampleFactory.BuildShellPlan(shellInputs)),
        SpikeCommandKind.Launch => SpikeRunner.BuildLaunchRun(
            SpikeSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
            options.JavaPromptDecision,
            options.JavaDownloadState),
        SpikeCommandKind.Crash => SpikeRunner.BuildCrashRun(
            SpikeSampleFactory.BuildCrashPlan(crashInputs),
            options.CrashAction),
        SpikeCommandKind.All => new SpikeRunBundle(
            SpikeRunner.BuildStartupRun(SpikeSampleFactory.BuildStartupPlan(startupInputs)),
            SpikeRunner.BuildLaunchRun(
                SpikeSampleFactory.BuildLaunchPlan(launchInputs, options.SaveBatchPath),
                options.JavaPromptDecision,
                options.JavaDownloadState),
            SpikeRunner.BuildCrashRun(SpikeSampleFactory.BuildCrashPlan(crashInputs), options.CrashAction)),
        _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
    };
}

static object BuildExecutePayload(SpikeCommandOptions options)
{
    var workspaceRoot = ResolveWorkspaceRoot(options.WorkspaceRoot);
    var shellInputs = SpikeInputResolver.ResolveShellInputs(options);
    var startupInputs = SpikeInputResolver.ResolveStartupInputs(options);
    var launchInputs = SpikeInputResolver.ResolveLaunchInputs(options);
    var crashInputs = SpikeInputResolver.ResolveCrashInputs(options);

    return options.Command switch
    {
        SpikeCommandKind.Startup => CreateStartupExecution(startupInputs, workspaceRoot),
        SpikeCommandKind.Shell => CreateShellExecution(shellInputs, workspaceRoot),
        SpikeCommandKind.Launch => CreateLaunchExecution(
            launchInputs,
            workspaceRoot,
            options.JavaPromptDecision,
            options.JavaDownloadState,
            options.SaveBatchPath),
        SpikeCommandKind.Crash => CreateCrashExecution(crashInputs, workspaceRoot, options.CrashAction, options.ExportArchivePath),
        SpikeCommandKind.All => new SpikeExecutionBundle(
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

static JsonSerializerOptions CreateJsonOptions()
{
    return SpikeJson.CreateOptions();
}

static string ResolveWorkspaceRoot(string? requestedWorkspaceRoot)
{
    if (!string.IsNullOrWhiteSpace(requestedWorkspaceRoot))
    {
        return Path.GetFullPath(requestedWorkspaceRoot);
    }

    var workspaceId = Guid.NewGuid().ToString("N")[..8];
    var directoryName = $"spike-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{workspaceId}";
    return Path.Combine(Path.GetTempPath(), "PCL.Frontend.Spike", directoryName);
}

static StartupSpikeExecution CreateStartupExecution(StartupSpikeInputs inputs, string workspaceRoot)
{
    var execution = SpikeExecutor.ExecuteStartup(
        SpikeSampleFactory.BuildStartupPlan(inputs),
        workspaceRoot);
    var inputArtifact = SpikeInputStore.SaveStartupInputs(execution.Execution.WorkspaceRoot, inputs);
    return SpikeExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
}

static ShellSpikeExecution CreateShellExecution(ShellSpikeInputs inputs, string workspaceRoot)
{
    var execution = SpikeExecutor.ExecuteShell(
        SpikeSampleFactory.BuildShellPlan(inputs),
        workspaceRoot);
    var inputArtifact = SpikeInputStore.SaveShellInputs(execution.Execution.WorkspaceRoot, inputs);
    return SpikeExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
}

static LaunchSpikeExecution CreateLaunchExecution(
    LaunchSpikeInputs inputs,
    string workspaceRoot,
    PCL.Core.Minecraft.Launch.MinecraftLaunchJavaPromptDecision javaPromptDecision,
    SpikeJavaDownloadSessionState javaDownloadState,
    string? saveBatchPath)
{
    var execution = SpikeExecutor.ExecuteLaunch(
        SpikeSampleFactory.BuildLaunchPlan(inputs, saveBatchPath),
        workspaceRoot,
        javaPromptDecision,
        javaDownloadState);
    var inputArtifact = SpikeInputStore.SaveLaunchInputs(execution.Execution.WorkspaceRoot, inputs);
    return SpikeExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
}

static CrashSpikeExecution CreateCrashExecution(
    CrashSpikeInputs inputs,
    string workspaceRoot,
    PCL.Core.Minecraft.MinecraftCrashOutputPromptActionKind crashAction,
    string? exportArchivePath)
{
    var execution = SpikeExecutor.ExecuteCrash(
        SpikeSampleFactory.BuildCrashPlan(inputs),
        workspaceRoot,
        crashAction,
        exportArchivePath);
    var inputArtifact = SpikeInputStore.SaveCrashInputs(execution.Execution.WorkspaceRoot, inputs);
    return SpikeExecutionAugmenter.AddInputArtifact(execution, inputArtifact);
}
