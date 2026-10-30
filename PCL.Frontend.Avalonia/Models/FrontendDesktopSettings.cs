namespace PCL.Frontend.Avalonia.Models;

/// <summary>What the launcher does when its close button is used.</summary>
internal enum WindowCloseAction
{
    /// <summary>Hide the launcher and keep it reachable through the tray or the pet.</summary>
    MinimizeToTray,

    /// <summary>Close the launcher and end the process.</summary>
    Exit
}

internal sealed record FrontendDesktopSettings(
    bool KeepRunningInTray = false,
    bool PetEnabled = false,
    bool PetTopmost = true,
    double PetScale = 1,
    bool PetUseCustom = false,
    string PetPresetId = "creeper_gif",
    string PetImagePath = "",
    PetAction PetClickAction = PetAction.PlayAnimation,
    PetAction PetDoubleClickAction = PetAction.ShowLauncher,
    WindowCloseAction? CloseAction = null);
