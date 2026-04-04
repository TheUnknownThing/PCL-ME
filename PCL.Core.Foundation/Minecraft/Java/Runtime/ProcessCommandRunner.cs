using System.Diagnostics;

namespace PCL.Core.Minecraft.Java.Runtime;

public sealed class ProcessCommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return new CommandResult(-1, string.Empty, string.Empty);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }
}
