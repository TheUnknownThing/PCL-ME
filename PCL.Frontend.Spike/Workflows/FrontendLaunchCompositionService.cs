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
        var manifestSummary = ReadManifestSummary(launcherFolder, selectedInstanceName);
        var selectedProfile = ReadSelectedProfile(runtimePaths);
        var javaWorkflowRequest = BuildJavaWorkflowRequest(scenarioDefaults.JavaWorkflowRequest, manifestSummary);
        var javaWorkflow = MinecraftLaunchJavaWorkflowService.BuildPlan(javaWorkflowRequest);
        var selectedJavaRuntime = ResolveJavaRuntime(sharedConfig, launcherFolder, javaWorkflow);
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
            GameDirectory: launcherFolder,
            AssetsRoot: Path.Combine(launcherFolder, "assets"),
            UserProperties: "{}",
            AuthPlayerName: selectedProfile.UserName,
            AuthUuid: selectedProfile.Uuid ?? "unknown-uuid",
            AccessToken: selectedProfile.AccessToken ?? string.Empty,
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
            PrimaryOptionsFilePath: Path.Combine(launcherFolder, "options.txt"),
            PrimaryOptionsFileExists: File.Exists(Path.Combine(launcherFolder, "options.txt")),
            PrimaryCurrentLanguage: "zh_cn",
            YosbrOptionsFilePath: Path.Combine(launcherFolder, "config", "yosbr", "options.txt"),
            YosbrOptionsFileExists: File.Exists(Path.Combine(launcherFolder, "config", "yosbr", "options.txt")),
            HasExistingSaves: Directory.Exists(Path.Combine(launcherFolder, "saves")) &&
                              Directory.EnumerateFileSystemEntries(Path.Combine(launcherFolder, "saves")).Any(),
            ReleaseTime: manifestSummary.ReleaseTime ?? javaWorkflowRequest.ReleaseTime,
            LaunchWindowType: ReadValue(localConfig, "LaunchArgumentWindowType", (int)GameWindowSizeMode.Default),
            AutoChangeLanguage: false));
        var launchCount = LauncherFrontendRuntimeStateService.ReadProtectedInt(
            runtimePaths.SharedConfigDirectory,
            runtimePaths.SharedConfigPath,
            "SystemLaunchCount");
        var supportPrompt = MinecraftLaunchShellService.GetSupportPrompt(launchCount);
        var precheckRequest = new MinecraftLaunchPrecheckRequest(
            InstanceName: string.IsNullOrWhiteSpace(selectedInstanceName) ? "未选择实例" : selectedInstanceName,
            InstancePathIndie: instancePath,
            InstancePath: launcherFolder,
            IsInstanceSelected: !string.IsNullOrWhiteSpace(selectedInstanceName),
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: true,
            IsNonAsciiPathWarningDisabled: ReadValue(sharedConfig, "HintDisableGamePathCheckTip", false),
            IsInstancePathAscii: IsAscii(instancePath),
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
            plan.CompletionNotification);
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
        string launcherFolder,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var rawSelectedJava = ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty);
        var selectedJavaPath = NormalizeSelectedJavaPath(rawSelectedJava);
        var javaEntries = ParseJavaEntries(ReadValue(sharedConfig, "LaunchArgumentJavaUser", "[]"));

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
        return File.Exists(bundledJava)
            ? new FrontendJavaRuntimeSummary(
                bundledJava,
                $"Java {javaWorkflow.RecommendedMajorVersion}",
                javaWorkflow.RecommendedMajorVersion,
                IsEnabled: true,
                Is64Bit: Environment.Is64BitOperatingSystem)
            : null;
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

        return MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            new MinecraftJavaRuntimeManifestRequestPlanRequest(
                hostJavaInputs.IndexJson,
                hostJavaInputs.PlatformKey,
                javaWorkflow.MissingJavaPrompt.DownloadTarget,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
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
        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                hostJavaInputs.ManifestJson,
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

    private static string ReadFileOrDefault(string path, string fallback)
    {
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
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
