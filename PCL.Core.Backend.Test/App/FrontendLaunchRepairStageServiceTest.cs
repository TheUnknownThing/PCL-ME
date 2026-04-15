using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendLaunchRepairStageServiceTest
{
    [TestMethod]
    public void ResolveStage_UsesValidationLabelForAssetsWithCurrentFile()
    {
        var snapshot = new FrontendInstanceRepairProgressSnapshot(
            new Dictionary<FrontendInstanceRepairFileGroup, FrontendInstanceRepairGroupSnapshot>
            {
                [FrontendInstanceRepairFileGroup.Assets] = new(
                    FrontendInstanceRepairFileGroup.Assets,
                    CompletedFiles: 2,
                    TotalFiles: 5,
                    CompletedBytes: 40,
                    TotalBytes: 100,
                    CurrentFileName: "minecraft/sounds.json")
            },
            CurrentFileName: "minecraft/sounds.json",
            DownloadedFileCount: 2,
            ReusedFileCount: 0,
            TotalFileCount: 5,
            RemainingFileCount: 3,
            DownloadedBytes: 40,
            TotalBytes: 100,
            SpeedBytesPerSecond: 2048);

        var result = FrontendLaunchRepairStageService.ResolveStage(snapshot);

        Assert.AreEqual("校验资源文件 • minecraft/sounds.json", result);
    }

    [TestMethod]
    public void ResolveStage_FallsBackToSupportFilesAfterAssetsComplete()
    {
        var snapshot = new FrontendInstanceRepairProgressSnapshot(
            new Dictionary<FrontendInstanceRepairFileGroup, FrontendInstanceRepairGroupSnapshot>
            {
                [FrontendInstanceRepairFileGroup.Assets] = new(
                    FrontendInstanceRepairFileGroup.Assets,
                    CompletedFiles: 5,
                    TotalFiles: 5,
                    CompletedBytes: 100,
                    TotalBytes: 100,
                    CurrentFileName: string.Empty),
                [FrontendInstanceRepairFileGroup.Libraries] = new(
                    FrontendInstanceRepairFileGroup.Libraries,
                    CompletedFiles: 1,
                    TotalFiles: 3,
                    CompletedBytes: 10,
                    TotalBytes: 30,
                    CurrentFileName: "guava.jar")
            },
            CurrentFileName: "guava.jar",
            DownloadedFileCount: 6,
            ReusedFileCount: 0,
            TotalFileCount: 8,
            RemainingFileCount: 2,
            DownloadedBytes: 110,
            TotalBytes: 130,
            SpeedBytesPerSecond: 1024);

        var result = FrontendLaunchRepairStageService.ResolveStage(snapshot);

        Assert.AreEqual("补全支持文件 • guava.jar", result);
    }
}
