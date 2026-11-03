using System.Globalization;
using Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Controls;
using static PCL.Frontend.Avalonia.E2E.DesktopFixture;

namespace PCL.Frontend.Avalonia.E2E;

[TestClass]
[DoNotParallelize]
public sealed class SetupMemoryE2E
{
    [TestMethod]
    public Task SetupLaunchPage_ShowsRealSystemMemoryUsage() => Run(async () =>
    {
        await using var app = new DesktopFixture();
        await app.Start();
        await app.Session.NavigateAsync("Setup/SetupLaunch");
        await Settle();
        var summary = app.Window.GetVisualDescendants().OfType<PclMemorySummary>().Single();
        // The fallback state reports the launcher's own managed heap as "used", so wait
        // until the async platform refresh has replaced it before comparing values.
        await Wait(() => ParseGigabytes(summary.UsedRamLabel) > 0.1, "The memory summary did not report any used memory.");
        await Wait(() => ParseGigabytes(summary.TotalRamLabel) > 0.5, "The memory summary did not report the installed memory.");

        var usedGb = ParseGigabytes(summary.UsedRamLabel);
        var totalGb = ParseGigabytes(summary.TotalRamLabel);
        Assert.AreEqual(app.Launcher.UsedRamLabel, summary.UsedRamLabel);
        Assert.AreEqual(app.Launcher.TotalRamLabel, summary.TotalRamLabel);
        Assert.IsTrue(usedGb < totalGb, $"Used memory ({usedGb} GB) must stay below installed memory ({totalGb} GB).");

        if (File.Exists("/proc/meminfo"))
        {
            var (systemTotalGb, systemAvailableGb) = ReadLinuxMemoryGigabytes();
            Assert.AreEqual(systemTotalGb, totalGb, systemTotalGb * 0.02,
                "The displayed installed memory must match the system's physical memory.");
            Assert.AreEqual(systemTotalGb - systemAvailableGb, usedGb, 2.0,
                "The displayed used memory must match real system-wide usage, not the launcher's own heap.");
        }

        app.Screenshot("setup-launch-memory");
    });

    private static double ParseGigabytes(string label)
    {
        var value = label.TrimEnd('G', 'B', ' ').Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var gigabytes) ? gigabytes : -1;
    }

    private static (double TotalGb, double AvailableGb) ReadLinuxMemoryGigabytes()
    {
        long totalKilobytes = 0;
        long availableKilobytes = 0;
        foreach (var line in File.ReadLines("/proc/meminfo"))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                totalKilobytes = ParseKilobytes(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                availableKilobytes = ParseKilobytes(line);
            }
        }

        return (totalKilobytes / 1024d / 1024d, availableKilobytes / 1024d / 1024d);
    }

    private static long ParseKilobytes(string line)
    {
        var value = line.Split(':', 2, StringSplitOptions.TrimEntries);
        return long.TryParse(value[1].Split(' ', 2)[0], out var kilobytes) ? kilobytes : 0;
    }
}
