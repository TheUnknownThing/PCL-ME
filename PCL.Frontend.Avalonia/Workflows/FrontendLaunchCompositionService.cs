using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchCompositionService
{
    private static readonly HttpClient JavaRuntimeHttpClient = new();
    private static readonly object HostJavaProbeLock = new();
    private static IReadOnlyList<FrontendStoredJavaRuntime>? CachedHostJavaRuntimes;
    private static bool IsHostJavaProbeCached;

    public static FrontendLaunchComposition Compose(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths,
        bool ignoreJavaCompatibilityWarningOnce = false)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var localConfig = runtimePaths.OpenLocalConfigProvider();

        var launcherFolder = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty);
        var instancePath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? Path.Combine(launcherFolder, "versions")
            : Path.Combine(launcherFolder, "versions", selectedInstanceName);
        var instanceConfig = Directory.Exists(instancePath)
            ? FrontendRuntimePaths.OpenInstanceConfigProvider(instancePath)
            : null;
        var manifestSummary = ReadManifestSummary(launcherFolder, selectedInstanceName, instanceConfig);
        var indieDirectory = ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary)
            ? instancePath
            : launcherFolder;
        var selectedProfile = ReadSelectedProfile(runtimePaths);
        var javaWorkflowRequest = BuildJavaWorkflowRequest(CreateRuntimeJavaWorkflowFallback(manifestSummary), manifestSummary);
        var javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(javaWorkflowRequest);
        var javaSelection = ResolveJavaRuntime(
            sharedConfig,
            localConfig,
            instanceConfig,
            launcherFolder,
            manifestSummary,
            javaWorkflow,
            ignoreJavaCompatibilityWarningOnce);
        var selectedJavaRuntime = javaSelection.Runtime;
        var retroWrapperOptions = ResolveRetroWrapperOptions(
            launcherFolder,
            manifestSummary,
            sharedConfig,
            instanceConfig);
        var requiredArtifacts = BuildRequiredArtifacts(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime);
        var resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(BuildResolutionRequest(
            localConfig,
            CreateRuntimeResolutionFallback(),
            manifestSummary,
            selectedJavaRuntime,
            javaWorkflow));
        var classpathPlan = MinecraftLaunchClasspathService.BuildPlan(BuildClasspathRequest(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            instanceConfig,
            retroWrapperOptions));
        var nativesDirectory = MinecraftLaunchNativesDirectoryService.ResolvePath(new MinecraftLaunchNativesDirectoryRequest(
            PreferredInstanceDirectory: Path.Combine(instancePath, $"{selectedInstanceName}-natives"),
            PreferInstanceDirectory: false,
            AppDataNativesDirectory: Path.Combine(launcherFolder, "bin", "natives"),
            FinalFallbackDirectory: Path.Combine(runtimePaths.TempDirectory, "PCL", "natives")));
        var nativePathPlan = BuildNativePathPlan(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            nativesDirectory);
        var nativeSyncRequest = BuildNativeSyncRequest(
            launcherFolder,
            selectedInstanceName,
            nativePathPlan.ExtractionDirectory,
            manifestSummary,
            selectedJavaRuntime);
        var versionType = ResolveVersionType(localConfig, instanceConfig, manifestSummary);
        var replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(new MinecraftLaunchReplacementValueRequest(
            ClasspathSeparator: GetClasspathSeparator(),
            NativesDirectory: nativePathPlan.SearchPath,
            LibraryDirectory: Path.Combine(launcherFolder, "libraries"),
            LibrariesDirectory: Path.Combine(launcherFolder, "libraries"),
            LauncherName: "PCLCE",
            LauncherVersion: "frontend-avalonia",
            VersionName: string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            VersionType: versionType,
            GameDirectory: indieDirectory,
            AssetsRoot: Path.Combine(launcherFolder, "assets"),
            UserProperties: "{}",
            AuthPlayerName: selectedProfile.UserName,
            AuthUuid: ResolveProfileUuid(selectedProfile),
            AccessToken: ResolveAccessToken(selectedProfile),
            UserType: GetUserType(selectedProfile.Kind),
            ResolutionWidth: resolutionPlan.Width,
            ResolutionHeight: resolutionPlan.Height,
            GameAssetsDirectory: Path.Combine(launcherFolder, "assets", "virtual", "legacy"),
            AssetsIndexName: manifestSummary.AssetsIndexName ?? "legacy",
            Classpath: classpathPlan.JoinedClasspath));
        var prerunPlan = MinecraftLaunchPrerunWorkflowService.BuildPlan(new MinecraftLaunchPrerunWorkflowRequest(
            LauncherProfilesPath: Path.Combine(launcherFolder, "launcher_profiles.json"),
            IsMicrosoftLogin: selectedProfile.Kind == MinecraftLaunchProfileKind.Microsoft,
            ExistingLauncherProfilesJson: ReadFileOrDefault(Path.Combine(launcherFolder, "launcher_profiles.json"), "{}"),
            UserName: selectedProfile.UserName,
            ClientToken: selectedProfile.ClientToken ?? "frontend-avalonia",
            LauncherProfilesDefaultTimestamp: DateTime.Now,
            PrimaryOptionsFilePath: Path.Combine(indieDirectory, "options.txt"),
            PrimaryOptionsFileExists: File.Exists(Path.Combine(indieDirectory, "options.txt")),
            PrimaryCurrentLanguage: ReadOptionValue(Path.Combine(indieDirectory, "options.txt"), "lang"),
            YosbrOptionsFilePath: Path.Combine(indieDirectory, "config", "yosbr", "options.txt"),
            YosbrOptionsFileExists: File.Exists(Path.Combine(indieDirectory, "config", "yosbr", "options.txt")),
            HasExistingSaves: Directory.Exists(Path.Combine(indieDirectory, "saves")) &&
                              Directory.EnumerateFileSystemEntries(Path.Combine(indieDirectory, "saves")).Any(),
            ReleaseTime: manifestSummary.ReleaseTime ?? javaWorkflowRequest.ReleaseTime,
            LaunchWindowType: ReadValue(localConfig, "LaunchArgumentWindowType", (int)GameWindowSizeMode.Default),
            AutoChangeLanguage: false));
        var argumentPlan = BuildArgumentPlan(
            runtimePaths,
            launcherFolder,
            selectedInstanceName,
            indieDirectory,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            sharedConfig,
            instanceConfig,
            retroWrapperOptions,
            replacementPlan);
        var sessionStartPlan = BuildSessionStartPlan(
            launcherFolder,
            selectedInstanceName,
            instancePath,
            indieDirectory,
            nativesDirectory,
            nativePathPlan.SearchPath,
            nativePathPlan.ExtractionDirectory,
            nativePathPlan.AliasDirectory,
            nativeSyncRequest?.NativeArchives.Count ?? 0,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            sharedConfig,
            instanceConfig,
            argumentPlan);
        var postLaunchShell = MinecraftLaunchShellService.GetPostLaunchShellPlan(
            new MinecraftLaunchPostLaunchShellRequest(
                ReadValue(sharedConfig, "LaunchArgumentVisible", LauncherVisibility.DoNothing),
                ReadValue(localConfig, "UiMusicStop", false),
                ReadValue(localConfig, "UiMusicStart", false)));
        var launchCount = LauncherFrontendRuntimeStateService.ReadProtectedInt(
            runtimePaths.SharedConfigDirectory,
            runtimePaths.SharedConfigPath,
            "SystemLaunchCount");
        var supportPrompt = MinecraftLaunchShellService.GetSupportPrompt(launchCount);
        var loginRequirement = ResolveLoginRequirement(instanceConfig);
        var requiredAuthServer = loginRequirement is MinecraftLaunchLoginRequirement.Auth or MinecraftLaunchLoginRequirement.MicrosoftOrAuth
            ? instanceConfig is null
                ? null
                : NullIfWhiteSpace(ReadValue(instanceConfig, "VersionServerAuthServer", string.Empty))
            : null;
        var precheckRequest = new MinecraftLaunchPrecheckRequest(
            InstanceName: string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            InstancePathIndie: indieDirectory,
            InstancePath: launcherFolder,
            IsInstanceSelected: !string.IsNullOrWhiteSpace(selectedInstanceName),
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: true,
            IsNonAsciiPathWarningDisabled: ReadValue(sharedConfig, "HintDisableGamePathCheckTip", false),
            IsInstancePathAscii: IsAscii(indieDirectory),
            ProfileValidationMessage: string.Empty,
            SelectedProfileKind: selectedProfile.Kind,
            HasLabyMod: manifestSummary.HasLabyMod,
            LoginRequirement: loginRequirement,
            RequiredAuthServer: requiredAuthServer,
            SelectedAuthServer: selectedProfile.AuthServer,
            HasMicrosoftProfile: selectedProfile.HasMicrosoftProfile,
            IsRestrictedFeatureAllowed: true);
        var precheckResult = MinecraftLaunchPrecheckService.Evaluate(precheckRequest);
        var manifestPlan = BuildJavaRuntimeManifestPlan(
            javaWorkflow,
            manifestSummary,
            selectedJavaRuntime);
        var transferPlan = BuildJavaRuntimeTransferPlan(launcherFolder, manifestPlan);

        return new FrontendLaunchComposition(
            options.Scenario,
            string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            instancePath,
            requiredArtifacts,
            selectedProfile,
            selectedJavaRuntime,
            javaSelection.WarningMessage,
            javaSelection.CompatibilityPrompt,
            launchCount,
            precheckRequest,
            precheckResult,
            supportPrompt,
            javaWorkflow,
            manifestPlan,
            transferPlan,
            resolutionPlan,
            classpathPlan,
            nativesDirectory,
            nativePathPlan.AliasDirectory,
            nativeSyncRequest,
            replacementPlan,
            prerunPlan,
            argumentPlan,
            sessionStartPlan,
            postLaunchShell,
            MinecraftLaunchShellService.GetCompletionNotification(new MinecraftLaunchCompletionRequest(
                InstanceName: string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
                Outcome: MinecraftLaunchOutcome.Succeeded,
                IsScriptExport: false,
                AbortHint: null)));
    }

    private static MinecraftLaunchArgumentPlan BuildArgumentPlan(
        FrontendRuntimePaths runtimePaths,
        string launcherFolder,
        string selectedInstanceName,
        string indieDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        FrontendRetroWrapperOptions retroWrapperOptions,
        MinecraftLaunchReplacementValuePlan replacementPlan)
    {
        var javaMajorVersion = selectedJavaRuntime?.MajorVersion
                               ?? manifestSummary.JsonRequiredMajorVersion
                               ?? manifestSummary.MojangRecommendedMajorVersion
                               ?? 8;
        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var effectiveJvmArguments = string.IsNullOrWhiteSpace(instanceConfig is null
                ? null
                : ReadValue(instanceConfig, "VersionAdvanceJvm", string.Empty))
            ? ReadValue(localConfig, "LaunchAdvanceJvm", string.Empty)
            : ReadValue(instanceConfig!, "VersionAdvanceJvm", string.Empty);
        var arguments = BuildJvmArguments(
            runtimePaths,
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            localConfig,
            instanceConfig,
            sharedConfig,
            retroWrapperOptions,
            effectiveJvmArguments,
            replacementPlan.Values,
            indieDirectory,
            javaMajorVersion);

        var legacyMinecraftArguments = ReadManifestProperty(launcherFolder, selectedInstanceName, "minecraftArguments");
        if (!string.IsNullOrWhiteSpace(legacyMinecraftArguments))
        {
            arguments += " " + MinecraftLaunchGameArgumentService.BuildLegacyPlan(
                new MinecraftLaunchLegacyGameArgumentRequest(
                    legacyMinecraftArguments,
                    UseRetroWrapper: retroWrapperOptions.UseRetroWrapper,
                    manifestSummary.HasForgeLike || manifestSummary.HasLiteLoader,
                    manifestSummary.HasOptiFine)).Arguments;
        }

        var modernGameSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "game");
        if (modernGameSections.Count > 0)
        {
            var launchArgumentFeatures = BuildLaunchArgumentFeatures(localConfig);
            arguments += " " + MinecraftLaunchGameArgumentService.BuildModernPlan(
                new MinecraftLaunchModernGameArgumentRequest(
                    MinecraftLaunchJsonArgumentService.ExtractValues(
                        new MinecraftLaunchJsonArgumentRequest(
                            modernGameSections,
                            Environment.OSVersion.Version.ToString(),
                            runtimeArchitecture == MachineType.I386,
                            launchArgumentFeatures)),
                    manifestSummary.HasForgeLike || manifestSummary.HasLiteLoader,
                    manifestSummary.HasOptiFine)).Arguments;
        }

        var customGameArguments = string.IsNullOrWhiteSpace(instanceConfig is null
                ? null
                : ReadValue(instanceConfig, "VersionAdvanceGame", string.Empty))
            ? ReadValue(localConfig, "LaunchAdvanceGame", string.Empty)
            : ReadValue(instanceConfig!, "VersionAdvanceGame", string.Empty);
        var autoJoinServer = instanceConfig is null
            ? null
            : NullIfWhiteSpace(ReadValue(instanceConfig, "VersionServerEnter", string.Empty));

        return MinecraftLaunchArgumentWorkflowService.BuildPlan(
            new MinecraftLaunchArgumentPlanRequest(
                arguments,
                javaMajorVersion,
                ReadValue(localConfig, "LaunchArgumentWindowType", (int)GameWindowSizeMode.Default) == 0,
                ExtraArguments: null,
                CustomGameArguments: customGameArguments,
                replacementPlan.Values,
                WorldName: null,
                ServerAddress: autoJoinServer,
                ReleaseTime: manifestSummary.ReleaseTime ?? DateTime.Now,
                HasOptiFine: manifestSummary.HasOptiFine));
    }

    private static MinecraftLaunchSessionStartWorkflowPlan BuildSessionStartPlan(
        string launcherFolder,
        string selectedInstanceName,
        string instanceDirectory,
        string indieDirectory,
        string nativesDirectory,
        string nativeSearchPath,
        string nativeExtractionDirectory,
        string? nativePathAliasDirectory,
        int nativeArchiveCount,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        MinecraftLaunchArgumentPlan argumentPlan)
    {
        var javaExecutablePath = selectedJavaRuntime?.ExecutablePath;
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
        {
            return BuildPendingJavaSessionStartPlan(
                launcherFolder,
                selectedInstanceName,
                instanceDirectory,
                indieDirectory,
                nativesDirectory,
                nativeSearchPath,
                nativeExtractionDirectory,
                nativePathAliasDirectory,
                nativeArchiveCount,
                manifestSummary,
                selectedProfile,
                selectedJavaRuntime,
                instanceConfig,
                localConfig);
        }

        var javaFolder = ResolveJavaFolderPath(javaExecutablePath, launcherFolder);
        var javawExecutablePath = OperatingSystem.IsWindows()
            ? Path.Combine(javaFolder, "javaw.exe")
            : javaExecutablePath;
        var instanceName = string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName;
        var watcherWorkflowRequest = BuildWatcherWorkflowRequest(
            launcherFolder,
            selectedInstanceName,
            instanceDirectory,
            indieDirectory,
            nativesDirectory,
            nativeSearchPath,
            nativeExtractionDirectory,
            nativePathAliasDirectory,
            nativeArchiveCount,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            instanceConfig,
            localConfig);
        var globalCommand = ReplaceLaunchTokens(
            ReadValue(localConfig, "LaunchAdvanceRun", string.Empty),
            selectedProfile,
            instanceName,
            launcherFolder,
            indieDirectory,
            javaFolder,
            replaceTime: true);
        var instanceCommand = instanceConfig is null
            ? string.Empty
            : ReplaceLaunchTokens(
                ReadValue(instanceConfig, "VersionAdvanceRun", string.Empty),
                selectedProfile,
                instanceName,
                launcherFolder,
                indieDirectory,
                javaFolder,
                replaceTime: true);
        var launchEnvironmentOverrides = BuildLaunchEnvironmentOverrides(localConfig, instanceConfig);

        return MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            new MinecraftLaunchSessionStartWorkflowRequest(
                new MinecraftLaunchCustomCommandWorkflowRequest(
                    new MinecraftLaunchCustomCommandRequest(
                        selectedJavaRuntime?.MajorVersion ?? 8,
                        instanceName,
                        indieDirectory,
                        javaExecutablePath,
                        argumentPlan.FinalArguments,
                        launchEnvironmentOverrides,
                        NullIfWhiteSpace(globalCommand),
                        ReadValue(localConfig, "LaunchAdvanceRunWait", true),
                        NullIfWhiteSpace(instanceCommand),
                        instanceConfig is null || ReadValue(instanceConfig, "VersionAdvanceRunWait", true)),
                    ShellWorkingDirectory: launcherFolder),
                new MinecraftLaunchProcessRequest(
                    ReadValue(sharedConfig, "LaunchAdvanceNoJavaw", false),
                    javaExecutablePath,
                    File.Exists(javawExecutablePath) ? javawExecutablePath : null,
                    javaFolder,
                    Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                    launcherFolder,
                    indieDirectory,
                    argumentPlan.FinalArguments,
                    launchEnvironmentOverrides,
                    ReadValue(sharedConfig, "LaunchArgumentPriority", 1)),
                watcherWorkflowRequest));
    }

    private static MinecraftLaunchSessionStartWorkflowPlan BuildPendingJavaSessionStartPlan(
        string launcherFolder,
        string selectedInstanceName,
        string instanceDirectory,
        string indieDirectory,
        string nativesDirectory,
        string nativeSearchPath,
        string nativeExtractionDirectory,
        string? nativePathAliasDirectory,
        int nativeArchiveCount,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider? instanceConfig,
        YamlFileProvider localConfig)
    {
        var watcherWorkflowRequest = BuildWatcherWorkflowRequest(
            launcherFolder,
            selectedInstanceName,
            instanceDirectory,
            indieDirectory,
            nativesDirectory,
            nativeSearchPath,
            nativeExtractionDirectory,
            nativePathAliasDirectory,
            nativeArchiveCount,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            instanceConfig,
            localConfig);

        return new MinecraftLaunchSessionStartWorkflowPlan(
            new MinecraftLaunchCustomCommandPlan(
                BatchScriptContent: string.Empty,
                UseUtf8Encoding: true,
                CommandExecutions: []),
            [],
            new MinecraftLaunchProcessShellPlan(
                FileName: OperatingSystem.IsWindows() ? "cmd.exe" : "/usr/bin/env",
                Arguments: OperatingSystem.IsWindows() ? "/c exit /b 1" : "false",
                WorkingDirectory: indieDirectory,
                CreateNoWindow: true,
                UseShellExecute: false,
                RedirectStandardOutput: true,
                RedirectStandardError: true,
                PathEnvironmentValue: string.Empty,
                AppDataEnvironmentValue: string.Empty,
                EnvironmentVariables: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PriorityKind: MinecraftLaunchProcessPriorityKind.Normal,
                StartedLogMessage: "缺少 Java 运行时，尚未生成可执行的启动命令。",
                AbortKillLogMessage: "缺少 Java 运行时，无需终止游戏进程。"),
            MinecraftLaunchWatcherWorkflowService.BuildPlan(watcherWorkflowRequest));
    }

    private static IReadOnlyDictionary<string, bool> BuildLaunchArgumentFeatures(YamlFileProvider localConfig)
    {
        var windowMode = ReadValue(localConfig, "LaunchArgumentWindowType", (int)GameWindowSizeMode.Default);
        var hasCustomResolution = windowMode is (int)GameWindowSizeMode.Launcher or (int)GameWindowSizeMode.Custom;

        return new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["has_custom_resolution"] = hasCustomResolution,
            ["is_demo_user"] = false,
            ["has_quick_plays_support"] = false,
            ["is_quick_play_singleplayer"] = false,
            ["is_quick_play_multiplayer"] = false,
            ["is_quick_play_realms"] = false
        };
    }

    private static IReadOnlyDictionary<string, string> BuildLaunchEnvironmentOverrides(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var environmentVariables = ParseLaunchEnvironmentVariables(
            ReadValue(localConfig, "LaunchAdvanceEnvironmentVariables", string.Empty));
        if (instanceConfig is not null)
        {
            MergeEnvironmentVariables(
                environmentVariables,
                ParseLaunchEnvironmentVariables(ReadValue(instanceConfig, "VersionAdvanceEnvironmentVariables", string.Empty)));
        }

        if (ShouldForceX11OnWayland(localConfig, instanceConfig))
        {
            environmentVariables["XDG_SESSION_TYPE"] = "x11";
            environmentVariables["WAYLAND_DISPLAY"] = string.Empty;
        }

        return environmentVariables;
    }

    private static Dictionary<string, string> ParseLaunchEnvironmentVariables(string rawValue)
    {
        var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return environmentVariables;
        }

        foreach (var rawLine in rawValue.Replace("\r", string.Empty).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line[7..].TrimStart();
            }

            var separatorIndex = line.IndexOf('=');
            var key = separatorIndex >= 0
                ? line[..separatorIndex].Trim()
                : line;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0
                ? line[(separatorIndex + 1)..]
                : string.Empty;
            environmentVariables[key] = value;
        }

        return environmentVariables;
    }

    private static void MergeEnvironmentVariables(
        IDictionary<string, string> target,
        IReadOnlyDictionary<string, string> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            target[key] = value;
        }
    }

    private static bool ShouldForceX11OnWayland(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var isEnabled = ResolveForceX11OnWayland(localConfig, instanceConfig);
        if (!isEnabled)
        {
            return false;
        }

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        var display = Environment.GetEnvironmentVariable("DISPLAY");
        return string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(display);
    }

    private static bool ResolveForceX11OnWayland(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var globalEnabled = ReadValue(localConfig, "LaunchAdvanceForceX11OnWayland", false);
        if (instanceConfig is null)
        {
            return globalEnabled;
        }

        var instanceMode = Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceForceX11OnWayland", 0), 0, 2);
        return instanceMode switch
        {
            1 => true,
            2 => false,
            _ => globalEnabled
        };
    }

    private static MinecraftLaunchResolutionRequest BuildResolutionRequest(
        YamlFileProvider localConfig,
        MinecraftLaunchResolutionRequest fallback,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return fallback with
        {
            WindowMode = ReadValue(localConfig, "LaunchArgumentWindowType", fallback.WindowMode),
            CustomWidth = ReadValue(localConfig, "LaunchArgumentWindowWidth", fallback.CustomWidth),
            CustomHeight = ReadValue(localConfig, "LaunchArgumentWindowHeight", fallback.CustomHeight),
            GameVersionDrop = ResolveGameVersionDrop(manifestSummary.VanillaVersion),
            JavaMajorVersion = selectedJavaRuntime?.MajorVersion ?? javaWorkflow.RecommendedMajorVersion,
            HasOptiFine = manifestSummary.HasOptiFine,
            HasForge = manifestSummary.HasForgeLike
        };
    }

    private static MinecraftLaunchClasspathRequest BuildClasspathRequest(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider? instanceConfig,
        FrontendRetroWrapperOptions retroWrapperOptions)
    {
        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var instanceJarPath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? null
            : Path.Combine(launcherFolder, "versions", selectedInstanceName, $"{selectedInstanceName}.jar");
        var customHeadEntries = BuildClasspathHeadEntries(instanceConfig, instanceJarPath);

        return new MinecraftLaunchClasspathRequest(
            Libraries: ReadClasspathLibraries(launcherFolder, selectedInstanceName, runtimeArchitecture),
            CustomHeadEntries: customHeadEntries,
            RetroWrapperPath: retroWrapperOptions.RetroWrapperPath,
            ClasspathSeparator: GetClasspathSeparator());
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> ReadClasspathLibraries(
        string launcherFolder,
        string selectedInstanceName,
        MachineType runtimeArchitecture)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return [];
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var libraries = new List<MinecraftLaunchClasspathLibrary>();
        CollectClasspathLibrariesRecursive(
            launcherFolder,
            selectedInstanceName,
            runtimeArchitecture,
            visited,
            libraries);
        return DeduplicateClasspathLibraries(libraries);
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> DeduplicateClasspathLibraries(
        IReadOnlyList<MinecraftLaunchClasspathLibrary> libraries)
    {
        if (libraries.Count <= 1)
        {
            return libraries;
        }

        var deduplicated = new List<MinecraftLaunchClasspathLibrary>(libraries.Count);
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            var key = GetClasspathLibraryIdentityKey(library);
            if (indexByKey.TryGetValue(key, out var existingIndex))
            {
                deduplicated[existingIndex] = library;
                continue;
            }

            indexByKey[key] = deduplicated.Count;
            deduplicated.Add(library);
        }

        return deduplicated;
    }

    private static string GetClasspathLibraryIdentityKey(MinecraftLaunchClasspathLibrary library)
    {
        if (TryParseLibraryCoordinate(library.Name, out var coordinate))
        {
            return $"{coordinate.GroupId}:{coordinate.ArtifactId}:{coordinate.Classifier ?? string.Empty}:{library.IsNatives}";
        }

        if (!string.IsNullOrWhiteSpace(library.Name))
        {
            var parts = library.Name.Split(':', StringSplitOptions.None);
            if (parts.Length >= 3)
            {
                var classifier = parts.Length >= 4 ? parts[3] : string.Empty;
                return $"{parts[0]}:{parts[1]}:{classifier}:{library.IsNatives}";
            }

            return $"{library.Name}:{library.IsNatives}";
        }

        return $"{library.Path}:{library.IsNatives}";
    }

    private static bool TryParseLibraryCoordinate(string? name, out LibraryCoordinate coordinate)
    {
        coordinate = new LibraryCoordinate(string.Empty, string.Empty, string.Empty, null);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var parts = name.Split(':', StringSplitOptions.None);
        if (parts.Length is < 3 or > 4)
        {
            return false;
        }

        coordinate = new LibraryCoordinate(
            parts[0],
            parts[1],
            parts[2],
            parts.Length >= 4 ? parts[3] : null);
        return true;
    }

    private static void CollectClasspathLibrariesRecursive(
        string launcherFolder,
        string versionName,
        MachineType runtimeArchitecture,
        ISet<string> visited,
        IList<MinecraftLaunchClasspathLibrary> libraries)
    {
        if (!visited.Add(versionName))
        {
            return;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        if (!string.IsNullOrWhiteSpace(parentVersion))
        {
            CollectClasspathLibrariesRecursive(
                launcherFolder,
                parentVersion,
                runtimeArchitecture,
                visited,
                libraries);
        }

        if (!root.TryGetProperty("libraries", out var manifestLibraries) ||
            manifestLibraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in manifestLibraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture))
            {
                continue;
            }

            if (!TryResolveEffectiveArtifactDownload(library, launcherFolder, runtimeArchitecture, out var artifactDownload))
            {
                continue;
            }

            libraries.Add(new MinecraftLaunchClasspathLibrary(
                GetString(library, "name"),
                artifactDownload.TargetPath,
                IsNatives: library.TryGetProperty("natives", out _)));
        }
    }

    private static FrontendNativePathPlan BuildNativePathPlan(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        string baseNativesDirectory)
    {
        var extractionDirectory = ResolveNativeExtractionDirectory(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            baseNativesDirectory);
        var aliasDirectory = ShouldUseLegacyAsciiNativePathWorkaround(manifestSummary, baseNativesDirectory)
            ? BuildNativePathAliasDirectory(baseNativesDirectory)
            : null;
        var searchPath = string.IsNullOrWhiteSpace(aliasDirectory)
            ? baseNativesDirectory
            : aliasDirectory + Path.PathSeparator + baseNativesDirectory;

        return new FrontendNativePathPlan(
            baseNativesDirectory,
            extractionDirectory,
            searchPath,
            aliasDirectory);
    }

    private static string ResolveNativeExtractionDirectory(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        string baseNativesDirectory)
    {
        var modernJvmSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "jvm");
        if (modernJvmSections.Count == 0)
        {
            return baseNativesDirectory;
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var jvmArguments = MinecraftLaunchJsonArgumentService.ExtractValues(
            new MinecraftLaunchJsonArgumentRequest(
                modernJvmSections,
                Environment.OSVersion.Version.ToString(),
                runtimeArchitecture == MachineType.I386));
        const string prefix = "-Djava.library.path=${natives_directory}/";
        foreach (var argument in jvmArguments)
        {
            if (!argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = argument[prefix.Length..];
            var resolvedPath = TryResolveNativeExtractionSubdirectory(baseNativesDirectory, relativePath);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return baseNativesDirectory;
    }

    private static string? TryResolveNativeExtractionSubdirectory(string baseNativesDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return null;
        }

        try
        {
            var basePath = Path.GetFullPath(baseNativesDirectory);
            var resolvedPath = Path.GetFullPath(Path.Combine(basePath, normalizedRelativePath));
            var basePrefix = basePath.EndsWith(Path.DirectorySeparatorChar)
                ? basePath
                : basePath + Path.DirectorySeparatorChar;
            return resolvedPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)
                ? resolvedPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldUseLegacyAsciiNativePathWorkaround(
        FrontendVersionManifestSummary manifestSummary,
        string nativesDirectory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (IsAscii(nativesDirectory))
        {
            return false;
        }

        return manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion < new Version(1, 19);
    }

    private static string BuildNativePathAliasDirectory(string nativesDirectory)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(nativesDirectory)))
            .ToLowerInvariant();
        return Path.Combine("/tmp", $"pclce-natives-{hash[..12]}");
    }

    private static IReadOnlyList<FrontendLaunchArtifactRequirement> BuildRequiredArtifacts(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return [];
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requirements = new Dictionary<string, FrontendLaunchArtifactRequirement>(StringComparer.OrdinalIgnoreCase);
        CollectRequiredArtifactsRecursive(
            launcherFolder,
            selectedInstanceName,
            runtimeArchitecture,
            visited,
            requirements);
        return requirements.Values.ToArray();
    }

    private static MinecraftLaunchNativesSyncRequest? BuildNativeSyncRequest(
        string launcherFolder,
        string selectedInstanceName,
        string nativesDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return null;
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archives = new Dictionary<string, MinecraftLaunchNativeArchive>(StringComparer.OrdinalIgnoreCase);
        CollectNativeArchivesRecursive(
            launcherFolder,
            selectedInstanceName,
            runtimeArchitecture,
            visited,
            archives);
        return archives.Count == 0
            ? null
            : new MinecraftLaunchNativesSyncRequest(nativesDirectory, archives.Values.ToArray(), LogSkippedFiles: false);
    }

    private static void CollectRequiredArtifactsRecursive(
        string launcherFolder,
        string versionName,
        MachineType runtimeArchitecture,
        ISet<string> visited,
        IDictionary<string, FrontendLaunchArtifactRequirement> requirements)
    {
        if (!visited.Add(versionName))
        {
            return;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        if (!string.IsNullOrWhiteSpace(parentVersion))
        {
            CollectRequiredArtifactsRecursive(
                launcherFolder,
                parentVersion,
                runtimeArchitecture,
                visited,
                requirements);
        }

        var clientDownloadUrl = GetNestedString(root, "downloads", "client", "url");
        if (!string.IsNullOrWhiteSpace(clientDownloadUrl))
        {
            var clientJarPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.jar");
            requirements[clientJarPath] = new FrontendLaunchArtifactRequirement(
                clientJarPath,
                clientDownloadUrl,
                GetNestedString(root, "downloads", "client", "sha1"));
        }

        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture))
            {
                continue;
            }

            if (TryResolveEffectiveArtifactDownload(library, launcherFolder, runtimeArchitecture, out var artifactDownload) &&
                !string.IsNullOrWhiteSpace(artifactDownload.DownloadUrl))
            {
                requirements[artifactDownload.TargetPath] = new FrontendLaunchArtifactRequirement(
                    artifactDownload.TargetPath,
                    artifactDownload.DownloadUrl!,
                    artifactDownload.Sha1);
            }

            if (TryResolveNativeArchiveDownload(library, launcherFolder, runtimeArchitecture, out var nativeArchive) &&
                !string.IsNullOrWhiteSpace(nativeArchive.DownloadUrl))
            {
                requirements[nativeArchive.TargetPath] = new FrontendLaunchArtifactRequirement(
                    nativeArchive.TargetPath,
                    nativeArchive.DownloadUrl!,
                    nativeArchive.Sha1);
            }
        }
    }

    private static void CollectNativeArchivesRecursive(
        string launcherFolder,
        string versionName,
        MachineType runtimeArchitecture,
        ISet<string> visited,
        IDictionary<string, MinecraftLaunchNativeArchive> archives)
    {
        if (!visited.Add(versionName))
        {
            return;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        if (!string.IsNullOrWhiteSpace(parentVersion))
        {
            CollectNativeArchivesRecursive(
                launcherFolder,
                parentVersion,
                runtimeArchitecture,
                visited,
                archives);
        }

        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture) ||
                !TryResolveNativeArchiveDownload(library, launcherFolder, runtimeArchitecture, out var nativeArchive))
            {
                continue;
            }

            archives[nativeArchive.TargetPath] = new MinecraftLaunchNativeArchive(
                nativeArchive.TargetPath,
                nativeArchive.ExtractExcludes);
        }
    }

    private static MinecraftLaunchJavaWorkflowRequest BuildJavaWorkflowRequest(
        MinecraftLaunchJavaWorkflowRequest fallback,
        FrontendVersionManifestSummary manifestSummary)
    {
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: manifestSummary.IsVersionInfoValid || fallback.IsVersionInfoValid,
            ReleaseTime: manifestSummary.ReleaseTime ?? fallback.ReleaseTime,
            VanillaVersion: manifestSummary.VanillaVersion ?? fallback.VanillaVersion,
            HasOptiFine: manifestSummary.HasOptiFine,
            HasForge: manifestSummary.HasForgeLike,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabricLike,
            HasLiteLoader: manifestSummary.HasLiteLoader,
            HasLabyMod: manifestSummary.HasLabyMod,
            JsonRequiredMajorVersion: manifestSummary.JsonRequiredMajorVersion ?? fallback.JsonRequiredMajorVersion,
            MojangRecommendedMajorVersion: manifestSummary.MojangRecommendedMajorVersion ?? fallback.MojangRecommendedMajorVersion,
            MojangRecommendedComponent: manifestSummary.MojangRecommendedComponent ?? fallback.MojangRecommendedComponent);
    }

    private static MinecraftLaunchJavaWorkflowRequest CreateRuntimeJavaWorkflowFallback(FrontendVersionManifestSummary manifestSummary)
    {
        var recommendedMajorVersion = manifestSummary.MojangRecommendedMajorVersion
                                     ?? manifestSummary.JsonRequiredMajorVersion
                                     ?? 8;
        var inferredReleaseTime = InferJavaRequirementReleaseTime(manifestSummary.VanillaVersion);
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: manifestSummary.IsVersionInfoValid,
            ReleaseTime: manifestSummary.ReleaseTime ?? inferredReleaseTime ?? DateTime.Now,
            VanillaVersion: manifestSummary.VanillaVersion ?? new Version(1, 20, 1),
            HasOptiFine: manifestSummary.HasOptiFine,
            HasForge: manifestSummary.HasForgeLike,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabricLike,
            HasLiteLoader: manifestSummary.HasLiteLoader,
            HasLabyMod: manifestSummary.HasLabyMod,
            JsonRequiredMajorVersion: manifestSummary.JsonRequiredMajorVersion ?? recommendedMajorVersion,
            MojangRecommendedMajorVersion: recommendedMajorVersion,
            MojangRecommendedComponent: manifestSummary.MojangRecommendedComponent ?? "jre-legacy");
    }

    private static MinecraftLaunchResolutionRequest CreateRuntimeResolutionFallback()
    {
        return new MinecraftLaunchResolutionRequest(
            WindowMode: (int)GameWindowSizeMode.Default,
            LauncherWindowWidth: null,
            LauncherWindowHeight: null,
            LauncherTitleBarHeight: 0,
            CustomWidth: 854,
            CustomHeight: 480,
            GameVersionDrop: 0,
            JavaMajorVersion: 8,
            JavaRevision: 0,
            HasOptiFine: false,
            HasForge: false,
            DpiScale: 1);
    }

    private static FrontendLaunchProfileSummary ReadSelectedProfile(FrontendRuntimePaths runtimePaths)
    {
        try
        {
            var profileDocument = FrontendProfileStorageService.Load(runtimePaths).Document;
            var document = profileDocument;
            var selectedProfile = document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
                ? document.Profiles[document.LastUsedProfile]
                : document.Profiles.FirstOrDefault();
            if (selectedProfile is null)
            {
                return BuildFallbackProfile(runtimePaths);
            }

            var kind = selectedProfile.Kind switch
            {
                MinecraftLaunchStoredProfileKind.Microsoft => MinecraftLaunchProfileKind.Microsoft,
                MinecraftLaunchStoredProfileKind.Authlib => MinecraftLaunchProfileKind.Auth,
                _ => MinecraftLaunchProfileKind.Legacy
            };

            return new FrontendLaunchProfileSummary(
                kind,
                string.IsNullOrWhiteSpace(selectedProfile.Username) ? "未选择档案" : selectedProfile.Username,
                selectedProfile.Uuid,
                selectedProfile.AccessToken,
                selectedProfile.ClientToken,
                selectedProfile.Server,
                selectedProfile.ServerName,
                selectedProfile.SkinHeadId,
                selectedProfile.RawJson,
                document.Profiles.Any(profile => profile.Kind == MinecraftLaunchStoredProfileKind.Microsoft));
        }
        catch
        {
            return BuildFallbackProfile(runtimePaths);
        }
    }

    private static FrontendLaunchProfileSummary BuildFallbackProfile(FrontendRuntimePaths runtimePaths)
    {
        var legacyName = LauncherFrontendRuntimeStateService.TryReadProtectedString(
            runtimePaths.SharedConfigDirectory,
            runtimePaths.SharedConfigPath,
            "LoginLegacyName");
        return new FrontendLaunchProfileSummary(
            string.IsNullOrWhiteSpace(legacyName) ? MinecraftLaunchProfileKind.None : MinecraftLaunchProfileKind.Legacy,
            string.IsNullOrWhiteSpace(legacyName) ? "未选择档案" : legacyName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            HasMicrosoftProfile: false);
    }

    private static FrontendJavaSelectionResult ResolveJavaRuntime(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        string launcherFolder,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        bool ignoreJavaCompatibilityWarningOnce)
    {
        var selectedJavaPath = ResolveConfiguredJavaSelection(sharedConfig, instanceConfig, launcherFolder);
        var ignoreJavaCompatibilityWarning = ignoreJavaCompatibilityWarningOnce ||
                                            instanceConfig is not null &&
                                            ReadValue(instanceConfig, "VersionAdvanceJava", false);
        var javaEntries = FrontendJavaInventoryService.ParseAvailableRuntimes(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));
        MinecraftLaunchPrompt? compatibilityPrompt = null;

        if (!string.IsNullOrWhiteSpace(selectedJavaPath))
        {
            var selectedRuntime = ResolveConfiguredRuntime(javaEntries, selectedJavaPath, javaWorkflow);
            if (selectedRuntime is not null)
            {
                var isCompatible = IsCompatibleWithWorkflow(selectedRuntime, manifestSummary, javaWorkflow);
                if (ShouldUseConfiguredRuntime(selectedRuntime, manifestSummary, javaWorkflow, ignoreJavaCompatibilityWarning))
                {
                    return new FrontendJavaSelectionResult(
                        ToRuntimeSummary(selectedRuntime),
                        ignoreJavaCompatibilityWarning && !isCompatible
                            ? BuildIgnoredJavaCompatibilityWarning(selectedRuntime, javaWorkflow)
                            : null,
                        CompatibilityPrompt: null);
                }

                compatibilityPrompt = BuildJavaCompatibilityPrompt(selectedRuntime, javaWorkflow);
            }
        }

        var autoEntry = SelectCompatibleRuntime(javaEntries, manifestSummary, javaWorkflow);
        if (autoEntry is not null)
        {
            return new FrontendJavaSelectionResult(ToRuntimeSummary(autoEntry), null, compatibilityPrompt);
        }

        var bundledJava = OperatingSystem.IsWindows()
            ? Path.Combine(launcherFolder, "runtime", "java", "bin", "java.exe")
            : Path.Combine(launcherFolder, "runtime", "java", "bin", "java");
        if (TryResolveCompatibleRuntime(
                bundledJava,
                manifestSummary,
                javaWorkflow,
                fallbackDisplayName: $"Java {javaWorkflow.RecommendedMajorVersion}") is { } bundledRuntime)
        {
            return new FrontendJavaSelectionResult(bundledRuntime, null, compatibilityPrompt);
        }

        return new FrontendJavaSelectionResult(ProbeHostJavaRuntime(manifestSummary, javaWorkflow), null, compatibilityPrompt);
    }

    private static FrontendVersionManifestSummary ReadManifestSummary(
        string launcherFolder,
        string selectedInstanceName,
        YamlFileProvider? instanceConfig)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var profile = FrontendVersionManifestInspector.ReadProfile(launcherFolder, selectedInstanceName);
        var storedVersionName = instanceConfig is null
            ? null
            : NullIfWhiteSpace(ReadValue(instanceConfig, "VersionVanillaName", string.Empty));
        var fallbackVersion = TryParseVanillaVersion(storedVersionName)
                              ?? TryParseVanillaVersion(selectedInstanceName);
        var fallbackReleaseTime = instanceConfig is null
            ? null
            : TryParseReleaseTime(ReadValue(instanceConfig, "ReleaseTime", string.Empty));
        var effectiveVersion = profile.ParsedVanillaVersion ?? fallbackVersion;
        var effectiveReleaseTime = profile.ReleaseTime
                                   ?? fallbackReleaseTime
                                   ?? InferJavaRequirementReleaseTime(effectiveVersion);
        return new FrontendVersionManifestSummary(
            IsVersionInfoValid: profile.IsManifestValid || effectiveVersion is not null || effectiveReleaseTime is not null,
            ReleaseTime: effectiveReleaseTime,
            VanillaVersion: effectiveVersion,
            VersionType: profile.VersionType,
            AssetsIndexName: profile.AssetsIndexName,
            Libraries: ReadManifestLibraries(launcherFolder, selectedInstanceName),
            HasOptiFine: profile.HasOptiFine,
            HasForge: profile.HasForge,
            ForgeVersion: profile.ForgeVersion,
            NeoForgeVersion: profile.NeoForgeVersion,
            HasCleanroom: profile.HasCleanroom,
            HasFabric: profile.HasFabric,
            LegacyFabricVersion: profile.LegacyFabricVersion,
            QuiltVersion: profile.QuiltVersion,
            HasLiteLoader: profile.HasLiteLoader,
            HasLabyMod: profile.HasLabyMod,
            JsonRequiredMajorVersion: profile.JsonRequiredMajorVersion,
            MojangRecommendedMajorVersion: profile.MojangRecommendedMajorVersion,
            MojangRecommendedComponent: profile.MojangRecommendedComponent);
    }

    private static bool ResolveIsolationEnabled(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (instanceConfig is not null && instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return ReadValue(instanceConfig, "VersionArgumentIndieV2", false);
        }

        var globalMode = ReadValue(localConfig, "LaunchArgumentIndieV2", 4);
        return FrontendIsolationPolicyService.ShouldIsolateByGlobalMode(
            globalMode,
            IsModable(manifestSummary),
            FrontendIsolationPolicyService.IsNonReleaseVersionType(manifestSummary.VersionType));
    }

    private static bool IsModable(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary.HasForgeLike
               || manifestSummary.HasCleanroom
               || manifestSummary.HasFabricLike
               || manifestSummary.HasLiteLoader
               || manifestSummary.HasLabyMod
               || manifestSummary.HasOptiFine;
    }

    private static MinecraftLaunchLoginRequirement ResolveLoginRequirement(YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null)
        {
            return MinecraftLaunchLoginRequirement.None;
        }

        return (MinecraftLaunchLoginRequirement)Math.Clamp(
            ReadValue(instanceConfig, "VersionServerLoginRequire", 0),
            0,
            3);
    }

    private static string ResolveVersionType(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        var instanceCustomInfo = instanceConfig is null
            ? null
            : FirstNonEmpty(
                NullIfWhiteSpace(ReadValue(instanceConfig, "VersionArgumentInfo", string.Empty)),
                NullIfWhiteSpace(ReadValue(instanceConfig, "CustomInfo", string.Empty)));
        if (!string.IsNullOrWhiteSpace(instanceCustomInfo))
        {
            return instanceCustomInfo;
        }

        var globalCustomInfo = NullIfWhiteSpace(ReadValue(localConfig, "LaunchArgumentInfo", "PCLCE"));
        if (!string.IsNullOrWhiteSpace(globalCustomInfo))
        {
            return globalCustomInfo;
        }

        return manifestSummary.VersionType ?? "PCL CE";
    }

    private static FrontendRetroWrapperOptions ResolveRetroWrapperOptions(
        string launcherFolder,
        FrontendVersionManifestSummary manifestSummary,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig)
    {
        var disableGlobalRetroWrapper = ReadValue(sharedConfig, "LaunchAdvanceDisableRW", false);
        var disableInstanceRetroWrapper = instanceConfig is not null &&
                                          ReadValue(instanceConfig, "VersionAdvanceDisableRW", false);
        var gameVersionDrop = ResolveGameVersionDrop(manifestSummary.VanillaVersion);
        var shouldUseRetroWrapper = MinecraftLaunchRetroWrapperService.ShouldUse(new MinecraftLaunchRetroWrapperRequest(
            manifestSummary.ReleaseTime ?? DateTime.Now,
            gameVersionDrop,
            disableGlobalRetroWrapper,
            disableInstanceRetroWrapper));
        var retroWrapperPath = Path.Combine(launcherFolder, "libraries", "retrowrapper", "RetroWrapper.jar");
        if (!shouldUseRetroWrapper || !File.Exists(retroWrapperPath))
        {
            return new FrontendRetroWrapperOptions(false, null);
        }

        return new FrontendRetroWrapperOptions(true, retroWrapperPath);
    }

    private static IReadOnlyList<string> BuildClasspathHeadEntries(
        YamlFileProvider? instanceConfig,
        string? instanceJarPath)
    {
        var customHeadEntries = new List<string>();
        if (!string.IsNullOrWhiteSpace(instanceJarPath) && File.Exists(instanceJarPath))
        {
            customHeadEntries.Add(instanceJarPath);
        }

        if (instanceConfig is null)
        {
            return customHeadEntries;
        }

        var rawClasspathHead = ReadValue(instanceConfig, "VersionAdvanceClasspathHead", string.Empty);
        var userEntries = ParseClasspathHeadEntries(rawClasspathHead);
        for (var index = userEntries.Count - 1; index >= 0; index--)
        {
            customHeadEntries.Add(userEntries[index]);
        }

        return customHeadEntries;
    }

    private static IReadOnlyList<string> ParseClasspathHeadEntries(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([Path.PathSeparator, '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static FrontendProxyOptions ResolveProxyOptions(
        FrontendRuntimePaths runtimePaths,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null || !ReadValue(instanceConfig, "VersionAdvanceUseProxyV2", false))
        {
            return FrontendProxyOptions.None;
        }

        var proxyType = ReadValue(sharedConfig, "SystemHttpProxyType", 1);
        return proxyType switch
        {
            2 => TryParseProxyUri(LauncherFrontendRuntimeStateService.TryReadProtectedString(
                    runtimePaths.SharedConfigDirectory,
                    runtimePaths.SharedConfigPath,
                    "SystemHttpProxy"))
                ?? FrontendProxyOptions.None,
            1 => ResolveSystemProxyOptions(),
            _ => FrontendProxyOptions.None
        };
    }

    private static FrontendProxyOptions ResolveSystemProxyOptions()
    {
        try
        {
            var probeTarget = new Uri("http://example.com");
            var proxyUri = WebRequest.DefaultWebProxy?.GetProxy(probeTarget);
            if (proxyUri is null ||
                !proxyUri.IsAbsoluteUri ||
                string.Equals(proxyUri.Host, probeTarget.Host, StringComparison.OrdinalIgnoreCase))
            {
                return FrontendProxyOptions.None;
            }

            return TryParseProxyUri(proxyUri.ToString()) ?? FrontendProxyOptions.None;
        }
        catch
        {
            return FrontendProxyOptions.None;
        }
    }

    private static FrontendProxyOptions? TryParseProxyUri(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var candidate = rawValue.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "http://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        var scheme = uri.Scheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase)
            ? "socks"
            : string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                ? "https"
                : "http";
        var port = uri.IsDefaultPort
            ? scheme switch
            {
                "https" => 443,
                "socks" => 1080,
                _ => 80
            }
            : uri.Port;
        if (port <= 0)
        {
            return null;
        }

        return new FrontendProxyOptions(scheme, uri.Host, port);
    }

    private static FrontendJavaWrapperOptions ResolveJavaWrapperOptions(
        string launcherFolder,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var disableGlobalJavaWrapper = ReadValue(localConfig, "LaunchAdvanceDisableJLW", true);
        var disableInstanceJavaWrapper = instanceConfig is not null &&
                                         ReadValue(instanceConfig, "VersionAdvanceDisableJLW", false);
        var isRequested = !disableGlobalJavaWrapper && !disableInstanceJavaWrapper;
        var wrapperPath = ResolveJavaWrapperPath(launcherFolder);
        if (!isRequested || string.IsNullOrWhiteSpace(wrapperPath))
        {
            return FrontendJavaWrapperOptions.Disabled;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "PCL", "JavaWrapper");
        try
        {
            Directory.CreateDirectory(tempDirectory);
        }
        catch
        {
            return FrontendJavaWrapperOptions.Disabled;
        }

        return new FrontendJavaWrapperOptions(true, tempDirectory, wrapperPath);
    }

    private static string? ResolveDebugLog4jConfigurationPath(
        string launcherFolder,
        string indieDirectory,
        YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null || !ReadValue(instanceConfig, "VersionUseDebugLog4j2Config", false))
        {
            return null;
        }

        string[] candidates =
        [
            Path.Combine(indieDirectory, "PCL", "log4j2-debug.xml"),
            Path.Combine(indieDirectory, "PCL", "log4j2.xml"),
            Path.Combine(indieDirectory, "log4j2-debug.xml"),
            Path.Combine(indieDirectory, "log4j2.xml"),
            Path.Combine(launcherFolder, "PCL", "log4j2-debug.xml"),
            Path.Combine(launcherFolder, "PCL", "log4j2.xml")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveRendererAgentArgument(
        string launcherFolder,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var effectiveRendererIndex = ResolveEffectiveRendererIndex(localConfig, instanceConfig);
        var rendererMode = ResolveRendererModeName(effectiveRendererIndex);
        if (string.IsNullOrWhiteSpace(rendererMode))
        {
            return null;
        }

        var rendererAgentPath = ResolveRendererAgentPath(launcherFolder);
        return string.IsNullOrWhiteSpace(rendererAgentPath)
            ? null
            : $"-javaagent:\"{rendererAgentPath}\"={rendererMode}";
    }

    private static int ResolveEffectiveRendererIndex(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var globalRendererIndex = Math.Clamp(ReadValue(localConfig, "LaunchAdvanceRenderer", 0), 0, 3);
        if (instanceConfig is null)
        {
            return globalRendererIndex;
        }

        var instanceRendererIndex = Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceRenderer", 0), 0, 4);
        return instanceRendererIndex == 0
            ? globalRendererIndex
            : instanceRendererIndex - 1;
    }

    private static string? ResolveRendererModeName(int effectiveRendererIndex)
    {
        return effectiveRendererIndex switch
        {
            1 => "llvmpipe",
            2 => "d3d12",
            3 => "zink",
            _ => null
        };
    }

    private static string? ResolveRendererAgentPath(string launcherFolder)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string[] directCandidates =
        [
            Path.Combine(launcherFolder, "PCL", "mesa-loader-windows.jar"),
            Path.Combine(launcherFolder, "PCL", "mesa-loader.jar"),
            Path.Combine(launcherFolder, "libraries", "mesa-loader-windows.jar"),
            Path.Combine(launcherFolder, "libraries", "mesa-loader.jar")
        ];

        var directMatch = directCandidates.FirstOrDefault(path => File.Exists(path) && IsJavaAgentJar(path));
        if (!string.IsNullOrWhiteSpace(directMatch))
        {
            return directMatch;
        }

        string[] searchRoots =
        [
            Path.Combine(launcherFolder, "PCL"),
            Path.Combine(launcherFolder, "libraries")
        ];
        foreach (var root in searchRoots)
        {
            var discovered = TryDiscoverRendererAgentPath(root);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                return discovered;
            }
        }

        return null;
    }

    private static string? TryDiscoverRendererAgentPath(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return null;
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*.jar", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.Contains("mesa", StringComparison.OrdinalIgnoreCase) &&
                    fileName.Contains("loader", StringComparison.OrdinalIgnoreCase) &&
                    IsJavaAgentJar(filePath))
                {
                    return filePath;
                }
            }
        }
        catch
        {
            // Ignore best-effort discovery failures and fall back to default behavior.
        }

        return null;
    }

    private static bool IsJavaAgentJar(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var manifestEntry = archive.GetEntry("META-INF/MANIFEST.MF");
            if (manifestEntry is null)
            {
                return false;
            }

            using var manifestStream = manifestEntry.Open();
            using var reader = new StreamReader(manifestStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is not null && line.StartsWith("Premain-Class:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Invalid archives should not break launch composition.
        }

        return false;
    }

    private static string? ResolveJavaWrapperPath(string launcherFolder)
    {
        string[] candidates =
        [
            Path.Combine(launcherFolder, "PCL", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "PCL", "JavaWrapper.jar"),
            Path.Combine(launcherFolder, "libraries", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "libraries", "java-wrapper", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "runtime", "java-wrapper.jar")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int ResolveGameVersionDrop(Version? vanillaVersion)
    {
        if (vanillaVersion is null)
        {
            return 0;
        }

        return vanillaVersion.Major == 1
            ? vanillaVersion.Minor * 10
            : vanillaVersion.Major * 10 + vanillaVersion.Minor;
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> ReadManifestLibraries(
        string launcherFolder,
        string selectedInstanceName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return [];
        }

        return ReadManifestLibrariesRecursive(
            launcherFolder,
            selectedInstanceName,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> ReadManifestLibrariesRecursive(
        string launcherFolder,
        string versionName,
        ISet<string> visited)
    {
        if (!visited.Add(versionName))
        {
            return [];
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        var parentLibraries = string.IsNullOrWhiteSpace(parentVersion)
            ? []
            : ReadManifestLibrariesRecursive(launcherFolder, parentVersion, visited);
        var currentLibraries = ParseLibraries(root, launcherFolder);
        return parentLibraries.Concat(currentLibraries).ToArray();
    }

    private static MinecraftLaunchClasspathLibrary[] ParseLibraries(JsonElement root, string launcherFolder)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<MinecraftLaunchClasspathLibrary>();
        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library))
            {
                continue;
            }

            var name = GetString(library, "name");
            var artifactPath = GetNestedString(library, "downloads", "artifact", "path");
            if (string.IsNullOrWhiteSpace(artifactPath) && !string.IsNullOrWhiteSpace(name))
            {
                artifactPath = DeriveLibraryPathFromName(name);
            }

            if (string.IsNullOrWhiteSpace(artifactPath))
            {
                continue;
            }

            result.Add(new MinecraftLaunchClasspathLibrary(
                name,
                Path.Combine(launcherFolder, "libraries", artifactPath.Replace('/', Path.DirectorySeparatorChar)),
                IsNatives: library.TryGetProperty("natives", out _)));
        }

        return result.ToArray();
    }

    private static bool IsLibraryAllowedOnCurrentPlatform(JsonElement library, MachineType runtimeArchitecture)
    {
        return FrontendLibraryArtifactResolver.IsLibraryAllowed(library, runtimeArchitecture);
    }

    private static bool IsLibraryAllowedOnCurrentPlatform(JsonElement library)
    {
        return IsLibraryAllowedOnCurrentPlatform(library, GetPreferredMachineType());
    }

    private static bool RuleMatchesCurrentPlatform(JsonElement rule, MachineType runtimeArchitecture)
    {
        if (!rule.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var osName = GetString(os, "name");
        if (!string.IsNullOrWhiteSpace(osName) && !IsCurrentOs(osName))
        {
            return false;
        }

        var osArch = GetString(os, "arch");
        if (!string.IsNullOrWhiteSpace(osArch) && !IsCurrentArchitecture(osArch, runtimeArchitecture))
        {
            return false;
        }

        return true;
    }

    private static bool IsCurrentOs(string osName)
    {
        return osName.ToLowerInvariant() switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "osx" or "macos" => OperatingSystem.IsMacOS(),
            "linux" => OperatingSystem.IsLinux(),
            _ => true
        };
    }

    private static bool IsCurrentArchitecture(string osArch, MachineType runtimeArchitecture)
    {
        return osArch.ToLowerInvariant() switch
        {
            "x86" => runtimeArchitecture == MachineType.I386,
            "x86_64" or "amd64" => runtimeArchitecture == MachineType.AMD64,
            "arm64" or "aarch64" => runtimeArchitecture == MachineType.ARM64,
            "arm" => runtimeArchitecture is MachineType.ARM or MachineType.ARMNT,
            _ => true
        };
    }

    private static bool TryResolveNativeArchiveDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out NativeArchiveDownloadInfo nativeArchive)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveNativeArchiveDownload(
                library,
                launcherFolder,
                runtimeArchitecture,
                out var resolved))
        {
            nativeArchive = null!;
            return false;
        }

        nativeArchive = new NativeArchiveDownloadInfo(
            resolved.TargetPath,
            resolved.DownloadUrl,
            resolved.Sha1,
            resolved.ExtractExcludes);
        return true;
    }

    private static bool TryResolveEffectiveArtifactDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out LibraryDownloadInfo download)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveArtifactDownload(
                library,
                launcherFolder,
                runtimeArchitecture,
                out var resolved))
        {
            download = null!;
            return false;
        }

        download = new LibraryDownloadInfo(resolved.TargetPath, resolved.DownloadUrl, resolved.Sha1);
        return true;
    }

    private static MinecraftJavaRuntimeManifestRequestPlan? BuildJavaRuntimeManifestPlan(
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        if (string.IsNullOrWhiteSpace(javaWorkflow.MissingJavaPrompt.DownloadTarget))
        {
            return null;
        }

        try
        {
            var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
            var liveIndexJson = TryDownloadUtf8String(MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().AllUrls);
            if (string.IsNullOrWhiteSpace(liveIndexJson))
            {
                return null;
            }

            return MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
                new MinecraftJavaRuntimeManifestRequestPlanRequest(
                    liveIndexJson,
                    ResolveJavaRuntimePlatformKey(runtimeArchitecture),
                    javaWorkflow.MissingJavaPrompt.DownloadTarget,
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        }
        catch
        {
            return null;
        }
    }

    private static MinecraftJavaRuntimeDownloadTransferPlan? BuildJavaRuntimeTransferPlan(
        string launcherFolder,
        MinecraftJavaRuntimeManifestRequestPlan? manifestPlan)
    {
        if (manifestPlan is null)
        {
            return null;
        }

        var runtimeBaseDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
            launcherFolder,
            manifestPlan.Selection.ComponentKey);
        var manifestJson = TryDownloadUtf8String(manifestPlan.RequestUrls.AllUrls);
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return null;
        }

        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestJson,
                runtimeBaseDirectory,
                Array.Empty<string>(),
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
        var existingRelativePaths = workflowPlan.Files
            .Where(file => File.Exists(file.TargetPath))
            .Select(file => file.RelativePath)
            .ToArray();

        return MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
            new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                workflowPlan,
                existingRelativePaths));
    }

    private static string? TryDownloadUtf8String(IReadOnlyList<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                return JavaRuntimeHttpClient.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch
            {
                // Try the next mirror.
            }
        }

        return null;
    }

    private static string ResolveJavaRuntimePlatformKey(MachineType runtimeArchitecture)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "windows-arm64",
                MachineType.I386 => "windows-x86",
                _ => "windows-x64"
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return runtimeArchitecture == MachineType.ARM64
                ? "mac-os-arm64"
                : "mac-os";
        }

        return runtimeArchitecture == MachineType.I386
            ? "linux-i386"
            : "linux";
    }

    private sealed record LibraryDownloadInfo(
        string TargetPath,
        string? DownloadUrl,
        string? Sha1);

    private sealed record LibraryCoordinate(
        string GroupId,
        string ArtifactId,
        string Version,
        string? Classifier);

    private sealed record NativeArchiveDownloadInfo(
        string TargetPath,
        string? DownloadUrl,
        string? Sha1,
        IReadOnlyList<string> ExtractExcludes);

    private static string BuildLibraryUrl(JsonElement library, string relativePath)
    {
        return FrontendLibraryArtifactResolver.BuildLibraryUrl(library, relativePath);
    }

    private static string NormalizeSelectedJavaPath(string rawValue)
    {
        var trimmed = rawValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var path = GetString(document.RootElement, "Path");
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.Combine(path, OperatingSystem.IsWindows() ? "java.exe" : "java");
        }
        catch
        {
            return trimmed;
        }
    }

    private static string ResolveConfiguredJavaSelection(
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        string launcherFolder)
    {
        if (instanceConfig is null || !instanceConfig.Exists("VersionArgumentJavaSelect"))
        {
            return NormalizeSelectedJavaPath(ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty));
        }

        var instanceSelection = ReadInstanceJavaSelection(
            ReadValue(instanceConfig, "VersionArgumentJavaSelect", string.Empty),
            launcherFolder);
        return instanceSelection.FollowGlobal
            ? NormalizeSelectedJavaPath(ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty))
            : NormalizeSelectedJavaPath(instanceSelection.RawSelection);
    }

    private static FrontendConfiguredJavaSelection ReadInstanceJavaSelection(string rawValue, string launcherFolder)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "使用全局设置", StringComparison.Ordinal))
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty);
        }

        if (!rawValue.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: false, RawSelection: rawValue);
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            var kind = GetString(document.RootElement, "kind")?.ToLowerInvariant();
            return kind switch
            {
                "auto" => new FrontendConfiguredJavaSelection(FollowGlobal: false, RawSelection: string.Empty),
                "exist" => new FrontendConfiguredJavaSelection(
                    FollowGlobal: false,
                    RawSelection: GetString(document.RootElement, "JavaExePath") ?? string.Empty),
                "relative" => new FrontendConfiguredJavaSelection(
                    FollowGlobal: false,
                    RawSelection: Path.Combine(launcherFolder, GetString(document.RootElement, "RelativePath") ?? string.Empty)),
                _ => new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty)
            };
        }
        catch
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty);
        }
    }

    private static FrontendJavaRuntimeSummary? ProbeHostJavaRuntime(
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        lock (HostJavaProbeLock)
        {
            if (IsHostJavaProbeCached)
            {
                return SelectCompatibleRuntime(CachedHostJavaRuntimes ?? [], manifestSummary, javaWorkflow) is { } cachedRuntime
                    ? ToRuntimeSummary(cachedRuntime)
                    : null;
            }

            var runtimes = new List<FrontendStoredJavaRuntime>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in EnumerateHostJavaRuntimeCandidates())
            {
                var runtime = TryProbeJavaRuntime(candidate);
                if (runtime is not null)
                {
                    if (seenPaths.Add(runtime.ExecutablePath))
                    {
                        runtimes.Add(runtime);
                    }
                }
            }

            IsHostJavaProbeCached = true;
            CachedHostJavaRuntimes = runtimes;
            return SelectCompatibleRuntime(runtimes, manifestSummary, javaWorkflow) is { } resolvedRuntime
                ? ToRuntimeSummary(resolvedRuntime)
                : null;
        }
    }

    private static IReadOnlyList<string> EnumerateHostJavaRuntimeCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["java.exe"];
        }

        var candidates = new List<string>();
        void AddCandidate(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            candidates.Add(candidate);
        }

        AddCandidate("/opt/homebrew/opt/openjdk/bin/java");
        AddCandidate("/usr/local/opt/openjdk/bin/java");

        foreach (var directory in EnumerateJavaHomeDirectories("/Library/Java/JavaVirtualMachines"))
        {
            AddCandidate(Path.Combine(directory, "Contents", "Home", "bin", "java"));
        }

        foreach (var directory in EnumerateJavaHomeDirectories(Path.Combine(
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     "Library",
                     "Java",
                     "JavaVirtualMachines")))
        {
            AddCandidate(Path.Combine(directory, "Contents", "Home", "bin", "java"));
        }

        foreach (var candidate in ResolveWhichJavaCandidates())
        {
            AddCandidate(candidate);
        }

        AddCandidate("java");
        AddCandidate("/usr/bin/java");
        return candidates;
    }

    private static IEnumerable<string> EnumerateJavaHomeDirectories(string rootPath)
    {
        try
        {
            return Directory.Exists(rootPath)
                ? Directory.EnumerateDirectories(rootPath)
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> ResolveWhichJavaCandidates()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-lc \"which -a java 2>/dev/null\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return [];
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore best-effort probe cleanup.
                }

                return [];
            }

            return process.StandardOutput
                .ReadToEnd()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static FrontendStoredJavaRuntime? TryProbeJavaRuntime(string executablePath)
    {
        if (FrontendJavaInventoryService.TryResolveRuntime(executablePath) is { ParsedVersion: not null } resolvedRuntime)
        {
            var majorVersion = resolvedRuntime.MajorVersion ?? GetMajorVersion(resolvedRuntime.ParsedVersion);
            return resolvedRuntime with
            {
                DisplayName = majorVersion is { } major ? $"Java {major}" : resolvedRuntime.DisplayName
            };
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore best-effort probe cleanup.
                }

                return null;
            }

            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            var parsedVersion = TryParseJavaVersion(output);
            var majorVersion = GetMajorVersion(parsedVersion);
            if (process.ExitCode != 0 ||
                parsedVersion is null ||
                majorVersion is null ||
                output.Contains("Unable to locate a Java Runtime", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("No Java runtime present", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new FrontendStoredJavaRuntime(
                ResolveProbedJavaExecutablePath(executablePath, process),
                $"Java {majorVersion.Value}",
                parsedVersion,
                majorVersion.Value,
                IsEnabled: true,
                Is64Bit: TryParseJavaBitness(output),
                IsJre: null,
                Brand: null,
                Architecture: null);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveProbedJavaExecutablePath(string executablePath, Process process)
    {
        if (!string.IsNullOrWhiteSpace(executablePath) && Path.IsPathRooted(executablePath))
        {
            return executablePath;
        }

        try
        {
            var mainModulePath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModulePath))
            {
                return mainModulePath;
            }
        }
        catch
        {
            // Some runtimes may block reading MainModule metadata.
        }

        return executablePath;
    }

    private static string ResolveJavaFolderPath(string? executablePath, string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return fallbackDirectory;
        }

        var folder = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(folder)
            ? fallbackDirectory
            : folder;
    }

    private static string ReadFileOrDefault(string path, string fallback)
    {
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
    }

    private static string? ReadOptionValue(string optionsPath, string key)
    {
        if (!File.Exists(optionsPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(optionsPath))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            if (string.Equals(line[..separatorIndex], key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separatorIndex + 1)..];
            }
        }

        return null;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string BuildJvmArguments(
        FrontendRuntimePaths runtimePaths,
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        JsonFileProvider sharedConfig,
        FrontendRetroWrapperOptions retroWrapperOptions,
        string effectiveJvmArguments,
        IReadOnlyDictionary<string, string> replacementValues,
        string indieDirectory,
        int javaMajorVersion)
    {
        var modernJvmSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "jvm");
        var mainClass = ReadManifestProperty(launcherFolder, selectedInstanceName, "mainClass")
                        ?? "net.minecraft.client.main.Main";
        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var proxyOptions = ResolveProxyOptions(runtimePaths, sharedConfig, instanceConfig);
        var javaWrapperOptions = ResolveJavaWrapperOptions(launcherFolder, localConfig, instanceConfig);
        var debugLog4jConfigurationPath = ResolveDebugLog4jConfigurationPath(launcherFolder, indieDirectory, instanceConfig);
        var rendererAgentArgument = ResolveRendererAgentArgument(launcherFolder, localConfig, instanceConfig);
        return modernJvmSections.Count > 0
            ? MinecraftLaunchJvmArgumentService.BuildModernArguments(
                new MinecraftLaunchModernJvmArgumentRequest(
                    MinecraftLaunchJsonArgumentService.ExtractValues(
                        new MinecraftLaunchJsonArgumentRequest(
                            modernJvmSections,
                            Environment.OSVersion.Version.ToString(),
                            runtimeArchitecture == MachineType.I386)),
                    effectiveJvmArguments,
                    ReadValue(sharedConfig, "LaunchPreferredIpStack", JvmPreferredIpStack.Default),
                    ResolveYoungGenerationMemoryMegabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary),
                    ResolveAllocatedMemoryMegabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary),
                    UseRetroWrapper: retroWrapperOptions.UseRetroWrapper,
                    javaMajorVersion,
                    AuthlibInjectorArgument: null,
                    DebugLog4jConfigurationFilePath: debugLog4jConfigurationPath,
                    RendererAgentArgument: rendererAgentArgument,
                    ProxyScheme: proxyOptions.Scheme,
                    ProxyHost: proxyOptions.Host,
                    ProxyPort: proxyOptions.Port,
                    UseJavaWrapper: javaWrapperOptions.IsRequested,
                    JavaWrapperTempDirectory: javaWrapperOptions.TempDirectory,
                    JavaWrapperPath: javaWrapperOptions.WrapperPath,
                    MainClass: mainClass))
            : MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
                new MinecraftLaunchLegacyJvmArgumentRequest(
                    effectiveJvmArguments,
                    ResolveYoungGenerationMemoryMegabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary),
                    ResolveAllocatedMemoryMegabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary),
                    replacementValues["${natives_directory}"],
                    javaMajorVersion,
                    AuthlibInjectorArgument: null,
                    DebugLog4jConfigurationFilePath: debugLog4jConfigurationPath,
                    RendererAgentArgument: rendererAgentArgument,
                    ProxyScheme: proxyOptions.Scheme,
                    ProxyHost: proxyOptions.Host,
                    ProxyPort: proxyOptions.Port,
                    UseJavaWrapper: javaWrapperOptions.IsRequested,
                    JavaWrapperTempDirectory: javaWrapperOptions.TempDirectory,
                    JavaWrapperPath: javaWrapperOptions.WrapperPath,
                    MainClass: mainClass));
    }

    private static MinecraftLaunchWatcherWorkflowRequest BuildWatcherWorkflowRequest(
        string launcherFolder,
        string selectedInstanceName,
        string instanceDirectory,
        string indieDirectory,
        string nativesDirectory,
        string nativeSearchPath,
        string nativeExtractionDirectory,
        string? nativePathAliasDirectory,
        int nativeArchiveCount,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider? instanceConfig,
        YamlFileProvider localConfig)
    {
        var javaFolder = ResolveJavaFolderPath(selectedJavaRuntime?.ExecutablePath, launcherFolder);
        return new MinecraftLaunchWatcherWorkflowRequest(
            new MinecraftLaunchSessionLogRequest(
                LauncherVersionName: "frontend-avalonia",
                LauncherVersionCode: 1,
                GameVersionDisplayName: manifestSummary.VanillaVersion?.ToString() ?? selectedInstanceName,
                GameVersionRaw: manifestSummary.VanillaVersion?.ToString() ?? selectedInstanceName,
                GameVersionDrop: ResolveGameVersionDrop(manifestSummary.VanillaVersion),
                IsGameVersionReliable: manifestSummary.IsVersionInfoValid,
                AssetsIndexName: manifestSummary.AssetsIndexName ?? "legacy",
                InheritedInstanceName: ReadManifestProperty(launcherFolder, selectedInstanceName, "inheritsFrom"),
                AllocatedMemoryInGigabytes: ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary),
                MinecraftFolder: launcherFolder,
                InstanceFolder: instanceDirectory,
                IsVersionIsolated: !string.Equals(indieDirectory, launcherFolder, StringComparison.OrdinalIgnoreCase),
                IsHmclFormatJson: false,
                JavaDescription: selectedJavaRuntime?.DisplayName,
                NativesFolder: nativesDirectory,
                NativeSearchPath: nativeSearchPath,
                NativeExtractionDirectory: nativeExtractionDirectory,
                NativeAliasDirectory: nativePathAliasDirectory,
                NativeArchiveCount: nativeArchiveCount,
                PlayerName: selectedProfile.UserName,
                AccessToken: ResolveAccessToken(selectedProfile),
                ClientToken: selectedProfile.ClientToken ?? "frontend-avalonia",
                Uuid: ResolveProfileUuid(selectedProfile),
                LoginType: DescribeProfileKind(selectedProfile.Kind)),
            new MinecraftLaunchWatcherRequest(
                instanceConfig is null ? null : NullIfWhiteSpace(ReadValue(instanceConfig, "VersionArgumentTitle", string.Empty)),
                instanceConfig is not null && ReadValue(instanceConfig, "VersionArgumentTitleEmpty", false),
                NullIfWhiteSpace(ReadValue(localConfig, "LaunchArgumentTitle", string.Empty)),
                javaFolder,
                File.Exists(Path.Combine(javaFolder, OperatingSystem.IsWindows() ? "jstack.exe" : "jstack"))),
            OutputRealTimeLog: true);
    }

    private static IReadOnlyList<string> CollectArgumentSectionJsons(
        string launcherFolder,
        string selectedInstanceName,
        string sectionName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return [];
        }

        var results = new List<string>();
        foreach (var document in EnumerateManifestDocuments(launcherFolder, selectedInstanceName))
        {
            if (!document.RootElement.TryGetProperty("arguments", out var argumentsElement) ||
                argumentsElement.ValueKind != JsonValueKind.Object ||
                !argumentsElement.TryGetProperty(sectionName, out var sectionElement))
            {
                continue;
            }

            results.Add(sectionElement.ToString());
        }

        return results;
    }

    private static string? ReadManifestProperty(string launcherFolder, string selectedInstanceName, string propertyName)
    {
        foreach (var document in EnumerateManifestDocuments(launcherFolder, selectedInstanceName))
        {
            var value = GetString(document.RootElement, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<JsonDocument> EnumerateManifestDocuments(string launcherFolder, string selectedInstanceName)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentVersion = selectedInstanceName;
        while (!string.IsNullOrWhiteSpace(currentVersion) && visited.Add(currentVersion))
        {
            var manifestPath = Path.Combine(launcherFolder, "versions", currentVersion, $"{currentVersion}.json");
            if (!File.Exists(manifestPath))
            {
                yield break;
            }

            var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            yield return document;
            currentVersion = GetString(document.RootElement, "inheritsFrom");
        }
    }

    private static int ResolveAllocatedMemoryMegabytes(
        string indieDirectory,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        return (int)Math.Round(ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary) * 1024d);
    }

    private static int ResolveYoungGenerationMemoryMegabytes(
        string indieDirectory,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        return (int)Math.Round(ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime, localConfig, instanceConfig, manifestSummary) * 1024d * 0.15d);
    }

    private static double ResolveAllocatedMemoryGigabytes(
        string indieDirectory,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        var (totalMemoryGb, availableMemoryGb) = FrontendSystemMemoryService.GetPhysicalMemoryState();
        var modsDirectory = Path.Combine(indieDirectory, "mods");
        var modCount = Directory.Exists(modsDirectory)
            ? Directory.EnumerateFiles(modsDirectory, "*", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var (memoryMode, customMemoryGb) = ResolveLaunchMemoryPreference(localConfig, instanceConfig);

        return FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            memoryMode,
            customMemoryGb,
            isModable: IsModable(manifestSummary) || modCount > 0,
            hasOptiFine: manifestSummary.HasOptiFine,
            modCount,
            selectedJavaRuntime?.Is64Bit,
            totalMemoryGb,
            availableMemoryGb);
    }

    private static (int Mode, double CustomMemoryGb) ResolveLaunchMemoryPreference(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var globalMode = Math.Clamp(ReadValue(localConfig, "LaunchRamType", 0), 0, 1);
        var globalCustomMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(ReadValue(localConfig, "LaunchRamCustom", 15));
        if (instanceConfig is null)
        {
            return (globalMode, globalCustomMemoryGb);
        }

        var instanceMode = Math.Clamp(ReadValue(instanceConfig, "VersionRamType", 2), 0, 2);
        var instanceCustomMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(ReadValue(instanceConfig, "VersionRamCustom", 15));
        return instanceMode == 2
            ? (globalMode, globalCustomMemoryGb)
            : (instanceMode, instanceCustomMemoryGb);
    }

    private static string GetClasspathSeparator()
    {
        return OperatingSystem.IsWindows() ? ";" : ":";
    }

    private static string GetUserType(MinecraftLaunchProfileKind profileKind)
    {
        return profileKind switch
        {
            MinecraftLaunchProfileKind.Microsoft => "msa",
            MinecraftLaunchProfileKind.Auth => "authlib",
            MinecraftLaunchProfileKind.Legacy => "legacy",
            _ => "unknown"
        };
    }

    private static string ResolveProfileUuid(FrontendLaunchProfileSummary profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Uuid))
        {
            return profile.Uuid;
        }

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(profile.UserName));
        return new Guid(hash).ToString();
    }

    private static string ResolveAccessToken(FrontendLaunchProfileSummary profile)
    {
        return string.IsNullOrWhiteSpace(profile.AccessToken)
            ? "offline-access-token"
            : profile.AccessToken;
    }

    private static string DescribeProfileKind(MinecraftLaunchProfileKind profileKind)
    {
        return profileKind switch
        {
            MinecraftLaunchProfileKind.Microsoft => "Microsoft",
            MinecraftLaunchProfileKind.Auth => "Authlib-Injector",
            MinecraftLaunchProfileKind.Legacy => "Offline",
            _ => "Unknown"
        };
    }

    private static string ReplaceLaunchTokens(
        string text,
        FrontendLaunchProfileSummary selectedProfile,
        string instanceName,
        string launcherFolder,
        string indieDirectory,
        string javaFolder,
        bool replaceTime)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var replaced = text
            .Replace("{java}", javaFolder, StringComparison.Ordinal)
            .Replace("{minecraft}", launcherFolder, StringComparison.Ordinal)
            .Replace("{version_path}", indieDirectory, StringComparison.Ordinal)
            .Replace("{verpath}", indieDirectory, StringComparison.Ordinal)
            .Replace("{version_indie}", indieDirectory, StringComparison.Ordinal)
            .Replace("{verindie}", indieDirectory, StringComparison.Ordinal)
            .Replace("{name}", instanceName, StringComparison.Ordinal)
            .Replace("{version}", instanceName, StringComparison.Ordinal)
            .Replace("{user}", selectedProfile.UserName, StringComparison.Ordinal)
            .Replace("{uuid}", ResolveProfileUuid(selectedProfile), StringComparison.Ordinal)
            .Replace("{login}", DescribeProfileKind(selectedProfile.Kind), StringComparison.Ordinal);

        if (replaceTime)
        {
            replaced = replaced
                .Replace("{date}", DateTime.Now.ToString("yyyy/M/d"), StringComparison.Ordinal)
                .Replace("{time}", DateTime.Now.ToString("HH:mm:ss"), StringComparison.Ordinal);
        }

        return replaced;
    }

    private static FrontendJavaRuntimeSummary? TryResolveCompatibleRuntime(
        string executablePath,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        string? fallbackDisplayName = null)
    {
        var runtime = FrontendJavaInventoryService.TryResolveRuntime(executablePath, fallbackDisplayName: fallbackDisplayName);
        return runtime is not null && IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow)
            ? ToRuntimeSummary(runtime)
            : null;
    }

    private static FrontendStoredJavaRuntime? ResolveConfiguredRuntime(
        IReadOnlyList<FrontendStoredJavaRuntime> javaEntries,
        string selectedJavaPath,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var selectedEntry = javaEntries.FirstOrDefault(entry =>
            string.Equals(entry.ExecutablePath, selectedJavaPath, StringComparison.OrdinalIgnoreCase));
        if (selectedEntry is not null)
        {
            return selectedEntry.IsEnabled ? selectedEntry : null;
        }

        if (!File.Exists(selectedJavaPath))
        {
            return null;
        }

        return FrontendJavaInventoryService.TryResolveRuntime(
                   selectedJavaPath,
                   isEnabled: true,
                   fallbackDisplayName: Path.GetFileName(Path.GetDirectoryName(selectedJavaPath)) ?? $"Java {javaWorkflow.RecommendedMajorVersion}")
               ?? new FrontendStoredJavaRuntime(
                   selectedJavaPath,
                   Path.GetFileName(Path.GetDirectoryName(selectedJavaPath)) ?? $"Java {javaWorkflow.RecommendedMajorVersion}",
                   ParsedVersion: null,
                   MajorVersion: null,
                   IsEnabled: true,
                   Is64Bit: null,
                   IsJre: null,
                   Brand: null,
                   Architecture: null);
    }

    private static bool ShouldUseConfiguredRuntime(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        bool ignoreJavaCompatibilityWarning)
    {
        return runtime.IsEnabled &&
               (ignoreJavaCompatibilityWarning || IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow));
    }

    private static string BuildIgnoredJavaCompatibilityWarning(
        FrontendStoredJavaRuntime runtime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var runtimeLabel = string.IsNullOrWhiteSpace(runtime.DisplayName)
            ? Path.GetFileName(Path.GetDirectoryName(runtime.ExecutablePath)) ?? runtime.ExecutablePath
            : runtime.DisplayName;
        return $"已忽略 Java 兼容性检查，当前使用 {runtimeLabel}。推荐范围：{javaWorkflow.MinimumVersion} - {javaWorkflow.MaximumVersion}";
    }

    private static MinecraftLaunchPrompt BuildJavaCompatibilityPrompt(
        FrontendStoredJavaRuntime runtime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var runtimeLabel = string.IsNullOrWhiteSpace(runtime.DisplayName)
            ? Path.GetFileName(Path.GetDirectoryName(runtime.ExecutablePath)) ?? runtime.ExecutablePath
            : runtime.DisplayName;

        return new MinecraftLaunchPrompt(
            $"你手动选择的 Java {runtimeLabel} 与当前 Minecraft 版本不兼容。{Environment.NewLine}" +
            $"推荐范围：{javaWorkflow.MinimumVersion} - {javaWorkflow.MaximumVersion}{Environment.NewLine}" +
            $"你可以改用兼容 Java，或仅在本次启动中强制忽略兼容性检查后继续启动。",
            "所选 Java 不兼容",
            [
                new MinecraftLaunchPromptButton(
                    "强制使用当前 Java",
                    [
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.IgnoreJavaCompatibilityOnce),
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                    ]),
                new MinecraftLaunchPromptButton(
                    "改用兼容 Java",
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)]),
                new MinecraftLaunchPromptButton(
                    "取消启动",
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)])
            ],
            IsWarning: true);
    }

    private static FrontendStoredJavaRuntime? SelectCompatibleRuntime(
        IEnumerable<FrontendStoredJavaRuntime> runtimes,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return runtimes
            .Where(runtime => runtime.IsEnabled && IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow))
            .OrderBy(runtime => runtime.MajorVersion ?? int.MaxValue)
            .ThenBy(runtime => GetArchitecturePreference(runtime, manifestSummary))
            .ThenBy(runtime => runtime.IsJre ?? true)
            .ThenBy(runtime => runtime.Brand ?? JavaBrandType.Unknown)
            .ThenByDescending(runtime => runtime.ParsedVersion)
            .FirstOrDefault();
    }

    private static bool IsCompatibleWithWorkflow(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return runtime.ParsedVersion is not null &&
               JavaManager.IsVersionSuitable(runtime.ParsedVersion, javaWorkflow.MinimumVersion, javaWorkflow.MaximumVersion) &&
               IsArchitectureCompatible(runtime, manifestSummary) &&
               !ViolatesLegacyLinuxJavaConstraint(runtime, manifestSummary) &&
               !ViolatesLegacyLaunchWrapperConstraint(runtime, manifestSummary);
    }

    private static int GetArchitecturePreference(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (runtime.Architecture is null or MachineType.Unknown)
        {
            return 2;
        }

        if (ShouldForceX86Java(manifestSummary))
        {
            return runtime.Architecture == MachineType.I386 ? 0 : 1;
        }

        return runtime.Architecture == GetPreferredMachineType()
            ? 0
            : 1;
    }

    private static bool IsArchitectureCompatible(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (runtime.Architecture is null or MachineType.Unknown)
        {
            return true;
        }

        if (ShouldForceX86Java(manifestSummary))
        {
            return runtime.Architecture == MachineType.I386;
        }

        return runtime.Architecture == GetPreferredMachineType();
    }

    private static DateTime? InferJavaRequirementReleaseTime(Version? vanillaVersion)
    {
        if (vanillaVersion is null)
        {
            return null;
        }

        if (vanillaVersion >= new Version(1, 20, 5))
        {
            return new DateTime(2024, 4, 2);
        }

        if (vanillaVersion >= new Version(1, 18))
        {
            return new DateTime(2021, 11, 16);
        }

        if (vanillaVersion >= new Version(1, 17))
        {
            return new DateTime(2021, 5, 11);
        }

        return new DateTime(2011, 11, 18);
    }

    private static bool ShouldForceX86Java(FrontendVersionManifestSummary manifestSummary)
    {
        if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
        {
            return false;
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        return manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion < new Version(1, 6);
    }

    private static MachineType GetPreferredMachineType()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => MachineType.I386,
            Architecture.X64 => MachineType.AMD64,
            Architecture.Arm64 => MachineType.ARM64,
            _ => MachineType.Unknown
        };
    }

    private static MachineType ResolveTargetJavaArchitecture(
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (selectedJavaRuntime?.Architecture is { } runtimeArchitecture &&
            runtimeArchitecture != MachineType.Unknown)
        {
            return runtimeArchitecture;
        }

        return ShouldForceX86Java(manifestSummary)
            ? MachineType.I386
            : GetPreferredMachineType();
    }

    private static bool ViolatesLegacyLinuxJavaConstraint(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        return OperatingSystem.IsLinux() &&
               RuntimeInformation.OSArchitecture == Architecture.X64 &&
               !manifestSummary.HasCleanroom &&
               manifestSummary.VanillaVersion is not null &&
               manifestSummary.VanillaVersion <= new Version(1, 12, 999) &&
               runtime.Architecture == MachineType.AMD64 &&
               (runtime.MajorVersion ?? int.MaxValue) > 8;
    }

    private static bool ViolatesLegacyLaunchWrapperConstraint(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion > new Version(1, 12, 999))
        {
            return false;
        }

        var launchWrapperVersion = ExtractLibraryVersion(manifestSummary.Libraries, "net.minecraft:launchwrapper");
        if (!Version.TryParse(launchWrapperVersion, out var parsedLaunchWrapperVersion))
        {
            return false;
        }

        return parsedLaunchWrapperVersion < new Version(1, 13) &&
               (runtime.MajorVersion ?? int.MaxValue) > 8;
    }

    private static FrontendJavaRuntimeSummary ToRuntimeSummary(FrontendStoredJavaRuntime runtime)
    {
        return new FrontendJavaRuntimeSummary(
            runtime.ExecutablePath,
            runtime.DisplayName,
            runtime.MajorVersion,
            runtime.IsEnabled,
            runtime.Is64Bit,
            runtime.Architecture);
    }

    private readonly record struct FrontendConfiguredJavaSelection(
        bool FollowGlobal,
        string RawSelection);

    private readonly record struct FrontendJavaSelectionResult(
        FrontendJavaRuntimeSummary? Runtime,
        string? WarningMessage,
        MinecraftLaunchPrompt? CompatibilityPrompt);

    private readonly record struct FrontendRetroWrapperOptions(
        bool UseRetroWrapper,
        string? RetroWrapperPath);

    private readonly record struct FrontendProxyOptions(
        string? Scheme,
        string? Host,
        int? Port)
    {
        public static FrontendProxyOptions None { get; } = new(null, null, null);
    }

    private readonly record struct FrontendJavaWrapperOptions(
        bool IsRequested,
        string? TempDirectory,
        string? WrapperPath)
    {
        public static FrontendJavaWrapperOptions Disabled { get; } = new(false, null, null);
    }

    private sealed record FrontendNativePathPlan(
        string BaseDirectory,
        string ExtractionDirectory,
        string SearchPath,
        string? AliasDirectory);

    private static int? GetMajorVersion(Version? version)
    {
        if (version is null)
        {
            return null;
        }

        return version.Major == 1 ? version.Minor : version.Major;
    }

    private static Version? TryParseJavaVersion(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var versionStart = output.IndexOf('"');
        if (versionStart < 0)
        {
            return null;
        }

        var versionEnd = output.IndexOf('"', versionStart + 1);
        if (versionEnd <= versionStart)
        {
            return null;
        }

        var rawVersion = output[(versionStart + 1)..versionEnd];
        var matches = System.Text.RegularExpressions.Regex.Matches(rawVersion, @"\d+");
        if (matches.Count == 0)
        {
            return null;
        }

        var parts = new int[Math.Min(4, matches.Count)];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = int.Parse(matches[i].Value);
        }

        return parts.Length switch
        {
            1 => new Version(parts[0], 0),
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            _ => new Version(parts[0], parts[1], parts[2], parts[3])
        };
    }

    private static bool? TryParseJavaBitness(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        if (output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (output.Contains("32-Bit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Version? TryParseVanillaVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(rawValue.Trim(), @"\d+(?:\.\d+){1,3}");
        if (!match.Success || !Version.TryParse(match.Value, out var version))
        {
            return null;
        }

        return version.Major > 99 ? null : version;
    }

    private static DateTime? TryParseReleaseTime(string? rawValue)
    {
        return DateTime.TryParse(rawValue, out var releaseTime) ? releaseTime : null;
    }

    private static bool IsAscii(string value)
    {
        return value.All(character => character <= sbyte.MaxValue);
    }

    private static bool ContainsLibrary(IEnumerable<MinecraftLaunchClasspathLibrary> libraries, string searchText)
    {
        return libraries.Any(library => library.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? ExtractLibraryVersion(IEnumerable<MinecraftLaunchClasspathLibrary> libraries, string prefix)
    {
        var match = libraries.FirstOrDefault(library => library.Name?.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase) == true);
        return match?.Name?.Split(':').LastOrDefault();
    }

    private static string DeriveLibraryPathFromName(string libraryName, string? classifier = null)
    {
        return FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(libraryName, classifier);
    }

    private static void ParseLibraryCoordinateExtension(ref string? value, ref string extension)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var extensionIndex = value.IndexOf('@');
        if (extensionIndex < 0)
        {
            return;
        }

        if (extensionIndex < value.Length - 1)
        {
            extension = value[(extensionIndex + 1)..];
        }

        value = value[..extensionIndex];
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static int? GetNestedInt(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static bool? GetNestedBoolean(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : null;
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        var rawValue = GetString(element, propertyName);
        return DateTime.TryParse(rawValue, out var value) ? value : null;
    }

    private sealed record FrontendVersionManifestSummary(
        bool IsVersionInfoValid,
        DateTime? ReleaseTime,
        Version? VanillaVersion,
        string? VersionType,
        string? AssetsIndexName,
        IReadOnlyList<MinecraftLaunchClasspathLibrary> Libraries,
        bool HasOptiFine,
        bool HasForge,
        string? ForgeVersion,
        string? NeoForgeVersion,
        bool HasCleanroom,
        bool HasFabric,
        string? LegacyFabricVersion,
        string? QuiltVersion,
        bool HasLiteLoader,
        bool HasLabyMod,
        int? JsonRequiredMajorVersion,
        int? MojangRecommendedMajorVersion,
        string? MojangRecommendedComponent)
    {
        public bool HasForgeLike => HasForge || !string.IsNullOrWhiteSpace(NeoForgeVersion);

        public bool HasFabricLike => HasFabric
                                     || !string.IsNullOrWhiteSpace(LegacyFabricVersion)
                                     || !string.IsNullOrWhiteSpace(QuiltVersion);

        public static FrontendVersionManifestSummary Empty { get; } = new(
            false,
            null,
            null,
            null,
            null,
            Array.Empty<MinecraftLaunchClasspathLibrary>(),
            false,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null);
    }
}
