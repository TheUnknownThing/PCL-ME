namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendSetupComposition(
    FrontendSetupAboutState About,
    FrontendSetupLogState Log,
    FrontendSetupUpdateState Update,
    FrontendSetupLaunchState Launch,
    FrontendSetupGameManageState GameManage,
    FrontendSetupLauncherMiscState LauncherMisc,
    FrontendSetupJavaState Java,
    FrontendSetupUiState Ui);

internal sealed record FrontendSetupAboutState(
    string LauncherVersionSummary);

internal sealed record FrontendSetupLogState(
    IReadOnlyList<FrontendSetupLogEntry> Entries);

internal sealed record FrontendSetupLogEntry(
    string Title,
    string Summary,
    string Path);

internal sealed record FrontendSetupUpdateState(
    int UpdateChannelIndex,
    int UpdateModeIndex);

internal sealed record FrontendSetupLaunchState(
    int IsolationIndex,
    string WindowTitle,
    string CustomInfo,
    int VisibilityIndex,
    int PriorityIndex,
    int WindowTypeIndex,
    string WindowWidth,
    string WindowHeight,
    bool UseAutomaticRamAllocation,
    double CustomRamAllocationGb,
    bool OptimizeMemoryBeforeLaunch,
    int RendererIndex,
    string JvmArguments,
    string GameArguments,
    string BeforeCommand,
    string EnvironmentVariables,
    bool WaitForBeforeCommand,
    bool ForceX11OnWayland,
    bool DisableJavaLaunchWrapper,
    bool DisableRetroWrapper,
    bool RequireDedicatedGpu,
    bool UseJavaExecutable,
    int MicrosoftAuthIndex,
    int PreferredIpStackIndex);

internal sealed record FrontendSetupGameManageState(
    int DownloadSourceIndex,
    int VersionSourceIndex,
    double DownloadThreadLimit,
    double DownloadSpeedLimit,
    double DownloadTimeoutSeconds,
    bool AutoSelectNewInstance,
    bool UpgradePartialAuthlib,
    int CommunityDownloadSourceIndex,
    int FileNameFormatIndex,
    int ModLocalNameStyleIndex,
    bool IgnoreQuiltLoader,
    bool NotifyReleaseUpdates,
    bool NotifySnapshotUpdates,
    bool AutoSwitchGameLanguageToChinese,
    bool DetectClipboardResourceLinks);

internal sealed record FrontendSetupLauncherMiscState(
    int SystemActivityIndex,
    double AnimationFpsLimit,
    double MaxRealTimeLogValue,
    bool DisableHardwareAcceleration,
    bool EnableDoH,
    int HttpProxyTypeIndex,
    string HttpProxyAddress,
    string HttpProxyUsername,
    string HttpProxyPassword,
    double DebugAnimationSpeed,
    bool SkipCopyDuringDownload,
    bool DebugModeEnabled,
    bool DebugDelayEnabled);

internal sealed record FrontendSetupJavaState(
    string SelectedRuntimeKey,
    IReadOnlyList<FrontendSetupJavaRuntimeEntry> Entries);

internal sealed record FrontendSetupJavaRuntimeEntry(
    string Key,
    string Title,
    string Folder,
    IReadOnlyList<string> Tags,
    bool IsEnabled);

internal sealed record FrontendSetupUiState(
    int DarkModeIndex,
    int LightColorIndex,
    int DarkColorIndex,
    string LightCustomColorHex,
    string DarkCustomColorHex,
    double LauncherOpacity,
    bool ShowLauncherLogo,
    bool LockWindowSize,
    bool ShowLaunchingHint,
    int GlobalFontIndex,
    int MotdFontIndex,
    bool BackgroundColorful,
    double BackgroundOpacity,
    double BackgroundBlur,
    int BackgroundSuitIndex,
    double MusicVolume,
    bool MusicRandomPlay,
    bool MusicAutoStart,
    bool MusicStartOnGameLaunch,
    bool MusicStopOnGameLaunch,
    bool MusicEnableSmtc,
    int LogoTypeIndex,
    bool LogoAlignLeft,
    string LogoText,
    int HomepageTypeIndex,
    string HomepageUrl,
    int HomepagePresetIndex,
    IReadOnlyList<FrontendSetupUiToggleGroup> ToggleGroups);

internal sealed record FrontendSetupUiToggleGroup(
    string Title,
    IReadOnlyList<FrontendSetupUiToggleItem> Items);

internal sealed record FrontendSetupUiToggleItem(
    string Title,
    string ConfigKey,
    bool IsChecked);
