using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendApplicationNameTest
{
    [TestMethod]
    public void AppConstructor_SetsApplicationNameToLauncherDisplayName()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-me-frontend-app-name-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new FrontendRuntimePaths(
                root,
                Path.Combine(root, "temp"),
                Path.Combine(root, "data"),
                Path.Combine(root, "shared"),
                Path.Combine(root, "shared", "config.json"),
                Path.Combine(root, "data", "config.yml"),
                Path.Combine(root, "appdata"),
                []);
            var app = new App(AvaloniaCommandParser.CreateDefaultOptions(), paths, new FrontendPlatformAdapter());

            // The macOS backend exports this name as the menu-bar title during setup;
            // Avalonia's default would otherwise read "Avalonia Application".
            Assert.AreEqual(FrontendApplicationIdentity.DisplayName, app.Name);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
