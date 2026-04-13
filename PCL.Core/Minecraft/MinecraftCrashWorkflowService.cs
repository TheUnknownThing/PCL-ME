using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCL.Core.Minecraft;

public static class MinecraftCrashWorkflowService
{
    private const string ModLoaderIncompatiblePrefix = "Mod 加载器版本与 Mod 不兼容";

    public static MinecraftCrashOutputPrompt BuildOutputPrompt(MinecraftCrashOutputPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ResultText);

        var buttons = new List<MinecraftCrashOutputPromptButton>
        {
            new("确定", MinecraftCrashOutputPromptActionKind.Close)
        };

        if (!request.IsManualAnalysis)
        {
            if (request.CanOpenModLoaderSettings &&
                request.ResultText.StartsWith(ModLoaderIncompatiblePrefix, StringComparison.Ordinal))
            {
                buttons.Add(new MinecraftCrashOutputPromptButton("前往修改", MinecraftCrashOutputPromptActionKind.OpenInstanceSettings));
            }
            else if (request.HasDirectFile)
            {
                buttons.Add(new MinecraftCrashOutputPromptButton("查看日志", MinecraftCrashOutputPromptActionKind.ViewLog, ClosesPrompt: false));
            }

            buttons.Add(new MinecraftCrashOutputPromptButton("导出错误报告", MinecraftCrashOutputPromptActionKind.ExportReport));
        }

        return new MinecraftCrashOutputPrompt(
            request.ResultText,
            request.IsManualAnalysis ? "错误报告分析结果" : "Minecraft 出现错误",
            buttons);
    }

    public static string GetSuggestedExportArchiveName(DateTime timestamp, CultureInfo? culture = null)
    {
        var formattedTimestamp = timestamp
            .ToString("G", culture ?? CultureInfo.CurrentCulture)
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace(":", ".", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return $"错误报告-{formattedTimestamp}.zip";
    }
}

public sealed record MinecraftCrashOutputPromptRequest(
    string ResultText,
    bool IsManualAnalysis,
    bool HasDirectFile,
    bool CanOpenModLoaderSettings);

public sealed record MinecraftCrashOutputPrompt(
    string Message,
    string Title,
    IReadOnlyList<MinecraftCrashOutputPromptButton> Buttons);

public sealed record MinecraftCrashOutputPromptButton(
    string Label,
    MinecraftCrashOutputPromptActionKind Action,
    bool ClosesPrompt = true);

public enum MinecraftCrashOutputPromptActionKind
{
    Close = 0,
    ViewLog = 1,
    OpenInstanceSettings = 2,
    ExportReport = 3
}
