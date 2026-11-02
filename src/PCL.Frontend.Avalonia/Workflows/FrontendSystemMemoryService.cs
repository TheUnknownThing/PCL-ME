using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendSystemMemoryService
{
    private const long FallbackTotalMemoryBytes = 8L * 1024L * 1024L * 1024L;
    private static readonly TimeSpan MemoryStateCacheDuration = TimeSpan.FromSeconds(5);
    private static readonly object MemoryStateCacheLock = new();
    private static DateTimeOffset _cachedMemoryStateTime;
    private static (double TotalGb, double AvailableGb) _cachedMemoryState;

    public static bool TryGetCachedPhysicalMemoryState(out (double TotalGb, double AvailableGb) memoryState)
    {
        var now = DateTimeOffset.UtcNow;
        lock (MemoryStateCacheLock)
        {
            if (now - _cachedMemoryStateTime <= MemoryStateCacheDuration &&
                _cachedMemoryState.TotalGb > 0)
            {
                memoryState = _cachedMemoryState;
                return true;
            }
        }

        memoryState = default;
        return false;
    }

    public static (double TotalGb, double AvailableGb) GetFallbackPhysicalMemoryState()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        var totalBytes = gcInfo.TotalAvailableMemoryBytes > 0
            ? gcInfo.TotalAvailableMemoryBytes
            : FallbackTotalMemoryBytes;
        var availableBytes = Math.Max(totalBytes - GC.GetTotalMemory(forceFullCollection: false), 0L);
        return ToGigabytes(totalBytes, availableBytes);
    }

    public static (double TotalGb, double AvailableGb) GetPhysicalMemoryState()
    {
        var now = DateTimeOffset.UtcNow;
        lock (MemoryStateCacheLock)
        {
            if (now - _cachedMemoryStateTime <= MemoryStateCacheDuration &&
                _cachedMemoryState.TotalGb > 0)
            {
                return _cachedMemoryState;
            }
        }

        var memoryState = ReadPhysicalMemoryState();
        lock (MemoryStateCacheLock)
        {
            _cachedMemoryState = memoryState;
            _cachedMemoryStateTime = now;
        }

        return memoryState;
    }

    private static (double TotalGb, double AvailableGb) ReadPhysicalMemoryState()
    {
        var (totalBytes, availableBytes) = TryReadPhysicalMemoryBytes();
        if (totalBytes <= 0)
        {
            return GetFallbackPhysicalMemoryState();
        }

        if (availableBytes <= 0)
        {
            availableBytes = Math.Max(totalBytes - GC.GetTotalMemory(forceFullCollection: false), 0L);
        }

        return ToGigabytes(totalBytes, availableBytes);
    }

    private static (double TotalGb, double AvailableGb) ToGigabytes(long totalBytes, long availableBytes)
    {
        return (
            totalBytes / 1024d / 1024d / 1024d,
            availableBytes / 1024d / 1024d / 1024d);
    }

    public static double CalculateAllocatedMemoryGb(
        int memoryModeIndex,
        double customMemoryGb,
        bool isModable,
        bool hasOptiFine,
        int modCount,
        bool? is64BitJava,
        double totalMemoryGb,
        double availableMemoryGb)
    {
        var allocated = memoryModeIndex switch
        {
            0 => CalculateAutomaticAllocationGb(isModable, hasOptiFine, modCount, availableMemoryGb),
            _ => customMemoryGb
        };

        if (is64BitJava == false)
        {
            allocated = Math.Min(1.0, allocated);
        }

        return Math.Round(Math.Min(allocated, Math.Max(totalMemoryGb, 1.0)), 1);
    }

    private static double CalculateAutomaticAllocationGb(
        bool isModable,
        bool hasOptiFine,
        int modCount,
        double availableMemoryGb)
    {
        double minimum;
        double target1;
        double target2;
        double target3;

        if (isModable)
        {
            minimum = 0.5 + modCount / 150d;
            target1 = 1.5 + modCount / 90d;
            target2 = 2.7 + modCount / 50d;
            target3 = 4.5 + modCount / 25d;
        }
        else if (hasOptiFine)
        {
            minimum = 0.5;
            target1 = 1.5;
            target2 = 3.0;
            target3 = 5.0;
        }
        else
        {
            minimum = 0.5;
            target1 = 1.5;
            target2 = 2.5;
            target3 = 4.0;
        }

        var remaining = Math.Max(availableMemoryGb, 0);
        var allocated = 0d;

        allocated += ConsumeStage(ref remaining, target1, 1.0);
        allocated += ConsumeStage(ref remaining, target2 - target1, 0.7);
        allocated += ConsumeStage(ref remaining, target3 - target2, 0.4);
        allocated += ConsumeStage(ref remaining, target3, 0.15);

        return Math.Round(Math.Max(allocated, minimum), 1);
    }

    private static double ConsumeStage(ref double availableMemoryGb, double deltaGb, double efficiency)
    {
        if (availableMemoryGb < 0.1 || deltaGb <= 0)
        {
            return 0;
        }

        var granted = Math.Min(availableMemoryGb * efficiency, deltaGb);
        availableMemoryGb -= deltaGb / efficiency;
        return granted;
    }

    private static (long TotalBytes, long AvailableBytes) TryReadPhysicalMemoryBytes()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                return TryReadLinuxPhysicalMemoryBytes();
            }

            if (OperatingSystem.IsMacOS())
            {
                return TryReadMacPhysicalMemoryBytes();
            }
        }
        catch
        {
            // Fall through to GC-based fallback.
        }

        return (0, 0);
    }

    private static (long TotalBytes, long AvailableBytes) TryReadLinuxPhysicalMemoryBytes()
    {
        if (!File.Exists("/proc/meminfo"))
        {
            return (0, 0);
        }

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

            if (totalKilobytes > 0 && availableKilobytes > 0)
            {
                break;
            }
        }

        return (totalKilobytes * 1024L, availableKilobytes * 1024L);
    }

    private static (long TotalBytes, long AvailableBytes) TryReadMacPhysicalMemoryBytes()
    {
        var totalBytes = ReadCommandNumber("/usr/sbin/sysctl", "-n", "hw.memsize");
        if (totalBytes <= 0)
        {
            totalBytes = ReadCommandNumber("sysctl", "-n", "hw.memsize");
        }

        var vmStatOutput = RunCommand("/usr/bin/vm_stat");
        if (string.IsNullOrWhiteSpace(vmStatOutput))
        {
            vmStatOutput = RunCommand("vm_stat");
        }

        var availableBytes = string.IsNullOrWhiteSpace(vmStatOutput)
            ? 0
            : ParseMacAvailableBytes(vmStatOutput);
        return (totalBytes, availableBytes);
    }

    private static long ParseKilobytes(string line)
    {
        var value = line.Split(':', 2, StringSplitOptions.TrimEntries);
        if (value.Length < 2)
        {
            return 0;
        }

        var digits = new string(value[1].TakeWhile(ch => char.IsDigit(ch) || char.IsWhiteSpace(ch)).ToArray()).Trim();
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static long ParseMacAvailableBytes(string vmStatOutput)
    {
        if (string.IsNullOrWhiteSpace(vmStatOutput))
        {
            return 0;
        }

        var pageSizeMatch = Regex.Match(vmStatOutput, @"page size of (\d+) bytes", RegexOptions.CultureInvariant);
        if (!pageSizeMatch.Success ||
            !long.TryParse(pageSizeMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageSize))
        {
            return 0;
        }

        long freePages = 0;
        long inactivePages = 0;
        long speculativePages = 0;
        foreach (var line in vmStatOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            freePages += ParseVmStatPages(line, "Pages free");
            inactivePages += ParseVmStatPages(line, "Pages inactive");
            speculativePages += ParseVmStatPages(line, "Pages speculative");
        }

        return checked((freePages + inactivePages + speculativePages) * pageSize);
    }

    private static long ParseVmStatPages(string line, string label)
    {
        if (!line.StartsWith(label, StringComparison.Ordinal))
        {
            return 0;
        }

        var numberText = line[(line.IndexOf(':') + 1)..].Trim().TrimEnd('.');
        return long.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pages)
            ? pages
            : 0;
    }

    private static long ReadCommandNumber(string fileName, params string[] arguments)
    {
        var output = RunCommand(fileName, arguments);
        return long.TryParse(output?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string? RunCommand(string fileName, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["LC_ALL"] = "C";
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
