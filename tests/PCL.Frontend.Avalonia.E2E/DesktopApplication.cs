using Avalonia;
using Avalonia.Headless;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Workflows;

[assembly: AvaloniaTestApplication(typeof(PCL.Frontend.Avalonia.E2E.DesktopApplication))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]

namespace PCL.Frontend.Avalonia.E2E;

/// <summary>Runs the production application, styles and templates with a real Skia renderer.</summary>
public static class DesktopApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-e2e-application", Guid.NewGuid().ToString("N"));
        var paths = new FrontendRuntimePaths(root, root + "/temp", root + "/data", root + "/shared", root + "/shared/config.json", root + "/data/config.yml", root + "/appdata", []);
        return AppBuilder.Configure(() => new App(Cli.AvaloniaCommandParser.CreateDefaultOptions(), paths, new FrontendPlatformAdapter()))
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
