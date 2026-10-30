namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendDesktopSettings(
    bool KeepRunningInTray = false,
    bool PetEnabled = false,
    bool PetTopmost = true,
    double PetScale = 1,
    string PetImagePath = "",
    string PetAppearance = "creeper");
