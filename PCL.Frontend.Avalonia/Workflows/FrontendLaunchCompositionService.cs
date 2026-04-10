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
using PCL.Frontend.Avalonia.Workflows.Inspection;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchCompositionService
{
    private static readonly HttpClient JavaRuntimeHttpClient = new();
    private static readonly object HostJavaProbeLock = new();
    private static readonly object NativeReplacementCatalogLock = new();
    private static IReadOnlyList<FrontendStoredJavaRuntime>? CachedHostJavaRuntimes;
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>? CachedNativeReplacementCatalog;
    private static bool IsHostJavaProbeCached;
    private static bool IsNativeReplacementCatalogCached;

    public static FrontendLaunchComposition Compose(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths)
    {
        var replayComposition = FrontendInspectionLaunchCompositionService.TryComposeReplay(
            options,
            options.SaveBatchPath);
        if (replayComposition is not null)
        {
            return replayComposition;
        }

        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        var localConfig = new YamlFileProvider(runtimePaths.LocalConfigPath);

        var launcherFolder = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty);
        var instancePath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? Path.Combine(launcherFolder, "versions")
            : Path.Combine(launcherFolder, "versions", selectedInstanceName);
        var instanceConfig = Directory.Exists(instancePath)
            ? OpenInstanceConfigProvider(instancePath)
            : null;
        var manifestSummary = ReadManifestSummary(launcherFolder, selectedInstanceName);
        var indieDirectory = ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary)
            ? instancePath
            : launcherFolder;
        var selectedProfile = ReadSelectedProfile(runtimePaths);
        var javaWorkflowRequest = BuildJavaWorkflowRequest(CreateRuntimeJavaWorkflowFallback(manifestSummary), manifestSummary);
        var javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(javaWorkflowRequest);
        var javaSelection = ResolveJavaRuntime(sharedConfig, localConfig, instanceConfig, launcherFolder, manifestSummary, javaWorkflow);
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

    public static FrontendLaunchComposition FromAvaloniaPlan(LaunchAvaloniaPlan plan)
    {
        var selectedProfileKind = plan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft
            ? MinecraftLaunchProfileKind.Microsoft
            : MinecraftLaunchProfileKind.Auth;
        var playerName = plan.ReplacementPlan.Values.TryGetValue("${auth_player_name}", out var authPlayerName)
            ? authPlayerName
            : "DemoPlayer";
        var versionName = plan.ReplacementPlan.Values.TryGetValue("${version_name}", out var instanceName)
            ? instanceName
            : plan.Scenario;
        var replayPrecheckRequest = new MinecraftLaunchPrecheckRequest(
            InstanceName: versionName,
            InstancePathIndie: plan.ReplacementPlan.Values.TryGetValue("${game_directory}", out var indiePath) ? indiePath : string.Empty,
            InstancePath: plan.ReplacementPlan.Values.TryGetValue("${game_directory}", out var path) ? path : string.Empty,
            IsInstanceSelected: true,
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: true,
            IsNonAsciiPathWarningDisabled: false,
            IsInstancePathAscii: false,
            ProfileValidationMessage: string.Empty,
            SelectedProfileKind: selectedProfileKind,
            HasLabyMod: false,
            LoginRequirement: MinecraftLaunchLoginRequirement.None,
            RequiredAuthServer: null,
            SelectedAuthServer: null,
            HasMicrosoftProfile: plan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft,
            IsRestrictedFeatureAllowed: true);

        return new FrontendLaunchComposition(
            plan.Scenario,
            versionName,
            plan.ReplacementPlan.Values.TryGetValue("${game_directory}", out var gameDirectory)
                ? gameDirectory
                : string.Empty,
            Array.Empty<FrontendLaunchArtifactRequirement>(),
            new FrontendLaunchProfileSummary(
                selectedProfileKind,
                playerName,
                plan.ReplacementPlan.Values.TryGetValue("${auth_uuid}", out var uuid) ? uuid : null,
                plan.ReplacementPlan.Values.TryGetValue("${auth_access_token}", out var accessToken) ? accessToken : null,
                null,
                null,
                null,
                null,
                null,
                plan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft),
            null,
            null,
            10,
            replayPrecheckRequest,
            MinecraftLaunchPrecheckService.Evaluate(replayPrecheckRequest),
            MinecraftLaunchShellService.GetSupportPrompt(10),
            plan.JavaWorkflow,
            plan.JavaRuntimeManifestPlan,
            plan.JavaRuntimeTransferPlan,
            plan.ResolutionPlan,
            plan.ClasspathPlan,
            plan.NativesDirectory,
            plan.NativePathAliasDirectory,
            null,
            plan.ReplacementPlan,
            plan.PrerunPlan,
            plan.ArgumentPlan,
            plan.SessionStartPlan,
            plan.PostLaunchShell,
            plan.CompletionNotification);
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
                    manifestSummary.HasForge || manifestSummary.HasLiteLoader,
                    manifestSummary.HasOptiFine)).Arguments;
        }

        var modernGameSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "game");
        if (modernGameSections.Count > 0)
        {
            arguments += " " + MinecraftLaunchGameArgumentService.BuildModernPlan(
                new MinecraftLaunchModernGameArgumentRequest(
                    MinecraftLaunchJsonArgumentService.ExtractValues(
                        new MinecraftLaunchJsonArgumentRequest(
                            modernGameSections,
                            Environment.OSVersion.Version.ToString(),
                            runtimeArchitecture == MachineType.I386)),
                    manifestSummary.HasForge || manifestSummary.HasLiteLoader,
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

        return MinecraftLaunchSessionWorkflowService.BuildStartPlan(
            new MinecraftLaunchSessionStartWorkflowRequest(
                new MinecraftLaunchCustomCommandWorkflowRequest(
                    new MinecraftLaunchCustomCommandRequest(
                        selectedJavaRuntime?.MajorVersion ?? 8,
                        instanceName,
                        indieDirectory,
                        javaExecutablePath,
                        argumentPlan.FinalArguments,
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
                PriorityKind: MinecraftLaunchProcessPriorityKind.Normal,
                StartedLogMessage: "缺少 Java 运行时，尚未生成可执行的启动命令。",
                AbortKillLogMessage: "缺少 Java 运行时，无需终止游戏进程。"),
            MinecraftLaunchWatcherWorkflowService.BuildPlan(watcherWorkflowRequest));
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
            HasForge = manifestSummary.HasForge
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
            HasForge: manifestSummary.HasForge,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabric,
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
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: manifestSummary.IsVersionInfoValid,
            ReleaseTime: manifestSummary.ReleaseTime ?? DateTime.Now,
            VanillaVersion: manifestSummary.VanillaVersion ?? new Version(1, 20, 1),
            HasOptiFine: manifestSummary.HasOptiFine,
            HasForge: manifestSummary.HasForge,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabric,
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
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var selectedJavaPath = ResolveConfiguredJavaSelection(sharedConfig, instanceConfig, launcherFolder);
        var ignoreJavaCompatibilityWarning = instanceConfig is not null &&
                                            ReadValue(instanceConfig, "VersionAdvanceJava", false);
        var javaEntries = FrontendJavaInventoryService.ParseAvailableRuntimes(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));

        if (!string.IsNullOrWhiteSpace(selectedJavaPath))
        {
            var selectedRuntime = ResolveConfiguredRuntime(javaEntries, selectedJavaPath, javaWorkflow);
            if (selectedRuntime is not null &&
                ShouldUseConfiguredRuntime(selectedRuntime, manifestSummary, javaWorkflow, ignoreJavaCompatibilityWarning))
            {
                return new FrontendJavaSelectionResult(
                    ToRuntimeSummary(selectedRuntime),
                    ignoreJavaCompatibilityWarning && !IsCompatibleWithWorkflow(selectedRuntime, manifestSummary, javaWorkflow)
                        ? BuildIgnoredJavaCompatibilityWarning(selectedRuntime, javaWorkflow)
                        : null);
            }
        }

        var autoEntry = SelectCompatibleRuntime(javaEntries, manifestSummary, javaWorkflow);
        if (autoEntry is not null)
        {
            return new FrontendJavaSelectionResult(ToRuntimeSummary(autoEntry), null);
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
            return new FrontendJavaSelectionResult(bundledRuntime, null);
        }

        return new FrontendJavaSelectionResult(ProbeHostJavaRuntime(manifestSummary, javaWorkflow), null);
    }

    private static FrontendVersionManifestSummary ReadManifestSummary(string launcherFolder, string selectedInstanceName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ReadManifestSummaryRecursive(launcherFolder, selectedInstanceName, visited);
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
        return manifestSummary.HasForge
               || manifestSummary.HasCleanroom
               || manifestSummary.HasFabric
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

    private static FrontendVersionManifestSummary ReadManifestSummaryRecursive(
        string launcherFolder,
        string versionName,
        ISet<string> visited)
    {
        if (!visited.Add(versionName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        var parentSummary = string.IsNullOrWhiteSpace(parentVersion)
            ? FrontendVersionManifestSummary.Empty
            : ReadManifestSummaryRecursive(launcherFolder, parentVersion, visited);
        var currentLibraries = ParseLibraries(root, launcherFolder);
        var allLibraries = parentSummary.Libraries.Concat(currentLibraries).ToArray();
        var currentId = GetString(root, "id");
        var vanillaVersionText = FirstNonEmpty(parentVersion, currentId);

        return new FrontendVersionManifestSummary(
            IsVersionInfoValid: true,
            ReleaseTime: GetDateTime(root, "releaseTime") ?? parentSummary.ReleaseTime,
            VanillaVersion: TryParseVanillaVersion(vanillaVersionText) ?? parentSummary.VanillaVersion,
            VersionType: FirstNonEmpty(GetString(root, "type"), parentSummary.VersionType),
            AssetsIndexName: GetNestedString(root, "assetIndex", "id") ??
                             GetString(root, "assets") ??
                             parentSummary.AssetsIndexName,
            Libraries: allLibraries,
            HasOptiFine: parentSummary.HasOptiFine || ContainsLibrary(allLibraries, "optifine"),
            HasForge: parentSummary.HasForge || ContainsLibrary(allLibraries, "net.minecraftforge:forge"),
            ForgeVersion: parentSummary.ForgeVersion ?? ExtractLibraryVersion(allLibraries, "net.minecraftforge:forge"),
            HasCleanroom: parentSummary.HasCleanroom || ContainsLibrary(allLibraries, "com.cleanroommc"),
            HasFabric: parentSummary.HasFabric || ContainsLibrary(allLibraries, "net.fabricmc:fabric-loader"),
            HasLiteLoader: parentSummary.HasLiteLoader || ContainsLibrary(allLibraries, "liteloader"),
            HasLabyMod: parentSummary.HasLabyMod || ContainsLibrary(allLibraries, "labymod"),
            JsonRequiredMajorVersion: GetNestedInt(root, "javaVersion", "majorVersion") ?? parentSummary.JsonRequiredMajorVersion,
            MojangRecommendedMajorVersion: GetNestedInt(root, "javaVersion", "majorVersion") ?? parentSummary.MojangRecommendedMajorVersion,
            MojangRecommendedComponent: GetNestedString(root, "javaVersion", "component") ?? parentSummary.MojangRecommendedComponent);
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
        if (!library.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            if (!RuleMatchesCurrentPlatform(rule, runtimeArchitecture))
            {
                continue;
            }

            allowed = !string.Equals(GetString(rule, "action"), "disallow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
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
        nativeArchive = null!;
        LibraryDownloadInfo download;
        if (IsDedicatedNativeLibrary(library))
        {
            if (!TryResolveEffectiveArtifactDownload(library, launcherFolder, runtimeArchitecture, out download))
            {
                return false;
            }
        }
        else
        {
            var entryName = ResolveNativeEntryName(library, runtimeArchitecture);
            if (string.IsNullOrWhiteSpace(entryName) ||
                !TryResolveLibraryDownload(library, entryName, launcherFolder, out download))
            {
                return false;
            }
        }

        nativeArchive = new NativeArchiveDownloadInfo(
            download.TargetPath,
            download.DownloadUrl,
            download.Sha1,
            GetExtractExcludes(library));
        return true;
    }

    private static bool TryResolveLibraryDownload(
        JsonElement library,
        string entryName,
        string launcherFolder,
        out LibraryDownloadInfo download)
    {
        download = null!;
        var libraryName = GetString(library, "name");
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return TryResolveLegacyLibraryDownloadWithoutDownloads(library, entryName, launcherFolder, out download);
        }

        if (!library.TryGetProperty("downloads", out var downloads) || downloads.ValueKind != JsonValueKind.Object)
        {
            if (!string.Equals(entryName, "artifact", StringComparison.Ordinal))
            {
                return false;
            }

            var fallbackPath = DeriveLibraryPathFromName(libraryName);
            var fallbackTargetPath = Path.Combine(launcherFolder, "libraries", fallbackPath.Replace('/', Path.DirectorySeparatorChar));
            download = new LibraryDownloadInfo(
                fallbackTargetPath,
                BuildLibraryUrl(library, fallbackPath),
                GetString(library, "sha1"));
            return true;
        }

        JsonElement downloadEntry;
        if (string.Equals(entryName, "artifact", StringComparison.Ordinal))
        {
            if (!downloads.TryGetProperty("artifact", out downloadEntry) || downloadEntry.ValueKind != JsonValueKind.Object)
            {
                return TryResolveLegacyLibraryDownloadWithoutDownloads(library, entryName, launcherFolder, out download);
            }
        }
        else
        {
            if (!downloads.TryGetProperty("classifiers", out var classifiers) ||
                classifiers.ValueKind != JsonValueKind.Object ||
                !classifiers.TryGetProperty(entryName, out downloadEntry) ||
                downloadEntry.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
        }

        var path = GetString(downloadEntry, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            path = DeriveLibraryPathFromName(
                libraryName,
                string.Equals(entryName, "artifact", StringComparison.Ordinal) ? null : entryName);
        }

        var targetPath = Path.Combine(launcherFolder, "libraries", path.Replace('/', Path.DirectorySeparatorChar));
        var hasExplicitUrl = downloadEntry.TryGetProperty("url", out var urlElement);
        var url = hasExplicitUrl && urlElement.ValueKind == JsonValueKind.String
            ? urlElement.GetString()
            : GetString(downloadEntry, "url");
        if (!hasExplicitUrl || !string.IsNullOrWhiteSpace(url))
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = BuildLibraryUrl(library, path);
            }
        }

        download = new LibraryDownloadInfo(targetPath, url, GetString(downloadEntry, "sha1"));
        return true;
    }

    private static bool TryResolveLegacyLibraryDownloadWithoutDownloads(
        JsonElement library,
        string entryName,
        string launcherFolder,
        out LibraryDownloadInfo download)
    {
        download = null!;
        if (!string.Equals(entryName, "artifact", StringComparison.Ordinal))
        {
            return false;
        }

        var libraryName = GetString(library, "name");
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return false;
        }

        var relativePath = DeriveLibraryPathFromName(libraryName);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var targetPath = Path.Combine(launcherFolder, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));
        var downloadUrl = BuildLibraryUrl(library, relativePath);
        download = new LibraryDownloadInfo(targetPath, downloadUrl, Sha1: null);
        return true;
    }

    private static bool TryResolveEffectiveArtifactDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out LibraryDownloadInfo download)
    {
        if (TryResolveArtifactReplacementDownload(library, launcherFolder, runtimeArchitecture, out download))
        {
            return true;
        }

        return TryResolveLibraryDownload(library, "artifact", launcherFolder, out download);
    }

    private static bool TryResolveArtifactReplacementDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out LibraryDownloadInfo download)
    {
        download = null!;
        var libraryName = GetString(library, "name");
        return !string.IsNullOrWhiteSpace(libraryName) &&
               TryResolveArtifactReplacementDownload(libraryName, launcherFolder, runtimeArchitecture, out download);
    }

    private static bool TryResolveArtifactReplacementDownload(
        string libraryName,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out LibraryDownloadInfo download)
    {
        download = null!;
        var platformKey = ResolveNativeReplacementPlatformKey(runtimeArchitecture);
        if (string.IsNullOrWhiteSpace(platformKey))
        {
            return false;
        }

        var catalog = GetNativeReplacementCatalog();
        if (!catalog.TryGetValue(platformKey, out var platformCatalog) ||
            !platformCatalog.TryGetValue(libraryName, out var replacement))
        {
            return false;
        }

        var replacementTargetPath = Path.Combine(
            launcherFolder,
            "libraries",
            replacement.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var replacementUrl = string.IsNullOrWhiteSpace(replacement.DownloadUrl)
            ? "https://repo1.maven.org/maven2/" + replacement.ArtifactPath
            : replacement.DownloadUrl;

        download = new LibraryDownloadInfo(
            replacementTargetPath,
            replacementUrl,
            replacement.Sha1);
        return true;
    }

    private static string? ResolveNativeReplacementPlatformKey(MachineType runtimeArchitecture)
    {
        if (OperatingSystem.IsWindows())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "windows-arm64",
                MachineType.I386 => "windows-x86",
                MachineType.AMD64 => "windows-x64",
                _ => null
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "macos-arm64",
                MachineType.AMD64 => "macos-x64",
                _ => null
            };
        }

        if (OperatingSystem.IsLinux())
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "linux-arm64",
                MachineType.I386 => "linux-x86",
                MachineType.AMD64 => "linux-x64",
                _ => null
            };
        }

        return null;
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

    private static bool IsDedicatedNativeLibrary(JsonElement library)
    {
        return TryParseLibraryCoordinate(GetString(library, "name"), out var coordinate) &&
               !string.IsNullOrWhiteSpace(coordinate.Classifier) &&
               (coordinate.Classifier.StartsWith("natives-", StringComparison.OrdinalIgnoreCase) ||
                coordinate.Classifier.StartsWith("native-", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> GetNativeReplacementCatalog()
    {
        lock (NativeReplacementCatalogLock)
        {
            if (IsNativeReplacementCatalogCached)
            {
                return CachedNativeReplacementCatalog ?? EmptyNativeReplacementCatalog;
            }

            CachedNativeReplacementCatalog = LoadNativeReplacementCatalog();
            IsNativeReplacementCatalogCached = true;
            return CachedNativeReplacementCatalog;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> LoadNativeReplacementCatalog()
    {
        var catalogPath = FrontendLauncherAssetLocator.GetPath("NativeReplacements", "native-replacements.json");
        if (!File.Exists(catalogPath))
        {
            return EmptyNativeReplacementCatalog;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return EmptyNativeReplacementCatalog;
            }

            var platforms = new Dictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var platformProperty in document.RootElement.EnumerateObject())
            {
                if (platformProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var replacements = new Dictionary<string, ReplacementArtifactInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var replacementProperty in platformProperty.Value.EnumerateObject())
                {
                    if (TryParseReplacementArtifactInfo(replacementProperty.Value, out var replacement))
                    {
                        replacements[replacementProperty.Name] = replacement;
                    }
                }

                if (replacements.Count > 0)
                {
                    platforms[platformProperty.Name] = replacements;
                }
            }

            return platforms.Count == 0 ? EmptyNativeReplacementCatalog : platforms;
        }
        catch
        {
            return EmptyNativeReplacementCatalog;
        }
    }

    private static bool TryParseReplacementArtifactInfo(JsonElement element, out ReplacementArtifactInfo replacement)
    {
        replacement = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("artifact", out var artifact) ||
            artifact.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var path = GetString(artifact, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        replacement = new ReplacementArtifactInfo(
            GetString(element, "name"),
            path,
            GetString(artifact, "url"),
            GetString(artifact, "sha1"));
        return true;
    }

    private static string? ResolveNativeEntryName(JsonElement library, MachineType runtimeArchitecture)
    {
        var osKey = GetCurrentNativeOsKey();
        if (library.TryGetProperty("natives", out var natives) &&
            natives.ValueKind == JsonValueKind.Object &&
            natives.TryGetProperty(osKey, out var classifierValue) &&
            classifierValue.ValueKind == JsonValueKind.String)
        {
            return classifierValue.GetString()?.Replace(
                "${arch}",
                GetNativeArchitectureToken(runtimeArchitecture),
                StringComparison.Ordinal);
        }

        if (!library.TryGetProperty("downloads", out var downloads) ||
            downloads.ValueKind != JsonValueKind.Object ||
            !downloads.TryGetProperty("classifiers", out var classifiers) ||
            classifiers.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var candidate in GetCurrentNativeClassifierCandidates(runtimeArchitecture))
        {
            if (classifiers.TryGetProperty(candidate, out _))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetCurrentNativeClassifierCandidates(MachineType runtimeArchitecture)
    {
        var candidates = new List<string>();
        string[] osNames = OperatingSystem.IsMacOS()
            ? ["osx", "macos", "mac-os", "mac"]
            : OperatingSystem.IsWindows()
                ? ["windows"]
                : ["linux"];
        var archNames = runtimeArchitecture switch
        {
            MachineType.I386 => new[] { "x86", "32" },
            MachineType.ARM64 => new[] { "arm64", "aarch64", "64" },
            _ => new[] { "x86_64", "amd64", "64" }
        };

        foreach (var osName in osNames)
        {
            candidates.Add($"natives-{osName}");
            candidates.Add($"native-{osName}");
            foreach (var archName in archNames)
            {
                candidates.Add($"natives-{osName}-{archName}");
                candidates.Add($"native-{osName}-{archName}");
            }
        }

        return candidates;
    }

    private static string GetCurrentNativeOsKey()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "osx";
        }

        return "linux";
    }

    private static string GetNativeArchitectureToken(MachineType runtimeArchitecture)
    {
        return runtimeArchitecture == MachineType.I386 ? "32" : "64";
    }

    private static IReadOnlyList<string> GetExtractExcludes(JsonElement library)
    {
        if (!library.TryGetProperty("extract", out var extract) ||
            extract.ValueKind != JsonValueKind.Object ||
            !extract.TryGetProperty("exclude", out var exclude) ||
            exclude.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return exclude.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Replace('\\', '/'))
            .ToArray();
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

    private sealed record ReplacementArtifactInfo(
        string? ReplacementName,
        string ArtifactPath,
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

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>> EmptyNativeReplacementCatalog =
        new Dictionary<string, IReadOnlyDictionary<string, ReplacementArtifactInfo>>(StringComparer.OrdinalIgnoreCase);

    private static string BuildLibraryUrl(JsonElement library, string relativePath)
    {
        var baseUrl = GetString(library, "url");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://libraries.minecraft.net/";
        }

        return $"{baseUrl.TrimEnd('/')}/{relativePath.Replace('\\', '/')}";
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

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        return new YamlFileProvider(Path.Combine(instanceDirectory, "PCL", "config.v1.yml"));
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
        return $"已按实例设置忽略 Java 兼容性检查，当前使用 {runtimeLabel}。推荐范围：{javaWorkflow.MinimumVersion} - {javaWorkflow.MaximumVersion}";
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

    private static FrontendJavaRuntimeSummary? ToRuntimeSummaryOrNull(FrontendStoredJavaRuntime? runtime)
    {
        return runtime is null ? null : ToRuntimeSummary(runtime);
    }

    private readonly record struct FrontendConfiguredJavaSelection(
        bool FollowGlobal,
        string RawSelection);

    private readonly record struct FrontendJavaSelectionResult(
        FrontendJavaRuntimeSummary? Runtime,
        string? WarningMessage);

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

    private static int? TryParseJavaMajorVersion(string output)
    {
        return GetMajorVersion(TryParseJavaVersion(output));
    }

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

    private static Version? TryParseVanillaVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var candidate = rawValue.Trim();
        if (candidate.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[1..];
        }

        var filtered = new string(candidate
            .TakeWhile(character => char.IsDigit(character) || character == '.')
            .ToArray());
        return Version.TryParse(filtered, out var version)
            ? version
            : null;
    }

    private static string DeriveLibraryPathFromName(string libraryName, string? classifier = null)
    {
        var segments = libraryName.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return libraryName.Replace(':', Path.DirectorySeparatorChar);
        }

        var groupPath = segments[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = segments[1];
        var version = segments[2];
        var effectiveClassifier = string.IsNullOrWhiteSpace(classifier)
            ? (segments.Length >= 4 ? segments[3] : null)
            : classifier;
        var classifierSuffix = string.IsNullOrWhiteSpace(effectiveClassifier) ? string.Empty : "-" + effectiveClassifier;
        return Path.Combine(groupPath, artifact, version, $"{artifact}-{version}{classifierSuffix}.jar");
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
        bool HasCleanroom,
        bool HasFabric,
        bool HasLiteLoader,
        bool HasLabyMod,
        int? JsonRequiredMajorVersion,
        int? MojangRecommendedMajorVersion,
        string? MojangRecommendedComponent)
    {
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
            false,
            false,
            false,
            false,
            null,
            null,
            null);
    }
}
