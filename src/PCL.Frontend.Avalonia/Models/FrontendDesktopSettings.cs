namespace PCL.Frontend.Avalonia.Models;

internal enum WindowCloseAction { MinimizeToTray, Exit }

internal sealed record FrontendDesktopSettings(
    bool KeepRunningInTray = false,
    WindowCloseAction CloseAction = WindowCloseAction.MinimizeToTray);
