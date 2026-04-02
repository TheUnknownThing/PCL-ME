using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.Minecraft.Java;

public sealed class SystemJavaRuntimeEnvironment : IJavaRuntimeEnvironment
{
    public static SystemJavaRuntimeEnvironment Current { get; } = new();

    public string ExecutableDirectory { get; } = Path.GetDirectoryName(Environment.ProcessPath!) ?? Environment.CurrentDirectory;
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsMacOS => OperatingSystem.IsMacOS();
    public bool Is64BitOperatingSystem => Environment.Is64BitOperatingSystem;
    public char PathListSeparator => Path.PathSeparator;
    public string JavaCommandName => IsWindows ? "java.exe" : "java";
    public string CommandLookupToolName => IsWindows ? "where" : "which";
    public string JavacExecutableName => IsWindows ? "javac.exe" : "javac";
    public string? JavaWindowExecutableName => IsWindows ? "javaw.exe" : null;
    public IReadOnlyList<string> JavaExecutableNames => IsWindows ? ["java.exe", "java"] : ["java"];

    public string GetFolderPath(Special folder) => Environment.GetFolderPath(folder);

    public string? GetEnvironmentVariable(string key) => Environment.GetEnvironmentVariable(key);

    public IEnumerable<string> GetFixedDriveRoots()
    {
        return DriveInfo.GetDrives()
            .Where(static drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(static drive => drive.Name);
    }
}
