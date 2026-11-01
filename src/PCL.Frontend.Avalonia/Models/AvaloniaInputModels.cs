using PCL.Frontend.Avalonia.Workflows;
using PCL.Core.Minecraft;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record CrashAvaloniaInputs(
    MinecraftCrashOutputPromptRequest OutputPromptRequest,
    MinecraftCrashExportPlanRequest ExportPlanRequest,
    II18nService? I18n = null);
