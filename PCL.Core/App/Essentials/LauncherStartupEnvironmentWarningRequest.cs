using System;

namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupEnvironmentWarningRequest(
    string ExecutableDirectory,
    Version DetectedWindowsVersion,
    bool Is64BitOperatingSystem);
