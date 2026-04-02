using System;
using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public static class LauncherVersionTransitionWorkflowService
{
    public static LauncherVersionTransitionWorkflowPlan BuildPlan(LauncherVersionTransitionWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TransitionRequest);

        var transition = LauncherVersionTransitionService.Evaluate(request.TransitionRequest);
        var settings = new List<LauncherVersionTransitionSettingWrite>();

        if (transition.ShouldStoreCurrentVersion)
        {
            settings.Add(new LauncherVersionTransitionSettingWrite("SystemLastVersionReg", request.TransitionRequest.CurrentVersionCode));
        }

        string? highestVersionLogMessage = null;
        if (transition.HighestVersionStorageKey is not null && transition.HighestVersionToStore.HasValue)
        {
            settings.Add(new LauncherVersionTransitionSettingWrite(transition.HighestVersionStorageKey, transition.HighestVersionToStore.Value));
            highestVersionLogMessage = "[Start] 最高版本号从 " + request.TransitionRequest.HighestRecordedVersionCode + " 升高到 " + transition.HighestVersionToStore.Value;
        }

        if (transition.LaunchArgumentWindowTypeToStore.HasValue)
        {
            settings.Add(new LauncherVersionTransitionSettingWrite("LaunchArgumentWindowType", transition.LaunchArgumentWindowTypeToStore.Value));
        }

        if (transition.ThemeHiddenV2ToStore is not null)
        {
            settings.Add(new LauncherVersionTransitionSettingWrite("UiLauncherThemeHide2", transition.ThemeHiddenV2ToStore));
        }

        string? modNameMigrationLogMessage = null;
        if (transition.ModNameSettingV2ToStore.HasValue)
        {
            settings.Add(new LauncherVersionTransitionSettingWrite("ToolDownloadTranslateV2", transition.ModNameSettingV2ToStore.Value));
            modNameMigrationLogMessage = "[Start] 已从老版本迁移 Mod 命名设置";
        }

        var customSkinMigration = BuildCustomSkinMigrationPlan(transition.CustomSkinMigrationSource, request);

        return new LauncherVersionTransitionWorkflowPlan(
            transition,
            settings,
            highestVersionLogMessage,
            customSkinMigration,
            transition.ShouldUnhideSetupAbout ? "[Start] 已解除帮助页面的隐藏" : null,
            modNameMigrationLogMessage);
    }

    private static LauncherVersionTransitionFileCopyPlan? BuildCustomSkinMigrationPlan(
        LauncherCustomSkinMigrationSourceKind? migrationSource,
        LauncherVersionTransitionWorkflowRequest request)
    {
        return migrationSource switch
        {
            LauncherCustomSkinMigrationSourceKind.ExecutableDirectory => new LauncherVersionTransitionFileCopyPlan(
                request.LegacyExecutableCustomSkinPath,
                request.AppDataCustomSkinPath,
                "[Start] 已移动离线自定义皮肤 (162)"),
            LauncherCustomSkinMigrationSourceKind.TempDirectory => new LauncherVersionTransitionFileCopyPlan(
                request.LegacyTempCustomSkinPath,
                request.AppDataCustomSkinPath,
                "[Start] 已移动离线自定义皮肤 (264)"),
            _ => null
        };
    }
}

public sealed record LauncherVersionTransitionWorkflowRequest(
    LauncherVersionTransitionRequest TransitionRequest,
    string LegacyExecutableCustomSkinPath,
    string LegacyTempCustomSkinPath,
    string AppDataCustomSkinPath);

public sealed record LauncherVersionTransitionWorkflowPlan(
    LauncherVersionTransitionResult Transition,
    IReadOnlyList<LauncherVersionTransitionSettingWrite> SettingWrites,
    string? HighestVersionLogMessage,
    LauncherVersionTransitionFileCopyPlan? CustomSkinMigration,
    string? SetupAboutUnhideLogMessage,
    string? ModNameMigrationLogMessage);

public sealed record LauncherVersionTransitionSettingWrite(
    string Key,
    object Value);

public sealed record LauncherVersionTransitionFileCopyPlan(
    string SourcePath,
    string TargetPath,
    string LogMessage);
