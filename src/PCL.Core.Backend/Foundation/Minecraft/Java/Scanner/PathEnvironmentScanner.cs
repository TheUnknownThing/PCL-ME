using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Core.Minecraft.Java.Scanner;

public class PathEnvironmentScanner(IJavaRuntimeEnvironment? runtime = null) : IJavaScanner
{
    private readonly IJavaRuntimeEnvironment _runtime = runtime ?? SystemJavaRuntimeEnvironment.Current;

    public void Scan(ICollection<string> results)
    {
        try
        {
            var pathVar = _runtime.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return;

            foreach (var dir in pathVar.Split(_runtime.PathListSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var executableName in _runtime.JavaExecutableNames)
                {
                    var javaExe = Path.Combine(dir, executableName);
                    if (File.Exists(javaExe)) results.Add(javaExe);
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "PATH environment scan failed.");
        }
    }
}
