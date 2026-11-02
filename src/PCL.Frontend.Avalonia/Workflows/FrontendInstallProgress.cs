namespace PCL.Frontend.Avalonia.Workflows;

internal enum FrontendInstanceRepairFileGroup
{
    Client,
    Libraries,
    AssetIndex,
    Assets
}

internal sealed record FrontendInstanceRepairGroupSnapshot(
    FrontendInstanceRepairFileGroup Group,
    int CompletedFiles,
    int TotalFiles,
    long CompletedBytes,
    long TotalBytes,
    string CurrentFileName)
{
    public double Progress => TotalBytes > 0
        ? Math.Clamp((double)CompletedBytes / TotalBytes, 0d, 1d)
        : TotalFiles > 0
            ? Math.Clamp((double)CompletedFiles / TotalFiles, 0d, 1d)
            : 1d;
}

internal sealed record FrontendInstanceRepairProgressSnapshot(
    IReadOnlyDictionary<FrontendInstanceRepairFileGroup, FrontendInstanceRepairGroupSnapshot> Groups,
    string CurrentFileName,
    int DownloadedFileCount,
    int ReusedFileCount,
    int TotalFileCount,
    int RemainingFileCount,
    long DownloadedBytes,
    long TotalBytes,
    double SpeedBytesPerSecond)
{
    public double Progress => TotalBytes > 0
        ? Math.Clamp((double)DownloadedBytes / TotalBytes, 0d, 1d)
        : TotalFileCount > 0
            ? Math.Clamp((double)(DownloadedFileCount + ReusedFileCount) / TotalFileCount, 0d, 1d)
            : 1d;
}
