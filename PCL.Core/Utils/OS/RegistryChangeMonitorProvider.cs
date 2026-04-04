using System;

namespace PCL.Core.Utils.OS;

internal static class RegistryChangeMonitorProvider
{
    public static IRegistryChangeMonitor Create(string keyPath)
    {
        return OperatingSystem.IsWindows()
            ? new WindowsRegistryChangeMonitor(keyPath)
            : new NoOpRegistryChangeMonitor();
    }
}
