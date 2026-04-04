using System.Text.Json;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Serialization;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class SpikeInputStore
{
    public static StartupSpikeInputs? LoadStartupInputs(string? inputRoot)
    {
        return LoadJson<StartupSpikeInputs>(ResolveInputPath(inputRoot, "startup.json"));
    }

    public static ShellSpikeInputs? LoadShellInputs(string? inputRoot)
    {
        return LoadJson<ShellSpikeInputs>(ResolveInputPath(inputRoot, "shell.json"));
    }

    public static LaunchSpikeInputs? LoadLaunchInputs(string? inputRoot)
    {
        return LoadJson<LaunchSpikeInputs>(ResolveInputPath(inputRoot, "launch.json"));
    }

    public static CrashSpikeInputs? LoadCrashInputs(string? inputRoot)
    {
        return LoadJson<CrashSpikeInputs>(ResolveInputPath(inputRoot, "crash.json"));
    }

    public static SpikeExecutionArtifact SaveStartupInputs(string workspaceRoot, StartupSpikeInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "startup.json"), inputs, "Startup inputs");
    }

    public static SpikeExecutionArtifact SaveShellInputs(string workspaceRoot, ShellSpikeInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "shell.json"), inputs, "Shell inputs");
    }

    public static SpikeExecutionArtifact SaveLaunchInputs(string workspaceRoot, LaunchSpikeInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "launch.json"), inputs, "Launch inputs");
    }

    public static SpikeExecutionArtifact SaveCrashInputs(string workspaceRoot, CrashSpikeInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "crash.json"), inputs, "Crash inputs");
    }

    private static T? LoadJson<T>(string? path) where T : class
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var content = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(content, SpikeJson.CreateOptions());
    }

    private static SpikeExecutionArtifact SaveJson<T>(string path, T payload, string label)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = JsonSerializer.Serialize(payload, SpikeJson.CreateOptions());
        File.WriteAllText(path, content);
        return new SpikeExecutionArtifact(label, path);
    }

    private static string? ResolveInputPath(string? inputRoot, string fileName)
    {
        if (string.IsNullOrWhiteSpace(inputRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(inputRoot);
        return Directory.Exists(fullPath)
            ? Path.Combine(fullPath, fileName)
            : fullPath;
    }
}
