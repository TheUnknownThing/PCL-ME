namespace PCL.Core.App.Essentials.Telemetry;

internal sealed class DefaultOfficialLauncherUsageProbe : IOfficialLauncherUsageProbe
{
    public bool HasUsedOfficialLauncher() => false;
}
