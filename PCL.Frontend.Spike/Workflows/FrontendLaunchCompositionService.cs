using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows.Inspection;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendLaunchCompositionService
{
    private static readonly HttpClient JavaRuntimeHttpClient = new();

    public static FrontendLaunchComposition Compose(
        SpikeCommandOptions options,
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
        var inspectionDefaults = FrontendInspectionLaunchCompositionService.CreateRuntimeDefaults(options.Scenario);
        var scenarioDefaults = inspectionDefaults.ScenarioDefaults;
        var hostJavaInputs = inspectionDefaults.HostJavaInputs;

        var launcherFolder = ResolveLauncherFolder(ReadValue(localConfig, "LaunchFolderSelect", "$.minecraft\\"), runtimePaths);
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty);
        var instancePath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? Path.Combine(launcherFolder, "versions")
            : Path.Combine(launcherFolder, "versions", selectedInstanceName);
        var instanceConfig = Directory.Exists(instancePath)
            ? OpenInstanceConfigProvider(instancePath)
            : null;
        var indieDirectory = instanceConfig is not null && ReadValue(instanceConfig, "VersionArgumentIndieV2", false)
            ? instancePath
            : launcherFolder;
        var manifestSummary = ReadManifestSummary(launcherFolder, selectedInstanceName);
        var selectedProfile = ReadSelectedProfile(runtimePaths);
        var javaWorkflowRequest = BuildJavaWorkflowRequest(scenarioDefaults.JavaWorkflowRequest, manifestSummary);
        var javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(javaWorkflowRequest);
        var selectedJavaRuntime = ResolveJavaRuntime(sharedConfig, localConfig, instanceConfig, launcherFolder, javaWorkflow);
        var resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(BuildResolutionRequest(
            localConfig,
            scenarioDefaults.ResolutionRequest,
            manifestSummary,
            selectedJavaRuntime,
            javaWorkflow));
        var classpathPlan = MinecraftLaunchClasspathService.BuildPlan(BuildClasspathRequest(
            launcherFolder,
            selectedInstanceName,
            manifestSummary));
        var nativesDirectory = MinecraftLaunchNativesDirectoryService.ResolvePath(new MinecraftLaunchNativesDirectoryRequest(
            PreferredInstanceDirectory: Path.Combine(instancePath, $"{selectedInstanceName}-natives"),
            PreferInstanceDirectory: false,
            AppDataNativesDirectory: Path.Combine(launcherFolder, "bin", "natives"),
            FinalFallbackDirectory: Path.Combine(runtimePaths.TempDirectory, "PCL", "natives")));
        var replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(new MinecraftLaunchReplacementValueRequest(
            ClasspathSeparator: GetClasspathSeparator(),
            NativesDirectory: nativesDirectory,
            LibraryDirectory: Path.Combine(launcherFolder, "libraries"),
            LibrariesDirectory: Path.Combine(launcherFolder, "libraries"),
            LauncherName: "PCLCE",
            LauncherVersion: "frontend-spike",
            VersionName: string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            VersionType: manifestSummary.VersionType ?? "PCL CE",
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
            ClientToken: selectedProfile.ClientToken ?? "frontend-spike",
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
            launcherFolder,
            selectedInstanceName,
            indieDirectory,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            sharedConfig,
            instanceConfig,
            replacementPlan);
        var sessionStartPlan = BuildSessionStartPlan(
            launcherFolder,
            selectedInstanceName,
            instancePath,
            indieDirectory,
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
            LoginRequirement: MinecraftLaunchLoginRequirement.None,
            RequiredAuthServer: null,
            SelectedAuthServer: selectedProfile.AuthServer,
            HasMicrosoftProfile: selectedProfile.HasMicrosoftProfile,
            IsRestrictedFeatureAllowed: true);
        var precheckResult = MinecraftLaunchPrecheckService.Evaluate(precheckRequest);
        var manifestPlan = BuildJavaRuntimeManifestPlan(hostJavaInputs, launcherFolder, javaWorkflow);
        var transferPlan = BuildJavaRuntimeTransferPlan(hostJavaInputs, launcherFolder, manifestPlan);

        return new FrontendLaunchComposition(
            options.Scenario,
            string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            instancePath,
            selectedProfile,
            selectedJavaRuntime,
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

    public static FrontendLaunchComposition FromSpikePlan(LaunchSpikePlan plan)
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
            new FrontendLaunchProfileSummary(
                selectedProfileKind,
                playerName,
                plan.ReplacementPlan.Values.TryGetValue("${auth_uuid}", out var uuid) ? uuid : null,
                plan.ReplacementPlan.Values.TryGetValue("${auth_access_token}", out var accessToken) ? accessToken : null,
                null,
                null,
                plan.LoginPlan.Provider == LaunchLoginProviderKind.Microsoft),
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
            plan.ReplacementPlan,
            plan.PrerunPlan,
            plan.ArgumentPlan,
            plan.SessionStartPlan,
            plan.PostLaunchShell,
            plan.CompletionNotification);
    }

    private static MinecraftLaunchArgumentPlan BuildArgumentPlan(
        string launcherFolder,
        string selectedInstanceName,
        string indieDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        MinecraftLaunchReplacementValuePlan replacementPlan)
    {
        var javaMajorVersion = selectedJavaRuntime?.MajorVersion
                               ?? manifestSummary.JsonRequiredMajorVersion
                               ?? manifestSummary.MojangRecommendedMajorVersion
                               ?? 8;
        var effectiveJvmArguments = string.IsNullOrWhiteSpace(instanceConfig is null
                ? null
                : ReadValue(instanceConfig, "VersionAdvanceJvm", string.Empty))
            ? ReadValue(localConfig, "LaunchAdvanceJvm", string.Empty)
            : ReadValue(instanceConfig!, "VersionAdvanceJvm", string.Empty);
        var arguments = BuildJvmArguments(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            sharedConfig,
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
                    UseRetroWrapper: false,
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
                            !Environment.Is64BitOperatingSystem)),
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
                manifestSummary,
                selectedProfile,
                selectedJavaRuntime,
                instanceConfig,
                localConfig);
        }

        var javaFolder = Path.GetDirectoryName(javaExecutablePath) ?? launcherFolder;
        var javawExecutablePath = OperatingSystem.IsWindows()
            ? Path.Combine(javaFolder, "javaw.exe")
            : javaExecutablePath;
        var instanceName = string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName;
        var watcherWorkflowRequest = BuildWatcherWorkflowRequest(
            launcherFolder,
            selectedInstanceName,
            instanceDirectory,
            indieDirectory,
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
            JavaMajorVersion = selectedJavaRuntime?.MajorVersion ?? javaWorkflow.RecommendedMajorVersion,
            HasOptiFine = manifestSummary.HasOptiFine,
            HasForge = manifestSummary.HasForge
        };
    }

    private static MinecraftLaunchClasspathRequest BuildClasspathRequest(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary)
    {
        var instanceJarPath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? null
            : Path.Combine(launcherFolder, "versions", selectedInstanceName, $"{selectedInstanceName}.jar");
        var customHeadEntries = string.IsNullOrWhiteSpace(instanceJarPath) || !File.Exists(instanceJarPath)
            ? Array.Empty<string>()
            : [instanceJarPath];

        return new MinecraftLaunchClasspathRequest(
            Libraries: manifestSummary.Libraries,
            CustomHeadEntries: customHeadEntries,
            RetroWrapperPath: null,
            ClasspathSeparator: GetClasspathSeparator());
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

    private static FrontendLaunchProfileSummary ReadSelectedProfile(FrontendRuntimePaths runtimePaths)
    {
        var profilesPath = Path.Combine(runtimePaths.LauncherAppDataDirectory, "profiles.json");
        if (!File.Exists(profilesPath))
        {
            return BuildFallbackProfile(runtimePaths);
        }

        try
        {
            var document = MinecraftLaunchProfileStorageService.ParseDocument(
                File.ReadAllText(profilesPath),
                value => LauncherFrontendRuntimeStateService.TryUnprotectString(
                    runtimePaths.SharedConfigDirectory,
                    value) ?? value ?? string.Empty);
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
            HasMicrosoftProfile: false);
    }

    private static FrontendJavaRuntimeSummary? ResolveJavaRuntime(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        string launcherFolder,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var rawSelectedJava = instanceConfig is not null && instanceConfig.Exists("VersionArgumentJavaSelect")
            ? NormalizeInstanceJavaSelection(ReadValue(instanceConfig, "VersionArgumentJavaSelect", string.Empty), launcherFolder)
            : ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty);
        var selectedJavaPath = NormalizeSelectedJavaPath(rawSelectedJava);
        var javaEntries = ParseJavaEntries(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));

        if (!string.IsNullOrWhiteSpace(selectedJavaPath))
        {
            var selectedEntry = javaEntries.FirstOrDefault(entry =>
                string.Equals(entry.ExecutablePath, selectedJavaPath, StringComparison.OrdinalIgnoreCase));
            if (selectedEntry is not null)
            {
                return selectedEntry.IsEnabled ? selectedEntry : null;
            }

            if (File.Exists(selectedJavaPath))
            {
                return new FrontendJavaRuntimeSummary(
                    selectedJavaPath,
                    Path.GetFileName(Path.GetDirectoryName(selectedJavaPath)) ?? $"Java {javaWorkflow.RecommendedMajorVersion}",
                    MajorVersion: null,
                    IsEnabled: true,
                    Is64Bit: null);
            }
        }

        var autoEntry = javaEntries.FirstOrDefault(entry => entry.IsEnabled);
        if (autoEntry is not null)
        {
            return autoEntry;
        }

        var bundledJava = OperatingSystem.IsWindows()
            ? Path.Combine(launcherFolder, "runtime", "java", "bin", "java.exe")
            : Path.Combine(launcherFolder, "runtime", "java", "bin", "java");
        if (File.Exists(bundledJava))
        {
            return new FrontendJavaRuntimeSummary(
                bundledJava,
                $"Java {javaWorkflow.RecommendedMajorVersion}",
                javaWorkflow.RecommendedMajorVersion,
                IsEnabled: true,
                Is64Bit: Environment.Is64BitOperatingSystem);
        }

        return ProbeHostJavaRuntime();
    }

    private static List<FrontendJavaRuntimeSummary> ParseJavaEntries(string rawJson)
    {
        var result = new List<FrontendJavaRuntimeSummary>();
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var executablePath = GetNestedString(item, "Installation", "JavaExePath");
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    continue;
                }

                var majorVersion = GetNestedInt(item, "Installation", "MajorVersion");
                var versionText = GetNestedString(item, "Installation", "Version");
                var isEnabled = GetBoolean(item, "IsEnabled") ?? true;
                var is64Bit = GetNestedBoolean(item, "Installation", "Is64Bit");
                var displayName = !string.IsNullOrWhiteSpace(versionText)
                    ? versionText
                    : majorVersion is { } major
                        ? $"Java {major}"
                        : Path.GetFileName(Path.GetDirectoryName(executablePath)) ?? "Java";

                result.Add(new FrontendJavaRuntimeSummary(
                    executablePath,
                    displayName,
                    majorVersion,
                    isEnabled,
                    is64Bit));
            }
        }
        catch
        {
            return result;
        }

        return result;
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

    private static MinecraftJavaRuntimeManifestRequestPlan? BuildJavaRuntimeManifestPlan(
        JavaRuntimeSpikeInputs hostJavaInputs,
        string launcherFolder,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        if (string.IsNullOrWhiteSpace(javaWorkflow.MissingJavaPrompt.DownloadTarget))
        {
            return null;
        }

        try
        {
            return MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
                new MinecraftJavaRuntimeManifestRequestPlanRequest(
                    hostJavaInputs.IndexJson,
                    hostJavaInputs.PlatformKey,
                    javaWorkflow.MissingJavaPrompt.DownloadTarget,
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        }
        catch
        {
            var liveIndexJson = TryDownloadUtf8String(MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan().AllUrls);
            return string.IsNullOrWhiteSpace(liveIndexJson)
                ? null
                : MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
                    new MinecraftJavaRuntimeManifestRequestPlanRequest(
                        liveIndexJson,
                        hostJavaInputs.PlatformKey,
                        javaWorkflow.MissingJavaPrompt.DownloadTarget,
                        MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
        }
    }

    private static MinecraftJavaRuntimeDownloadTransferPlan? BuildJavaRuntimeTransferPlan(
        JavaRuntimeSpikeInputs hostJavaInputs,
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
        var manifestJson = manifestPlan.RequestUrls.AllUrls.Any(url => url.Contains("example.invalid", StringComparison.OrdinalIgnoreCase))
            ? TryDownloadUtf8String(manifestPlan.RequestUrls.AllUrls)
            : TryDownloadUtf8String(manifestPlan.RequestUrls.AllUrls) ?? hostJavaInputs.ManifestJson;
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return null;
        }

        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                manifestJson,
                runtimeBaseDirectory,
                hostJavaInputs.IgnoredSha1Hashes,
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

    private static string ResolveLauncherFolder(string rawValue, FrontendRuntimePaths runtimePaths)
    {
        var normalized = string.IsNullOrWhiteSpace(rawValue)
            ? "$.minecraft\\"
            : rawValue.Trim();
        normalized = normalized.Replace("$", EnsureTrailingSeparator(runtimePaths.ExecutableDirectory), StringComparison.Ordinal);
        return Path.GetFullPath(normalized);
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

    private static string NormalizeInstanceJavaSelection(string rawValue, string launcherFolder)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "使用全局设置", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            var kind = GetString(document.RootElement, "kind")?.ToLowerInvariant();
            return kind switch
            {
                "exist" => GetString(document.RootElement, "JavaExePath") ?? string.Empty,
                "relative" => Path.Combine(launcherFolder, GetString(document.RootElement, "RelativePath") ?? string.Empty),
                _ => string.Empty
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static FrontendJavaRuntimeSummary? ProbeHostJavaRuntime()
    {
        foreach (var candidate in EnumerateHostJavaRuntimeCandidates())
        {
            var runtime = TryProbeJavaRuntime(candidate);
            if (runtime is not null)
            {
                return runtime;
            }
        }

        return null;
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

    private static FrontendJavaRuntimeSummary? TryProbeJavaRuntime(string executablePath)
    {
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
            var majorVersion = TryParseJavaMajorVersion(output);
            if (process.ExitCode != 0 ||
                majorVersion is null ||
                output.Contains("Unable to locate a Java Runtime", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("No Java runtime present", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new FrontendJavaRuntimeSummary(
                executablePath,
                $"Java {majorVersion.Value}",
                majorVersion.Value,
                IsEnabled: true,
                Is64Bit: Environment.Is64BitOperatingSystem);
        }
        catch
        {
            return null;
        }
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
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        JsonFileProvider sharedConfig,
        string effectiveJvmArguments,
        IReadOnlyDictionary<string, string> replacementValues,
        string indieDirectory,
        int javaMajorVersion)
    {
        var modernJvmSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "jvm");
        var mainClass = ReadManifestProperty(launcherFolder, selectedInstanceName, "mainClass")
                        ?? "net.minecraft.client.main.Main";
        return modernJvmSections.Count > 0
            ? MinecraftLaunchJvmArgumentService.BuildModernArguments(
                new MinecraftLaunchModernJvmArgumentRequest(
                    MinecraftLaunchJsonArgumentService.ExtractValues(
                        new MinecraftLaunchJsonArgumentRequest(
                            modernJvmSections,
                            Environment.OSVersion.Version.ToString(),
                            !Environment.Is64BitOperatingSystem)),
                    effectiveJvmArguments,
                    ReadValue(sharedConfig, "LaunchPreferredIpStack", JvmPreferredIpStack.Default),
                    ResolveYoungGenerationMemoryMegabytes(indieDirectory, selectedJavaRuntime),
                    ResolveAllocatedMemoryMegabytes(indieDirectory, selectedJavaRuntime),
                    UseRetroWrapper: false,
                    javaMajorVersion,
                    AuthlibInjectorArgument: null,
                    DebugLog4jConfigurationFilePath: null,
                    RendererAgentArgument: null,
                    ProxyScheme: null,
                    ProxyHost: null,
                    ProxyPort: null,
                    UseJavaWrapper: false,
                    JavaWrapperTempDirectory: null,
                    JavaWrapperPath: null,
                    MainClass: mainClass))
            : MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
                new MinecraftLaunchLegacyJvmArgumentRequest(
                    effectiveJvmArguments,
                    ResolveYoungGenerationMemoryMegabytes(indieDirectory, selectedJavaRuntime),
                    ResolveAllocatedMemoryMegabytes(indieDirectory, selectedJavaRuntime),
                    replacementValues["${natives_directory}"],
                    javaMajorVersion,
                    AuthlibInjectorArgument: null,
                    DebugLog4jConfigurationFilePath: null,
                    RendererAgentArgument: null,
                    ProxyScheme: null,
                    ProxyHost: null,
                    ProxyPort: null,
                    UseJavaWrapper: false,
                    JavaWrapperTempDirectory: null,
                    JavaWrapperPath: null,
                    MainClass: mainClass));
    }

    private static MinecraftLaunchWatcherWorkflowRequest BuildWatcherWorkflowRequest(
        string launcherFolder,
        string selectedInstanceName,
        string instanceDirectory,
        string indieDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider? instanceConfig,
        YamlFileProvider localConfig)
    {
        var javaFolder = selectedJavaRuntime?.ExecutablePath is null
            ? launcherFolder
            : Path.GetDirectoryName(selectedJavaRuntime.ExecutablePath) ?? launcherFolder;
        return new MinecraftLaunchWatcherWorkflowRequest(
            new MinecraftLaunchSessionLogRequest(
                LauncherVersionName: "frontend-spike",
                LauncherVersionCode: 1,
                GameVersionDisplayName: manifestSummary.VanillaVersion?.ToString() ?? selectedInstanceName,
                GameVersionRaw: manifestSummary.VanillaVersion?.ToString() ?? selectedInstanceName,
                GameVersionDrop: 0,
                IsGameVersionReliable: manifestSummary.IsVersionInfoValid,
                AssetsIndexName: manifestSummary.AssetsIndexName ?? "legacy",
                InheritedInstanceName: ReadManifestProperty(launcherFolder, selectedInstanceName, "inheritsFrom"),
                AllocatedMemoryInGigabytes: ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime),
                MinecraftFolder: launcherFolder,
                InstanceFolder: instanceDirectory,
                IsVersionIsolated: !string.Equals(indieDirectory, launcherFolder, StringComparison.OrdinalIgnoreCase),
                IsHmclFormatJson: false,
                JavaDescription: selectedJavaRuntime?.DisplayName,
                NativesFolder: Path.Combine(indieDirectory, "natives"),
                PlayerName: selectedProfile.UserName,
                AccessToken: ResolveAccessToken(selectedProfile),
                ClientToken: selectedProfile.ClientToken ?? "frontend-spike",
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

    private static int ResolveAllocatedMemoryMegabytes(string indieDirectory, FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        return (int)Math.Round(ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime) * 1024d);
    }

    private static int ResolveYoungGenerationMemoryMegabytes(string indieDirectory, FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        return (int)Math.Round(ResolveAllocatedMemoryGigabytes(indieDirectory, selectedJavaRuntime) * 1024d * 0.15d);
    }

    private static double ResolveAllocatedMemoryGigabytes(string indieDirectory, FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var totalMemoryBytes = gcInfo.TotalAvailableMemoryBytes > 0
            ? gcInfo.TotalAvailableMemoryBytes
            : 8L * 1024L * 1024L * 1024L;
        var totalMemoryGb = totalMemoryBytes / 1024d / 1024d / 1024d;
        var availableMemoryGb = Math.Max(totalMemoryBytes - GC.GetTotalMemory(forceFullCollection: false), 0L) / 1024d / 1024d / 1024d;
        var modsDirectory = Path.Combine(indieDirectory, "mods");
        var modCount = Directory.Exists(modsDirectory)
            ? Directory.EnumerateFiles(modsDirectory, "*", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var allocatedMemoryGb = Math.Min(Math.Max(1.5 + modCount / 90.0, 0.5), Math.Max(availableMemoryGb, 1.0));
        if (selectedJavaRuntime?.Is64Bit == false)
        {
            allocatedMemoryGb = Math.Min(1.0, allocatedMemoryGb);
        }

        return Math.Round(Math.Min(allocatedMemoryGb, Math.Max(totalMemoryGb, 1.0)), 1);
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

    private static int? TryParseJavaMajorVersion(string output)
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
        if (rawVersion.StartsWith("1.", StringComparison.Ordinal))
        {
            return int.TryParse(rawVersion.Split('.')[1], out var legacyMajorVersion)
                ? legacyMajorVersion
                : null;
        }

        return int.TryParse(rawVersion.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), out var majorVersion)
            ? majorVersion
            : null;
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

    private static string DeriveLibraryPathFromName(string libraryName)
    {
        var segments = libraryName.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return libraryName.Replace(':', Path.DirectorySeparatorChar);
        }

        var groupPath = segments[0].Replace('.', Path.DirectorySeparatorChar);
        var artifact = segments[1];
        var version = segments[2];
        var classifier = segments.Length >= 4 ? "-" + segments[3] : string.Empty;
        return Path.Combine(groupPath, artifact, version, $"{artifact}-{version}{classifier}.jar");
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
