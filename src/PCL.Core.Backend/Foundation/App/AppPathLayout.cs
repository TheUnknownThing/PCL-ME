using System;
using System.IO;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

public sealed class AppPathLayout
{
    private const string PortableMarkerFileName = "PCL.portable";

    public string DefaultDirectory { get; }
    public string PortableData { get; }
    public string Data { get; set; }
    public string SharedData { get; set; }
    public string OldSharedData { get; set; }
    public string SharedLocalData { get; set; }
    public string Temp { get; set; }
    public bool UsesPortableDataDirectory => PathsEqual(Data, PortableData);

    private readonly IAppEnvironment _environment;

    public AppPathLayout(IAppEnvironment environment, string name, string oldName, bool enableDebugOverrides)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);

        _environment = environment;
        DefaultDirectory = environment.DefaultDirectory;
        SharedData = GetSpecialPath(Special.ApplicationData, name);
        SharedLocalData = GetSpecialPath(Special.LocalApplicationData, name);
        Temp = Path.Combine(environment.TempPath, name);
        OldSharedData = GetSpecialPath(Special.ApplicationData, oldName);
        PortableData = Path.Combine(DefaultDirectory, "PCL");
        Data = ResolveDefaultDataPath(environment, PortableData, SharedLocalData);

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

    private static string ResolveDefaultDataPath(
        IAppEnvironment environment,
        string portableDataPath,
        string sharedLocalDataPath)
    {
        if (TryReadPortableModeOverride(environment, out var usePortableData))
        {
            return usePortableData
                ? portableDataPath
                : Path.Combine(sharedLocalDataPath, "PCL");
        }

        if (File.Exists(Path.Combine(environment.DefaultDirectory, PortableMarkerFileName)))
        {
            return portableDataPath;
        }

        return Path.Combine(sharedLocalDataPath, "PCL");
    }

    private static bool TryReadPortableModeOverride(IAppEnvironment environment, out bool usePortableData)
    {
        var value = environment.GetEnvironmentVariable("PCL_PORTABLE");
        if (string.IsNullOrWhiteSpace(value))
        {
            usePortableData = false;
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                usePortableData = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                usePortableData = false;
                return true;
            default:
                usePortableData = false;
                return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }

    private void _TryOverride(string key, ref string target)
    {
        var value = _environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) target = value;
    }
}
