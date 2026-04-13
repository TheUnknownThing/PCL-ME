using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows.Inspection;

internal static class AvaloniaInputResolver
{
    public static StartupAvaloniaInputs ResolveStartupInputs(AvaloniaCommandOptions options)
    {
        return AvaloniaInputStore.LoadStartupInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? AvaloniaHostInputFactory.CreateStartupInputs()
                   : AvaloniaSampleFactory.CreateDefaultStartupInputs());
    }

    public static ShellAvaloniaInputs ResolveShellInputs(AvaloniaCommandOptions options)
    {
        return AvaloniaInputStore.LoadShellInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? AvaloniaHostInputFactory.CreateShellInputs()
                   : AvaloniaSampleFactory.CreateDefaultShellInputs());
    }

    public static LaunchAvaloniaInputs ResolveLaunchInputs(AvaloniaCommandOptions options)
    {
        return AvaloniaInputStore.LoadLaunchInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? AvaloniaHostInputFactory.CreateLaunchInputs(options.Scenario)
                   : AvaloniaSampleFactory.CreateDefaultLaunchInputs(options.Scenario));
    }

    public static CrashAvaloniaInputs ResolveCrashInputs(AvaloniaCommandOptions options)
    {
        return AvaloniaInputStore.LoadCrashInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? AvaloniaHostInputFactory.CreateCrashInputs()
                   : AvaloniaSampleFactory.CreateDefaultCrashInputs());
    }
}
