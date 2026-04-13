using System.Collections.Generic;

namespace PCL.Core.Minecraft;

public sealed record MinecraftCrashExportResult(
    string ReportDirectory,
    IReadOnlyList<string> WrittenFiles);
