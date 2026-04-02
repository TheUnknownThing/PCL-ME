using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;

var command = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "all";
var scenario = args.Skip(1).FirstOrDefault()?.Trim().ToLowerInvariant() ?? "modern-fabric";
var jsonOptions = CreateJsonOptions();

if (command is "help" or "--help" or "-h")
{
    PrintUsage();
    return;
}

var payload = command switch
{
    "startup" => BuildStartupSample(),
    "launch" => BuildLaunchSample(scenario),
    "crash" => BuildCrashSample(),
    "all" => new
    {
        startup = BuildStartupSample(),
        launch = BuildLaunchSample(scenario),
        crash = BuildCrashSample()
    },
    _ => new
    {
        error = $"Unknown command '{command}'.",
        usage = GetUsageText()
    }
};

Console.WriteLine(JsonSerializer.Serialize(payload, jsonOptions));

static object BuildStartupSample()
{
    var preparation = LauncherStartupPreparationService.Prepare(
        new LauncherStartupPreparationRequest(
            ExecutableDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\",
            TempDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\Temp\",
            AppDataDirectory: @"C:\Users\demo\AppData\Roaming\PCL\",
            IsBetaVersion: false,
            DetectedWindowsVersion: new Version(10, 0, 17700),
            Is64BitOperatingSystem: true));

    return new
    {
        immediateCommand = LauncherStartupShellService.ResolveImmediateCommand(["--memory"]),
        bootstrap = preparation,
        environmentPrompt = LauncherStartupShellService.GetEnvironmentWarningPrompt(preparation.EnvironmentWarningMessage),
        visual = LauncherStartupVisualService.GetVisualPlan(showStartupLogo: true),
        consent = LauncherStartupConsentService.Evaluate(
            new LauncherStartupConsentRequest(
                LauncherStartupSpecialBuildKind.Ci,
                IsSpecialBuildHintDisabled: false,
                HasAcceptedEula: false,
                IsTelemetryDefault: true))
    };
}

static object BuildLaunchSample(string scenario)
{
    var javaWorkflow = BuildJavaWorkflowPlan(scenario);
    var processPlan = MinecraftLaunchRuntimeService.BuildProcessPlan(
        new MinecraftLaunchProcessRequest(
            PreferConsoleJava: false,
            JavaExecutablePath: @"C:\Java\bin\java.exe",
            JavawExecutablePath: @"C:\Java\bin\javaw.exe",
            JavaFolder: @"C:\Java\bin",
            CurrentPathEnvironmentValue: @"C:\Windows\System32;C:\Windows",
            AppDataPath: @"C:\Minecraft\.minecraft",
            WorkingDirectory: @"C:\Minecraft\.minecraft",
            LaunchArguments: "--username DemoPlayer --version 1.20.5",
            PrioritySetting: 0));
    var watcherPlan = MinecraftLaunchRuntimeService.BuildWatcherPlan(
        new MinecraftLaunchWatcherRequest(
            VersionSpecificWindowTitleTemplate: "${version_name} - demo",
            VersionTitleExplicitlyEmpty: false,
            GlobalWindowTitleTemplate: "{user_type}",
            JavaFolder: @"C:\Java\bin",
            JstackExecutableExists: true));

    return new
    {
        scenario,
        javaWorkflow,
        initialSelection = MinecraftLaunchJavaWorkflowService.ResolveInitialSelection(javaWorkflow, hasSelectedJava: false),
        acceptedPromptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
            javaWorkflow.MissingJavaPrompt,
            MinecraftLaunchJavaPromptDecision.Download),
        postDownloadSelection = MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(javaWorkflow, hasSelectedJava: true),
        processPlan,
        watcherPlan,
        postLaunchShell = MinecraftLaunchShellService.GetPostLaunchShellPlan(
            new MinecraftLaunchPostLaunchShellRequest(
                LauncherVisibility.HideAndReopen,
                StopMusicInGame: true,
                StartMusicInGame: false)),
        completionNotification = MinecraftLaunchShellService.GetCompletionNotification(
            new MinecraftLaunchCompletionRequest(
                InstanceName: "Demo Instance",
                Outcome: MinecraftLaunchOutcome.Succeeded,
                IsScriptExport: false,
                AbortHint: null))
    };
}

