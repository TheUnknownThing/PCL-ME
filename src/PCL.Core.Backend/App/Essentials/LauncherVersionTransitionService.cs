using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.App.Essentials;

public static class LauncherVersionTransitionService
{
    private const string HighestBetaVersionKey = "SystemHighestBetaVersionReg";
    private const string HighestAlphaVersionKey = "SystemHighestAlphaVersionReg";

    public static LauncherVersionTransitionResult Evaluate(LauncherVersionTransitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isUpgrade = request.LastVersionCode < request.CurrentVersionCode;
        var isDowngrade = request.LastVersionCode > request.CurrentVersionCode;
        if (!isUpgrade && !isDowngrade)
        {
            return new LauncherVersionTransitionResult(
                IsUpgrade: false,
                IsDowngrade: false,
                ShouldStoreCurrentVersion: false,
                HighestVersionStorageKey: null,
                HighestVersionToStore: null,
                LaunchArgumentWindowTypeToStore: null,
                ThemeHiddenV2ToStore: null,
                CustomSkinMigrationSource: null,
                ShouldUnhideSetupAbout: false,
                ShouldMigrateOldProfile: false,
                ModNameSettingV2ToStore: null,
                ShouldShowCommunityAnnouncement: false,
                ShouldShowUpdateLog: false,
                Notices: []);
        }

        if (isDowngrade)
        {
            return new LauncherVersionTransitionResult(
                IsUpgrade: false,
                IsDowngrade: true,
                ShouldStoreCurrentVersion: true,
                HighestVersionStorageKey: null,
                HighestVersionToStore: null,
                LaunchArgumentWindowTypeToStore: null,
                ThemeHiddenV2ToStore: null,
                CustomSkinMigrationSource: null,
                ShouldUnhideSetupAbout: false,
                ShouldMigrateOldProfile: false,
                ModNameSettingV2ToStore: null,
                ShouldShowCommunityAnnouncement: false,
                ShouldShowUpdateLog: false,
                Notices: []);
        }

        var notices = new List<LauncherVersionTransitionNotice>();
        string? highestVersionStorageKey = null;
        int? highestVersionToStore = null;
        if (request.HighestRecordedVersionCode < request.CurrentVersionCode)
        {
            highestVersionStorageKey = request.IsBetaBuild ? HighestBetaVersionKey : HighestAlphaVersionKey;
            highestVersionToStore = request.CurrentVersionCode;
        }

        var themeIds = request.HighestRecordedVersionCode <= 207
            ? MergeThemeIds(request.ThemeHiddenLegacy, request.ThemeHiddenV2)
            : ParseThemeIds(request.ThemeHiddenV2);
        var shouldStoreThemeHiddenV2 = request.HighestRecordedVersionCode <= 207;

        if (request.LastVersionCode <= 115 && themeIds.Remove("13"))
        {
            shouldStoreThemeHiddenV2 = true;
            notices.Add(new LauncherVersionTransitionNotice(
                "Re-unlock notice",
                "The new PCL version changed how the Lucky theme is unlocked, so you need to unlock it again." + Environment.NewLine +
                "Thanks for understanding!"));
        }

        if (request.LastVersionCode <= 152 && themeIds.Remove("12"))
        {
            shouldStoreThemeHiddenV2 = true;
            notices.Add(new LauncherVersionTransitionNotice(
                "Re-unlock notice",
                "The new PCL version changed how the Comic theme is unlocked, so you need to unlock it again." + Environment.NewLine +
                "Thanks for understanding!"));
        }

        LauncherCustomSkinMigrationSourceKind? customSkinMigrationSource = null;
        if (!request.HasAppDataCustomSkin)
        {
            if (request.LastVersionCode <= 161 && request.HasLegacyExecutableCustomSkin)
            {
                customSkinMigrationSource = LauncherCustomSkinMigrationSourceKind.ExecutableDirectory;
            }
            else if (request.LastVersionCode <= 263 && request.HasLegacyTempCustomSkin)
            {
                customSkinMigrationSource = LauncherCustomSkinMigrationSourceKind.TempDirectory;
            }
        }

        int? modNameSettingV2ToStore = null;
        if (request.HasLegacyModNameSetting && !request.HasModNameSettingV2)
        {
            modNameSettingV2ToStore = request.LegacyModNameSetting + 1;
        }

        return new LauncherVersionTransitionResult(
            IsUpgrade: true,
            IsDowngrade: false,
            ShouldStoreCurrentVersion: true,
            HighestVersionStorageKey: highestVersionStorageKey,
            HighestVersionToStore: highestVersionToStore,
            LaunchArgumentWindowTypeToStore: request.LaunchArgumentWindowType == 5 ? 1 : null,
            ThemeHiddenV2ToStore: shouldStoreThemeHiddenV2 ? string.Join("|", themeIds) : null,
            CustomSkinMigrationSource: customSkinMigrationSource,
            ShouldUnhideSetupAbout: request.LastVersionCode <= 205,
            ShouldMigrateOldProfile: request.LastVersionCode <= 368,
            ModNameSettingV2ToStore: modNameSettingV2ToStore,
            ShouldShowCommunityAnnouncement: true,
            ShouldShowUpdateLog: request.LastVersionCode > 0 && request.HighestRecordedVersionCode < request.CurrentVersionCode,
            Notices: notices);
    }

    private static List<string> MergeThemeIds(string? legacyThemeIds, string? currentThemeIds)
    {
        var merged = new List<string> { "2" };
        AppendDistinct(merged, ParseThemeIds(legacyThemeIds));
        AppendDistinct(merged, ParseThemeIds(currentThemeIds));
        return merged;
    }

    private static List<string> ParseThemeIds(string? themeIds)
    {
        var parsed = new List<string>();
        if (string.IsNullOrWhiteSpace(themeIds))
        {
            return parsed;
        }

        foreach (var themeId in themeIds.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!parsed.Contains(themeId, StringComparer.Ordinal))
            {
                parsed.Add(themeId);
            }
        }

        return parsed;
    }

    private static void AppendDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!target.Contains(value, StringComparer.Ordinal))
            {
                target.Add(value);
            }
        }
    }
}

public sealed record LauncherVersionTransitionRequest(
    int LastVersionCode,
    int CurrentVersionCode,
    bool IsBetaBuild,
    int HighestRecordedVersionCode,
    int LaunchArgumentWindowType,
    string? ThemeHiddenLegacy,
    string? ThemeHiddenV2,
    bool HasLegacyExecutableCustomSkin,
    bool HasLegacyTempCustomSkin,
    bool HasAppDataCustomSkin,
    bool HasLegacyModNameSetting,
    bool HasModNameSettingV2,
    int LegacyModNameSetting);

public sealed record LauncherVersionTransitionResult(
    bool IsUpgrade,
    bool IsDowngrade,
    bool ShouldStoreCurrentVersion,
    string? HighestVersionStorageKey,
    int? HighestVersionToStore,
    int? LaunchArgumentWindowTypeToStore,
    string? ThemeHiddenV2ToStore,
    LauncherCustomSkinMigrationSourceKind? CustomSkinMigrationSource,
    bool ShouldUnhideSetupAbout,
    bool ShouldMigrateOldProfile,
    int? ModNameSettingV2ToStore,
    bool ShouldShowCommunityAnnouncement,
    bool ShouldShowUpdateLog,
    IReadOnlyList<LauncherVersionTransitionNotice> Notices);

public sealed record LauncherVersionTransitionNotice(
    string Title,
    string Message);

public enum LauncherCustomSkinMigrationSourceKind
{
    ExecutableDirectory = 0,
    TempDirectory = 1
}
