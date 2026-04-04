using System;
using System.IO;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

public sealed class AppPathLayout
{
    public string DefaultDirectory { get; }
    public string Data { get; set; }
    public string SharedData { get; set; }
    public string OldSharedData { get; set; }
    public string SharedLocalData { get; set; }
    public string Temp { get; set; }

    private readonly IAppEnvironment _environment;

    public AppPathLayout(IAppEnvironment environment, string name, string oldName, bool enableDebugOverrides)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);

        _environment = environment;
        DefaultDirectory = environment.DefaultDirectory;
        Data = Path.Combine(DefaultDirectory, "PCL");
        SharedData = GetSpecialPath(Special.ApplicationData, name);
        SharedLocalData = GetSpecialPath(Special.LocalApplicationData, name);
        Temp = Path.Combine(environment.TempPath, name);
        OldSharedData = GetSpecialPath(Special.ApplicationData, oldName);

        if (enableDebugOverrides)
        {
            var data = Data;
            var sharedData = SharedData;
            var sharedLocalData = SharedLocalData;
            var temp = Temp;
            _TryOverride("PCL_PATH", ref data);
            _TryOverride("PCL_PATH_SHARED", ref sharedData);
            _TryOverride("PCL_PATH_LOCAL", ref sharedLocalData);
            _TryOverride("PCL_PATH_TEMP", ref temp);
            Data = data;
            SharedData = sharedData;
            SharedLocalData = sharedLocalData;
            Temp = temp;
        }

        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(SharedData);
        Directory.CreateDirectory(SharedLocalData);
        Directory.CreateDirectory(Temp);
    }

    public string GetSpecialPath(Special folder, string relative)
    {
        var folderPath = _environment.GetFolderPath(folder);
        return Path.Combine(folderPath, relative);
    }

    private void _TryOverride(string key, ref string target)
    {
        var value = _environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) target = value;
    }
}
