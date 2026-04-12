namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendInstanceComposition(
    FrontendInstanceSelectionState Selection,
    FrontendInstanceOverviewState Overview,
    FrontendInstanceSetupState Setup,
    FrontendInstanceExportState Export,
    FrontendInstanceInstallState Install,
    FrontendInstanceContentState World,
    FrontendInstanceScreenshotState Screenshot,
    FrontendInstanceServerState Server,
    FrontendInstanceResourceState Mods,
    FrontendInstanceResourceState DisabledMods,
    FrontendInstanceResourceState ResourcePacks,
    FrontendInstanceResourceState Shaders,
    FrontendInstanceResourceState Schematics);

internal sealed record FrontendInstanceSelectionState(
    bool HasSelection,
    string InstanceName,
    string InstanceDirectory,
    string IndieDirectory,
    string LauncherDirectory,
    bool IsIndie,
    bool IsModable,
    bool HasLabyMod,
    string VanillaVersion);

internal sealed record FrontendInstanceOverviewState(
    string Name,
    string Subtitle,
    string? IconPath,
    int IconIndex,
    int CategoryIndex,
    bool IsStarred,
    IReadOnlyList<string> DisplayTags,
    IReadOnlyList<FrontendInstanceInfoEntry> InfoEntries);

internal sealed record FrontendInstanceInfoEntry(
    string Label,
    string Value);

internal sealed record FrontendInstanceSetupState(
    int IsolationIndex,
    string WindowTitle,
    bool UseDefaultWindowTitle,
    string CustomInfo,
    IReadOnlyList<FrontendInstanceJavaOption> JavaOptions,
    int SelectedJavaIndex,
    string SelectedJavaKey,
    int MemoryModeIndex,
    double CustomMemoryAllocationGb,
    int OptimizeMemoryIndex,
    double UsedMemoryGb,
    double TotalMemoryGb,
    double AutomaticAllocatedMemoryGb,
    double GlobalAllocatedMemoryGb,
    string UsedMemoryLabel,
    string TotalMemoryLabel,
    string AllocatedMemoryLabel,
    bool ShowMemoryWarning,
    bool Show32BitJavaWarning,
    int ServerLoginRequirementIndex,
    bool IsServerLoginLocked,
    string AuthServer,
    string AuthRegister,
    string AuthName,
    string AutoJoinServer,
    int RendererIndex,
    string JvmArguments,
    string GameArguments,
    string ClasspathHead,
    string PreLaunchCommand,
    string EnvironmentVariables,
    bool WaitForPreLaunchCommand,
    int ForceX11OnWaylandMode,
    bool IgnoreJavaCompatibilityWarning,
    bool DisableFileValidation,
    bool FollowLauncherProxy,
    bool DisableJavaLaunchWrapper,
    bool DisableRetroWrapper,
    bool UseDebugLog4jConfig);

internal sealed record FrontendInstanceJavaOption(
    string Key,
    string Label);

internal sealed record FrontendInstanceExportState(
    string Name,
    string Version,
    bool IncludeResources,
    bool ModrinthMode,
    bool HasOptiFine,
    IReadOnlyList<FrontendInstanceExportOptionGroup> OptionGroups);

internal sealed record FrontendInstanceExportOptionGroup(
    string Title,
    string Description,
    bool IsChecked,
    IReadOnlyList<FrontendInstanceExportOptionEntry> Children);

internal sealed record FrontendInstanceExportOptionEntry(
    string Title,
    string Description,
    bool IsChecked);

internal sealed record FrontendInstanceInstallState(
    string SelectionTitle,
    string SelectionSummary,
    string MinecraftVersion,
    string MinecraftIconName,
    IReadOnlyList<string> Hints,
    IReadOnlyList<FrontendInstanceInstallOption> Options);

internal sealed record FrontendInstanceInstallOption(
    string Title,
    string Selection,
    string IconName);

internal sealed record FrontendInstanceContentState(
    IReadOnlyList<FrontendInstanceDirectoryEntry> Entries);

internal sealed record FrontendInstanceDirectoryEntry(
    string Title,
    string Summary,
    string Path);

internal sealed record FrontendInstanceScreenshotState(
    IReadOnlyList<FrontendInstanceScreenshotEntry> Entries);

internal sealed record FrontendInstanceScreenshotEntry(
    string Title,
    string Summary,
    string Path);

internal sealed record FrontendInstanceServerState(
    IReadOnlyList<FrontendInstanceServerEntry> Entries);

internal sealed record FrontendInstanceServerEntry(
    string Title,
    string Address,
    string Status);

internal sealed record FrontendInstanceResourceState(
    IReadOnlyList<FrontendInstanceResourceEntry> Entries);

internal sealed record FrontendInstanceResourceEntry(
    string Title,
    string Summary,
    string Meta,
    string Path,
    string IconName,
    string Identity = "",
    bool IsEnabled = true,
    string Description = "",
    string Website = "",
    string Authors = "",
    string Version = "",
    string Loader = "",
    byte[]? IconBytes = null);
