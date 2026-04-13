using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupBootstrapResult(
    IReadOnlyList<string> DirectoriesToCreate,
    IReadOnlyList<string> ConfigKeysToLoad,
    IReadOnlyList<string> LegacyLogFilesToDelete,
    UpdateChannel DefaultUpdateChannel,
    string? EnvironmentWarningMessage);
