using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record StartupAvaloniaInputs(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest);

internal sealed record ShellAvaloniaInputs(
    StartupAvaloniaInputs StartupInputs,
    LauncherFrontendNavigationViewRequest NavigationRequest);

internal sealed record LaunchAvaloniaInputs(
    string Scenario,
    LaunchLoginAvaloniaInputs LoginInputs,
    JavaRuntimeAvaloniaInputs JavaRuntimeInputs,
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

internal sealed record JavaRuntimeAvaloniaInputs(
    string PlatformKey,
    string RuntimeBaseDirectory,
    string IndexJson,
    string ManifestJson,
    IReadOnlyList<string> IgnoredSha1Hashes,
    IReadOnlyList<string> ExistingRelativePaths);

internal sealed record CrashAvaloniaInputs(
    MinecraftCrashOutputPromptRequest OutputPromptRequest,
    MinecraftCrashExportPlanRequest ExportPlanRequest);
