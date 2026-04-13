using Avalonia;
using PCL.Frontend.Spike.Cli;

namespace PCL.Frontend.Spike.Desktop;

internal static class SpikeDesktopHost
{
    public static void Run(SpikeCommandOptions options)
    {
        BuildAvaloniaApp(options).StartWithClassicDesktopLifetime([]);
    }

    private static AppBuilder BuildAvaloniaApp(SpikeCommandOptions options)
    {
        return AppBuilder.Configure(() => new App(options))
            .UsePlatformDetect()
            .LogToTrace();
    }
}
