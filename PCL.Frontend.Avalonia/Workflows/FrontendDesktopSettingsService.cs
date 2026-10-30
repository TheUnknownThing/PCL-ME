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
    internal const string PetUseCustomKey = "UiDesktopPetUseCustom";
    internal const string PetPresetKey = "UiDesktopPetPreset";
    internal const string PetImagePathKey = "UiDesktopPetImage";
    internal const string PetClickActionKey = "UiDesktopPetClickAction";
    internal const string PetDoubleClickActionKey = "UiDesktopPetDoubleClickAction";
    internal const string WindowCloseActionKey = "UiWindowCloseAction";

    // Legacy keys, read once for migration.
    internal const string PetAppearanceKey = "UiDesktopPetAppearance";

    internal const string PetXKey = "UiDesktopPetX";
    internal const string PetYKey = "UiDesktopPetY";

    internal static readonly string[] BuiltinAppearances = ["creeper", "slime", "grass_block", "enderman"];

    public static FrontendDesktopSettings Read(YamlFileProvider config)
    {
        var imagePath = ReadValue(config, PetImagePathKey, string.Empty);
        var keepRunningInTray = ReadValue(config, KeepRunningInTrayKey, false);
        var presetId = NormalizePreset(ReadValue(config, PetPresetKey, string.Empty),
            ReadValue(config, PetAppearanceKey, string.Empty));
        return new FrontendDesktopSettings(
            keepRunningInTray,
            ReadValue(config, PetEnabledKey, true),
            ReadValue(config, PetTopmostKey, true),
            NormalizeScale(ReadValue(config, PetScaleKey, 1d)),
            ReadValue(config, PetUseCustomKey, !string.IsNullOrWhiteSpace(imagePath)),
            presetId,
            imagePath,
            ReadAction(config, PetClickActionKey, PetAction.PlayAnimation),
            ReadAction(config, PetDoubleClickActionKey, PetAction.ShowLauncher),
            ReadCloseAction(config));
    }

    public static string NormalizePreset(string? value, string? legacyAppearance = null)
    {
        if (PetPresetCatalog.IsKnown(value)) return value!;
        if (PetPresetCatalog.IsKnown(legacyAppearance)) return legacyAppearance!;
        return PetPresetCatalog.DefaultId;
    }

    public static string NormalizeAppearance(string value) => BuiltinAppearances.Contains(value, StringComparer.Ordinal)
        ? value
        : "creeper";

    public static WindowCloseAction NormalizeCloseAction(string? value, WindowCloseAction fallback) =>
        Enum.TryParse<WindowCloseAction>(value, true, out var action) ? action : fallback;

    private static WindowCloseAction ReadCloseAction(YamlFileProvider config) =>
        config.Exists(WindowCloseActionKey)
            ? NormalizeCloseAction(config.Get<string>(WindowCloseActionKey), WindowCloseAction.Exit)
            : WindowCloseAction.MinimizeToTray;

    public static double NormalizeScale(double value) => double.IsFinite(value)
        ? Math.Clamp(value, 0.5, 2)
        : 1;

    public static PixelPoint? ReadPosition(YamlFileProvider config) =>
        config.Exists(PetXKey) && config.Exists(PetYKey)
            ? new PixelPoint(config.Get<int>(PetXKey), config.Get<int>(PetYKey))
            : null;

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

    private static PetAction ReadAction(YamlFileProvider config, string key, PetAction fallback) =>
        config.Exists(key) && Enum.TryParse<PetAction>(config.Get<string>(key), true, out var action)
            ? action
            : fallback;

    private static T ReadValue<T>(YamlFileProvider config, string key, T fallback) =>
        config.Exists(key) ? config.Get<T>(key) : fallback;
}
