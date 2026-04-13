using System.Collections.Generic;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.Minecraft.Java;

public interface IJavaRuntimeEnvironment
{
    string ExecutableDirectory { get; }
    bool IsWindows { get; }
    bool IsLinux { get; }
    bool IsMacOS { get; }
    bool Is64BitOperatingSystem { get; }
    char PathListSeparator { get; }
    string JavaCommandName { get; }
    string CommandLookupToolName { get; }
    string JavacExecutableName { get; }
    string? JavaWindowExecutableName { get; }
    IReadOnlyList<string> JavaExecutableNames { get; }
    string GetFolderPath(Special folder);
    string? GetEnvironmentVariable(string key);
    IEnumerable<string> GetFixedDriveRoots();
}
