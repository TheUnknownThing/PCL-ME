using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashReportBuilder
{
    private const string LineBreak = "\r\n";

    public static string BuildEnvironmentReport(MinecraftCrashEnvironmentReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Environment);

        var launcherLogContent = request.LauncherLogContent ?? string.Empty;
        var launchScriptContent = request.LaunchScriptContent ?? string.Empty;
        var launchSection = BetweenAny(
            launcherLogContent,
            ["[Launch] ~ Base Parameters ~", "[Launch] ~ 基础参数 ~"],
            ["Start Minecraft log monitoring", "开始 Minecraft 日志监控"]);
        var allocatedMemory = ExtractBracketValue(launchSection, "Allocated memory:", "分配的内存：");
        var totalMemoryMb = (long)(request.Environment.TotalPhysicalMemoryBytes / 1024 / 1024);
        var builder = new StringBuilder();

        builder.Append("PCL-ME version: ")
            .Append(request.LauncherVersionName)
            .Append(' ')
            .Append(LineBreak);
        builder.Append("Identifier: ")
            .Append(request.UniqueAddress)
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- Profile -")
            .Append(LineBreak);
        builder.Append("Profile name: ")
            .Append(ExtractBracketValue(launchSection, "Player name:", "玩家用户名："))
            .Append(" (auth method: ")
            .Append(ExtractBracketValue(launchSection, "Login type:", "验证方式："))
            .Append(')')
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- Instance -")
            .Append(LineBreak);
        builder.Append("Selected Java runtime: ")
            .Append(ExtractBracketValue(launchSection, "Java info:", "Java 信息："))
            .Append(LineBreak);
        builder.Append("Log4j2 NoLookups: ")
            .Append(!launchScriptContent.ContainsF("-Dlog4j2.formatMsgNoLookups=false"))
            .Append(LineBreak);
        builder.Append("MC folder: ")
            .Append(ExtractBracketValue(launchSection, "Minecraft folder:", "MC 文件夹："))
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- Environment -")
            .Append(LineBreak);
        builder.Append("Operating system: ")
            .Append(GetOperatingSystemDisplay(request.Environment))
            .Append(" (64-bit: ")
            .Append(request.Environment.Is64BitOperatingSystem)
            .Append(", ARM64: ")
            .Append(request.Environment.OsArchitecture == Architecture.Arm64)
            .Append(')')
            .Append(LineBreak);
        builder.Append("CPU: ")
            .Append(request.Environment.CpuName)
            .Append(LineBreak);
        builder.Append("Memory allocation (allocated / installed physical memory): ")
            .Append(allocatedMemory)
            .Append(" / ")
            .Append(Math.Round(totalMemoryMb / 1024d, 2))
            .Append(" GB (")
            .Append(totalMemoryMb)
            .Append(" MB)")
            .Append(LineBreak);

        for (var i = 0; i < request.Environment.Gpus.Count; i++)
        {
            var gpu = request.Environment.Gpus[i];
            builder.Append("GPU ")
                .Append(i)
                .Append(": ")
                .Append(gpu.Name)
                .Append(" (")
                .Append(gpu.MemoryMegabytes >= 4095 ? ">= " + gpu.MemoryMegabytes : gpu.MemoryMegabytes)
                .Append(" MB, ")
                .Append(gpu.DriverVersion)
                .Append(')')
                .Append(LineBreak);
        }

        return builder.ToString();
    }

    private static string GetOperatingSystemDisplay(SystemEnvironmentSnapshot environment)
    {
        return $"{environment.OsDescription} {environment.OsVersion}".Trim();
    }

    private static string ExtractBracketValue(string source, params string[] prefixes)
    {
        return BetweenAny(source, prefixes, "[", "［").TrimEnd('[', '［').Trim();
    }

    private static string BetweenAny(string source, IReadOnlyList<string> afterValues, params string[] beforeValues)
    {
        var startPos = -1;
        foreach (var after in afterValues)
        {
            if (string.IsNullOrEmpty(after))
            {
                continue;
            }

            startPos = source.LastIndexOf(after, StringComparison.Ordinal);
            if (startPos >= 0)
            {
                startPos += after.Length;
                break;
            }
        }

        if (startPos < 0)
        {
            startPos = 0;
        }

        var endPos = -1;
        foreach (var before in beforeValues)
        {
            if (string.IsNullOrEmpty(before))
            {
                continue;
            }

            endPos = source.IndexOf(before, startPos, StringComparison.Ordinal);
            if (endPos >= 0)
            {
                break;
            }
        }

        if (endPos >= 0) return source.Substring(startPos, endPos - startPos);
        if (startPos > 0) return source[startPos..];
        return source;
    }
}
