using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendPlatformAdapterTest
{
    [TestMethod]
    public void IsSuccessfulStart_ReturnsTrue_WhenProcessExists()
    {
        using var currentProcess = Process.GetCurrentProcess();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false
        };

        Assert.IsTrue(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, currentProcess));
    }

    [TestMethod]
    public void IsSuccessfulStart_ReturnsTrue_WhenShellExecuteReturnsNull()
    {
        var startInfo = new ProcessStartInfo("C:\\")
        {
            UseShellExecute = true
        };

        Assert.IsTrue(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, null));
    }

    [TestMethod]
    public void IsSuccessfulStart_ReturnsFalse_WhenDirectStartReturnsNull()
    {
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = false
        };

        Assert.IsFalse(FrontendPlatformAdapter.IsSuccessfulStart(startInfo, null));
    }

    [TestMethod]
    public void CreateDirectLaunchShortcut_DeclaresMicrophoneUsageInMacBundle()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "pcl-mac-shortcut-" + Guid.NewGuid().ToString("N"));
        try
        {
            const string microphoneUsageDescription = "Minecraft mods need microphone access for voice chat & communication.";
            var adapter = new FrontendPlatformAdapter();
            var shortcut = adapter.CreateDirectLaunchShortcut(
                root,
                "/Applications/PCL-ME.app/Contents/MacOS/PCL.Frontend.Avalonia",
                "Backstabbed++",
                "Backstabbed++",
                microphoneUsageDescription);

            var plist = XDocument.Load(Path.Combine(shortcut.ShortcutPath, "Contents", "Info.plist"));
            var usageKey = plist.Root!.Element("dict")!.Elements("key")
                .Single(key => key.Value == "NSMicrophoneUsageDescription");

            Assert.AreEqual(microphoneUsageDescription, (usageKey.NextNode as XElement)?.Value);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void GetJavaExecutablePath_ResolvesNestedMacBundleLayout()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "pcl-java-runtime-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runtimeDirectory = Path.Combine(root, "runtime");
            var nestedExecutablePath = Path.Combine(runtimeDirectory, "jre.bundle", "Contents", "Home", "bin", "java");
            Directory.CreateDirectory(Path.GetDirectoryName(nestedExecutablePath)!);
            File.WriteAllText(nestedExecutablePath, string.Empty);

            var adapter = new FrontendPlatformAdapter();
            var resolvedPath = adapter.GetJavaExecutablePath(runtimeDirectory);

            Assert.AreEqual(nestedExecutablePath, resolvedPath);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
