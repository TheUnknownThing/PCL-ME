using Avalonia;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDesktopSettingsService
{
    internal const string KeepRunningInTrayKey = "UiKeepRunningInTray";
    internal const string PetEnabledKey = "UiDesktopPetEnabled";
    internal const string PetTopmostKey = "UiDesktopPetTopmost";
    internal const string PetScaleKey = "UiDesktopPetScale";
    internal const string PetXKey = "UiDesktopPetX";
    internal const string PetYKey = "UiDesktopPetY";

    public static FrontendDesktopSettings Read(YamlFileProvider config) => new(
        ReadValue(config, KeepRunningInTrayKey, false),
        ReadValue(config, PetEnabledKey, false),
        ReadValue(config, PetTopmostKey, true),
        NormalizeScale(ReadValue(config, PetScaleKey, 1d)));

    public static PixelPoint? ReadPosition(YamlFileProvider config) =>
        config.Exists(PetXKey) && config.Exists(PetYKey)
            ? new PixelPoint(config.Get<int>(PetXKey), config.Get<int>(PetYKey))
            : null;

    public static double NormalizeScale(double value) => double.IsFinite(value)
        ? Math.Clamp(value, 0.5, 2)
        : 1;

    public static PixelPoint RestorePosition(
        PixelPoint? saved,
        PixelSize size,
        IReadOnlyList<PixelRect> workingAreas,
        PixelRect fallbackArea)
    {
        // Negative coordinates are valid on monitors to the left or above the primary.
        var area = saved is { } point
            ? workingAreas.FirstOrDefault(candidate => candidate.Contains(point))
            : default;
        if (area.Width <= 0 || area.Height <= 0)
        {
            area = fallbackArea;
            saved = null;
        }

        var x = saved?.X ?? area.Right - size.Width - 24;
        var y = saved?.Y ?? area.Bottom - size.Height - 24;
        return new PixelPoint(
            Math.Clamp(x, area.X, Math.Max(area.X, area.Right - size.Width)),
            Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - size.Height)));
    }

    private static T ReadValue<T>(YamlFileProvider config, string key, T fallback) =>
        config.Exists(key) ? config.Get<T>(key) : fallback;
}
