using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record StartupSpikeInputs(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest);

internal sealed record ShellSpikeInputs(
    StartupSpikeInputs StartupInputs,
    LauncherFrontendNavigationViewRequest NavigationRequest);

internal sealed record LaunchSpikeInputs(
    string Scenario,
    LaunchLoginSpikeInputs LoginInputs,
    JavaRuntimeSpikeInputs JavaRuntimeInputs,
    MinecraftLaunchJavaWorkflowRequest JavaWorkflowRequest,
    MinecraftLaunchResolutionRequest ResolutionRequest,
    MinecraftLaunchClasspathRequest ClasspathRequest,
    MinecraftLaunchNativesDirectoryRequest NativesDirectoryRequest,
    MinecraftLaunchReplacementValueRequest ReplacementValueRequest,
    MinecraftLaunchPrerunWorkflowRequest PrerunWorkflowRequest,
    MinecraftLaunchSessionStartWorkflowRequest SessionStartWorkflowRequest,
    MinecraftLaunchArgumentPlanRequest ArgumentPlanRequest,
    MinecraftLaunchPostLaunchShellRequest PostLaunchShellRequest,
    MinecraftLaunchCompletionRequest CompletionRequest);

internal sealed record JavaRuntimeSpikeInputs(
    string PlatformKey,
    string RuntimeBaseDirectory,
    string IndexJson,
    string ManifestJson,
    IReadOnlyList<string> IgnoredSha1Hashes,
    IReadOnlyList<string> ExistingRelativePaths);

internal sealed record CrashSpikeInputs(
    MinecraftCrashOutputPromptRequest OutputPromptRequest,
    MinecraftCrashExportPlanRequest ExportPlanRequest);
