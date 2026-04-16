using System;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupVersionIsolationMigrationService
{
    public static LauncherStartupVersionIsolationMigrationResult Evaluate(LauncherStartupVersionIsolationMigrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HasVersionIsolationV2Setting)
        {
            return LauncherStartupVersionIsolationMigrationResult.None;
        }

        if (request.HasLegacyVersionIsolationSetting)
        {
            return new LauncherStartupVersionIsolationMigrationResult(
                ShouldStoreVersionIsolationV2: true,
                VersionIsolationV2Value: request.LegacyVersionIsolationValue,
                LogMessage: "[Start] Migrated version isolation from legacy PCL");
        }

        if (request.HasWindowHeightSetting)
        {
            return new LauncherStartupVersionIsolationMigrationResult(
                ShouldStoreVersionIsolationV2: true,
                VersionIsolationV2Value: request.LegacyVersionIsolationDefaultValue,
                LogMessage: "[Start] Upgraded from legacy PCL without a version-isolation change; using the legacy default");
        }

        return new LauncherStartupVersionIsolationMigrationResult(
            ShouldStoreVersionIsolationV2: true,
            VersionIsolationV2Value: request.VersionIsolationV2DefaultValue,
            LogMessage: "[Start] Fresh PCL install, using the new version-isolation default");
    }
}

public sealed record LauncherStartupVersionIsolationMigrationRequest(
    bool HasVersionIsolationV2Setting,
    bool HasLegacyVersionIsolationSetting,
    int LegacyVersionIsolationValue,
    bool HasWindowHeightSetting,
    int LegacyVersionIsolationDefaultValue,
    int VersionIsolationV2DefaultValue);

public sealed record LauncherStartupVersionIsolationMigrationResult(
    bool ShouldStoreVersionIsolationV2,
    int VersionIsolationV2Value,
    string? LogMessage)
{
    public static LauncherStartupVersionIsolationMigrationResult None { get; } =
        new(ShouldStoreVersionIsolationV2: false, VersionIsolationV2Value: 0, LogMessage: null);
}
