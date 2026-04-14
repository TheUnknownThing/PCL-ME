using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLoaderVisibilityService
{
    public static bool IsLoaderVisible(string? loader, bool hideQuiltLoader)
    {
        if (!hideQuiltLoader)
        {
            return true;
        }

        return !string.Equals(loader, "Quilt", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> FilterVisibleLoaders(IEnumerable<string> loaders, bool hideQuiltLoader)
    {
        ArgumentNullException.ThrowIfNull(loaders);

        return loaders
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Where(loader => IsLoaderVisible(loader, hideQuiltLoader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsDownloadSubpageVisible(LauncherFrontendSubpageKey subpage, bool hideQuiltLoader)
    {
        return subpage != LauncherFrontendSubpageKey.DownloadQuilt || !hideQuiltLoader;
    }
}
