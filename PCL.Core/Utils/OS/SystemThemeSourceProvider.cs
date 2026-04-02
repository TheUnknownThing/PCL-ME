using System;

namespace PCL.Core.Utils.OS;

internal static class SystemThemeSourceProvider
{
    public static ISystemThemeSource Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsRegistrySystemThemeSource() : new DefaultSystemThemeSource();
}
