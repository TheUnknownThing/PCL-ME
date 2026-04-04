using System;

namespace PCL.Core.App.Essentials.Telemetry;

internal static class OfficialLauncherUsageProbeProvider
{
    public static IOfficialLauncherUsageProbe Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsRegistryOfficialLauncherUsageProbe() : new DefaultOfficialLauncherUsageProbe();
}
