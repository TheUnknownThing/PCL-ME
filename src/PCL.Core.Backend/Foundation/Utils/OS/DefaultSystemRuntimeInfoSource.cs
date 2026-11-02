using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

internal sealed class DefaultSystemRuntimeInfoSource : ISystemRuntimeInfoSource
{
    public SystemRuntimeSnapshot GetSnapshot()
    {
        return new SystemRuntimeSnapshot(
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            0,
            0);
    }
}
