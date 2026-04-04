using System;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

/// <summary>
/// Application global paths.<br/>
/// <b>NOTE</b>: The behaviors of all path strings depends on <see cref="Path"/> API provided by
/// .NET standard library. You should use <see cref="Path"/> and other APIs relative to it to process
/// any path string from this service, rather than concat paths manually.
/// </summary>
public static class Paths
{
    private static readonly AppPathLayout _layout;

    /// <summary>
    /// The default directory used for relative path combining.
    /// </summary>
    public static string DefaultDirectory => _layout.DefaultDirectory;
    
    /// <summary>
    /// Per-instance data directory.
    /// </summary>
    public static string Data { get => _layout.Data; set => _layout.Data = value; }

    /// <summary>
    /// Shared synchronized data directory.
    /// </summary>
    public static string SharedData { get => _layout.SharedData; set => _layout.SharedData = value; }
    
    /// <summary>
    /// Shared synchronized data directory of old versions.<br/>
    /// Keep the value just for migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedData { get => _layout.OldSharedData; set => _layout.OldSharedData = value; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or downloaded back anytime.
    /// </summary>
    public static string SharedLocalData { get => _layout.SharedLocalData; set => _layout.SharedLocalData = value; }
    
    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is running).
    /// </summary>
    public static string Temp { get => _layout.Temp; set => _layout.Temp = value; }

    /// <summary>
    /// Get path string relative to a special folder.
    /// </summary>
    /// <param name="folder">the special folder</param>
    /// <param name="relative">the relative path</param>
    /// <returns>the path string relative to the special folder</returns>
    public static string GetSpecialPath(Special folder, string relative) => _layout.GetSpecialPath(folder, relative);

    static Paths()
    {
#if DEBUG
        const string name = "PCLCE_Debug";
        const string oldName = ".PCLCEDebug";
        const bool enableDebugOverrides = true;
#else
        const string name = "PCLCE";
        const string oldName = ".PCLCE";
        const bool enableDebugOverrides = false;
#endif
        _layout = new AppPathLayout(SystemAppEnvironment.Current, name, oldName, enableDebugOverrides);
    }
}
