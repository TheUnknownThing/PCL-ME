using System;

namespace PCL.Core.Utils.OS;

internal static class SystemRuntimeInfoSourceProvider
{
    public static ISystemRuntimeInfoSource Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsKernelSystemRuntimeInfoSource() : new DefaultSystemRuntimeInfoSource();
}
