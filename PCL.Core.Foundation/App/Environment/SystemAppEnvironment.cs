using System;
using System.IO;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

public sealed class SystemAppEnvironment : IAppEnvironment
{
    public static SystemAppEnvironment Current { get; } = new();

    public string DefaultDirectory { get; } = AppContext.BaseDirectory.TrimEnd(
                                                  Path.DirectorySeparatorChar,
                                                  Path.AltDirectorySeparatorChar);
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
