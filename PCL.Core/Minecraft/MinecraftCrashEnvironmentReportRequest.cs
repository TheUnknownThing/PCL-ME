using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft;

public sealed record MinecraftCrashEnvironmentReportRequest(
    string LauncherVersionName,
    string UniqueAddress,
    string? LauncherLogContent,
    string? LaunchScriptContent,
    SystemEnvironmentSnapshot Environment);
