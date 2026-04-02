using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record StartupSpikeInputs(
    LauncherStartupWorkflowRequest StartupWorkflowRequest,
    LauncherStartupConsentRequest StartupConsentRequest);

internal sealed record LaunchSpikeInputs(
    string Scenario,
    LaunchLoginSpikeInputs LoginInputs,
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

internal sealed record CrashSpikeInputs(
    MinecraftCrashOutputPromptRequest OutputPromptRequest,
    MinecraftCrashExportPlanRequest ExportPlanRequest);
