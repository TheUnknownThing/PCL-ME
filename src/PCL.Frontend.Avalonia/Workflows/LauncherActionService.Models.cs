using System.Diagnostics;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendCrashExportResult(
    string ArchivePath,
    int ArchivedFileCount);

internal sealed record FrontendJavaRuntimeInstallResult(
    string VersionName,
    string RuntimeDirectory,
    int DownloadedFileCount,
    int ReusedFileCount,
    string SummaryPath);

internal sealed record FrontendJavaRuntimeInstallProgressSnapshot(
    FrontendJavaRuntimeInstallPhase Phase,
    string CurrentFileRelativePath,
    int CompletedFileCount,
    int TotalFileCount,
    long DownloadedBytes,
    long TotalDownloadBytes,
    double SpeedBytesPerSecond);

internal enum FrontendJavaRuntimeInstallPhase
{
    Prepare = 0,
    Download = 1,
    Finalize = 2
}

internal sealed record FrontendLaunchStartResult(
    Process Process,
    string LaunchScriptPath,
    string SessionSummaryPath,
    string RawOutputLogPath);
