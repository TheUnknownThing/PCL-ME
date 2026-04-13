using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS;

public sealed record SystemEnvironmentSnapshot(
    string OsDescription,
    Version OsVersion,
    Architecture OsArchitecture,
    bool Is64BitOperatingSystem,
    ulong TotalPhysicalMemoryBytes,
    string CpuName,
    IReadOnlyList<SystemGpuInfo> Gpus);
