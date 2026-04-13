using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record StartupAvaloniaPlan(
    LauncherStartupWorkflowPlan StartupPlan,
    LauncherStartupConsentResult Consent);

internal sealed record LaunchAvaloniaPlan(
    string Scenario,
    LaunchLoginAvaloniaPlan LoginPlan,
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
    MinecraftLaunchNotification CompletionNotification,
    string? NativePathAliasDirectory = null,
    string? NativeExtractionDirectory = null,
    int NativeArchiveCount = 0);

internal sealed record CrashAvaloniaPlan(
    MinecraftCrashOutputPrompt OutputPrompt,
    MinecraftCrashExportPlan ExportPlan);

internal sealed record AvaloniaPlanBundle(
    StartupAvaloniaPlan Startup,
    LaunchAvaloniaPlan Launch,
    CrashAvaloniaPlan Crash);
