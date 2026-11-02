using System;

namespace PCL.Core.Utils.OS;

internal static class ProcessPlatformServiceProvider
{
    public static IProcessPlatformService Current { get; } =
        OperatingSystem.IsWindows() ? new WindowsProcessPlatformService() : new DefaultProcessPlatformService();
}
