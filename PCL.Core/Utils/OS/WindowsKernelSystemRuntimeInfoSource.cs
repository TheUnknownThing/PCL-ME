using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

internal sealed class WindowsKernelSystemRuntimeInfoSource : ISystemRuntimeInfoSource
{
    public SystemRuntimeSnapshot GetSnapshot()
    {
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemRuntimeSnapshot(
            Environment.OSVersion.Version,
            RuntimeInformation.OSArchitecture,
            Environment.Is64BitOperatingSystem,
            memory.Total,
            memory.Available);
    }
}
