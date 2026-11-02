namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendWindowManagerService
{
    public static bool IsTilingWindowManager()
    {
        return IsTilingWindowManager(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable);
    }

    internal static bool IsTilingWindowManager(bool isLinux, Func<string, string?> getEnvironmentVariable)
    {
        if (!isLinux)
        {
            return false;
        }

        // These markers also identify compositors used within another desktop session.
        foreach (var name in new[] { "NIRI_SOCKET", "SWAYSOCK", "HYPRLAND_INSTANCE_SIGNATURE", "I3SOCK" })
        {
            if (!string.IsNullOrWhiteSpace(getEnvironmentVariable(name)))
            {
                return true;
            }
        }

        // Prefer the current desktop over the login session, which may name a different WM.
        foreach (var name in new[] { "XDG_CURRENT_DESKTOP", "XDG_SESSION_DESKTOP", "DESKTOP_SESSION" })
        {
            var desktop = getEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(desktop))
            {
                continue;
            }

            return desktop.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(IsKnownTilingWindowManager);
        }

        return false;
    }

    private static bool IsKnownTilingWindowManager(string name)
    {
        return name.ToLowerInvariant() is "niri" or "sway" or "swayfx" or "hyprland"
            or "i3" or "i3wm" or "bspwm" or "awesome" or "dwm" or "xmonad"
            or "qtile" or "herbstluftwm" or "river" or "leftwm" or "spectrwm"
            or "ratpoison" or "stumpwm";
    }
}
