using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

public interface IAppEnvironment
{
    string DefaultDirectory { get; }
    string TempPath { get; }
    string GetFolderPath(Special folder);
    string? GetEnvironmentVariable(string key);
}
