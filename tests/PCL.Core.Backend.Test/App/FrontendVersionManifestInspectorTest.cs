using System.IO.Compression;
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
        Assert.AreEqual(new Version(21, 0, 10), profile.ParsedVanillaVersion);
        Assert.AreEqual("0.18.4", profile.FabricVersion);
    }

    [TestMethod]
    public void ReadProfile_UsesGoldComparableVersionParsingForModernReleaseNames()
    {
        Assert.AreEqual(new Version(21, 0, 10), FrontendVersionManifestInspector.ParseComparableVanillaVersion("1.21.10"));
        Assert.AreEqual(new Version(26, 1, 1), FrontendVersionManifestInspector.ParseComparableVanillaVersion("26.1.1"));
    }

    [TestMethod]
    public void ReadProfile_UsesFullModernVersionFromJsonIdFallback()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "loader-pack",
            new JsonObject
            {
                ["id"] = "loader-26.1.1"
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "loader-pack");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("26.1.1", profile.VanillaVersion);
        Assert.AreEqual(new Version(26, 1, 1), profile.ParsedVanillaVersion);
    }

    [TestMethod]
    public void ReadProfile_UsesForgeLibraryCoordinateGameVersionForModernVersions()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "26.1.2-Forge_64.0.0",
            new JsonObject
            {
                ["id"] = "custom-forge-pack",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.minecraftforge:forge:26.1.2-64.0.0"))
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "26.1.2-Forge_64.0.0");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("26.1.2", profile.VanillaVersion);
        Assert.AreEqual(new Version(26, 1, 2), profile.ParsedVanillaVersion);
        Assert.AreEqual("64.0.0", profile.ForgeVersion);
    }

    [TestMethod]
    public void ReadProfile_IgnoresForgeLibraryClassifierWhenResolvingVersions()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "26.1.2-Forge_64.0.0-universal",
            new JsonObject
            {
                ["id"] = "custom-forge-pack",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.minecraftforge:forge:26.1.2-64.0.0:universal"))
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "26.1.2-Forge_64.0.0-universal");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("26.1.2", profile.VanillaVersion);
        Assert.AreEqual(new Version(26, 1, 2), profile.ParsedVanillaVersion);
        Assert.AreEqual("64.0.0", profile.ForgeVersion);
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
        Assert.AreEqual(new Version(20, 0, 6), profile.ParsedVanillaVersion);
    }

    [TestMethod]
    public void ReadProfile_UsesSnapshotFallbackFromJsonId()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "snapshot-pack",
            new JsonObject
            {
                ["id"] = "custom-25w14a-pack"
            });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "snapshot-pack");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("25w14a", profile.VanillaVersion);
        Assert.AreEqual(new Version(9999, 0, 0), profile.ParsedVanillaVersion);
    }

    [TestMethod]
    public void ReadProfile_ReadsJarVersionJsonNameBeforeJsonIdFallback()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "craftmine",
            new JsonObject
            {
                ["id"] = "craftmine"
            });
        workspace.WriteJarVersionName("craftmine", "25w14craftmine");

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "craftmine");

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("25w14craftmine", profile.VanillaVersion);
        Assert.AreEqual(new Version(9999, 0, 0), profile.ParsedVanillaVersion);
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

        public void WriteJarVersionName(string versionName, string jarVersionName)
        {
            var versionDirectory = Path.Combine(LauncherFolder, "versions", versionName);
            Directory.CreateDirectory(versionDirectory);
            var jarPath = Path.Combine(versionDirectory, $"{versionName}.jar");

            using var fileStream = new FileStream(jarPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("version.json");
            using var stream = new StreamWriter(entry.Open());
            stream.Write(
                new JsonObject
                {
                    ["name"] = jarVersionName
                }.ToJsonString(new JsonSerializerOptions
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
