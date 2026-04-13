using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record StartupAvaloniaPlan(
    LauncherStartupWorkflowPlan StartupPlan,
    LauncherStartupConsentResult Consent);

internal sealed record CrashAvaloniaPlan(
    MinecraftCrashOutputPrompt OutputPrompt,
    MinecraftCrashExportPlan ExportPlan);
