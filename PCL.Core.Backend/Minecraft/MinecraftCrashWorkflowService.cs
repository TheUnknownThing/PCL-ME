using System;
using System.Collections.Generic;
using System.Globalization;
using PCL.Core.App.I18n;

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
            new(I18nText.Plain("crash.prompts.output.actions.close"), MinecraftCrashOutputPromptActionKind.Close)
        };

        if (!request.IsManualAnalysis)
        {
            if (request.CanOpenModLoaderSettings &&
                request.ResultText.StartsWith(ModLoaderIncompatiblePrefix, StringComparison.Ordinal))
            {
                buttons.Add(new MinecraftCrashOutputPromptButton(
                    I18nText.Plain("crash.prompts.output.actions.open_instance_settings"),
                    MinecraftCrashOutputPromptActionKind.OpenInstanceSettings));
            }
            else if (request.HasDirectFile)
            {
                buttons.Add(new MinecraftCrashOutputPromptButton(
                    I18nText.Plain("crash.prompts.output.actions.view_log"),
                    MinecraftCrashOutputPromptActionKind.ViewLog,
                    ClosesPrompt: false));
            }

            buttons.Add(new MinecraftCrashOutputPromptButton(
                I18nText.Plain("crash.prompts.output.actions.export_report"),
                MinecraftCrashOutputPromptActionKind.ExportReport));
        }

        return new MinecraftCrashOutputPrompt(
            request.ResultText,
            request.IsManualAnalysis
                ? I18nText.Plain("crash.prompts.output.manual_analysis.title")
                : I18nText.Plain("crash.prompts.output.launch_failure.title"),
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
    I18nText Title,
    IReadOnlyList<MinecraftCrashOutputPromptButton> Buttons);

public sealed record MinecraftCrashOutputPromptButton(
    I18nText Label,
    MinecraftCrashOutputPromptActionKind Action,
    bool ClosesPrompt = true);

public enum MinecraftCrashOutputPromptActionKind
{
    Close = 0,
    ViewLog = 1,
    OpenInstanceSettings = 2,
    ExportReport = 3
}
