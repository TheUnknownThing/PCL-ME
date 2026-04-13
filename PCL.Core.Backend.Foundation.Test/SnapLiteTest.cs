using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using PCL.Core.Utils.VersionControl;

namespace PCL.Core.Test;

[TestClass]
public class SnapLiteTest
{
    [TestMethod]
    public async Task TestMake()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "PCLTest", "SnapLiteTest", Guid.NewGuid().ToString("N"));
        var exportPath = Path.Combine(Path.GetTempPath(), "PCLTest", "SnapLiteTest", $"{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(testRoot);

        try
        {
            using var snap = new SnapLiteVersionControl(testRoot);
            var nodeId = await snap.CreateNewVersion();
            await snap.Export(nodeId, exportPath);
            Assert.IsTrue(await snap.CheckVersion(nodeId));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }

            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }
}
