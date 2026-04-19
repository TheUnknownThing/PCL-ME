using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendModpackExportWorkflowServiceTest
{
    [TestMethod]
    public void CreateArchive_WritesRecognizableMcbbsPackage()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "demo-pack",
            new JsonObject
            {
                ["id"] = "demo-pack",
                ["clientVersion"] = "1.20.1",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.minecraftforge:forge:1.20.1-47.3.0"),
                    CreateLibrary("optifine:OptiFine:1.20.1_HD_U_I6"))
            });

        var sourcePath = workspace.WriteFile("instance/config/demo.cfg", "demo=true");
        var archivePath = Path.Combine(workspace.RootPath, "demo-pack.zip");

        FrontendModpackExportWorkflowService.CreateArchive(new FrontendModpackExportRequest(
            archivePath,
            FrontendModpackExportPackageKind.Mcbbs,
            workspace.LauncherFolder,
            "demo-pack",
            "Exported Demo",
            "1.2.3",
            [new FrontendModpackExportSource(sourcePath, "overrides/config/demo.cfg")],
            "\"-Ddemo.value=hello world\" -Xmx4G",
            "--demo flag"));

        Assert.AreEqual("Exported Demo", FrontendModpackInstallWorkflowService.SuggestInstanceName(archivePath));

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.IsNotNull(archive.GetEntry("mcbbs.packmeta"));
        Assert.IsNotNull(archive.GetEntry("manifest.json"));
        Assert.IsNotNull(archive.GetEntry("overrides/config/demo.cfg"));

        var manifest = ReadJsonEntry(archive, "mcbbs.packmeta");
        Assert.AreEqual("minecraftModpack", manifest["manifestType"]?.GetValue<string>());
        Assert.AreEqual("Exported Demo", manifest["name"]?.GetValue<string>());
        Assert.AreEqual("1.2.3", manifest["version"]?.GetValue<string>());
        CollectionAssert.AreEquivalent(
            new[] { "game", "forge", "optifine" },
            (manifest["addons"] as JsonArray ?? [])
            .Select(node => node?["id"]?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray());
        CollectionAssert.AreEqual(
            new[] { "-Ddemo.value=hello world", "-Xmx4G" },
            (manifest["launchInfo"]?["javaArgument"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToArray());
        CollectionAssert.AreEqual(
            new[] { "--demo", "flag" },
            (manifest["launchInfo"]?["launchArgument"] as JsonArray ?? [])
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToArray());
    }

    [TestMethod]
    public void CreateArchive_WritesRecognizableModrinthPackage()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "fabric-pack",
            new JsonObject
            {
                ["id"] = "fabric-pack",
                ["clientVersion"] = "1.20.6",
                ["libraries"] = new JsonArray(
                    CreateLibrary("net.fabricmc:fabric-loader:0.15.11"))
            });

        var sourcePath = workspace.WriteFile("instance/resourcepacks/demo.zip", "resource-pack");
        var archivePath = Path.Combine(workspace.RootPath, "fabric-pack.mrpack");

        FrontendModpackExportWorkflowService.CreateArchive(new FrontendModpackExportRequest(
            archivePath,
            FrontendModpackExportPackageKind.Modrinth,
            workspace.LauncherFolder,
            "fabric-pack",
            "Fabric Demo",
            "2.0.0",
            [new FrontendModpackExportSource(sourcePath, "overrides/resourcepacks/demo.zip")]));

        Assert.AreEqual("Fabric Demo", FrontendModpackInstallWorkflowService.SuggestInstanceName(archivePath));

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.IsNotNull(archive.GetEntry("modrinth.index.json"));
        Assert.IsNull(archive.GetEntry("overrides/resourcepacks/demo.zip"));
        Assert.IsNotNull(archive.GetEntry("client-overrides/resourcepacks/demo.zip"));

        var manifest = ReadJsonEntry(archive, "modrinth.index.json");
        Assert.AreEqual("minecraft", manifest["game"]?.GetValue<string>());
        Assert.AreEqual("Fabric Demo", manifest["name"]?.GetValue<string>());
        Assert.AreEqual("2.0.0", manifest["versionId"]?.GetValue<string>());
        Assert.AreEqual("1.20.6", manifest["dependencies"]?["minecraft"]?.GetValue<string>());
        Assert.AreEqual("0.15.11", manifest["dependencies"]?["fabric-loader"]?.GetValue<string>());
    }

    [TestMethod]
    public void InspectPackage_ReadsExtendedMcbbsAddonVersions()
    {
        using var workspace = new TempLauncherWorkspace();
        workspace.WriteManifest(
            "extended-pack",
            new JsonObject
            {
                ["id"] = "extended-pack",
                ["clientVersion"] = "1.12.2",
                ["libraries"] = new JsonArray(
                    CreateLibrary("com.cleanroommc:cleanroom:0.2.4-alpha"),
                    CreateLibrary("net.legacyfabric:fabric-loader:0.14.0"),
                    CreateLibrary("com.mumfrey:liteloader:1.12.2-SNAPSHOT"),
                    CreateLibrary("net.labymod:labymod:4.0.0"))
            });

        var sourcePath = workspace.WriteFile("instance/options.txt", "demo");
        var archivePath = Path.Combine(workspace.RootPath, "extended-pack.zip");

        FrontendModpackExportWorkflowService.CreateArchive(new FrontendModpackExportRequest(
            archivePath,
            FrontendModpackExportPackageKind.Mcbbs,
            workspace.LauncherFolder,
            "extended-pack",
            "Extended Demo",
            "3.0.0",
            [new FrontendModpackExportSource(sourcePath, "overrides/options.txt")]));

        using var httpClient = new HttpClient();
        var package = FrontendModpackInstallWorkflowService.InspectPackage(
            archivePath,
            0,
            httpClient,
            CancellationToken.None);

        Assert.AreEqual("0.2.4-alpha", package.CleanroomVersion);
        Assert.AreEqual("0.14.0", package.LegacyFabricVersion);
        Assert.AreEqual("1.12.2-SNAPSHOT", package.LiteLoaderVersion);
        Assert.AreEqual("4.0.0", package.LabyModVersion);
    }

    [TestMethod]
    public void InspectPackage_ReadsMmcPackage()
    {
        using var workspace = new TempLauncherWorkspace();
        var archivePath = Path.Combine(workspace.RootPath, "mmc-pack.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            WriteTextEntry(
                archive,
                "mmc-pack.json",
                """
                {
                  "formatVersion": 1,
                  "components": [
                    { "uid": "net.minecraft", "version": "1.20.1" },
                    { "uid": "net.fabricmc.fabric-loader", "version": "0.15.11" }
                  ]
                }
                """);
            WriteTextEntry(
                archive,
                "instance.cfg",
                """
                name=Prism Demo
                OverrideCommands=true
                PreLaunchCommand=$INST_DIR/run-before.sh
                JoinServerOnLaunch=true
                JoinServerOnLaunchAddress=example.org
                IgnoreJavaCompatibility=true
                JvmArgs=-Ddemo=true
                """);
            WriteTextEntry(archive, ".minecraft/options.txt", "lang:en_us");
            WriteTextEntry(archive, "libraries/com/example/local/1.0/local-1.0.jar", "local");
            WriteTextEntry(
                archive,
                "patches/fabric-loader.json",
                """
                {
                  "uid": "net.fabricmc.fabric-loader",
                  "order": 10,
                  "+jvmArgs": ["-Dpatch=true"],
                  "minecraftArguments": "--demo value",
                  "libraries": [
                    { "name": "com.example:patch-lib:1.0", "MMC-hint": "local" }
                  ],
                  "compatibleJavaMajors": [8, 17]
                }
                """);
        }

        Assert.AreEqual("Prism Demo", FrontendModpackInstallWorkflowService.SuggestInstanceName(archivePath));

        using var httpClient = new HttpClient();
        var package = FrontendModpackInstallWorkflowService.InspectPackage(
            archivePath,
            0,
            httpClient,
            CancellationToken.None);

        Assert.AreEqual(FrontendModpackPackageKind.Mmc, package.Kind);
        Assert.AreEqual("1.20.1", package.MinecraftVersion);
        Assert.AreEqual("0.15.11", package.FabricVersion);
        Assert.AreEqual("-Ddemo=true", package.LaunchJvmArguments);
        Assert.IsNotNull(package.ManifestPatch);
        Assert.AreEqual(1, package.ManifestPatch!.Libraries.Count);
        Assert.AreEqual("local", package.ManifestPatch.Libraries[0]?["hint"]?.GetValue<string>());
        Assert.AreEqual(2, package.ManifestPatch.GameArguments.Count);
        Assert.AreEqual(1, package.ManifestPatch.JvmArguments.Count);
        Assert.AreEqual(17, package.ManifestPatch.ExtraProperties["javaVersion"]?["majorVersion"]?.GetValue<int>());
        Assert.AreEqual("{verpath}run-before.sh", package.InstanceConfigValues?["VersionAdvanceRun"]);
        Assert.AreEqual("example.org", package.InstanceConfigValues?["VersionServerEnter"]);
        Assert.AreEqual(true, package.InstanceConfigValues?["VersionAdvanceJava"]);
        Assert.IsTrue(package.OverrideSources.Any(source =>
            source.Target == FrontendModpackOverrideTarget.LauncherRoot &&
            string.Equals(source.TargetRelativePath, "libraries", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ManifestInspector_ReadsSingleJsonFileWhenNameDiffersFromFolder()
    {
        using var workspace = new TempLauncherWorkspace();
        var instanceDirectory = Path.Combine(workspace.LauncherFolder, "versions", "Prism Demo");
        Directory.CreateDirectory(instanceDirectory);
        File.WriteAllText(
            Path.Combine(instanceDirectory, "custom-version.json"),
            new JsonObject
            {
                ["id"] = "custom-version",
                ["clientVersion"] = "1.20.1",
                ["libraries"] = new JsonArray(CreateLibrary("net.fabricmc:fabric-loader:0.15.11"))
            }.ToJsonString());

        Assert.IsTrue(FrontendRuntimePaths.IsRecognizedInstanceDirectory(instanceDirectory));
        var profile = FrontendVersionManifestInspector.ReadProfile(workspace.LauncherFolder, "Prism Demo");
        Assert.IsTrue(profile.IsManifestValid);
        Assert.AreEqual("1.20.1", profile.VanillaVersion);
        Assert.AreEqual("0.15.11", profile.FabricVersion);
    }

    private static JsonObject CreateLibrary(string name)
    {
        return new JsonObject
        {
            ["name"] = name
        };
    }

    private static JsonObject ReadJsonEntry(ZipArchive archive, string entryPath)
    {
        using var stream = archive.GetEntry(entryPath)?.Open()
                           ?? throw new AssertFailedException($"Missing archive entry: {entryPath}");
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject()
               ?? throw new AssertFailedException($"Invalid JSON entry: {entryPath}");
    }

    private static void WriteTextEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private sealed class TempLauncherWorkspace : IDisposable
    {
        public TempLauncherWorkspace()
        {
            RootPath = Directory.CreateTempSubdirectory("pcl-modpack-export-").FullName;
            LauncherFolder = Path.Combine(RootPath, "launcher");
            Directory.CreateDirectory(LauncherFolder);
        }

        public string RootPath { get; }

        public string LauncherFolder { get; }

        public void WriteManifest(string versionName, JsonObject root)
        {
            var versionDirectory = Path.Combine(LauncherFolder, "versions", versionName);
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(
                Path.Combine(versionDirectory, $"{versionName}.json"),
                root.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        public string WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
