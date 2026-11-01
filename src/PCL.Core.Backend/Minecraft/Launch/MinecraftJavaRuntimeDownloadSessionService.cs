namespace PCL.Core.Minecraft.Launch;

public static class MinecraftJavaRuntimeDownloadSessionService
{
    private static readonly IReadOnlyList<string> DefaultIgnoredSha1Hashes =
    [
        "12976a6c2b227cbac58969c1455444596c894656",
        "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    ];

    public static IReadOnlyList<string> GetDefaultIgnoredSha1Hashes()
    {
        return DefaultIgnoredSha1Hashes;
    }

    public static string GetRuntimeBaseDirectory(string minecraftRootDirectory, string componentKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentKey);

        if (IsWindowsStyleAbsolutePath(minecraftRootDirectory))
        {
            return $"{minecraftRootDirectory.TrimEnd('\\', '/')}\\runtime\\{componentKey}";
        }

        return Path.Combine(minecraftRootDirectory, "runtime", componentKey);
    }

    public static MinecraftJavaRuntimeDownloadStateTransitionPlan ResolveStateTransition(
        MinecraftJavaRuntimeDownloadSessionState state,
        string? trackedRuntimeDirectory)
    {
        return state switch
        {
            MinecraftJavaRuntimeDownloadSessionState.Finished => new MinecraftJavaRuntimeDownloadStateTransitionPlan(
                CleanupDirectoryPath: null,
                CleanupLogMessage: null,
                ShouldRefreshJavaInventory: true,
                ShouldClearTrackedRuntimeDirectory: true),
            MinecraftJavaRuntimeDownloadSessionState.Failed or MinecraftJavaRuntimeDownloadSessionState.Aborted
                when !string.IsNullOrWhiteSpace(trackedRuntimeDirectory) => new MinecraftJavaRuntimeDownloadStateTransitionPlan(
                    trackedRuntimeDirectory,
                    $"[Java] Download did not finish; cleaning up incomplete Java files: {trackedRuntimeDirectory}",
                    ShouldRefreshJavaInventory: false,
                    ShouldClearTrackedRuntimeDirectory: true),
            MinecraftJavaRuntimeDownloadSessionState.Failed or MinecraftJavaRuntimeDownloadSessionState.Aborted => new MinecraftJavaRuntimeDownloadStateTransitionPlan(
                CleanupDirectoryPath: null,
                CleanupLogMessage: null,
                ShouldRefreshJavaInventory: false,
                ShouldClearTrackedRuntimeDirectory: true),
            _ => MinecraftJavaRuntimeDownloadStateTransitionPlan.NoAction
        };
    }

    private static bool IsWindowsStyleAbsolutePath(string path)
    {
        return path.Length >= 3 &&
               char.IsLetter(path[0]) &&
               path[1] == ':' &&
               (path[2] == '\\' || path[2] == '/');
    }
}

public enum MinecraftJavaRuntimeDownloadSessionState
{
    Loading = 0,
    Finished = 1,
    Failed = 2,
    Aborted = 3
}

public sealed record MinecraftJavaRuntimeDownloadStateTransitionPlan(
    string? CleanupDirectoryPath,
    string? CleanupLogMessage,
    bool ShouldRefreshJavaInventory,
    bool ShouldClearTrackedRuntimeDirectory)
{
    public static MinecraftJavaRuntimeDownloadStateTransitionPlan NoAction { get; } = new(
        CleanupDirectoryPath: null,
        CleanupLogMessage: null,
        ShouldRefreshJavaInventory: false,
        ShouldClearTrackedRuntimeDirectory: false);
}
