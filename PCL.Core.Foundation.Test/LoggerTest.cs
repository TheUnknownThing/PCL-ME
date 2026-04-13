using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Logging;

namespace PCL.Core.Test;

[TestClass]
public class LoggerTest
{
    [TestMethod]
    public async Task TestSimpleWrite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PCLTest", "Logger", Guid.NewGuid().ToString("N"));
        var loggerOps = new LoggerConfiguration(
            tempDir,
            10 * 1024 * 1024);
        await using var logger = new Logger(loggerOps);
        for (var i = 0; i < 10; i++)
            logger.Info($"Current we got {i}");
        await logger.DisposeAsync();
        Assert.IsTrue(logger.CurrentLogFiles.Any(File.Exists));
    }

    [TestMethod]
    public async Task TestHeavyWrite()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PCLTest", "Logger", Guid.NewGuid().ToString("N"));
        var loggerOps = new LoggerConfiguration(
            tempDir);
        await using var logger = new Logger(loggerOps);
        var tasks = new List<Task>();
        for (var i = 0; i < 25; i++)
        {
            int current = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 25565; j++)
                {
                    logger.Info($"Current we got {current}:{j}");
                }
            }));
        }
        await Task.WhenAll(tasks.ToArray());
        await logger.DisposeAsync();
        Assert.IsTrue(logger.CurrentLogFiles.Any(File.Exists));
    }
}
