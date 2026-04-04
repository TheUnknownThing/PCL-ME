using System.IO;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Utils.OS;

namespace PCL.Core.Link.Scaffolding.EasyTier;

public static class EasyTierMetadata
{
    public const string CurrentEasyTierVer = "2.5.0";

    public static string EasyTierFilePath => Path.Combine(Paths.SharedLocalData, "EasyTier",
        CurrentEasyTierVer,
        $"easytier-windows-{(SystemRuntimeInfoSourceProvider.Current.GetSnapshot().OsArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x86_64")}");
}
