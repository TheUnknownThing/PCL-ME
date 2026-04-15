using System.Globalization;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendDownloadTransferOptions(
    int MaxConcurrentFileTransfers,
    long? MaxBytesPerSecond);

internal static class FrontendDownloadSettingsService
{
    private const int DefaultThreadLimit = 63;
    private const int DefaultSpeedLimit = 42;

    public static FrontendDownloadTransferOptions ResolveTransferOptions(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        return ResolveTransferOptions(runtimePaths.OpenSharedConfigProvider());
    }

    public static FrontendDownloadTransferOptions ResolveTransferOptions(JsonFileProvider configProvider)
    {
        ArgumentNullException.ThrowIfNull(configProvider);

        var threadLimit = configProvider.Exists("ToolDownloadThread")
            ? configProvider.Get<int>("ToolDownloadThread")
            : DefaultThreadLimit;
        var speedLimit = configProvider.Exists("ToolDownloadSpeed")
            ? configProvider.Get<int>("ToolDownloadSpeed")
            : DefaultSpeedLimit;

        return new FrontendDownloadTransferOptions(
            ResolveMaxConcurrentFileTransfers(threadLimit),
            ResolveSpeedLimitBytesPerSecond(speedLimit));
    }

    public static int ResolveMaxConcurrentFileTransfers(double configuredValue)
    {
        return Math.Clamp((int)Math.Round(configuredValue, MidpointRounding.AwayFromZero), 1, 255);
    }

    public static long? ResolveSpeedLimitBytesPerSecond(double configuredValue)
    {
        var roundedValue = Math.Clamp((int)Math.Round(configuredValue, MidpointRounding.AwayFromZero), 0, 42);
        if (roundedValue <= 14)
        {
            return ToBytesPerSecond((roundedValue + 1) * 0.1d);
        }

        if (roundedValue <= 31)
        {
            return ToBytesPerSecond((roundedValue - 11) * 0.5d);
        }

        if (roundedValue <= 41)
        {
            return ToBytesPerSecond(roundedValue - 21);
        }

        return null;
    }

    public static string FormatThreadLimitLabel(double configuredValue)
    {
        return ResolveMaxConcurrentFileTransfers(configuredValue).ToString(CultureInfo.InvariantCulture);
    }

    public static string FormatSpeedLimitLabel(double configuredValue)
    {
        var roundedValue = Math.Clamp((int)Math.Round(configuredValue, MidpointRounding.AwayFromZero), 0, 42);
        return roundedValue switch
        {
            <= 14 => $"{(roundedValue + 1) * 0.1d:F1} M/s",
            <= 31 => $"{(roundedValue - 11) * 0.5d:F1} M/s",
            <= 41 => $"{roundedValue - 21} M/s",
            _ => "Unlimited"
        };
    }

    private static long ToBytesPerSecond(double megabytesPerSecond)
    {
        return (long)Math.Round(megabytesPerSecond * 1024d * 1024d, MidpointRounding.AwayFromZero);
    }
}
