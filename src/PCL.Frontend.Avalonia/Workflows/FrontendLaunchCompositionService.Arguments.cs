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
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendLaunchCompositionService
{
    private static MinecraftLaunchArgumentPlan BuildArgumentPlan(
        FrontendRuntimePaths runtimePaths,
        string launcherFolder,
        string selectedInstanceName,
        FrontendLaunchManifestContext manifestContext,
        string indieDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        FrontendRetroWrapperOptions retroWrapperOptions,
        MinecraftLaunchReplacementValuePlan replacementPlan,
        bool allowBlockingPreparation)
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
            manifestContext,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            instanceConfig,
            sharedConfig,
            retroWrapperOptions,
            effectiveJvmArguments,
            replacementPlan.Values,
            indieDirectory,
            javaMajorVersion,
            allowBlockingPreparation);

        var legacyMinecraftArguments = ReadManifestProperty(manifestContext, "minecraftArguments");
        if (!string.IsNullOrWhiteSpace(legacyMinecraftArguments))
        {
            arguments += " " + MinecraftLaunchGameArgumentService.BuildLegacyPlan(
                new MinecraftLaunchLegacyGameArgumentRequest(
                    legacyMinecraftArguments,
                    UseRetroWrapper: retroWrapperOptions.UseRetroWrapper,
                    manifestSummary.HasForgeLike || manifestSummary.HasLiteLoader,
                    manifestSummary.HasOptiFine)).Arguments;
        }

        var modernGameSections = CollectArgumentSectionJsons(manifestContext, "game");
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
        string resolvedInstanceName,
        string instanceDirectory,
        string indieDirectory,
        string nativesDirectory,
        string nativeSearchPath,
        string nativeExtractionDirectory,
        string? nativePathAliasDirectory,
        int nativeArchiveCount,
        FrontendLaunchManifestContext manifestContext,
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
                manifestContext,
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
        var instanceName = resolvedInstanceName;
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
            manifestContext,
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
        var wrapperCommand = string.IsNullOrWhiteSpace(instanceConfig is null
                ? null
                : ReadValue(instanceConfig, "VersionAdvanceWrapper", string.Empty))
            ? ReadValue(localConfig, "LaunchAdvanceWrapper", string.Empty)
            : ReadValue(instanceConfig!, "VersionAdvanceWrapper", string.Empty);
        var effectiveWrapperCommand = ReplaceLaunchTokens(
            wrapperCommand,
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
                        launchEnvironmentOverrides,
                        NullIfWhiteSpace(globalCommand),
                        ReadValue(localConfig, "LaunchAdvanceRunWait", true),
                        NullIfWhiteSpace(instanceCommand),
                        instanceConfig is null || ReadValue(instanceConfig, "VersionAdvanceRunWait", true),
                        NullIfWhiteSpace(effectiveWrapperCommand)),
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
                    NullIfWhiteSpace(effectiveWrapperCommand),
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
        FrontendLaunchManifestContext manifestContext,
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
            manifestContext,
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
                StartedLogMessage: "Missing Java runtime, so no executable launch command has been generated yet.",
                AbortKillLogMessage: "Missing Java runtime, so there is no game process to terminate."),
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
        FrontendLaunchManifestContext manifestContext,
        FrontendVersionManifestSummary manifestSummary,
        FrontendLaunchProfileSummary selectedProfile,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        JsonFileProvider sharedConfig,
        FrontendRetroWrapperOptions retroWrapperOptions,
        string effectiveJvmArguments,
        IReadOnlyDictionary<string, string> replacementValues,
        string indieDirectory,
        int javaMajorVersion,
        bool allowBlockingPreparation)
    {
        var modernJvmSections = CollectArgumentSectionJsons(manifestContext, "jvm");
        var mainClass = ReadManifestProperty(manifestContext, "mainClass")
                        ?? "net.minecraft.client.main.Main";
        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var proxyOptions = ResolveProxyOptions(runtimePaths, sharedConfig, instanceConfig);
        var javaWrapperOptions = ResolveJavaWrapperOptions(launcherFolder, localConfig, instanceConfig);
        var debugLog4jConfigurationPath = ResolveDebugLog4jConfigurationPath(launcherFolder, indieDirectory, instanceConfig);
        var rendererAgentArgument = ResolveRendererAgentArgument(launcherFolder, localConfig, instanceConfig);
        var authlibInjectorArgument = ResolveAuthlibInjectorArgument(
            launcherFolder,
            selectedProfile,
            allowBlockingPreparation);
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
                    AuthlibInjectorArgument: authlibInjectorArgument,
                    DebugLog4jConfigurationFilePath: debugLog4jConfigurationPath,
                    RendererAgentArgument: rendererAgentArgument,
                    ProxyScheme: proxyOptions.Scheme,
                    ProxyHost: proxyOptions.Host,
                    ProxyPort: proxyOptions.Port,
                    ProxyUsername: proxyOptions.Username,
                    ProxyPassword: proxyOptions.Password,
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
                    AuthlibInjectorArgument: authlibInjectorArgument,
                    DebugLog4jConfigurationFilePath: debugLog4jConfigurationPath,
                    RendererAgentArgument: rendererAgentArgument,
                    ProxyScheme: proxyOptions.Scheme,
                    ProxyHost: proxyOptions.Host,
                    ProxyPort: proxyOptions.Port,
                    ProxyUsername: proxyOptions.Username,
                    ProxyPassword: proxyOptions.Password,
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
        FrontendLaunchManifestContext manifestContext,
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
                InheritedInstanceName: ReadManifestProperty(manifestContext, "inheritsFrom"),
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
        FrontendLaunchManifestContext manifestContext,
        string sectionName)
    {
        if (manifestContext.ChildFirstDocuments.Count == 0)
        {
            return [];
        }

        var results = new List<string>();
        foreach (var document in manifestContext.ChildFirstDocuments)
        {
            if (!document.Root.TryGetProperty("arguments", out var argumentsElement) ||
                argumentsElement.ValueKind != JsonValueKind.Object ||
                !argumentsElement.TryGetProperty(sectionName, out var sectionElement))
            {
                continue;
            }

            results.Add(sectionElement.ToString());
        }

        return results;
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
            MinecraftLaunchProfileKind.Auth => "Third-party",
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

}
