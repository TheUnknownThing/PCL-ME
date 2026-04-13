using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class SpikeInputResolver
{
    public static StartupSpikeInputs ResolveStartupInputs(SpikeCommandOptions options)
    {
        return SpikeInputStore.LoadStartupInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? SpikeHostInputFactory.CreateStartupInputs()
                   : SpikeSampleFactory.CreateDefaultStartupInputs());
    }

    public static ShellSpikeInputs ResolveShellInputs(SpikeCommandOptions options)
    {
        return SpikeInputStore.LoadShellInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? SpikeHostInputFactory.CreateShellInputs()
                   : SpikeSampleFactory.CreateDefaultShellInputs());
    }

    public static LaunchSpikeInputs ResolveLaunchInputs(SpikeCommandOptions options)
    {
        return SpikeInputStore.LoadLaunchInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? SpikeHostInputFactory.CreateLaunchInputs(options.Scenario)
                   : SpikeSampleFactory.CreateDefaultLaunchInputs(options.Scenario));
    }

    public static CrashSpikeInputs ResolveCrashInputs(SpikeCommandOptions options)
    {
        return SpikeInputStore.LoadCrashInputs(options.InputRoot) ??
               (options.UseHostEnvironment
                   ? SpikeHostInputFactory.CreateCrashInputs()
                   : SpikeSampleFactory.CreateDefaultCrashInputs());
    }
}
