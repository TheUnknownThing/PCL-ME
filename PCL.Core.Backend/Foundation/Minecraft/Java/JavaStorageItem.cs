using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java;

public class JavaStorageItem
{
    public required string Path { get; init; }
    public bool IsEnable { get; init; }
    public JavaSource? Source { get; init; }
    public JavaStorageInstallationInfo? Installation { get; init; }
}

public sealed class JavaStorageInstallationInfo
{
    public required string JavaExePath { get; init; }
    public string? DisplayName { get; init; }
    public string? Version { get; init; }
    public int? MajorVersion { get; init; }
    public bool? Is64Bit { get; init; }
    public bool? IsJre { get; init; }
    public JavaBrandType? Brand { get; init; }
    public MachineType? Architecture { get; init; }
}
