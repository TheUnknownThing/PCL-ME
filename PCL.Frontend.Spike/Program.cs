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
    var startupPlan = LauncherStartupWorkflowService.BuildPlan(
        new LauncherStartupWorkflowRequest(
            CommandLineArguments: ["--memory"],
            ExecutableDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\",
            TempDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\Temp\",
            AppDataDirectory: @"C:\Users\demo\AppData\Roaming\PCL\",
            IsBetaVersion: false,
            DetectedWindowsVersion: new Version(10, 0, 17700),
            Is64BitOperatingSystem: true,
            ShowStartupLogo: true));

    return new
    {
        immediateCommand = startupPlan.ImmediateCommand,
        bootstrap = startupPlan.Bootstrap,
        environmentPrompt = startupPlan.EnvironmentWarningPrompt,
        visual = startupPlan.Visual,
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
    var resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(
        new MinecraftLaunchResolutionRequest(
            WindowMode: 3,
            LauncherWindowWidth: null,
            LauncherWindowHeight: null,
            LauncherTitleBarHeight: 0,
            CustomWidth: 1280,
            CustomHeight: 720,
            GameVersionDrop: 8,
            JavaMajorVersion: 21,
            JavaRevision: 0,
            HasOptiFine: false,
            HasForge: false,
            DpiScale: 1));
    var classpathPlan = MinecraftLaunchClasspathService.BuildPlan(
        new MinecraftLaunchClasspathRequest(
            Libraries:
            [
                new MinecraftLaunchClasspathLibrary("com.cleanroommc:cleanroom:0.2.4-alpha", @"C:\Minecraft\libraries\cleanroom.jar", false),
                new MinecraftLaunchClasspathLibrary("com.example:core", @"C:\Minecraft\libraries\core.jar", false),
                new MinecraftLaunchClasspathLibrary("optifine:OptiFine", @"C:\Minecraft\libraries\optifine.jar", false)
            ],
            CustomHeadEntries: [@"C:\Minecraft\libraries\override.jar"],
            RetroWrapperPath: @"C:\Minecraft\libraries\retrowrapper\RetroWrapper.jar",
            ClasspathSeparator: ";"));
    var nativesDirectory = MinecraftLaunchNativesDirectoryService.ResolvePath(
        new MinecraftLaunchNativesDirectoryRequest(
            PreferredInstanceDirectory: @"C:\Minecraft\instances\demo\demo-natives",
            PreferInstanceDirectory: false,
            AppDataNativesDirectory: @"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives",
            FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"));
    var replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(
        new MinecraftLaunchReplacementValueRequest(
            ClasspathSeparator: ";",
            NativesDirectory: nativesDirectory,
            LibraryDirectory: @"C:\Minecraft\libraries",
            LibrariesDirectory: @"C:\Minecraft\libraries",
            LauncherName: "PCLCE",
            LauncherVersion: "2110",
            VersionName: "Demo Instance",
            VersionType: "PCL CE",
            GameDirectory: @"C:\Minecraft\.minecraft",
            AssetsRoot: @"C:\Minecraft\assets",
            UserProperties: "{}",
            AuthPlayerName: "DemoPlayer",
            AuthUuid: "uuid",
            AccessToken: "token",
            UserType: "msa",
            ResolutionWidth: resolutionPlan.Width,
            ResolutionHeight: resolutionPlan.Height,
            GameAssetsDirectory: @"C:\Minecraft\assets\virtual\legacy",
            AssetsIndexName: "8",
            Classpath: classpathPlan.JoinedClasspath));
    var prerunPlan = MinecraftLaunchPrerunWorkflowService.BuildPlan(
        new MinecraftLaunchPrerunWorkflowRequest(
            LauncherProfilesPath: @"C:\Minecraft\.minecraft\launcher_profiles.json",
            IsMicrosoftLogin: true,
            ExistingLauncherProfilesJson: "{}",
            UserName: "DemoPlayer",
            ClientToken: "client",
            LauncherProfilesDefaultTimestamp: new DateTime(2026, 4, 2, 10, 20, 0),
            PrimaryOptionsFilePath: @"C:\Minecraft\.minecraft\options.txt",
            PrimaryOptionsFileExists: true,
            PrimaryCurrentLanguage: "en_us",
            YosbrOptionsFilePath: @"C:\Minecraft\.minecraft\config\yosbr\options.txt",
            YosbrOptionsFileExists: false,
            HasExistingSaves: true,
            ReleaseTime: new DateTime(2024, 4, 23),
            LaunchWindowType: 0,
            AutoChangeLanguage: true));
    var sessionStartPlan = MinecraftLaunchSessionWorkflowService.BuildStartPlan(
        new MinecraftLaunchSessionStartWorkflowRequest(
            new MinecraftLaunchCustomCommandWorkflowRequest(
                new MinecraftLaunchCustomCommandRequest(
                    JavaMajorVersion: 21,
                    InstanceName: "Demo Instance",
                    WorkingDirectory: @"C:\Minecraft\.minecraft",
                    JavaExecutablePath: @"C:\Java\bin\java.exe",
                    LaunchArguments: "--username DemoPlayer --version 1.20.5",
                    GlobalCommand: "echo preparing",
                    WaitForGlobalCommand: true,
                    InstanceCommand: "echo instance hook",
                    WaitForInstanceCommand: false),
                ShellWorkingDirectory: @"C:\Minecraft"),
            new MinecraftLaunchProcessRequest(
                PreferConsoleJava: false,
                JavaExecutablePath: @"C:\Java\bin\java.exe",
                JavawExecutablePath: @"C:\Java\bin\javaw.exe",
                JavaFolder: @"C:\Java\bin",
                CurrentPathEnvironmentValue: @"C:\Windows\System32;C:\Windows",
                AppDataPath: @"C:\Minecraft\.minecraft",
                WorkingDirectory: @"C:\Minecraft\.minecraft",
                LaunchArguments: "--username DemoPlayer --version 1.20.5",
                PrioritySetting: 0),
            new MinecraftLaunchWatcherWorkflowRequest(
                new MinecraftLaunchSessionLogRequest(
                    LauncherVersionName: "2.11.0",
                    LauncherVersionCode: 2110,
                    GameVersionDisplayName: "1.20.5",
                    GameVersionRaw: "1.20.5",
                    GameVersionDrop: 8,
                    IsGameVersionReliable: true,
                    AssetsIndexName: "8",
                    InheritedInstanceName: "Base",
                    AllocatedMemoryInGigabytes: 4,
                    MinecraftFolder: @"C:\Minecraft\.minecraft",
                    InstanceFolder: @"C:\Minecraft\.minecraft\versions\demo",
                    IsVersionIsolated: true,
                    IsHmclFormatJson: false,
                    JavaDescription: "Java 21",
                    NativesFolder: @"C:\Minecraft\natives",
                    PlayerName: "DemoPlayer",
                    AccessToken: "token",
                    ClientToken: "client",
                    Uuid: "uuid",
                    LoginType: "Microsoft"),
                new MinecraftLaunchWatcherRequest(
                    VersionSpecificWindowTitleTemplate: "${version_name} - demo",
                    VersionTitleExplicitlyEmpty: false,
                    GlobalWindowTitleTemplate: "{user_type}",
                    JavaFolder: @"C:\Java\bin",
                    JstackExecutableExists: true),
                OutputRealTimeLog: true)));
    var argumentPlan = MinecraftLaunchArgumentWorkflowService.BuildPlan(
        new MinecraftLaunchArgumentPlanRequest(
            BaseArguments: "--username ${auth_player_name} --versionType ${version_type} --gameDir ${game_directory}",
            JavaMajorVersion: 21,
            UseFullscreen: false,
            ExtraArguments: ["--demo"],
            CustomGameArguments: "--width ${resolution_width} --height ${resolution_height}",
            ReplacementValues: new Dictionary<string, string>
            {
                ["${auth_player_name}"] = "DemoPlayer",
                ["${version_type}"] = "PCL CE",
                ["${game_directory}"] = @"C:\Minecraft\.minecraft",
                ["${resolution_width}"] = resolutionPlan.Width.ToString(),
                ["${resolution_height}"] = resolutionPlan.Height.ToString()
            },
            WorldName: null,
            ServerAddress: "play.example.invalid",
            ReleaseTime: new DateTime(2024, 4, 23),
            HasOptiFine: false));
    return new
    {
        scenario,
        javaWorkflow,
        initialSelection = MinecraftLaunchJavaWorkflowService.ResolveInitialSelection(javaWorkflow, hasSelectedJava: false),
        acceptedPromptOutcome = MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
            javaWorkflow.MissingJavaPrompt,
            MinecraftLaunchJavaPromptDecision.Download),
        postDownloadSelection = MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(javaWorkflow, hasSelectedJava: true),
        resolutionPlan,
        classpathPlan,
        nativesDirectory,
        replacementPlan,
        argumentPlan,
        prerunPlan,
        sessionStartPlan,
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
