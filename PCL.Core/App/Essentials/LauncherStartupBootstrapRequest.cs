using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupBootstrapRequest(
    string ExecutableDirectory,
    string TempDirectory,
    string AppDataDirectory,
    bool IsBetaVersion,
    IReadOnlyList<string> EnvironmentWarnings);
