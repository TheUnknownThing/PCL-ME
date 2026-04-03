using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record StartupSpikePlan(
    LauncherStartupWorkflowPlan StartupPlan,
    LauncherStartupConsentResult Consent);

internal sealed record LaunchSpikePlan(
    string Scenario,
    LaunchLoginSpikePlan LoginPlan,
    MinecraftJavaRuntimeRequestUrlPlan JavaRuntimeIndexRequestUrls,
    MinecraftJavaRuntimeManifestRequestPlan? JavaRuntimeManifestPlan,
    MinecraftJavaRuntimeDownloadWorkflowPlan? JavaRuntimeDownloadWorkflowPlan,
    MinecraftJavaRuntimeDownloadTransferPlan? JavaRuntimeTransferPlan,
    MinecraftLaunchJavaWorkflowPlan JavaWorkflow,
    MinecraftLaunchJavaSelectionOutcome InitialSelection,
    MinecraftLaunchJavaPromptOutcome AcceptedPromptOutcome,
    MinecraftLaunchJavaPostDownloadOutcome PostDownloadSelection,
    MinecraftLaunchResolutionPlan ResolutionPlan,
    MinecraftLaunchClasspathPlan ClasspathPlan,
    string NativesDirectory,
    MinecraftLaunchReplacementValuePlan ReplacementPlan,
    MinecraftLaunchArgumentPlan ArgumentPlan,
    MinecraftLaunchPrerunWorkflowPlan PrerunPlan,
    MinecraftLaunchSessionStartWorkflowPlan SessionStartPlan,
    MinecraftLaunchScriptExportPlan? ScriptExportPlan,
    MinecraftGameShellPlan PostLaunchShell,
    MinecraftLaunchNotification CompletionNotification);

internal sealed record CrashSpikePlan(
    MinecraftCrashOutputPrompt OutputPrompt,
    MinecraftCrashExportPlan ExportPlan);

internal sealed record SpikePlanBundle(
    StartupSpikePlan Startup,
    LaunchSpikePlan Launch,
    CrashSpikePlan Crash);
