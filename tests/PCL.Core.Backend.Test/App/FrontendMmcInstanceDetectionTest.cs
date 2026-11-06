using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendMmcInstanceDetectionTest
{
    [TestMethod]
    public void ReadProfile_RecognizesMmcManifestAlongsideOtherMetadata()
    {
        using var workspace = new MmcWorkspace();
        workspace.WritePack(new JsonObject { ["uid"] = "net.fabricmc.fabric-loader", ["version"] = "0.15.11" });
        File.WriteAllText(Path.Combine(workspace.InstanceDirectory, "modrinth.json"), "{}");

        Assert.IsTrue(FrontendRuntimePaths.IsRecognizedInstanceDirectory(workspace.InstanceDirectory));
        Assert.AreEqual(workspace.ManifestPath,
            FrontendVersionManifestPathResolver.ResolveManifestPath(workspace.LauncherDirectory, MmcWorkspace.InstanceName));
        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName);

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.20.1", profile.VanillaVersion);
        Assert.AreEqual(new Version(20, 0, 1), profile.ParsedVanillaVersion);
        Assert.AreEqual("0.15.11", profile.FabricVersion);
        Assert.AreEqual("Fabric", profile.PrimaryLoaderName);
    }

    [TestMethod]
    [DataRow("net.minecraftforge", "47.2.0", "forge", "Forge")]
    [DataRow("net.neoforged", "20.1.112", "neoforge", "NeoForge")]
    [DataRow("net.neoforged.neoforge", "20.1.112", "neoforge", "NeoForge")]
    [DataRow("net.fabricmc.fabric-loader", "0.15.11", "fabric", "Fabric")]
    [DataRow("org.quiltmc.quilt-loader", "0.26.3", "quilt", "Quilt")]
    [DataRow("com.mumfrey.liteloader", "1.12.2-SNAPSHOT", "liteloader", "LiteLoader")]
    [DataRow("com.cleanroommc", "0.2.4-alpha", "cleanroom", "Cleanroom")]
    [DataRow("net.minecraftforge", "0.2.4-alpha", "cleanroom", "Cleanroom")]
    public void ReadProfile_IdentifiesMmcLoader(string uid, string version, string loaderKey, string loaderName)
    {
        using var workspace = new MmcWorkspace();
        workspace.WritePack(new JsonObject { ["uid"] = uid, ["version"] = version });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName);
        var loaders = FrontendVersionManifestInspector.CreateLoaderMap(profile);

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual(loaderName, profile.PrimaryLoaderName);
        Assert.AreEqual(1, loaders.Count);
        Assert.AreEqual(version, loaders[loaderKey]);
    }

    [TestMethod]
    public void ReadProfile_UsesCachedVersionsWhenExplicitVersionsAreBlank()
    {
        using var workspace = new MmcWorkspace();
        workspace.WriteComponents(
            new JsonObject { ["uid"] = "net.minecraft", ["version"] = "", ["cachedVersion"] = "1.20.1" },
            new JsonObject { ["uid"] = "net.fabricmc.fabric-loader", ["version"] = " ", ["cachedVersion"] = "0.15.11" });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName);

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.20.1", profile.VanillaVersion);
        Assert.AreEqual("0.15.11", profile.FabricVersion);
    }

    [TestMethod]
    public void ReadProfile_IgnoresDisabledLoadersAndMalformedComponentEntries()
    {
        using var workspace = new MmcWorkspace();
        workspace.WriteComponents(
            null,
            JsonValue.Create("invalid component"),
            new JsonObject { ["uid"] = "net.minecraft", ["version"] = "1.20.1" },
            new JsonObject { ["uid"] = "net.minecraftforge", ["version"] = "47.2.0", ["disabled"] = true },
            new JsonObject { ["uid"] = "net.fabricmc.fabric-loader", ["version"] = "0.15.11", ["disabled"] = false });

        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName);

        Assert.IsTrue(profile.IsManifestValid);
        Assert.IsFalse(profile.HasForge);
        Assert.AreEqual("Fabric", profile.PrimaryLoaderName);
    }

    [TestMethod]
    public void ReadProfile_PrefersInstalledVersionManifestOverMmcMetadata()
    {
        using var workspace = new MmcWorkspace();
        workspace.WritePack();
        var nativeManifestPath = Path.Combine(workspace.InstanceDirectory, $"{MmcWorkspace.InstanceName}.json");
        File.WriteAllText(nativeManifestPath, """{ "id": "native", "clientVersion": "1.21.1" }""");

        Assert.AreEqual(nativeManifestPath,
            FrontendVersionManifestPathResolver.ResolveManifestPath(workspace.LauncherDirectory, MmcWorkspace.InstanceName));
        Assert.AreEqual("1.21.1",
            FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName).VanillaVersion);
    }

    [TestMethod]
    public void ReadProfileFromManifestPath_ReadsMmcMetadataOutsideVersionsDirectory()
    {
        using var workspace = new MmcWorkspace();
        var externalDirectory = Path.Combine(workspace.RootDirectory, "PrismLauncher", "instances", "Prism Demo");
        Directory.CreateDirectory(externalDirectory);
        var manifestPath = Path.Combine(externalDirectory, "mmc-pack.json");
        workspace.WritePack();
        File.Copy(workspace.ManifestPath, manifestPath);
        File.WriteAllText(Path.Combine(externalDirectory, "modrinth.json"), "{}");

        Assert.IsTrue(FrontendRuntimePaths.IsRecognizedInstanceDirectory(externalDirectory));
        var profile = FrontendVersionManifestInspector.ReadProfileFromManifestPath(manifestPath);

        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.20.1", profile.VanillaVersion);
    }

    [TestMethod]
    [DataRow("{ invalid json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{ \"components\": {} }")]
    [DataRow("{ \"components\": [] }")]
    [DataRow("{ \"components\": [{ \"uid\": \"net.fabricmc.fabric-loader\", \"version\": \"0.15.11\" }] }")]
    [DataRow("{ \"components\": [{ \"uid\": \"net.minecraft\", \"version\": \"1.20.1\", \"disabled\": true }] }")]
    public void ReadProfile_InvalidMmcMetadataProducesEmptyProfile(string json)
    {
        using var workspace = new MmcWorkspace();
        File.WriteAllText(workspace.ManifestPath, json);

        Assert.IsFalse(FrontendVersionManifestInspector.ReadProfile(workspace.LauncherDirectory, MmcWorkspace.InstanceName).IsManifestValid);
        Assert.IsFalse(FrontendVersionManifestInspector.ReadProfileFromManifestPath(workspace.ManifestPath).IsManifestValid);
    }

    private sealed class MmcWorkspace : IDisposable
    {
        public const string InstanceName = "Prism Demo";

        public MmcWorkspace()
        {
            RootDirectory = Directory.CreateTempSubdirectory("pcl-mmc-detection-").FullName;
            LauncherDirectory = Path.Combine(RootDirectory, "launcher");
            InstanceDirectory = Path.Combine(LauncherDirectory, "versions", InstanceName);
            Directory.CreateDirectory(InstanceDirectory);
        }

        public string RootDirectory { get; }
        public string LauncherDirectory { get; }
        public string InstanceDirectory { get; }
        public string ManifestPath => Path.Combine(InstanceDirectory, "mmc-pack.json");

        public void WritePack(params JsonNode?[] loaders)
        {
            WriteComponents([
                new JsonObject { ["uid"] = "net.minecraft", ["version"] = "1.20.1" },
                .. loaders
            ]);
        }

        public void WriteComponents(params JsonNode?[] components)
        {
            File.WriteAllText(ManifestPath, new JsonObject
            {
                ["formatVersion"] = 1,
                ["components"] = new JsonArray(components)
            }.ToJsonString());
        }

        public void Dispose() => Directory.Delete(RootDirectory, recursive: true);
    }
}
