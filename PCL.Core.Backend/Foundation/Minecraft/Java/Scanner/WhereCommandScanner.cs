using PCL.Core.Logging;
using PCL.Core.Minecraft.Java.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.Java.Scanner;

public class WhereCommandScanner(IJavaRuntimeEnvironment? runtime = null, ICommandRunner? commandRunner = null) : IJavaScanner
{
    private readonly IJavaRuntimeEnvironment _runtime = runtime ?? SystemJavaRuntimeEnvironment.Current;
    private readonly ICommandRunner _commandRunner = commandRunner ?? new ProcessCommandRunner();

    public void Scan(ICollection<string> results)
    {
        try
        {
            var arguments = _runtime.IsWindows ? _runtime.JavaCommandName : $"-a {_runtime.JavaCommandName}";
            var command = _commandRunner.Run(_runtime.CommandLookupToolName, arguments);
            if (command.ExitCode != 0) return;

            var paths = command.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => File.Exists(p));

            foreach (var path in paths)
                results.Add(path);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "Command lookup scan failed.");
        }
    }
}
