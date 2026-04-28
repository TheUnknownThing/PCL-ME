using Avalonia;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class AvaloniaDesktopHost
{
    public static void Run(AvaloniaCommandOptions options)
    {
        BuildAvaloniaApp(options).StartWithClassicDesktopLifetime([]);
    }

    internal static AppBuilder BuildAvaloniaApp(AvaloniaCommandOptions options)
    {
        var platformAdapter = new FrontendPlatformAdapter();
        var runtimePaths = FrontendRuntimePaths.Resolve(platformAdapter);
        FrontendStartupScalingService.DisablePlatformScaling();
        var renderingConfiguration = FrontendStartupRenderingService.Resolve(
            runtimePaths,
            platformAdapter.GetDesktopPlatformKind());
        var builder = AppBuilder.Configure(() => new App(options, runtimePaths, platformAdapter))
            .UsePlatformDetect()
            .LogToTrace();

        return FrontendStartupRenderingService.Apply(builder, renderingConfiguration);
    }
}
