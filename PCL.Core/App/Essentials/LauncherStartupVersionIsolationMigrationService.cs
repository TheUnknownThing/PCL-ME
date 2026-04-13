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
                LogMessage: "[Start] 从老 PCL 迁移版本隔离");
        }

        if (request.HasWindowHeightSetting)
        {
            return new LauncherStartupVersionIsolationMigrationResult(
                ShouldStoreVersionIsolationV2: true,
                VersionIsolationV2Value: request.LegacyVersionIsolationDefaultValue,
                LogMessage: "[Start] 从老 PCL 升级，但此前未调整版本隔离，使用老的版本隔离默认值");
        }

        return new LauncherStartupVersionIsolationMigrationResult(
            ShouldStoreVersionIsolationV2: true,
            VersionIsolationV2Value: request.VersionIsolationV2DefaultValue,
            LogMessage: "[Start] 全新的 PCL，使用新的版本隔离默认值");
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
