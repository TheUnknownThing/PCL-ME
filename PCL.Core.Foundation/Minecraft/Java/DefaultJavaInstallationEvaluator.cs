using System;
using System.IO;

namespace PCL.Core.Minecraft.Java;

public sealed class DefaultJavaInstallationEvaluator(IJavaRuntimeEnvironment runtime) : IJavaInstallationEvaluator
{
    private static readonly string[] RuntimeMarkers =
    [
        "lib/modules",
        "lib/rt.jar",
        "lib/jvm.lib",
        "lib/server/libjvm.so",
        "lib/jli/libjli.dylib",
        "jmods"
    ];

    public bool ShouldEnableByDefault(JavaInstallation installation)
    {
        var javaHome = Directory.GetParent(installation.JavaFolder)?.FullName;
        if (javaHome == null) return false;

        var isUsable = false;
        foreach (var marker in RuntimeMarkers)
        {
            var fullPath = Path.Combine(javaHome, marker.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
            {
                isUsable = true;
                break;
            }
        }

        return !((installation.IsJre && installation.MajorVersion > 8) ||
                 (installation.Is64Bit ^ runtime.Is64BitOperatingSystem) ||
                 !isUsable);
    }
}
