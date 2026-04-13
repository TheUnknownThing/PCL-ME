using System.Diagnostics;
using System.Threading.Tasks;

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

        // Drain both redirected streams concurrently to avoid pipe deadlocks.
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutputTask, standardErrorTask);
        return new CommandResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
    }
}
