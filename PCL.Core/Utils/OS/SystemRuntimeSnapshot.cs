using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

internal sealed record SystemRuntimeSnapshot(
    Version OsVersion,
    Architecture OsArchitecture,
    bool Is64BitOperatingSystem,
    ulong TotalPhysicalMemoryBytes,
    ulong AvailablePhysicalMemoryBytes);
