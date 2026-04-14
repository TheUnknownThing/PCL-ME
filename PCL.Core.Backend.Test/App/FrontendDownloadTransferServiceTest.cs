using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendDownloadTransferServiceTest
{
    [TestMethod]
    public async Task CopyToPathAsync_WritesStreamAndReportsProgress()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "pcl-download-transfer-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            var payload = Enumerable.Range(0, 4096).Select(index => (byte)(index % byte.MaxValue)).ToArray();
            await using var stream = new MemoryStream(payload, writable: false);
            var progressSamples = new List<long>();

            await FrontendDownloadTransferService.CopyToPathAsync(
                stream,
                outputPath,
                transferredBytes => progressSamples.Add(transferredBytes));

            CollectionAssert.AreEqual(payload, File.ReadAllBytes(outputPath));
            Assert.IsTrue(progressSamples.Count > 0);
            Assert.AreEqual(payload.Length, progressSamples[^1]);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
