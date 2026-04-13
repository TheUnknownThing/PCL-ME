using System;
using System.IO;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

public sealed class SystemAppEnvironment : IAppEnvironment
{
    public static SystemAppEnvironment Current { get; } = new();

    public string DefaultDirectory { get; } = Path.GetDirectoryName(Environment.ProcessPath!)
                                              ?? Environment.CurrentDirectory;
    public string TempPath => Path.GetTempPath();

    public string GetFolderPath(Special folder)
    {
        return Environment.GetFolderPath(folder);
    }

    public string? GetEnvironmentVariable(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }
}
