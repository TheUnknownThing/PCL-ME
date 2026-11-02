using System.Collections.Generic;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft;

public sealed record MinecraftCrashExportRequest(
    string ReportDirectory,
    string LauncherVersionName,
    string UniqueAddress,
    IReadOnlyList<MinecraftCrashExportFile> SourceFiles,
    string? CurrentLauncherLogFilePath,
    SystemEnvironmentSnapshot Environment,
    string? CurrentAccessToken,
    string? CurrentUserUuid,
    string? UserProfilePath);
