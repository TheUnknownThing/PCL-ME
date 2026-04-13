using System.Text.Json;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Serialization;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class AvaloniaInputStore
{
    public static StartupAvaloniaInputs? LoadStartupInputs(string? inputRoot)
    {
        return LoadJson<StartupAvaloniaInputs>(ResolveInputPath(inputRoot, "startup.json"));
    }

    public static ShellAvaloniaInputs? LoadShellInputs(string? inputRoot)
    {
        return LoadJson<ShellAvaloniaInputs>(ResolveInputPath(inputRoot, "shell.json"));
    }

    public static LaunchAvaloniaInputs? LoadLaunchInputs(string? inputRoot)
    {
        return LoadJson<LaunchAvaloniaInputs>(ResolveInputPath(inputRoot, "launch.json"));
    }

    public static CrashAvaloniaInputs? LoadCrashInputs(string? inputRoot)
    {
        return LoadJson<CrashAvaloniaInputs>(ResolveInputPath(inputRoot, "crash.json"));
    }

    public static AvaloniaExecutionArtifact SaveStartupInputs(string workspaceRoot, StartupAvaloniaInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "startup.json"), inputs, "Startup inputs");
    }

    public static AvaloniaExecutionArtifact SaveShellInputs(string workspaceRoot, ShellAvaloniaInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "shell.json"), inputs, "Shell inputs");
    }

    public static AvaloniaExecutionArtifact SaveLaunchInputs(string workspaceRoot, LaunchAvaloniaInputs inputs)
    {
        return SaveJson(Path.Combine(workspaceRoot, "_inputs", "launch.json"), inputs, "Launch inputs");
    }

    public static AvaloniaExecutionArtifact SaveCrashInputs(string workspaceRoot, CrashAvaloniaInputs inputs)
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
        return JsonSerializer.Deserialize<T>(content, AvaloniaJson.CreateOptions());
    }

    private static AvaloniaExecutionArtifact SaveJson<T>(string path, T payload, string label)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = JsonSerializer.Serialize(payload, AvaloniaJson.CreateOptions());
        File.WriteAllText(path, content);
        return new AvaloniaExecutionArtifact(label, path);
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
