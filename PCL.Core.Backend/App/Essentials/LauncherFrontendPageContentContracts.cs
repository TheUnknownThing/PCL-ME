namespace PCL.Core.App.Essentials;

public sealed record LauncherFrontendPromptLaneSummary(
    string Id,
    string Title,
    string Summary,
    int Count,
    bool IsSelected);

public sealed record LauncherFrontendLaunchSurfaceData(
    string ScenarioLabel,
    string LoginProviderLabel,
    string SelectedIdentityLabel,
    int LoginStepCount,
    string JavaRuntimeLabel,
    string? JavaWarningMessage,
    string? JavaDownloadTarget,
    string ResolutionLabel,
    int ClasspathEntryCount,
    int ReplacementValueCount,
    string NativesDirectory,
    string OptionsTargetFilePath,
    bool WritesLauncherProfiles,
    bool HasScriptExport,
    string? ScriptExportPath,
    string CompletionMessage);

public sealed record LauncherFrontendCrashSurfaceData(
    string SuggestedArchiveName,
    int SourceFileCount,
    bool IncludesLauncherLog,
    string? LauncherLogPath);

public sealed record LauncherFrontendPageContent(
    string Eyebrow,
    string Summary,
    IReadOnlyList<LauncherFrontendPageFact> Facts,
    IReadOnlyList<LauncherFrontendPageSection> Sections);

public sealed record LauncherFrontendPageFact(
    string Label,
    string Value);

public sealed record LauncherFrontendPageSection(
    string Eyebrow,
    string Title,
    IReadOnlyList<string> Lines);