static MinecraftLaunchJavaWorkflowPlan BuildJavaWorkflowPlan(string scenario)
{
    var request = scenario switch
    {
        "legacy-forge" => new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: true,
            ReleaseTime: new DateTime(2013, 6, 25),
            VanillaVersion: new Version(1, 7, 10),
            HasOptiFine: false,
            HasForge: true,
            ForgeVersion: "10.13.4.1614",
            HasCleanroom: false,
            HasFabric: false,
            HasLiteLoader: false,
            HasLabyMod: false,
            JsonRequiredMajorVersion: 7,
            MojangRecommendedMajorVersion: 0,
            MojangRecommendedComponent: null),
        _ => new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: true,
            ReleaseTime: new DateTime(2024, 4, 23),
            VanillaVersion: new Version(20, 0, 5),
            HasOptiFine: false,
            HasForge: false,
            ForgeVersion: null,
            HasCleanroom: false,
            HasFabric: true,
            HasLiteLoader: false,
            HasLabyMod: false,
            JsonRequiredMajorVersion: 21,
            MojangRecommendedMajorVersion: 21,
            MojangRecommendedComponent: "jre-legacy")
    };

    return MinecraftLaunchJavaWorkflowService.BuildPlan(request);
}

static object BuildCrashSample()
{
    var environment = new SystemEnvironmentSnapshot(
        OsDescription: "Windows 11 Pro",
        OsVersion: new Version(10, 0, 22631),
        OsArchitecture: Architecture.X64,
        Is64BitOperatingSystem: true,
        TotalPhysicalMemoryBytes: 32UL * 1024UL * 1024UL * 1024UL,
        CpuName: "AMD Ryzen 7 7800X3D",
        Gpus:
        [
            new SystemGpuInfo("NVIDIA GeForce RTX 4070 SUPER", 12288, "555.85"),
            new SystemGpuInfo("AMD Radeon(TM) Graphics", 512, "31.0")
        ]);

    return new
    {
        outputPrompt = MinecraftCrashWorkflowService.BuildOutputPrompt(
            new MinecraftCrashOutputPromptRequest(
                ResultText: "Mod 加载器版本与 Mod 不兼容: DemoMod requires a newer loader.",
                IsManualAnalysis: false,
                HasDirectFile: true,
                CanOpenModLoaderSettings: true)),
        exportPlan = MinecraftCrashExportWorkflowService.CreatePlan(
            new MinecraftCrashExportPlanRequest(
                Timestamp: new DateTime(2026, 4, 2, 10, 15, 0),
                ReportDirectory: @"C:\PCL\CrashReport\2026-04-02\",
                LauncherVersionName: "2.11.0",
                UniqueAddress: "demo-unique-address",
                SourceFilePaths:
                [
                    @"C:\PCL\LatestLaunch.bat",
                    @"C:\PCL\RawOutput.log"
                ],
                AdditionalSourceFilePaths:
                [
                    @"C:\PCL\Logs\latest.log"
                ],
                CurrentLauncherLogFilePath: @"C:\PCL\Logs\PCL.log",
                Environment: environment,
                CurrentAccessToken: "12345abcdefghijklmnopqrstuvwxyz67890",
                CurrentUserUuid: "demo-uuid",
                UserProfilePath: @"C:\Users\demo"))
    };
}

static void PrintUsage()
{
    Console.WriteLine(GetUsageText());
}

static string GetUsageText()
{
    return """
PCL.Frontend.Spike

Usage:
  startup
  launch [modern-fabric|legacy-forge]
  crash
  all [modern-fabric|legacy-forge]
  help
""";
}

static JsonSerializerOptions CreateJsonOptions()
{
    return new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
