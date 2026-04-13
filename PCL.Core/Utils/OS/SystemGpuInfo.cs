namespace PCL.Core.Utils.OS;

public sealed record SystemGpuInfo(
    string Name,
    long MemoryMegabytes,
    string DriverVersion);
