using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

internal sealed class DefaultSystemEnvironmentSource(ISystemRuntimeInfoSource runtimeInfoSource) : ISystemEnvironmentSource
{
    public SystemEnvironmentSnapshot GetSnapshot()
    {
        var runtime = runtimeInfoSource.GetSnapshot();
        return new SystemEnvironmentSnapshot(
            RuntimeInformation.OSDescription,
            runtime.OsVersion,
            runtime.OsArchitecture,
            runtime.Is64BitOperatingSystem,
            runtime.TotalPhysicalMemoryBytes,
            string.Empty,
            []);
    }
}
