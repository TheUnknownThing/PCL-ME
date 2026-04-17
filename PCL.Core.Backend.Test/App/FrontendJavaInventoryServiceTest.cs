using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Java;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FrontendJavaInventoryServiceTest
{
    [TestMethod]
    public void ParseStorageItemsPreservesAutoInstalledSource()
    {
        const string executablePath = "/tmp/runtime/jre-21/bin/java";
        var result = FrontendJavaInventoryService.ParseStorageItems(
            """
            [
              {
                "Path": "/tmp/runtime/jre-21/bin/java",
                "IsEnable": true,
                "Source": "AutoInstalled"
              }
            ]
            """);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(Path.GetFullPath(executablePath), result.Single().Path);
        Assert.AreEqual(JavaSource.AutoInstalled, result.Single().Source);
        Assert.IsTrue(result.Single().IsEnable);
    }

    [TestMethod]
    public void ParseStoredJavaRuntimesUsesPersistedInstallationMetadataWhenRuntimeCannotBeExecuted()
    {
        var result = FrontendJavaInventoryService.ParseStoredJavaRuntimes(
            """
            [
              {
                "Path": "/tmp/runtime/jre-21/bin/java",
                "IsEnable": true,
                "Source": "AutoInstalled",
                "Installation": {
                  "JavaExePath": "/tmp/runtime/jre-21/bin/java",
                  "DisplayName": "Java 21.0.4",
                  "Version": "21.0.4+7",
                  "MajorVersion": 21,
                  "Is64Bit": true,
                  "IsJre": true,
                  "Brand": "OpenJDK",
                  "Architecture": "ARM64"
                }
              }
            ]
            """);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Java 21.0.4", result.Single().DisplayName);
        Assert.AreEqual(21, result.Single().MajorVersion);
        Assert.AreEqual(MachineType.ARM64, result.Single().Architecture);
        Assert.IsTrue(result.Single().Is64Bit);
        Assert.IsTrue(result.Single().IsJre);
        Assert.AreEqual(JavaBrandType.OpenJDK, result.Single().Brand);
    }

    [TestMethod]
    public void ParseStorageItemsAllowsNullInstallationPayload()
    {
        const string executablePath = "/tmp/runtime/jre-21/bin/java";
        var result = FrontendJavaInventoryService.ParseStorageItems(
            """
            [
              {
                "Path": "/tmp/runtime/jre-21/bin/java",
                "IsEnable": false,
                "Source": "AutoScanned",
                "Installation": null
              }
            ]
            """);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(Path.GetFullPath(executablePath), result.Single().Path);
        Assert.IsFalse(result.Single().IsEnable);
        Assert.IsNull(result.Single().Installation);
    }

    [TestMethod]
    public void ParseStoredJavaRuntimesAllowsNullInstallationPayload()
    {
        const string executablePath = "/tmp/runtime/jre-21/bin/java";
        var result = FrontendJavaInventoryService.ParseStoredJavaRuntimes(
            """
            [
              {
                "Path": "/tmp/runtime/jre-21/bin/java",
                "IsEnable": true,
                "Source": "AutoScanned",
                "Installation": null
              }
            ]
            """);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(Path.GetFullPath(executablePath), result.Single().ExecutablePath);
        Assert.IsTrue(result.Single().IsEnabled);
    }

    [TestMethod]
    public void ResolveJavaRuntimePlatformKeyForPlatformFallsBackToLinuxOnArm64()
    {
        var result = FrontendLaunchCompositionService.ResolveJavaRuntimePlatformKeyForPlatform(
            FrontendDesktopPlatformKind.Linux,
            MachineType.ARM64);

        Assert.AreEqual("linux", result);
    }

    [TestMethod]
    public void ResolveAdoptiumArchitectureTokenReturnsAarch64ForArm64()
    {
        var result = FrontendLaunchCompositionService.ResolveAdoptiumArchitectureToken(MachineType.ARM64);

        Assert.AreEqual("aarch64", result);
    }

    [TestMethod]
    public void ShouldPreferAdoptiumForJavaRuntimeReturnsTrueOnLinuxArm64()
    {
        var result = FrontendLaunchCompositionService.ShouldPreferAdoptiumForJavaRuntime(
            FrontendDesktopPlatformKind.Linux,
            MachineType.ARM64);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void BuildAdoptiumJavaRuntimeInstallPlanFromMetadataParsesArchivePackage()
    {
        var result = FrontendLaunchCompositionService.BuildAdoptiumJavaRuntimeInstallPlanFromMetadata(
            """
            [
              {
                "binary": {
                  "package": {
                    "name": "OpenJDK21U-jre_aarch64_linux_hotspot_21.0.10_7.tar.gz",
                    "link": "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.10%2B7/OpenJDK21U-jre_aarch64_linux_hotspot_21.0.10_7.tar.gz",
                    "checksum": "3ca84da7c4f57eee8d7e7f0645dc904a3a06456d32b37a4dd57a5e7527245250",
                    "size": 51036802
                  }
                },
                "release_name": "jdk-21.0.10+7",
                "version": {
                  "semver": "21.0.10+7.0.LTS"
                }
              }
            ]
            """,
            "/tmp/.minecraft",
            "21",
            21,
            FrontendDesktopPlatformKind.Linux,
            MachineType.ARM64,
            "jre");

        Assert.IsNotNull(result);
        Assert.AreEqual(FrontendJavaRuntimeInstallPlanKind.ArchivePackage, result.Kind);
        Assert.AreEqual("Adoptium", result.SourceName);
        Assert.AreEqual("21.0.10+7.0.LTS", result.VersionName);
        Assert.AreEqual(MachineType.ARM64, result.RuntimeArchitecture);
        Assert.IsTrue(result.IsJre);
        Assert.AreEqual(JavaBrandType.EclipseTemurin, result.Brand);
        StringAssert.Contains(result.RuntimeDirectory, "adoptium-jre-21-linux-aarch64");
        Assert.IsNotNull(result.ArchivePlan);
        Assert.AreEqual("OpenJDK21U-jre_aarch64_linux_hotspot_21.0.10_7.tar.gz", result.ArchivePlan.PackageName);
        Assert.AreEqual(51036802L, result.ArchivePlan.Size);
        Assert.AreEqual(
            "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.10%2B7/OpenJDK21U-jre_aarch64_linux_hotspot_21.0.10_7.tar.gz",
            result.ArchivePlan.RequestUrls.OfficialUrls.Single());
    }

    [TestMethod]
    public void RequiresUnixExecutableBits_MatchesNestedJavaBundleBinaries()
    {
        Assert.IsTrue(FrontendShellActionService.RequiresUnixExecutableBits("bin/java"));
        Assert.IsTrue(FrontendShellActionService.RequiresUnixExecutableBits("jre.bundle/Contents/Home/bin/java"));
        Assert.IsTrue(FrontendShellActionService.RequiresUnixExecutableBits("jre.bundle/Contents/Home/lib/jspawnhelper"));
        Assert.IsFalse(FrontendShellActionService.RequiresUnixExecutableBits("jre.bundle/Contents/Home/lib/server/libjvm.dylib"));
    }
}
