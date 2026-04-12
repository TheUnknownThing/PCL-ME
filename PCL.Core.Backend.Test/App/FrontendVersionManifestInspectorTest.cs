using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendVersionManifestInspectorTest
{
    [TestMethod]
    public void ReadProfile_ResolvesVersionFromFabricIntermediaryWhenPackIdStartsWithName()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "Fabulously Optimized 1.21.10",
            new JsonObject
            {
                ["id"] = "Fabulously Optimized 1.21.10",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.fabricmc:intermediary:1.21.10"),
                    CreateLibrary("net.fabricmc:fabric-loader:0.18.4"))
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "Fabulously Optimized 1.21.10");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.21.10", profile.VanillaVersion);
        Assert.AreEqual(new Version(1, 21, 10), profile.ParsedVanillaVersion);
        Assert.AreEqual("0.18.4", profile.FabricVersion);
    }

    [TestMethod]
    public void ReadProfile_PrefersMinecraftVersionOverLoaderVersionInCustomIds()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "fabric-loader-0.16.10-1.21.10",
            new JsonObject
            {
                ["id"] = "fabric-loader-0.16.10-1.21.10",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.fabricmc:intermediary:1.21.10"),
                    CreateLibrary("net.fabricmc:fabric-loader:0.16.10"))
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "fabric-loader-0.16.10-1.21.10");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.21.10", profile.VanillaVersion);
        Assert.AreEqual(new Version(1, 21, 10), profile.ParsedVanillaVersion);
    }

    [TestMethod]
    public void ReadProfile_UsesExplicitClientVersionBeforeFallbacks()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "custom-pack",
            new JsonObject
            {
                ["id"] = "custom-pack",
                ["clientVersion"] = "1.20.6",
                ["libraries"] = new JsonArray(CreateLibrary("net.fabricmc:fabric-loader:0.15.11"))
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "custom-pack");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.20.6", profile.VanillaVersion);
        Assert.AreEqual(new Version(1, 20, 6), profile.ParsedVanillaVersion);
    }

    private static JsonObject CreateLibrary(string name)
    {
        return new JsonObject
        {
            ["name"] = name
        };
    }

    private sealed class TempLauncherWorkspace : IDisposable
    {
        public TempLauncherWorkspace()
        {
            LauncherFolder = Directory.CreateTempSubdirectory("pcl-frontend-version-").FullName;
        }

        public string LauncherFolder { get; }

        public void WriteManifest(string versionName, JsonObject root)
        {
            var versionDirectory = Path.Combine(LauncherFolder, "versions", versionName);
            Directory.CreateDirectory(versionDirectory);
            var manifestPath = Path.Combine(versionDirectory, $"{versionName}.json");
            File.WriteAllText(
                manifestPath,
                root.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        public void Dispose()
        {
            Directory.Delete(LauncherFolder, recursive: true);
        }
    }
}
