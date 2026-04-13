using System;
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
        var launchSection = Between(
            launcherLogContent,
            "[Launch] ~ 基础参数 ~",
            "开始 Minecraft 日志监控");
        var allocatedMemory = ExtractBracketValue(launchSection, "分配的内存：");
        var totalMemoryMb = (long)(request.Environment.TotalPhysicalMemoryBytes / 1024 / 1024);
        var builder = new StringBuilder();

        builder.Append("PCL CE 版本：")
            .Append(request.LauncherVersionName)
            .Append(' ')
            .Append(LineBreak);
        builder.Append("识别码：")
            .Append(request.UniqueAddress)
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- 档案信息 -")
            .Append(LineBreak);
        builder.Append("档案名称：")
            .Append(ExtractBracketValue(launchSection, "玩家用户名："))
            .Append(" (验证方式：")
            .Append(ExtractBracketValue(launchSection, "验证方式："))
            .Append(')')
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- 实例信息 -")
            .Append(LineBreak);
        builder.Append("选定的 Java 虚拟机：")
            .Append(ExtractBracketValue(launchSection, "Java 信息："))
            .Append(LineBreak);
        builder.Append("Log4j2 NoLookups：")
            .Append(!launchScriptContent.ContainsF("-Dlog4j2.formatMsgNoLookups=false"))
            .Append(LineBreak);
        builder.Append("MC 文件夹：")
            .Append(ExtractBracketValue(launchSection, "MC 文件夹："))
            .Append(LineBreak);
        builder.Append(LineBreak)
            .Append("- 环境信息 -")
            .Append(LineBreak);
        builder.Append("操作系统：")
            .Append(GetOperatingSystemDisplay(request.Environment))
            .Append("（64 位：")
            .Append(request.Environment.Is64BitOperatingSystem)
            .Append(", ARM64: ")
            .Append(request.Environment.OsArchitecture == Architecture.Arm64)
            .Append('）')
            .Append(LineBreak);
        builder.Append("CPU：")
            .Append(request.Environment.CpuName)
            .Append(LineBreak);
        builder.Append("内存分配 (分配的内存 / 已安装物理内存)：")
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
            builder.Append("显卡 ")
                .Append(i)
                .Append('：')
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

    private static string ExtractBracketValue(string source, string prefix)
    {
        return Between(source, prefix, "[").TrimEnd('[').Trim();
    }

    private static string Between(string source, string after, string before)
    {
        var startPos = string.IsNullOrEmpty(after)
            ? -1
            : source.LastIndexOf(after, StringComparison.Ordinal);
        startPos = startPos >= 0 ? startPos + after.Length : 0;

        var endPos = string.IsNullOrEmpty(before)
            ? -1
            : source.IndexOf(before, startPos, StringComparison.Ordinal);
        if (endPos >= 0) return source.Substring(startPos, endPos - startPos);
        if (startPos > 0) return source[startPos..];
        return source;
    }
}
