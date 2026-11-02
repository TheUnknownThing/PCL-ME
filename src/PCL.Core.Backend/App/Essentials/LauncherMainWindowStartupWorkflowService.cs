using System;

namespace PCL.Core.App.Essentials;

public static class LauncherMainWindowStartupWorkflowService
{
    public static LauncherMainWindowStartupWorkflowPlan BuildPlan(LauncherMainWindowStartupWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var versionIsolationMigration = LauncherStartupVersionIsolationMigrationService.Evaluate(
            new LauncherStartupVersionIsolationMigrationRequest(
                request.HasVersionIsolationV2Setting,
                request.HasLegacyVersionIsolationSetting,
                request.LegacyVersionIsolationValue,
                request.HasWindowHeightSetting,
                request.LegacyVersionIsolationDefaultValue,
                request.VersionIsolationV2DefaultValue));

        var consent = LauncherStartupConsentService.Evaluate(
            new LauncherStartupConsentRequest(
                request.SpecialBuildKind,
                request.IsSpecialBuildHintDisabled,
                request.HasAcceptedEula));

        var milestone = LauncherStartupMilestoneService.AdvanceStartupCount(request.CurrentStartupCount);

        return new LauncherMainWindowStartupWorkflowPlan(
            versionIsolationMigration,
            consent,
            milestone);
    }
}

public sealed record LauncherMainWindowStartupWorkflowRequest(
    bool HasVersionIsolationV2Setting,
    bool HasLegacyVersionIsolationSetting,
    int LegacyVersionIsolationValue,
    bool HasWindowHeightSetting,
    int LegacyVersionIsolationDefaultValue,
    int VersionIsolationV2DefaultValue,
    LauncherStartupSpecialBuildKind SpecialBuildKind,
    bool IsSpecialBuildHintDisabled,
    bool HasAcceptedEula,
    int CurrentStartupCount);

public sealed record LauncherMainWindowStartupWorkflowPlan(
    LauncherStartupVersionIsolationMigrationResult VersionIsolationMigration,
    LauncherStartupConsentResult Consent,
    LauncherStartupMilestoneResult Milestone);
