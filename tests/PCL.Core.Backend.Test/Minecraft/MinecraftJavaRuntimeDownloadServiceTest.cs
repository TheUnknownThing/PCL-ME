using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftJavaRuntimeDownloadServiceTest
{
    [TestMethod]
    public void SelectRuntimeUsesExactComponentMatchWhenAvailable()
    {
        var result = MinecraftJavaRuntimeDownloadService.SelectRuntime(new MinecraftJavaRuntimeSelectionRequest(
            """
            {
              "windows-x64": {
                "jre-legacy": [
                  {
                    "version": { "name": "17.0.12+7" },
                    "manifest": { "url": "https://example.invalid/jre-legacy.json" }
                  }
                ]
              }
            }
            """,
            "windows-x64",
            "jre-legacy"));

        Assert.AreEqual("jre-legacy", result.ComponentKey);
        Assert.AreEqual("17.0.12+7", result.VersionName);
        Assert.AreEqual("https://example.invalid/jre-legacy.json", result.ManifestUrl);
        Assert.IsFalse(result.MatchedByPrefix);
    }

    [TestMethod]
    public void SelectRuntimeFallsBackToVersionPrefixWhenExactComponentIsMissing()
    {
        var result = MinecraftJavaRuntimeDownloadService.SelectRuntime(new MinecraftJavaRuntimeSelectionRequest(
            """
            {
              "windows-x64": {
                "java-runtime-gamma": [
                  {
                    "version": { "name": "21.0.4+7" },
                    "manifest": { "url": "https://example.invalid/java-runtime-gamma.json" }
                  }
                ]
              }
            }
            """,
            "windows-x64",
            "21"));

        Assert.AreEqual("java-runtime-gamma", result.ComponentKey);
        Assert.AreEqual("21.0.4+7", result.VersionName);
        Assert.IsTrue(result.MatchedByPrefix);
    }

    [TestMethod]
    public void BuildDownloadPlanSkipsIgnoredHashesAndPreventsPathTraversal()
    {
        var runtimeBaseDirectory = Path.Combine(Path.GetTempPath(), "pcl-java-runtime-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = MinecraftJavaRuntimeDownloadService.BuildDownloadPlan(new MinecraftJavaRuntimeDownloadPlanRequest(
                """
                {
                  "files": {
                    "bin/java": {
                      "downloads": {
                        "raw": {
                          "url": "https://example.invalid/bin/java",
                          "size": 123,
                          "sha1": "keep-me"
                        }
                      }
                    },
                    "legal/java.base/LICENSE": {
                      "downloads": {
                        "raw": {
                          "url": "https://example.invalid/license",
                          "size": 456,
                          "sha1": "skip-me"
                        }
                      }
                    }
                  }
                }
                """,
                runtimeBaseDirectory,
                ["skip-me"]));

            Assert.AreEqual(1, result.Files.Count);
            Assert.AreEqual("bin/java", result.Files[0].RelativePath);
            Assert.AreEqual(Path.GetFullPath(runtimeBaseDirectory), result.RuntimeBaseDirectory);
            Assert.AreEqual(Path.Combine(Path.GetFullPath(runtimeBaseDirectory), "bin", "java"), result.Files[0].TargetPath);
        }
        finally
        {
            if (Directory.Exists(runtimeBaseDirectory))
            {
                Directory.Delete(runtimeBaseDirectory, true);
            }
        }
    }

    [TestMethod]
    public void BuildDownloadPlanPreservesWindowsStyleRuntimePathsOnNonWindowsHosts()
    {
        var result = MinecraftJavaRuntimeDownloadService.BuildDownloadPlan(new MinecraftJavaRuntimeDownloadPlanRequest(
            """
            {
              "files": {
                "bin/java.exe": {
                  "downloads": {
                    "raw": {
                      "url": "https://example.invalid/bin/java.exe",
                      "size": 123,
                      "sha1": "keep-me"
                    }
                  }
                }
              }
            }
            """,
            @"C:\Minecraft\.minecraft\runtime\jre-legacy"));

        Assert.AreEqual(@"C:\Minecraft\.minecraft\runtime\jre-legacy", result.RuntimeBaseDirectory);
        Assert.AreEqual(@"C:\Minecraft\.minecraft\runtime\jre-legacy\bin\java.exe", result.Files[0].TargetPath);
    }

    [TestMethod]
    public void BuildDownloadPlanRejectsPathsOutsideRuntimeDirectory()
    {
        var runtimeBaseDirectory = Path.Combine(Path.GetTempPath(), "pcl-java-runtime-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                MinecraftJavaRuntimeDownloadService.BuildDownloadPlan(new MinecraftJavaRuntimeDownloadPlanRequest(
                    """
                    {
                      "files": {
                        "../escape.txt": {
                          "downloads": {
                            "raw": {
                              "url": "https://example.invalid/escape.txt",
                              "size": 1,
                              "sha1": "escape"
                            }
                          }
                        }
                      }
                    }
                    """,
                    runtimeBaseDirectory)));
        }
        finally
        {
            if (Directory.Exists(runtimeBaseDirectory))
            {
                Directory.Delete(runtimeBaseDirectory, true);
            }
        }
    }
}
