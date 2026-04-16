using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftJavaRuntimeDownloadWorkflowServiceTest
{
    [TestMethod]
    public void BuildManifestRequestPlanIncludesMirrorUrlAndSelectionMetadata()
    {
        var result = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
            new MinecraftJavaRuntimeManifestRequestPlanRequest(
                """
                {
                  "windows-x64": {
                    "java-runtime-gamma": [
                      {
                        "version": { "name": "21.0.4+7" },
                        "manifest": { "url": "https://piston-meta.mojang.com/runtime/java-runtime-gamma.json" }
                      }
                    ]
                  }
                }
                """,
                "windows-x64",
                "21",
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));

        Assert.AreEqual("java-runtime-gamma", result.Selection.ComponentKey);
        CollectionAssert.AreEqual(
            new[]
            {
                "https://piston-meta.mojang.com/runtime/java-runtime-gamma.json"
            },
            result.RequestUrls.OfficialUrls.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/runtime/java-runtime-gamma.json"
            },
            result.RequestUrls.MirrorUrls.ToArray());
        StringAssert.Contains(result.LogMessage, "java-runtime-gamma");
    }

    [TestMethod]
    public void BuildDownloadWorkflowPlanCarriesMirrorCandidatesForEachFile()
    {
        var result = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                """
                {
                  "files": {
                    "bin/java": {
                      "downloads": {
                        "raw": {
                          "url": "https://piston-data.mojang.com/v1/bin/java",
                          "size": 123,
                          "sha1": "keep-me"
                        }
                      }
                    },
                    "conf/security/policy.json": {
                      "downloads": {
                        "raw": {
                          "url": "https://downloads.example.invalid/policy.json",
                          "size": 456,
                          "sha1": "keep-too"
                        }
                      }
                    }
                  }
                }
                """,
                "/tmp/pcl-java-runtime",
                FileUrlRewrites: MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));

        Assert.AreEqual(2, result.Files.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                "https://piston-data.mojang.com/v1/bin/java"
            },
            result.Files[0].RequestUrls.OfficialUrls.ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/v1/bin/java"
            },
            result.Files[0].RequestUrls.MirrorUrls.ToArray());
        Assert.AreEqual(0, result.Files[1].RequestUrls.MirrorUrls.Count);
        StringAssert.Contains(result.LogMessage, "Need to download 2 files");
    }

    [TestMethod]
    public void BuildTransferPlanSplitsReusedFilesFromDownloadQueue()
    {
        var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                """
                {
                  "files": {
                    "bin/java": {
                      "downloads": {
                        "raw": {
                          "url": "https://piston-data.mojang.com/v1/bin/java",
                          "size": 123,
                          "sha1": "keep-me"
                        }
                      }
                    },
                    "conf/security/policy.json": {
                      "downloads": {
                        "raw": {
                          "url": "https://downloads.example.invalid/policy.json",
                          "size": 456,
                          "sha1": "keep-too"
                        }
                      }
                    }
                  }
                }
                """,
                "/tmp/pcl-java-runtime",
                FileUrlRewrites: MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));

        var transferPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
            new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                workflowPlan,
                ["conf\\security\\policy.json"]));

        Assert.AreEqual(1, transferPlan.FilesToDownload.Count);
        Assert.AreEqual("bin/java", transferPlan.FilesToDownload[0].RelativePath);
        Assert.AreEqual(1, transferPlan.ReusedFiles.Count);
        Assert.AreEqual("conf/security/policy.json", transferPlan.ReusedFiles[0].RelativePath);
        Assert.AreEqual(123L, transferPlan.DownloadBytes);
        StringAssert.Contains(transferPlan.LogMessage, "Need to download 1 files");
        StringAssert.Contains(transferPlan.LogMessage, "reuse 1 existing files");
    }

    [TestMethod]
    public void GetDefaultIndexRequestUrlPlanReturnsOfficialAndMirrorSources()
    {
        var result = MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan();

        Assert.AreEqual(1, result.OfficialUrls.Count);
        Assert.AreEqual(1, result.MirrorUrls.Count);
        StringAssert.Contains(result.OfficialUrls[0], "piston-meta.mojang.com");
        StringAssert.Contains(result.MirrorUrls[0], "bmclapi2.bangbang93.com");
    }

    [TestMethod]
    public void BuildDownloadWorkflowPlanSkipsKnownCompatibilityFilesWhenIgnoredHashesProvided()
    {
        var ignoredHashes = MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes();

        var result = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
            new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                """
                {
                  "files": {
                    "lib/security/cacerts": {
                      "downloads": {
                        "raw": {
                          "url": "https://piston-data.mojang.com/v1/lib/security/cacerts",
                          "size": 123,
                          "sha1": "12976a6c2b227cbac58969c1455444596c894656"
                        }
                      }
                    },
                    "bin/java": {
                      "downloads": {
                        "raw": {
                          "url": "https://piston-data.mojang.com/v1/bin/java",
                          "size": 456,
                          "sha1": "keep-me"
                        }
                      }
                    }
                  }
                }
                """,
                "/tmp/pcl-java-runtime",
                ignoredHashes,
                MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));

        Assert.AreEqual(1, result.Files.Count);
        Assert.AreEqual("bin/java", result.Files[0].RelativePath);
    }
}
