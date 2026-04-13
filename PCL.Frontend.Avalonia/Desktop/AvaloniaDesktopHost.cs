using Avalonia;
using PCL.Frontend.Avalonia.Cli;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class AvaloniaDesktopHost
{
    public static void Run(AvaloniaCommandOptions options)
    {
        BuildAvaloniaApp(options).StartWithClassicDesktopLifetime([]);
    }

    internal static AppBuilder BuildAvaloniaApp(AvaloniaCommandOptions options)
    {
        return AppBuilder.Configure(() => new App(options))
            .UsePlatformDetect()
            .LogToTrace();
    }
}
