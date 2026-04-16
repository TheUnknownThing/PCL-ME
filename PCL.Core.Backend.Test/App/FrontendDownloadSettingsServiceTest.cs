using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendDownloadSettingsServiceTest
{
    [TestMethod]
    public void ResolveTransferOptions_ReadsConfiguredThreadAndUnlimitedSpeed()
    {
        var configPath = CreateTempConfigPath();
        try
        {
            var provider = new JsonFileProvider(configPath);
            provider.Set("ToolDownloadThread", 96);
            provider.Set("ToolDownloadSpeed", 42);
            provider.Sync();

            var result = FrontendDownloadSettingsService.ResolveTransferOptions(provider);

            Assert.AreEqual(96, result.MaxConcurrentFileTransfers);
            Assert.IsNull(result.MaxBytesPerSecond);
        }
        finally
        {
            TryDeleteFile(configPath);
        }
    }

    [TestMethod]
    [DataRow(0d, "0.1 M/s", 104858L)]
    [DataRow(14d, "1.5 M/s", 1572864L)]
    [DataRow(15d, "2.0 M/s", 2097152L)]
    [DataRow(31d, "10.0 M/s", 10485760L)]
    [DataRow(41d, "20 M/s", 20971520L)]
    [DataRow(42d, "Unlimited", null)]
    public void SpeedLimitMapping_FormatsLabelsAndTransferRate(
        double configuredValue,
        string expectedLabel,
        long? expectedBytesPerSecond)
    {
        Assert.AreEqual(expectedLabel, FrontendDownloadSettingsService.FormatSpeedLimitLabel(configuredValue));
        Assert.AreEqual(expectedBytesPerSecond, FrontendDownloadSettingsService.ResolveSpeedLimitBytesPerSecond(configuredValue));
    }

    [TestMethod]
    [DataRow(-5d, 1)]
    [DataRow(1d, 1)]
    [DataRow(63d, 63)]
    [DataRow(512d, 255)]
    public void ThreadLimitMapping_ClampsToSupportedRange(double configuredValue, int expectedThreadLimit)
    {
        Assert.AreEqual(expectedThreadLimit, FrontendDownloadSettingsService.ResolveMaxConcurrentFileTransfers(configuredValue));
        Assert.AreEqual(expectedThreadLimit.ToString(), FrontendDownloadSettingsService.FormatThreadLimitLabel(configuredValue));
    }

    private static string CreateTempConfigPath()
    {
        return Path.Combine(Path.GetTempPath(), "pcl-download-settings-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
