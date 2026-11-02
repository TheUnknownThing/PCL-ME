using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchOptionsFileService
{
    public static MinecraftLaunchOptionsSyncPlan BuildPlan(MinecraftLaunchOptionsSyncRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var targetKind = request.AutoChangeLanguage &&
                         !request.PrimaryOptionsFileExists &&
                         request.YosbrOptionsFileExists
            ? MinecraftLaunchOptionsFileTargetKind.Yosbr
            : MinecraftLaunchOptionsFileTargetKind.Primary;

        var selectionLogMessage = targetKind == MinecraftLaunchOptionsFileTargetKind.Yosbr
            ? "Will modify options.txt in the Yosbr mod"
            : null;

        var writes = new List<MinecraftLaunchOptionWrite>();
        var logMessages = new List<string>();

        var currentLanguage = NormalizeLanguage(
            targetKind == MinecraftLaunchOptionsFileTargetKind.Yosbr
                ? "none"
                : request.PrimaryCurrentLanguage);

        if (targetKind == MinecraftLaunchOptionsFileTargetKind.Yosbr)
        {
            writes.Add(new MinecraftLaunchOptionWrite("lang", "none"));
        }

        if (request.AutoChangeLanguage)
        {
            var shouldUseDefault = currentLanguage == "none" || !request.HasExistingSaves;
            var requiredLanguage = ResolveRequiredLanguage(currentLanguage, shouldUseDefault, request.ReleaseTime);

            if (currentLanguage == requiredLanguage)
            {
                logMessages.Add($"Required language is {requiredLanguage}; current language is {currentLanguage}; no change needed");
            }
            else
            {
                writes.Add(new MinecraftLaunchOptionWrite("lang", "-"));
                writes.Add(new MinecraftLaunchOptionWrite("lang", requiredLanguage));
                logMessages.Add($"Changed language from {currentLanguage} to {requiredLanguage}");
            }

            if (shouldUseDefault)
            {
                writes.Add(new MinecraftLaunchOptionWrite("forceUnicodeFont", "true"));
                logMessages.Add("Enabled forceUnicodeFont to keep the font rendering correctly");
            }
        }

        switch (request.LaunchWindowType)
        {
            case 0:
                writes.Add(new MinecraftLaunchOptionWrite("fullscreen", "true"));
                break;
            case 1:
                break;
            default:
                writes.Add(new MinecraftLaunchOptionWrite("fullscreen", "false"));
                break;
        }

        return new MinecraftLaunchOptionsSyncPlan(
            targetKind,
            selectionLogMessage,
            writes,
            logMessages);
    }

    private static string ResolveRequiredLanguage(string currentLanguage, bool shouldUseDefault, DateTime releaseTime)
    {
        var isUnder1dot1 = releaseTime > new DateTime(2000, 1, 1) &&
                           releaseTime <= new DateTime(2011, 11, 18);
        if (isUnder1dot1)
        {
            return "none";
        }

        var requiredLanguage = shouldUseDefault ? "zh_cn" : currentLanguage.ToLowerInvariant();
        if (releaseTime >= new DateTime(2012, 1, 12) &&
            releaseTime <= new DateTime(2016, 6, 8))
        {
            requiredLanguage = "zh_CN";
        }

        return requiredLanguage;
    }

    private static string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? "none" : language;
    }
}

public sealed record MinecraftLaunchOptionsSyncRequest(
    bool AutoChangeLanguage,
    bool PrimaryOptionsFileExists,
    string? PrimaryCurrentLanguage,
    bool YosbrOptionsFileExists,
    bool HasExistingSaves,
    DateTime ReleaseTime,
    int LaunchWindowType);

public sealed record MinecraftLaunchOptionsSyncPlan(
    MinecraftLaunchOptionsFileTargetKind TargetKind,
    string? TargetSelectionLogMessage,
    IReadOnlyList<MinecraftLaunchOptionWrite> Writes,
    IReadOnlyList<string> LogMessages);

public sealed record MinecraftLaunchOptionWrite(
    string Key,
    string Value);

public enum MinecraftLaunchOptionsFileTargetKind
{
    Primary = 0,
    Yosbr = 1
}
