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
    private static readonly HttpClient JavaRuntimeHttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));
    private static readonly object HostJavaProbeLock = new();
    private static IReadOnlyList<FrontendStoredJavaRuntime>? CachedHostJavaRuntimes;
    private static bool IsHostJavaProbeCached;
    public static FrontendLaunchComposition Compose(
        AvaloniaCommandOptions options,
        FrontendRuntimePaths runtimePaths,
        bool ignoreJavaCompatibilityWarningOnce = false,
        II18nService? i18n = null,
        bool allowBlockingPreparation = false)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var localConfig = runtimePaths.OpenLocalConfigProvider();

        var launcherFolder = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var downloadProvider = FrontendDownloadProvider.FromPreference(ReadValue(sharedConfig, "ToolDownloadSource", 1));
        var selectedInstanceName = string.IsNullOrWhiteSpace(options.InstanceNameOverride)
            ? ReadValue(localConfig, "LaunchInstanceSelect", string.Empty)
            : options.InstanceNameOverride.Trim();
        var candidateInstancePath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? string.Empty
            : Path.Combine(launcherFolder, "versions", selectedInstanceName);
        var hasSelectedInstance = FrontendRuntimePaths.IsRecognizedInstanceDirectory(candidateInstancePath);
        var instancePath = hasSelectedInstance
            ? candidateInstancePath
            : launcherFolder;
        var instanceConfig = hasSelectedInstance && Directory.Exists(instancePath)
            ? FrontendRuntimePaths.OpenInstanceConfigProvider(instancePath)
            : null;
        using var manifestContext = FrontendLaunchManifestContext.Load(launcherFolder, selectedInstanceName);
        var manifestSummary = ReadManifestSummary(launcherFolder, selectedInstanceName, instanceConfig, manifestContext);
        var indieDirectory = hasSelectedInstance && ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary)
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
            ignoreJavaCompatibilityWarningOnce,
            allowBlockingPreparation);
        var selectedJavaRuntime = javaSelection.Runtime;
        var retroWrapperOptions = ResolveRetroWrapperOptions(
            launcherFolder,
            manifestSummary,
            sharedConfig,
            instanceConfig);
        var requiredArtifacts = BuildRequiredArtifacts(
            launcherFolder,
            manifestContext,
            manifestSummary,
            selectedJavaRuntime);
        var resolvedInstanceName = ResolveSelectedInstanceName(selectedInstanceName, i18n);
        var resolutionPlan = MinecraftLaunchResolutionService.BuildPlan(BuildResolutionRequest(
            localConfig,
            CreateRuntimeResolutionFallback(),
            manifestSummary,
            selectedJavaRuntime,
            javaWorkflow));
        var classpathPlan = MinecraftLaunchClasspathService.BuildPlan(BuildClasspathRequest(
            launcherFolder,
            selectedInstanceName,
            manifestContext,
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
            manifestContext,
            manifestSummary,
            selectedJavaRuntime,
            nativesDirectory);
        var nativeSyncRequest = BuildNativeSyncRequest(
            launcherFolder,
            manifestContext,
            nativePathPlan.ExtractionDirectory,
            manifestSummary,
            selectedJavaRuntime);
        var versionType = ResolveVersionType(localConfig, instanceConfig, manifestSummary);
        var replacementPlan = MinecraftLaunchReplacementValueService.BuildPlan(new MinecraftLaunchReplacementValueRequest(
            ClasspathSeparator: GetClasspathSeparator(),
            NativesDirectory: nativePathPlan.SearchPath,
            LibraryDirectory: Path.Combine(launcherFolder, "libraries"),
            LibrariesDirectory: Path.Combine(launcherFolder, "libraries"),
            LauncherName: "PCLME",
            LauncherVersion: "frontend-avalonia",
            VersionName: resolvedInstanceName,
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
            AutoChangeLanguage: ReadValue(sharedConfig, "ToolHelpChinese", true)));
        var argumentPlan = BuildArgumentPlan(
            runtimePaths,
            launcherFolder,
            selectedInstanceName,
            manifestContext,
            indieDirectory,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            sharedConfig,
            instanceConfig,
            retroWrapperOptions,
            replacementPlan,
            allowBlockingPreparation);
        var sessionStartPlan = BuildSessionStartPlan(
            launcherFolder,
            selectedInstanceName,
            resolvedInstanceName,
            instancePath,
            indieDirectory,
            nativesDirectory,
            nativePathPlan.SearchPath,
            nativePathPlan.ExtractionDirectory,
            nativePathPlan.AliasDirectory,
            nativeSyncRequest?.NativeArchives.Count ?? 0,
            manifestContext,
            manifestSummary,
            selectedProfile,
            selectedJavaRuntime,
            localConfig,
            sharedConfig,
            instanceConfig,
            argumentPlan);
        var postLaunchShell = MinecraftLaunchShellService.GetPostLaunchShellPlan(
            new MinecraftLaunchPostLaunchShellRequest(
                ReadValue(sharedConfig, "LaunchArgumentVisible", LauncherVisibility.DoNothing)));
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
            InstanceName: resolvedInstanceName,
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
        var targetJavaRuntimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var javaRuntimeInstallPlan = allowBlockingPreparation
            ? BuildJavaRuntimeInstallPlan(
                javaWorkflow,
                manifestSummary,
                selectedJavaRuntime,
                launcherFolder,
                downloadProvider)
            : null;

        return new FrontendLaunchComposition(
            options.Scenario,
            resolvedInstanceName,
            instancePath,
            launcherFolder,
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
            downloadProvider.Preference,
            targetJavaRuntimeArchitecture,
            javaRuntimeInstallPlan,
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
                InstanceName: resolvedInstanceName,
                Outcome: MinecraftLaunchOutcome.Succeeded,
                IsScriptExport: false,
                AbortHint: null)));
    }

    private static string ResolveSelectedInstanceName(string? selectedInstanceName, II18nService? i18n)
    {
        return string.IsNullOrWhiteSpace(selectedInstanceName)
            ? Text(i18n, "instance.common.no_selection", "No instance selected")
            : selectedInstanceName;
    }

    private static string Text(II18nService? i18n, string key, string fallback)
    {
        return i18n?.T(key) ?? fallback;
    }

}
