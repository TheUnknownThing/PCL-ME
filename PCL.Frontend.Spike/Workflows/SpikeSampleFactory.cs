using System.Runtime.InteropServices;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils.OS;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class SpikeSampleFactory
{
    public static StartupSpikeInputs CreateDefaultStartupInputs()
    {
        return new StartupSpikeInputs(
            new LauncherStartupWorkflowRequest(
                CommandLineArguments: ["--memory"],
                ExecutableDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\",
                TempDirectory: @"C:\Users\demo\AppData\Local\Temp\PCL\Temp\",
                AppDataDirectory: @"C:\Users\demo\AppData\Roaming\PCL\",
                IsBetaVersion: false,
                DetectedWindowsVersion: new Version(10, 0, 17700),
                Is64BitOperatingSystem: true,
                ShowStartupLogo: true),
            new LauncherStartupConsentRequest(
                LauncherStartupSpecialBuildKind.Ci,
                IsSpecialBuildHintDisabled: false,
                HasAcceptedEula: false,
                IsTelemetryDefault: true));
    }

    public static StartupSpikePlan BuildStartupPlan(StartupSpikeInputs inputs)
    {
        var startupPlan = LauncherStartupWorkflowService.BuildPlan(inputs.StartupWorkflowRequest);
        var consent = LauncherStartupConsentService.Evaluate(inputs.StartupConsentRequest);

        return new StartupSpikePlan(startupPlan, consent);
    }

    public static StartupSpikePlan BuildStartupPlan()
    {
        return BuildStartupPlan(CreateDefaultStartupInputs());
    }

    public static ShellSpikeInputs CreateDefaultShellInputs()
    {
        return new ShellSpikeInputs(
            CreateDefaultStartupInputs(),
            new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                HasRunningTasks: true,
                HasGameLogs: true));
    }

    public static LauncherFrontendShellPlan BuildShellPlan(ShellSpikeInputs inputs)
    {
        return LauncherFrontendShellService.BuildPlan(new LauncherFrontendShellRequest(
            inputs.StartupInputs.StartupWorkflowRequest,
            inputs.StartupInputs.StartupConsentRequest,
            inputs.NavigationRequest));
    }

    public static LaunchSpikeInputs CreateDefaultLaunchInputs(string scenario)
    {
        return new LaunchSpikeInputs(
            Scenario: scenario,
            LoginInputs: BuildLoginInputs(scenario),
            JavaRuntimeInputs: BuildJavaRuntimeInputs(scenario),
            JavaWorkflowRequest: BuildJavaWorkflowRequest(scenario),
            ResolutionRequest: new MinecraftLaunchResolutionRequest(
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
                DpiScale: 1),
            ClasspathRequest: new MinecraftLaunchClasspathRequest(
                Libraries:
                [
                    new MinecraftLaunchClasspathLibrary("com.cleanroommc:cleanroom:0.2.4-alpha", @"C:\Minecraft\libraries\cleanroom.jar", false),
                    new MinecraftLaunchClasspathLibrary("com.example:core", @"C:\Minecraft\libraries\core.jar", false),
                    new MinecraftLaunchClasspathLibrary("optifine:OptiFine", @"C:\Minecraft\libraries\optifine.jar", false)
                ],
                CustomHeadEntries: [@"C:\Minecraft\libraries\override.jar"],
                RetroWrapperPath: @"C:\Minecraft\libraries\retrowrapper\RetroWrapper.jar",
                ClasspathSeparator: ";"),
            NativesDirectoryRequest: new MinecraftLaunchNativesDirectoryRequest(
                PreferredInstanceDirectory: @"C:\Minecraft\instances\demo\demo-natives",
                PreferInstanceDirectory: false,
                AppDataNativesDirectory: @"C:\Users\demo\AppData\Roaming\.minecraft\bin\natives",
                FinalFallbackDirectory: @"C:\ProgramData\PCL\natives"),
            ReplacementValueRequest: new MinecraftLaunchReplacementValueRequest(
                ClasspathSeparator: ";",
                NativesDirectory: @"C:\Minecraft\instances\demo\demo-natives",
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
                ResolutionWidth: 1280,
                ResolutionHeight: 720,
                GameAssetsDirectory: @"C:\Minecraft\assets\virtual\legacy",
                AssetsIndexName: "8",
                Classpath: "C:\\Minecraft\\libraries\\override.jar;C:\\Minecraft\\libraries\\cleanroom.jar;C:\\Minecraft\\libraries\\retrowrapper\\RetroWrapper.jar;C:\\Minecraft\\libraries\\optifine.jar;C:\\Minecraft\\libraries\\cleanroom.jar;C:\\Minecraft\\libraries\\core.jar"),
            PrerunWorkflowRequest: new MinecraftLaunchPrerunWorkflowRequest(
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
                AutoChangeLanguage: true),
            SessionStartWorkflowRequest: new MinecraftLaunchSessionStartWorkflowRequest(
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
                    OutputRealTimeLog: true)),
            ArgumentPlanRequest: new MinecraftLaunchArgumentPlanRequest(
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
                    ["${resolution_width}"] = "1280",
                    ["${resolution_height}"] = "720"
                },
                WorldName: null,
                ServerAddress: "play.example.invalid",
                ReleaseTime: new DateTime(2024, 4, 23),
                HasOptiFine: false),
            PostLaunchShellRequest: new MinecraftLaunchPostLaunchShellRequest(
                LauncherVisibility.HideAndReopen,
                StopMusicInGame: true,
                StartMusicInGame: false),
            CompletionRequest: new MinecraftLaunchCompletionRequest(
                InstanceName: "Demo Instance",
                Outcome: MinecraftLaunchOutcome.Succeeded,
                IsScriptExport: false,
                AbortHint: null));
    }

    public static LaunchSpikePlan BuildLaunchPlan(LaunchSpikeInputs inputs, string? saveBatchPath = null)
    {
        var loginPlan = SpikeLaunchLoginFactory.BuildPlan(inputs.LoginInputs);
        var javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(inputs.JavaWorkflowRequest);
        var javaRuntimeIndexRequestUrls = MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan();
        var javaRuntimeManifestPlan = javaWorkflow.MissingJavaPrompt.DownloadTarget is null
            ? null
            : MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
                new MinecraftJavaRuntimeManifestRequestPlanRequest(
                    inputs.JavaRuntimeInputs.IndexJson,
                    inputs.JavaRuntimeInputs.PlatformKey,
                    javaWorkflow.MissingJavaPrompt.DownloadTarget,
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        var javaRuntimeDownloadWorkflowPlan = javaRuntimeManifestPlan is null
            ? null
            : MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
                new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                    inputs.JavaRuntimeInputs.ManifestJson,
                    inputs.JavaRuntimeInputs.RuntimeBaseDirectory,
                    inputs.JavaRuntimeInputs.IgnoredSha1Hashes,
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
        var javaRuntimeTransferPlan = javaRuntimeDownloadWorkflowPlan is null
            ? null
            : MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
                new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                    javaRuntimeDownloadWorkflowPlan,
                    inputs.JavaRuntimeInputs.ExistingRelativePaths));
        var resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(inputs.ResolutionRequest);
        var classpathPlan = MinecraftLaunchClasspathService.BuildPlan(inputs.ClasspathRequest);
        var nativesDirectory = MinecraftLaunchNativesDirectoryService.ResolvePath(inputs.NativesDirectoryRequest);
        var replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(
            inputs.ReplacementValueRequest with
            {
                NativesDirectory = nativesDirectory,
                ResolutionWidth = resolutionPlan.Width,
                ResolutionHeight = resolutionPlan.Height,
                Classpath = classpathPlan.JoinedClasspath
            });
        var prerunPlan = MinecraftLaunchPrerunWorkflowService.BuildPlan(inputs.PrerunWorkflowRequest);
        var sessionStartPlan = MinecraftLaunchSessionWorkflowService.BuildStartPlan(inputs.SessionStartWorkflowRequest);
        var argumentPlan = MinecraftLaunchArgumentWorkflowService.BuildPlan(inputs.ArgumentPlanRequest);
        var scriptExportPlan = string.IsNullOrWhiteSpace(saveBatchPath)
            ? null
            : MinecraftLaunchShellService.BuildScriptExportPlan(saveBatchPath);
        return new LaunchSpikePlan(
            inputs.Scenario,
            loginPlan,
            javaRuntimeIndexRequestUrls,
            javaRuntimeManifestPlan,
            javaRuntimeDownloadWorkflowPlan,
            javaRuntimeTransferPlan,
            javaWorkflow,
            MinecraftLaunchJavaWorkflowService.ResolveInitialSelection(javaWorkflow, hasSelectedJava: false),
            MinecraftLaunchJavaWorkflowService.ResolvePromptDecision(
                javaWorkflow.MissingJavaPrompt,
                MinecraftLaunchJavaPromptDecision.Download),
            MinecraftLaunchJavaWorkflowService.ResolvePostDownloadSelection(javaWorkflow, hasSelectedJava: true),
            resolutionPlan,
            classpathPlan,
            nativesDirectory,
            replacementPlan,
            argumentPlan,
            prerunPlan,
            sessionStartPlan,
            scriptExportPlan,
            MinecraftLaunchShellService.GetPostLaunchShellPlan(
                inputs.PostLaunchShellRequest),
            MinecraftLaunchShellService.GetCompletionNotification(inputs.CompletionRequest));
    }

    public static LaunchSpikePlan BuildLaunchPlan(string scenario, string? saveBatchPath = null)
    {
        return BuildLaunchPlan(CreateDefaultLaunchInputs(scenario), saveBatchPath);
    }

    public static CrashSpikeInputs CreateDefaultCrashInputs()
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

        return new CrashSpikeInputs(
            new MinecraftCrashOutputPromptRequest(
                ResultText: "Mod 加载器版本与 Mod 不兼容: DemoMod requires a newer loader.",
                IsManualAnalysis: false,
                HasDirectFile: true,
                CanOpenModLoaderSettings: true),
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
                UserProfilePath: @"C:\Users\demo"));
    }

    public static CrashSpikePlan BuildCrashPlan(CrashSpikeInputs inputs)
    {
        return new CrashSpikePlan(
            MinecraftCrashWorkflowService.BuildOutputPrompt(inputs.OutputPromptRequest),
            MinecraftCrashExportWorkflowService.CreatePlan(inputs.ExportPlanRequest));
    }

    public static CrashSpikePlan BuildCrashPlan()
    {
        return BuildCrashPlan(CreateDefaultCrashInputs());
    }

    private static MinecraftLaunchJavaWorkflowRequest BuildJavaWorkflowRequest(string scenario)
    {
        return scenario switch
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
    }

    private static LaunchLoginSpikeInputs BuildLoginInputs(string scenario)
    {
        return scenario switch
        {
            "legacy-forge" => new LaunchLoginSpikeInputs(
                LaunchLoginProviderKind.Authlib,
                Microsoft: null,
                Authlib: new AuthlibLaunchLoginSpikeInputs(
                    ForceReselectProfile: true,
                    CachedProfileId: "cached-profile-id",
                    ServerSelectedProfileId: null,
                    IsExistingProfile: false,
                    SelectedProfileIndex: null,
                    ServerBaseUrl: "https://auth.example.invalid/authserver",
                    LoginName: "demo@example.invalid",
                    Password: "demo-password",
                    AuthenticateResponseJson:
                    """
                    {
                      "accessToken": "authlib-access-token",
                      "clientToken": "authlib-client-token",
                      "selectedProfile": {
                        "id": "server-selected-profile",
                        "name": "ServerDefault"
                      },
                      "availableProfiles": [
                        {
                          "id": "cached-profile-id",
                          "name": "CachedDemo"
                        },
                        {
                          "id": "alt-profile-id",
                          "name": "AltDemo"
                        }
                      ]
                    }
                    """,
                    RefreshResponseJson:
                    """
                    {
                      "accessToken": "authlib-refreshed-access-token",
                      "clientToken": "authlib-refreshed-client-token",
                      "selectedProfile": {
                        "id": "cached-profile-id",
                        "name": "CachedDemo"
                      }
                    }
                    """,
                    MetadataResponseJson:
                    """
                    {
                      "meta": {
                        "serverName": "Demo Auth Server"
                      }
                    }
                    """)),
            _ => new LaunchLoginSpikeInputs(
                LaunchLoginProviderKind.Microsoft,
                Microsoft: new MicrosoftLaunchLoginSpikeInputs(
                    SessionReuseRequest: new MinecraftLaunchMicrosoftSessionReuseRequest(
                        IsForceRestarting: false,
                        AccessToken: "cached-access-token",
                        LastRefreshTick: 0,
                        CurrentTick: 1000 * 60 * 60),
                    OAuthRefreshToken: "cached-refresh-token",
                    OAuthRefreshResponseJson:
                    """
                    {
                      "access_token": "oauth-access-token",
                      "refresh_token": "oauth-refresh-token"
                    }
                    """,
                    XboxLiveResponseJson:
                    """
                    {
                      "Token": "xbl-token"
                    }
                    """,
                    XstsResponseJson:
                    """
                    {
                      "Token": "xsts-token",
                      "DisplayClaims": {
                        "xui": [
                          {
                            "uhs": "user-hash"
                          }
                        ]
                      }
                    }
                    """,
                    MinecraftAccessTokenResponseJson:
                    """
                    {
                      "access_token": "minecraft-access-token"
                    }
                    """,
                    OwnershipResponseJson:
                    """
                    {
                      "items": [
                        {
                          "name": "product_minecraft"
                        }
                      ]
                    }
                    """,
                    ProfileResponseJson:
                    """
                    {
                      "id": "demo-uuid",
                      "name": "DemoPlayer"
                    }
                    """,
                    IsCreatingProfile: true,
                    SelectedProfileIndex: null,
                    Profiles:
                    [
                        new MinecraftLaunchStoredProfile(
                            MinecraftLaunchStoredProfileKind.Microsoft,
                            "existing-uuid",
                            "ExistingPlayer",
                            Server: null,
                            ServerName: null,
                            AccessToken: "existing-access-token",
                            RefreshToken: "existing-refresh-token",
                            LoginName: null,
                            Password: null,
                            ClientToken: null,
                            RawJson: "{}")
                    ]),
                Authlib: null)
        };
    }

    private static JavaRuntimeSpikeInputs BuildJavaRuntimeInputs(string scenario)
    {
        var requestedComponent = scenario == "legacy-forge" ? "8" : "jre-legacy";
        var versionName = scenario == "legacy-forge" ? "8u412-b08" : "21.0.4+7";
        var componentKey = scenario == "legacy-forge" ? "jre-8u412" : "jre-legacy";
        var runtimeBaseDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
            @"C:\Minecraft\.minecraft",
            componentKey);

        return new JavaRuntimeSpikeInputs(
            PlatformKey: "windows-x64",
            RuntimeBaseDirectory: runtimeBaseDirectory,
            IndexJson:
            $$"""
            {
              "windows-x64": {
                "{{componentKey}}": [
                  {
                    "version": {
                      "name": "{{versionName}}"
                    },
                    "manifest": {
                      "url": "https://example.invalid/{{componentKey}}/manifest.json"
                    }
                  }
                ]
              }
            }
            """,
            ManifestJson:
            $$"""
            {
              "files": {
                "bin/java.exe": {
                  "downloads": {
                    "raw": {
                      "url": "https://example.invalid/{{componentKey}}/bin/java.exe",
                      "size": 1024,
                      "sha1": "keep-bin"
                    }
                  }
                },
                "conf/net.properties": {
                  "downloads": {
                    "raw": {
                      "url": "https://example.invalid/{{componentKey}}/conf/net.properties",
                      "size": 256,
                      "sha1": "keep-conf"
                    }
                  }
                },
                "legal/java.base/LICENSE": {
                  "downloads": {
                    "raw": {
                      "url": "https://example.invalid/{{componentKey}}/legal/license",
                      "size": 128,
                      "sha1": "skip-license"
                    }
                  }
                }
              }
            }
            """,
            IgnoredSha1Hashes: ["skip-license"],
            ExistingRelativePaths: ["conf/net.properties"]);
    }
}
