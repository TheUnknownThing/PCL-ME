using PCL.Core.Minecraft;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record CrashAvaloniaInputs(
    MinecraftCrashOutputPromptRequest OutputPromptRequest,
    MinecraftCrashExportPlanRequest ExportPlanRequest);
