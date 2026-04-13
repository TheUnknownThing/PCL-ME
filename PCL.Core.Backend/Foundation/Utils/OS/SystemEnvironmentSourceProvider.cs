using System;

namespace PCL.Core.Utils.OS;

internal static class SystemEnvironmentSourceProvider
{
    private static ISystemEnvironmentSource _current = CreateDefault();

    public static ISystemEnvironmentSource Current => _current;

    internal static void SetCurrent(ISystemEnvironmentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _current = source;
    }

    internal static void Reset()
    {
        _current = CreateDefault();
    }

    private static ISystemEnvironmentSource CreateDefault()
    {
        return OperatingSystem.IsWindows()
            ? new WindowsSystemEnvironmentSource(SystemRuntimeInfoSourceProvider.Current)
            : new DefaultSystemEnvironmentSource(SystemRuntimeInfoSourceProvider.Current);
    }
}
