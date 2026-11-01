using Avalonia;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.DesktopPet;

internal sealed record PetSettings(
    bool PetTopmost = true, double PetScale = 1, bool PetUseCustom = false,
    string PetPresetId = "creeper_gif", string PetImagePath = "",
    PetAction PetClickAction = PetAction.PlayAnimation, PetAction PetDoubleClickAction = PetAction.ShowLauncher);

internal static class PetSettingsService
{
    public static PetSettings Read(IExtensionSettings settings) => new(
        settings.Get("topmost", true), NormalizeScale(settings.Get("scale", 1d)), settings.Get("custom", false),
        PetPresetCatalog.Find(settings.Get("preset", PetPresetCatalog.DefaultId)).Id, settings.Get("image", ""),
        Enum.TryParse<PetAction>(settings.Get("click", "PlayAnimation"), out var click) ? click : PetAction.PlayAnimation,
        Enum.TryParse<PetAction>(settings.Get("doubleClick", "ShowLauncher"), out var doubleClick) ? doubleClick : PetAction.ShowLauncher);

    public static double NormalizeScale(double value) => double.IsFinite(value) ? Math.Clamp(value, .5, 2) : 1;

    public static PixelPoint RestorePosition(PixelPoint? saved, PixelSize size, IReadOnlyList<PixelRect> workingAreas, PixelRect fallbackArea)
    {
        var area = saved is { } point ? workingAreas.FirstOrDefault(candidate => candidate.Contains(point)) : default;
        if (area.Width <= 0 || area.Height <= 0) { area = fallbackArea; saved = null; }
        return new PixelPoint(
            Math.Clamp(saved?.X ?? area.Right - size.Width - 24, area.X, Math.Max(area.X, area.Right - size.Width)),
            Math.Clamp(saved?.Y ?? area.Bottom - size.Height - 24, area.Y, Math.Max(area.Y, area.Bottom - size.Height)));
    }
}
