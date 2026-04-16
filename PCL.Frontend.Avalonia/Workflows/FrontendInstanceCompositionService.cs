using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendInstanceCompositionService
{
    private static readonly string LauncherRootDirectory = FrontendLauncherAssetLocator.RootDirectory;
    private static readonly string[] ScreenshotPatterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];
    private static readonly string[] EnabledModExtensions = [".jar", ".litemod"];
    private static readonly string[] DisabledModExtensions = [".disabled", ".old"];
    private static readonly string[] ArchiveExtensions = [".zip", ".rar", ".7z"];
    private static readonly string[] SchematicExtensions = [".litematic", ".nbt", ".schematic", ".schem"];
    private static readonly Regex TomlQuotedValueRegex = new("\"(?<value>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex TomlSingleQuotedValueRegex = new("'(?<value>[^']*)'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const int MaxEmbeddedIconBytes = 2 * 1024 * 1024;
    private const string LegacyGlobalJavaPreferenceLabel = "\u4f7f\u7528\u5168\u5c40\u8bbe\u7f6e";

    internal enum LoadMode
    {
        Lightweight = 0,
        InstallAware = 1,
        Full = 2
    }

    public static FrontendInstanceComposition Compose(FrontendRuntimePaths runtimePaths, II18nService? i18n = null)
    {
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        return Compose(runtimePaths, selectedInstanceName, LoadMode.Full, i18n);
    }

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        LoadMode loadMode,
        II18nService? i18n = null)
    {
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        return Compose(runtimePaths, selectedInstanceName, loadMode, i18n);
    }

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        string? selectedInstanceName,
        II18nService? i18n = null)
        => Compose(runtimePaths, selectedInstanceName, LoadMode.Full, i18n);

    public static FrontendInstanceComposition Compose(
        FrontendRuntimePaths runtimePaths,
        string? selectedInstanceName,
        LoadMode loadMode,
        II18nService? i18n = null)
    {
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var launcherDirectory = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var resolvedInstanceName = selectedInstanceName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resolvedInstanceName))
        {
            return CreateEmptyComposition(launcherDirectory, i18n: i18n);
        }

        var instanceDirectory = Path.Combine(launcherDirectory, "versions", resolvedInstanceName);
        if (!Directory.Exists(instanceDirectory))
        {
            return CreateEmptyComposition(launcherDirectory, resolvedInstanceName, i18n);
        }

        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(instanceDirectory);
        var manifestSummary = ReadManifestSummary(launcherDirectory, resolvedInstanceName);
        var isIndie = ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary);
        var indieDirectory = isIndie ? instanceDirectory : launcherDirectory;
        var vanillaVersion = manifestSummary.VanillaVersion?.ToString() ?? ReadValue(instanceConfig, "VersionVanillaName", Text(i18n, "instance.common.unknown_version", "Unknown"));
        var selection = new FrontendInstanceSelectionState(
            HasSelection: true,
            InstanceName: resolvedInstanceName,
            InstanceDirectory: instanceDirectory,
            IndieDirectory: indieDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: isIndie,
            IsModable: IsModable(manifestSummary),
            HasLabyMod: manifestSummary.HasLabyMod,
            VanillaVersion: vanillaVersion);
        var javaEntries = ParseJavaEntries(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));
        var installManifestSummary = MergeInstallAddonStates(
            selection,
            manifestSummary,
            includeMetadataFallback: loadMode >= LoadMode.InstallAware);
        var setupState = BuildSetupState(selection, manifestSummary, sharedConfig, localConfig, instanceConfig, javaEntries, i18n);
        var exportState = loadMode == LoadMode.Full
            ? BuildExportState(selection, manifestSummary, i18n)
            : CreatePlaceholderExportState(selection, manifestSummary);
        var modEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Mods, i18n)
            : [];
        var disabledModEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.DisabledMods, i18n)
            : [];
        var resourcePackEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.ResourcePacks, i18n)
            : [];
        var shaderEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Shaders, i18n)
            : [];
        var schematicEntries = loadMode == LoadMode.Full
            ? BuildResourceEntries(selection, ResourceKind.Schematics, i18n)
            : [];

        return new FrontendInstanceComposition(
            selection,
            BuildOverviewState(selection, manifestSummary, instanceConfig, setupState, i18n),
            setupState,
            exportState,
            BuildInstallState(selection, installManifestSummary, i18n),
            new FrontendInstanceContentState(BuildWorldEntries(selection, i18n)),
            new FrontendInstanceScreenshotState(BuildScreenshotEntries(selection, i18n)),
            new FrontendInstanceServerState(BuildServerEntries(selection, i18n)),
            new FrontendInstanceResourceState(modEntries),
            new FrontendInstanceResourceState(disabledModEntries),
            new FrontendInstanceResourceState(resourcePackEntries),
            new FrontendInstanceResourceState(shaderEntries),
            new FrontendInstanceResourceState(schematicEntries));
    }

    private static FrontendInstanceComposition CreateEmptyComposition(
        string launcherDirectory,
        string? instanceName = null,
        II18nService? i18n = null)
    {
        var resolvedInstanceName = string.IsNullOrWhiteSpace(instanceName)
            ? Text(i18n, "instance.common.no_selection", "No instance selected")
            : instanceName;
        var selection = new FrontendInstanceSelectionState(
            HasSelection: false,
            InstanceName: resolvedInstanceName,
            InstanceDirectory: string.Empty,
            IndieDirectory: launcherDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: false,
            IsModable: false,
            HasLabyMod: false,
            VanillaVersion: Text(i18n, "instance.common.unknown_version", "Unknown"));
        var setup = new FrontendInstanceSetupState(
            IsolationIndex: 0,
            WindowTitle: string.Empty,
            UseDefaultWindowTitle: false,
            CustomInfo: string.Empty,
            JavaOptions:
            [
                new FrontendInstanceJavaOption("global", Text(i18n, "instance.settings.options.follow_global", "Follow global setting")),
                new FrontendInstanceJavaOption("auto", Text(i18n, "instance.settings.java_options.auto_select", "Automatically choose a suitable Java"))
            ],
            SelectedJavaIndex: 0,
            SelectedJavaKey: "global",
            MemoryModeIndex: 2,
            CustomMemoryAllocationGb: FrontendSetupCompositionService.MapStoredLaunchRamToGb(15),
            OptimizeMemoryIndex: 0,
            UsedMemoryGb: 0,
            TotalMemoryGb: 0,
            AutomaticAllocatedMemoryGb: 0,
            GlobalAllocatedMemoryGb: 0,
            UsedMemoryLabel: "0.0 GB",
            TotalMemoryLabel: " / 0.0 GB",
            AllocatedMemoryLabel: "0.0 GB",
            ShowMemoryWarning: false,
            Show32BitJavaWarning: false,
            ServerLoginRequirementIndex: 0,
            IsServerLoginLocked: false,
            AuthServer: string.Empty,
            AuthRegister: string.Empty,
            AuthName: string.Empty,
            AutoJoinServer: string.Empty,
            RendererIndex: 0,
            JvmArguments: string.Empty,
            GameArguments: string.Empty,
            ClasspathHead: string.Empty,
            PreLaunchCommand: string.Empty,
            EnvironmentVariables: string.Empty,
            WaitForPreLaunchCommand: true,
            ForceX11OnWaylandMode: 0,
            IgnoreJavaCompatibilityWarning: false,
            DisableFileValidation: false,
            FollowLauncherProxy: false,
            DisableJavaLaunchWrapper: false,
            DisableRetroWrapper: false,
            UseDebugLog4jConfig: false);

        return new FrontendInstanceComposition(
            selection,
            new FrontendInstanceOverviewState(
                resolvedInstanceName,
                Text(i18n, "instance.overview.messages.select_to_view_details", "Select an instance to view details."),
                null,
                0,
                0,
                false,
                [],
                []),
            setup,
            new FrontendInstanceExportState(resolvedInstanceName, "1.0.0", false, false, false, []),
            new FrontendInstanceInstallState(
                resolvedInstanceName,
                Text(i18n, "instance.common.no_selection", "No instance selected"),
                Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", Text(i18n, "instance.common.unknown_version", "Unknown"))),
                "Grass.png",
                [],
                []),
            new FrontendInstanceContentState([]),
            new FrontendInstanceScreenshotState([]),
            new FrontendInstanceServerState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]),
            new FrontendInstanceResourceState([]));
    }

    private static FrontendInstanceOverviewState BuildOverviewState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        YamlFileProvider instanceConfig,
        FrontendInstanceSetupState setupState,
        II18nService? i18n)
    {
        var isStarred = ReadValue(instanceConfig, "IsStar", false);
        var categoryIndex = MapInstanceCategoryIndex(ReadValue(instanceConfig, "DisplayType", 0));
        var customInfo = ReadValue(
            instanceConfig,
            "VersionArgumentInfo",
            ReadValue(instanceConfig, "CustomInfo", string.Empty));
        var launchCount = ReadValue(instanceConfig, "VersionLaunchCount", 0);
        var modpackVersion = ReadValue(instanceConfig, "VersionModpackVersion", string.Empty);
        var infoEntries = new List<FrontendInstanceInfoEntry>();

        infoEntries.Add(new FrontendInstanceInfoEntry(
            Text(i18n, "instance.overview.info.launch_count", "Launch count"),
            launchCount == 0
                ? Text(i18n, "instance.overview.info.launch_count_never", "Never launched")
                : Text(i18n, "instance.overview.info.launch_count_value", "Launched {count} times", ("count", launchCount))));

        if (!string.IsNullOrWhiteSpace(modpackVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry(Text(i18n, "instance.overview.info.modpack_version", "Modpack version"), modpackVersion));
        }

        infoEntries.Add(new FrontendInstanceInfoEntry("Minecraft", selection.VanillaVersion));

        if (manifestSummary.HasForge && !string.IsNullOrWhiteSpace(manifestSummary.ForgeVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Forge", manifestSummary.ForgeVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.NeoForgeVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("NeoForge", manifestSummary.NeoForgeVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.CleanroomVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Cleanroom", manifestSummary.CleanroomVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.FabricVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Fabric", manifestSummary.FabricVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Quilt", manifestSummary.QuiltVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("OptiFine", manifestSummary.OptiFineVersion));
        }

        if (manifestSummary.HasLiteLoader)
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("LiteLoader", Text(i18n, "instance.common.installed", "Installed")));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.LegacyFabricVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Legacy Fabric", manifestSummary.LegacyFabricVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.LabyModVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("LabyMod", manifestSummary.LabyModVersion));
        }

        if (!string.IsNullOrWhiteSpace(customInfo))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry(Text(i18n, "instance.overview.info.description", "Description"), customInfo));
        }

        var tags = new List<string>();
        AddIfNotEmpty(tags, DeterminePrimaryLoaderLabel(manifestSummary));
        if (selection.IsModable)
        {
            tags.Add(Text(i18n, "instance.overview.tags.mod_supported", "Supports Mods"));
        }

        if (isStarred)
        {
            tags.Add(Text(i18n, "instance.overview.tags.favorited", "Favorited"));
        }
        else
        {
            AddIfNotEmpty(tags, DetermineCategoryLabel(categoryIndex, i18n));
        }

        var iconPath = ResolveOverviewIconPath(selection, manifestSummary, instanceConfig);
        return new FrontendInstanceOverviewState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary, i18n),
            iconPath,
            ResolveOverviewIconIndex(instanceConfig, manifestSummary),
            categoryIndex,
            isStarred,
            tags,
            infoEntries);
    }

    private static FrontendInstanceSetupState BuildSetupState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        YamlFileProvider instanceConfig,
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        II18nService? i18n)
    {
        var javaPreference = ReadJavaPreference(ReadValue(instanceConfig, "VersionArgumentJavaSelect", "global"));
        var javaOptions = BuildJavaOptions(javaEntries, selection.LauncherDirectory, javaPreference, i18n);
        var selectedJavaKey = ResolveSelectedJavaKey(javaPreference, selection.LauncherDirectory);
        var selectedJavaIndex = javaOptions.FindIndex(option => string.Equals(option.Key, selectedJavaKey, StringComparison.Ordinal));
        if (selectedJavaIndex < 0)
        {
            selectedJavaIndex = 0;
        }

        var resolvedJava = ResolveSelectedJava(javaPreference, javaEntries, selection.LauncherDirectory);
        var (totalMemoryGb, availableMemoryGb) = FrontendSystemMemoryService.GetPhysicalMemoryState();
        var customMemoryValue = ReadValue(instanceConfig, "VersionRamCustom", 15);
        var memoryModeIndex = Math.Clamp(ReadValue(instanceConfig, "VersionRamType", 2), 0, 2);
        var globalMemoryModeIndex = Math.Clamp(ReadValue(localConfig, "LaunchRamType", 0), 0, 1);
        var globalCustomMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(ReadValue(localConfig, "LaunchRamCustom", 15));
        var customMemoryGb = FrontendSetupCompositionService.MapStoredLaunchRamToGb(customMemoryValue);
        var modCount = Directory.Exists(ResolveResourceDirectory(selection, ResourceKind.Mods))
            ? Directory.EnumerateFiles(ResolveResourceDirectory(selection, ResourceKind.Mods), "*", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var effectiveMemoryModeIndex = memoryModeIndex == 2 ? globalMemoryModeIndex : memoryModeIndex;
        var effectiveCustomMemoryGb = memoryModeIndex == 2 ? globalCustomMemoryGb : customMemoryGb;
        var automaticAllocatedMemory = FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            0,
            effectiveCustomMemoryGb,
            selection.IsModable,
            !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            modCount,
            resolvedJava?.Is64Bit,
            totalMemoryGb,
            availableMemoryGb);
        var allocatedMemory = FrontendSystemMemoryService.CalculateAllocatedMemoryGb(
            effectiveMemoryModeIndex,
            effectiveCustomMemoryGb,
            selection.IsModable,
            !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            modCount,
            resolvedJava?.Is64Bit,
            totalMemoryGb,
            availableMemoryGb);
        var usedMemoryGb = Math.Max(totalMemoryGb - availableMemoryGb, 0);

        return new FrontendInstanceSetupState(
            IsolationIndex: ResolveInstanceIsolationIndex(instanceConfig),
            WindowTitle: ReadValue(instanceConfig, "VersionArgumentTitle", string.Empty),
            UseDefaultWindowTitle: ReadValue(instanceConfig, "VersionArgumentTitleEmpty", false),
            CustomInfo: ReadValue(instanceConfig, "VersionArgumentInfo", string.Empty),
            JavaOptions: javaOptions,
            SelectedJavaIndex: selectedJavaIndex,
            SelectedJavaKey: selectedJavaKey,
            MemoryModeIndex: memoryModeIndex,
            CustomMemoryAllocationGb: customMemoryGb,
            OptimizeMemoryIndex: Math.Clamp(ReadValue(instanceConfig, "VersionRamOptimize", 0), 0, 2),
            UsedMemoryGb: usedMemoryGb,
            TotalMemoryGb: totalMemoryGb,
            AutomaticAllocatedMemoryGb: automaticAllocatedMemory,
            GlobalAllocatedMemoryGb: allocatedMemory,
            UsedMemoryLabel: $"{usedMemoryGb:0.0} GB",
            TotalMemoryLabel: $" / {totalMemoryGb:0.0} GB",
            AllocatedMemoryLabel: $"{allocatedMemory:0.0} GB",
            ShowMemoryWarning: memoryModeIndex == 1 && totalMemoryGb > 0 && allocatedMemory / totalMemoryGb > 0.75,
            Show32BitJavaWarning: resolvedJava is { Is64Bit: false },
            ServerLoginRequirementIndex: Math.Clamp(ReadValue(instanceConfig, "VersionServerLoginRequire", 0), 0, 3),
            IsServerLoginLocked: ReadValue(instanceConfig, "VersionServerLoginLock", false),
            AuthServer: ReadValue(instanceConfig, "VersionServerAuthServer", string.Empty),
            AuthRegister: ReadValue(instanceConfig, "VersionServerAuthRegister", string.Empty),
            AuthName: ReadValue(instanceConfig, "VersionServerAuthName", string.Empty),
            AutoJoinServer: ReadValue(instanceConfig, "VersionServerEnter", string.Empty),
            RendererIndex: Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceRenderer", 0), 0, 4),
            JvmArguments: ReadValue(instanceConfig, "VersionAdvanceJvm", string.Empty),
            GameArguments: ReadValue(instanceConfig, "VersionAdvanceGame", string.Empty),
            ClasspathHead: ReadValue(instanceConfig, "VersionAdvanceClasspathHead", string.Empty),
            PreLaunchCommand: ReadValue(instanceConfig, "VersionAdvanceRun", string.Empty),
            EnvironmentVariables: ReadValue(instanceConfig, "VersionAdvanceEnvironmentVariables", string.Empty),
            WaitForPreLaunchCommand: ReadValue(instanceConfig, "VersionAdvanceRunWait", true),
            ForceX11OnWaylandMode: Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceForceX11OnWayland", 0), 0, 2),
            IgnoreJavaCompatibilityWarning: ReadValue(instanceConfig, "VersionAdvanceJava", false),
            DisableFileValidation: ReadValue(instanceConfig, "VersionAdvanceAssetsV2", false),
            FollowLauncherProxy: ReadValue(instanceConfig, "VersionAdvanceUseProxyV2", false),
            DisableJavaLaunchWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableJLW", false),
            DisableRetroWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableRW", false),
            UseDebugLog4jConfig: ReadValue(instanceConfig, "VersionUseDebugLog4j2Config", false));
    }

    private static FrontendInstanceExportState BuildExportState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var resourcePackEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.ResourcePacks), ArchiveExtensions, allowDirectories: true, recursive: false);
        var shaderEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Shaders), ArchiveExtensions, allowDirectories: true, recursive: false);
        var schematicEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Schematics), SchematicExtensions, allowDirectories: false, recursive: true);
        var disabledModEntries = BuildResourceEntries(selection, ResourceKind.DisabledMods, i18n);
        var saveEntries = BuildExportWorldOptions(selection);
        var screenshotEntries = BuildScreenshotEntries(selection, i18n);
        var replayEntries = BuildFolderEntries(Path.Combine(selection.IndieDirectory, "replay_recordings"), [".mcpr"], allowDirectories: false, recursive: false);
        var hasServers = File.Exists(Path.Combine(selection.IndieDirectory, "servers.dat"));
        var hasLauncherContent = Directory.Exists(Path.Combine(selection.InstanceDirectory, "PCL"));

        var groups = new List<FrontendInstanceExportOptionGroup>
        {
            new(
                "game",
                Text(i18n, "instance.export.groups.game", "Game"),
                string.Empty,
                true,
                [
                    CreateExportOption("game_settings", Text(i18n, "instance.export.items.game_settings", "Game settings"), File.Exists(Path.Combine(selection.IndieDirectory, "options.txt")) ? Text(i18n, "instance.export.detected.options", "Detected options.txt") : Text(i18n, "instance.export.detected.config_missing", "Configuration file not found"), File.Exists(Path.Combine(selection.IndieDirectory, "options.txt"))),
                    CreateExportOption("game_personal", Text(i18n, "instance.export.items.game_personal", "Personal game data"), File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt")) ? Text(i18n, "instance.export.detected.optifine_settings", "Detected OptiFine settings") : Text(i18n, "instance.export.detected.personal_missing", "Personal settings not found"), File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt"))),
                    CreateExportOption(
                        "optifine_settings",
                        Text(i18n, "instance.export.items.optifine_settings", "OptiFine settings"),
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion) ? Text(i18n, "instance.export.detected.optifine_present", "This instance includes OptiFine") : Text(i18n, "instance.export.detected.optifine_missing", "This instance does not have OptiFine installed"),
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion))
                ]),
            new(
                "mods",
                Text(i18n, "instance.export.groups.mods", "Mods"),
                Text(i18n, "instance.export.descriptions.mods", "Mods"),
                selection.IsModable,
                [
                    CreateExportOption("disabled_mods", Text(i18n, "instance.export.items.disabled_mods", "Disabled mods"), Text(i18n, "instance.export.count.items", "{count} items", ("count", disabledModEntries.Count)), disabledModEntries.Count > 0),
                    CreateExportOption("important_data", Text(i18n, "instance.export.items.important_data", "Important modpack data"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? Text(i18n, "instance.export.detected.config_folder", "Detected config folder") : Text(i18n, "instance.export.detected.config_folder_missing", "Config folder not found"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config"))),
                    CreateExportOption("mod_settings", Text(i18n, "instance.export.items.mod_settings", "Mod settings"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? Text(i18n, "instance.export.detected.config_directory", "Detected configuration directory") : Text(i18n, "instance.export.detected.config_directory_missing", "Configuration directory not found"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")))
                ]),
            new("resource_packs", Text(i18n, "instance.export.groups.resource_packs", "Resource packs"), Text(i18n, "instance.export.descriptions.resource_packs", "Texture packs / resource packs"), resourcePackEntries.Count > 0, resourcePackEntries.Select(ToExportOption).ToArray()),
            new("shaders", Text(i18n, "instance.export.groups.shaders", "Shader packs"), string.Empty, shaderEntries.Count > 0, shaderEntries.Select(ToExportOption).ToArray()),
            new("screenshots", Text(i18n, "instance.export.groups.screenshots", "Screenshots"), string.Empty, screenshotEntries.Count > 0, []),
            new("schematics", Text(i18n, "instance.export.groups.schematics", "Schematics"), Text(i18n, "instance.export.descriptions.schematics", "schematics folder"), schematicEntries.Count > 0, schematicEntries.Select(ToExportOption).ToArray()),
            new("replays", Text(i18n, "instance.export.groups.replays", "Replays"), Text(i18n, "instance.export.descriptions.replays", "Replay Mod recordings"), replayEntries.Count > 0, replayEntries.Select(ToExportOption).ToArray()),
            new("worlds", Text(i18n, "instance.export.groups.worlds", "World saves"), Text(i18n, "instance.export.descriptions.worlds", "Worlds / maps"), false, saveEntries),
            new("servers", Text(i18n, "instance.export.groups.servers", "Server list"), string.Empty, hasServers, []),
            new(
                "launcher",
                Text(i18n, "instance.export.groups.launcher", "PCL launcher"),
                Text(i18n, "instance.export.descriptions.launcher", "Bundle the cross-platform PCL launcher so players without it can install the modpack."),
                hasLauncherContent,
                [
                    CreateExportOption("launcher_personalization", Text(i18n, "instance.export.items.launcher_personalization", "PCL personalization content"), hasLauncherContent ? Text(i18n, "instance.export.detected.pcl_directory", "Detected instance PCL configuration directory") : Text(i18n, "instance.export.detected.pcl_directory_missing", "Instance PCL configuration directory not found"), hasLauncherContent)
                ])
        };

        return new FrontendInstanceExportState(
            selection.InstanceName,
            ReadVersionFallback(selection.InstanceDirectory),
            IncludeResources: false,
            ModrinthMode: false,
            HasOptiFine: !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            OptionGroups: groups);
    }

    private static FrontendInstanceExportState CreatePlaceholderExportState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary)
    {
        return new FrontendInstanceExportState(
            selection.InstanceName,
            ReadVersionFallback(selection.InstanceDirectory),
            IncludeResources: false,
            ModrinthMode: false,
            HasOptiFine: !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            OptionGroups: []);
    }

    private static FrontendInstanceInstallState BuildInstallState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifestSummary.FabricVersion) && !manifestSummary.HasFabricApi)
        {
            hints.Add(Text(i18n, "instance.install.hints.fabric_api_required", "Fabric API is not installed, so most mods will not work."));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion) && !manifestSummary.HasQsl)
        {
            hints.Add(Text(i18n, "instance.install.hints.qsl_required", "QFAPI / QSL is not installed, so most mods will not work."));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion)
            && !string.IsNullOrWhiteSpace(manifestSummary.FabricVersion)
            && !manifestSummary.HasOptiFabric)
        {
            hints.Add(Text(i18n, "instance.install.hints.optifabric_required", "OptiFabric is not installed, so OptiFine will not work."));
        }

        return new FrontendInstanceInstallState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary, i18n),
            Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", selection.VanillaVersion)),
            DetermineInstallIconName(manifestSummary),
            hints,
            [
                new FrontendInstanceInstallOption("Forge", DisplayVersion(manifestSummary.ForgeVersion, i18n), "Anvil.png"),
                new FrontendInstanceInstallOption("Cleanroom", DisplayVersion(manifestSummary.CleanroomVersion, i18n), "Cleanroom.png"),
                new FrontendInstanceInstallOption("NeoForge", DisplayVersion(manifestSummary.NeoForgeVersion, i18n), "NeoForge.png"),
                new FrontendInstanceInstallOption("Fabric", DisplayVersion(manifestSummary.FabricVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("Legacy Fabric", DisplayVersion(manifestSummary.LegacyFabricVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("Fabric API", DisplayInstalled(manifestSummary.HasFabricApi, manifestSummary.FabricApiVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("QFAPI / QSL", DisplayInstalled(manifestSummary.HasQsl, manifestSummary.QslVersion, i18n), "Quilt.png"),
                new FrontendInstanceInstallOption("Quilt", DisplayVersion(manifestSummary.QuiltVersion, i18n), "Quilt.png"),
                new FrontendInstanceInstallOption("LabyMod", DisplayVersion(manifestSummary.LabyModVersion, i18n), "LabyMod.png"),
                new FrontendInstanceInstallOption("OptiFine", DisplayVersion(manifestSummary.OptiFineVersion, i18n), "GrassPath.png"),
                new FrontendInstanceInstallOption("OptiFabric", DisplayInstalled(manifestSummary.HasOptiFabric, manifestSummary.OptiFabricVersion, i18n), "OptiFabric.png"),
                new FrontendInstanceInstallOption("LiteLoader", DisplayInstalled(manifestSummary.HasLiteLoader, manifestSummary.LiteLoaderVersion, i18n), "Egg.png")
            ]);
    }

    private static FrontendVersionManifestSummary MergeInstallAddonStates(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        bool includeMetadataFallback)
    {
        var modsDirectory = ResolveResourceDirectory(selection, ResourceKind.Mods);
        if (!Directory.Exists(modsDirectory))
        {
            return manifestSummary;
        }

        var hasFabricApi = manifestSummary.HasFabricApi;
        string? fabricApiVersion = null;
        var hasQsl = manifestSummary.HasQsl;
        string? qslVersion = null;
        var hasOptiFabric = manifestSummary.HasOptiFabric;
        string? optiFabricVersion = null;

        var modFiles = Directory.EnumerateFiles(modsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => EnabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                FileName = Path.GetFileNameWithoutExtension(path),
                NormalizedFileName = NormalizeManagedAddonIdentity(Path.GetFileNameWithoutExtension(path))
            })
            .ToArray();

        foreach (var file in modFiles)
        {
            if (!hasFabricApi && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "fabricapi"))
            {
                hasFabricApi = true;
            }

            if (!hasQsl && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "quiltedfabricapi", "qsl"))
            {
                hasQsl = true;
            }

            if (!hasOptiFabric && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "optifabric"))
            {
                hasOptiFabric = true;
            }
        }

        if (includeMetadataFallback && (!hasFabricApi || !hasQsl || !hasOptiFabric))
        {
            foreach (var file in modFiles)
            {
                var metadata = TryReadLocalModMetadata(file.Path);

                if (!hasFabricApi && IsManagedAddonMod(metadata, file.FileName, "fabricapi"))
                {
                    hasFabricApi = true;
                    fabricApiVersion = NormalizeInlineText(metadata?.Version);
                    continue;
                }

                if (!hasQsl && IsManagedAddonMod(metadata, file.FileName, "quiltedfabricapi", "qsl"))
                {
                    hasQsl = true;
                    qslVersion = NormalizeInlineText(metadata?.Version);
                    continue;
                }

                if (!hasOptiFabric && IsManagedAddonMod(metadata, file.FileName, "optifabric"))
                {
                    hasOptiFabric = true;
                    optiFabricVersion = NormalizeInlineText(metadata?.Version);
                }
            }
        }

        return manifestSummary with
        {
            HasFabricApi = hasFabricApi,
            FabricApiVersion = FirstNonEmpty(manifestSummary.FabricApiVersion, fabricApiVersion),
            HasQsl = hasQsl,
            QslVersion = FirstNonEmpty(manifestSummary.QslVersion, qslVersion),
            HasOptiFabric = hasOptiFabric,
            OptiFabricVersion = FirstNonEmpty(manifestSummary.OptiFabricVersion, optiFabricVersion)
        };
    }

    private static bool IsManagedAddonMod(
        RecognizedModMetadata? metadata,
        string fileNameWithoutExtension,
        params string[] identifiers)
    {
        return MatchesManagedAddonIdentity(metadata?.Identity, identifiers)
               || MatchesManagedAddonIdentity(metadata?.Title, identifiers)
               || identifiers.Any(identifier => NormalizeManagedAddonIdentity(fileNameWithoutExtension)
                   .StartsWith(NormalizeManagedAddonIdentity(identifier), StringComparison.Ordinal));
    }

    private static bool LooksLikeManagedAddonFromFileName(string normalizedFileName, params string[] identifiers)
    {
        return identifiers.Any(identifier => normalizedFileName.StartsWith(
            NormalizeManagedAddonIdentity(identifier),
            StringComparison.Ordinal));
    }

    private static bool MatchesManagedAddonIdentity(string? value, IEnumerable<string> identifiers)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeManagedAddonIdentity(value);
        return identifiers.Any(identifier => string.Equals(
            normalized,
            NormalizeManagedAddonIdentity(identifier),
            StringComparison.Ordinal));
    }

    private static string NormalizeManagedAddonIdentity(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static IReadOnlyList<FrontendInstanceDirectoryEntry> BuildWorldEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var savesDirectory = Path.Combine(selection.IndieDirectory, "saves");
        if (!Directory.Exists(savesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(savesDirectory)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => new FrontendInstanceDirectoryEntry(
                directory.Name,
                Text(i18n, "instance.content.world.summary", "Created: {created_at} • Modified: {modified_at}", ("created_at", directory.CreationTime.ToString("yyyy/MM/dd")), ("modified_at", directory.LastWriteTime.ToString("yyyy/MM/dd"))),
                directory.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceExportOptionEntry> BuildExportWorldOptions(FrontendInstanceSelectionState selection)
    {
        var savesDirectory = Path.Combine(selection.IndieDirectory, "saves");
        if (!Directory.Exists(savesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(savesDirectory)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => new FrontendInstanceExportOptionEntry(
                directory.FullName,
                directory.Name,
                directory.LastWriteTime.ToString("yyyy/MM/dd HH:mm"),
                true))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceScreenshotEntry> BuildScreenshotEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var screenshotDirectory = Path.Combine(selection.IndieDirectory, "screenshots");
        if (!Directory.Exists(screenshotDirectory))
        {
            return [];
        }

        return ScreenshotPatterns
            .SelectMany(pattern => Directory.EnumerateFiles(screenshotDirectory, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file => new FrontendInstanceScreenshotEntry(
                file.Name,
                Text(i18n, "instance.content.screenshot.summary", "{created_at} • {file_size}", ("created_at", file.CreationTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))),
                file.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceServerEntry> BuildServerEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var serversPath = Path.Combine(selection.IndieDirectory, "servers.dat");
        if (!File.Exists(serversPath))
        {
            return [];
        }

        try
        {
            var file = new NbtFile();
            using var stream = File.OpenRead(serversPath);
            file.LoadFromStream(stream, NbtCompression.AutoDetect);
            var list = file.RootTag.Get<NbtList>("servers");
            if (list is null)
            {
                return [];
            }

            return list
                .OfType<NbtCompound>()
                .Select(server => new FrontendInstanceServerEntry(
                    Title: server.Get<NbtString>("name")?.Value ?? Text(i18n, "instance.content.server.dialogs.edit.name_default", "Minecraft Server"),
                    Address: server.Get<NbtString>("ip")?.Value ?? string.Empty,
                    Status: Text(i18n, "instance.content.server.status.saved", "Saved server")))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildResourceEntries(
        FrontendInstanceSelectionState selection,
        ResourceKind kind,
        II18nService? i18n)
    {
        return kind switch
        {
            ResourceKind.Mods => BuildModResourceEntries(
                ResolveResourceDirectory(selection, kind),
                fileFilter: path => EnabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                defaultIconName: DetermineInstallIconNameFromExtension("mods", selection),
                isEnabled: true,
                i18n: i18n),
            ResourceKind.DisabledMods => BuildModResourceEntries(
                ResolveResourceDirectory(selection, ResourceKind.Mods),
                fileFilter: path => DisabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                defaultIconName: "RedstoneBlock.png",
                isEnabled: false,
                i18n: i18n),
            ResourceKind.ResourcePacks => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), Text(i18n, "instance.content.resource.kind.resource_pack", "Resource pack"), "Grass.png", i18n),
            ResourceKind.Shaders => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), Text(i18n, "instance.content.resource.kind.shader", "Shader pack"), "RedstoneLampOn.png", i18n),
            ResourceKind.Schematics => BuildFileResourceEntries(
                ResolveResourceDirectory(selection, kind),
                recursive: true,
                fileFilter: path => SchematicExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                metaPrefix: Text(i18n, "instance.content.resource.kind.schematic_file", "Schematic file"),
                defaultIconName: "CommandBlock.png",
                i18n: i18n),
            _ => []
        };
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildModResourceEntries(
        string directory,
        Func<string, bool> fileFilter,
        string defaultIconName,
        bool isEnabled,
        II18nService? i18n)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(fileFilter)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => BuildModResourceEntry(directory, file, defaultIconName, isEnabled, i18n))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFileResourceEntries(
        string directory,
        bool recursive,
        Func<string, bool> fileFilter,
        string metaPrefix,
        string defaultIconName,
        bool isEnabled = true,
        II18nService? i18n = null)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(directory, "*", option)
            .Where(fileFilter)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new FrontendInstanceResourceEntry(
                Title: Path.GetFileNameWithoutExtension(file.Name),
                Summary: Text(i18n, "instance.content.resource.summary.file", "{parent_directory} • {modified_at}", ("parent_directory", GetRelativeParent(directory, file.FullName, i18n)), ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm"))),
                Meta: $"{metaPrefix} • {file.Extension.TrimStart('.').ToUpperInvariant()}",
                Path: file.FullName,
                IconName: defaultIconName,
                IsEnabled: isEnabled))
            .ToArray();
    }

    private static FrontendInstanceResourceEntry BuildModResourceEntry(
        string directory,
        FileInfo file,
        string defaultIconName,
        bool isEnabled,
        II18nService? i18n)
    {
        var metadata = TryReadLocalModMetadata(file.FullName);
        var title = !string.IsNullOrWhiteSpace(metadata?.Title)
            ? metadata.Title!
            : GetFallbackModTitle(file.Name);
        var summary = BuildModSummary(file, metadata, i18n);
        var meta = BuildModMeta(file, metadata, i18n);
        var iconName = DetermineModIconName(metadata?.Loader, defaultIconName);

        return new FrontendInstanceResourceEntry(
            Title: title,
            Summary: summary,
            Meta: meta,
            Path: file.FullName,
            IconName: iconName,
            Identity: metadata?.Identity ?? string.Empty,
            IsEnabled: isEnabled,
            Description: NormalizeInlineText(metadata?.Description),
            Website: metadata?.Website ?? string.Empty,
            Authors: metadata?.Authors ?? string.Empty,
            Version: metadata?.Version ?? string.Empty,
            Loader: metadata?.Loader ?? string.Empty,
            IconBytes: metadata?.IconBytes);
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFolderAndArchiveEntries(
        string directory,
        string metaPrefix,
        string iconName,
        II18nService? i18n)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => ArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .Select(file => new FrontendInstanceResourceEntry(
                Title: Path.GetFileNameWithoutExtension(file.Name),
                Summary: Text(i18n, "instance.content.resource.summary.archive", "{modified_at} • {file_size}", ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))),
                Meta: $"{metaPrefix} • {Text(i18n, "instance.content.resource.meta.archive", "Archive")}",
                Path: file.FullName,
                IconName: iconName));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .Select(folder => new FrontendInstanceResourceEntry(
                Title: folder.Name,
                Summary: Text(i18n, "instance.content.resource.summary.folder", "{modified_at} • {folder_kind}", ("modified_at", folder.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("folder_kind", Text(i18n, "instance.content.resource.meta.folder", "Folder"))),
                Meta: $"{metaPrefix} • {Text(i18n, "instance.content.resource.meta.folder", "Folder")}",
                Path: folder.FullName,
                IconName: iconName));
        return files.Concat(folders)
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceDirectoryEntry> BuildFolderEntries(
        string directory,
        IReadOnlyCollection<string> extensions,
        bool allowDirectories,
        bool recursive)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(directory, "*", option)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new FrontendInstanceDirectoryEntry(
                file.Name,
                $"{file.LastWriteTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                file.FullName));
        if (!allowDirectories)
        {
            return files.ToArray();
        }

        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .OrderByDescending(folder => folder.LastWriteTimeUtc)
            .Select(folder => new FrontendInstanceDirectoryEntry(
                folder.Name,
                folder.LastWriteTime.ToString("yyyy/MM/dd HH:mm"),
                folder.FullName));
        return files.Concat(folders).ToArray();
    }

    private static FrontendInstanceExportOptionEntry ToExportOption(FrontendInstanceDirectoryEntry entry)
    {
        return new FrontendInstanceExportOptionEntry(entry.Path, entry.Title, entry.Summary, true);
    }

    private static FrontendInstanceExportOptionEntry CreateExportOption(string key, string title, string description, bool isChecked)
    {
        return new FrontendInstanceExportOptionEntry(key, title, description, isChecked);
    }

    private static string ReadVersionFallback(string instanceDirectory)
    {
        var iniPath = Path.Combine(instanceDirectory, "PCL", "config.v1.yml");
        return File.Exists(iniPath) ? "1.0.0" : "1.0.0";
    }

    private static List<FrontendInstanceJavaOption> BuildJavaOptions(
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        string launcherDirectory,
        FrontendJavaPreference preference,
        II18nService? i18n)
    {
        var options = new List<FrontendInstanceJavaOption>
        {
            new("global", Text(i18n, "instance.settings.options.follow_global", "Follow global setting")),
            new("auto", Text(i18n, "instance.settings.java_options.auto_select", "Automatically choose a suitable Java"))
        };

        if (preference.Kind == FrontendJavaPreferenceKind.RelativePath && !string.IsNullOrWhiteSpace(preference.Value))
        {
            options.Add(new FrontendInstanceJavaOption(
                $"relative:{preference.Value}",
                Text(i18n, "instance.settings.java_options.launcher_relative_selected", "Java under launcher directory | {relative_path}", ("relative_path", preference.Value))));
        }
        else
        {
            options.Add(new FrontendInstanceJavaOption("relative", Text(i18n, "instance.settings.java_options.launcher_relative", "Choose Java under launcher directory")));
        }

        foreach (var entry in javaEntries)
        {
            options.Add(new FrontendInstanceJavaOption($"existing:{entry.ExecutablePath}", entry.DisplayName));
        }

        return options;
    }

    private static string ResolveSelectedJavaKey(FrontendJavaPreference preference, string launcherDirectory)
    {
        return preference.Kind switch
        {
            FrontendJavaPreferenceKind.Global => "global",
            FrontendJavaPreferenceKind.Auto => "auto",
            FrontendJavaPreferenceKind.RelativePath => $"relative:{preference.Value}",
            FrontendJavaPreferenceKind.Existing => $"existing:{preference.Value}",
            _ => "global"
        };
    }

    private static FrontendJavaEntry? ResolveSelectedJava(
        FrontendJavaPreference preference,
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        string launcherDirectory)
    {
        return preference.Kind switch
        {
            FrontendJavaPreferenceKind.Existing => javaEntries.FirstOrDefault(entry =>
                string.Equals(entry.ExecutablePath, preference.Value, StringComparison.OrdinalIgnoreCase)),
            FrontendJavaPreferenceKind.RelativePath => javaEntries.FirstOrDefault(entry =>
                string.Equals(entry.ExecutablePath, Path.GetFullPath(Path.Combine(launcherDirectory, preference.Value ?? string.Empty)), StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    private static FrontendJavaPreference ReadJavaPreference(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || IsGlobalJavaPreferenceValue(rawValue))
        {
            return new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null);
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            var kind = GetString(document.RootElement, "kind")?.ToLowerInvariant();
            return kind switch
            {
                "auto" => new FrontendJavaPreference(FrontendJavaPreferenceKind.Auto, null),
                "exist" => new FrontendJavaPreference(
                    FrontendJavaPreferenceKind.Existing,
                    GetString(document.RootElement, "JavaExePath")),
                "relative" => new FrontendJavaPreference(
                    FrontendJavaPreferenceKind.RelativePath,
                    GetString(document.RootElement, "RelativePath")),
                _ => new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null)
            };
        }
        catch
        {
            return new FrontendJavaPreference(FrontendJavaPreferenceKind.Global, null);
        }
    }

    private static List<FrontendJavaEntry> ParseJavaEntries(string rawJson)
    {
        return FrontendJavaInventoryService.ParseStoredJavaRuntimes(rawJson)
            .Select(runtime => new FrontendJavaEntry(
                runtime.ExecutablePath,
                runtime.DisplayName,
                runtime.Is64Bit))
            .ToList();
    }

    private static bool IsModable(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary.HasForge
               || !string.IsNullOrWhiteSpace(manifestSummary.NeoForgeVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.CleanroomVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.FabricVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.LegacyFabricVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion)
               || manifestSummary.HasLiteLoader
               || !string.IsNullOrWhiteSpace(manifestSummary.LabyModVersion);
    }

    private static string BuildInstanceSubtitle(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var parts = new List<string>();
        AddIfNotEmpty(parts, DeterminePrimaryLoaderLabel(manifestSummary));
        AddIfNotEmpty(parts, Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", selection.VanillaVersion)));
        parts.Add(selection.IsIndie
            ? Text(i18n, "instance.common.independent", "Independent instance")
            : Text(i18n, "instance.common.shared", "Shared instance"));
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string DeterminePrimaryLoaderLabel(FrontendVersionManifestSummary manifestSummary)
    {
        return
            FirstNonEmpty(
                PrefixVersion("NeoForge", manifestSummary.NeoForgeVersion),
                PrefixVersion("Cleanroom", manifestSummary.CleanroomVersion),
                PrefixVersion("Fabric", manifestSummary.FabricVersion),
                PrefixVersion("Legacy Fabric", manifestSummary.LegacyFabricVersion),
                PrefixVersion("Quilt", manifestSummary.QuiltVersion),
                PrefixVersion("Forge", manifestSummary.ForgeVersion),
                PrefixVersion("OptiFine", manifestSummary.OptiFineVersion),
                manifestSummary.HasLiteLoader ? "LiteLoader" : null,
                PrefixVersion("LabyMod", manifestSummary.LabyModVersion))
            ?? "Minecraft";
    }

    private static string DetermineInstallIconName(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary switch
        {
            { NeoForgeVersion: not null and not "" } => "NeoForge.png",
            { CleanroomVersion: not null and not "" } => "Cleanroom.png",
            { FabricVersion: not null and not "" } => "Fabric.png",
            { QuiltVersion: not null and not "" } => "Quilt.png",
            { ForgeVersion: not null and not "" } => "Anvil.png",
            { OptiFineVersion: not null and not "" } => "GrassPath.png",
            _ => "Grass.png"
        };
    }

    private static string DetermineInstallIconNameFromExtension(string family, FrontendInstanceSelectionState selection)
    {
        return family switch
        {
            "mods" => "Fabric.png",
            _ => "Grass.png"
        };
    }

    private static RecognizedModMetadata? TryReadLocalModMetadata(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            return TryReadFabricModMetadata(archive)
                ?? TryReadQuiltModMetadata(archive)
                ?? TryReadForgeModMetadata(archive, neoforge: true)
                ?? TryReadForgeModMetadata(archive, neoforge: false)
                ?? TryReadForgeLegacyMetadata(archive)
                ?? TryReadLiteLoaderMetadata(archive);
        }
        catch
        {
            return null;
        }
    }

    private static RecognizedModMetadata? TryReadFabricModMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "fabric.mod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        if (root is null)
        {
            return null;
        }

        var id = GetString(root, "id");
        var contact = root["contact"] as JsonObject;
        return BuildRecognizedModMetadata(
            Identity: id,
            Title: GetString(root, "name") ?? id,
            Description: GetString(root, "description"),
            Authors: JoinAuthorArray(root["authors"] as JsonArray),
            Version: GetString(root, "version"),
            Website: GetString(contact, "homepage"),
            Loader: "Fabric",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadIconReference(root["icon"])));
    }

    private static RecognizedModMetadata? TryReadQuiltModMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "quilt.mod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        var loader = root?["quilt_loader"] as JsonObject;
        var metadata = loader?["metadata"] as JsonObject;
        var contributors = metadata?["contributors"] as JsonObject;
        var contact = metadata?["contact"] as JsonObject;
        if (loader is null)
        {
            return null;
        }

        return BuildRecognizedModMetadata(
            Identity: GetString(loader, "id"),
            Title: GetString(metadata, "name") ?? GetString(loader, "id"),
            Description: GetString(metadata, "description"),
            Authors: JoinContributors(contributors),
            Version: GetString(loader, "version"),
            Website: GetString(contact, "homepage"),
            Loader: "Quilt",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadIconReference(metadata?["icon"])));
    }

    private static RecognizedModMetadata? TryReadForgeLegacyMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "mcmod.info");
        if (entry is null)
        {
            return null;
        }

        JsonObject? metadata = null;
        try
        {
            var node = JsonNode.Parse(ReadArchiveEntryText(entry));
            metadata = node switch
            {
                JsonArray array when array.Count > 0 => array[0] as JsonObject,
                JsonObject obj when obj["modList"] is JsonArray list && list.Count > 0 => list[0] as JsonObject,
                JsonObject obj => obj,
                _ => null
            };
        }
        catch
        {
            metadata = null;
        }

        if (metadata is null)
        {
            return null;
        }

        var authors = FirstNonEmpty(
            GetString(metadata, "author"),
            JoinJsonArray(metadata["authors"] as JsonArray),
            JoinJsonArray(metadata["authorList"] as JsonArray),
            GetString(metadata, "credits"));

        return BuildRecognizedModMetadata(
            Identity: GetString(metadata, "modid"),
            Title: GetString(metadata, "name") ?? GetString(metadata, "modid"),
            Description: GetString(metadata, "description"),
            Authors: authors,
            Version: GetString(metadata, "version"),
            Website: FirstNonEmpty(GetString(metadata, "url"), GetString(metadata, "updateUrl")),
            Loader: "Forge",
            IconBytes: TryReadEmbeddedIconBytes(archive, GetString(metadata, "logoFile")));
    }

    private static RecognizedModMetadata? TryReadForgeModMetadata(ZipArchive archive, bool neoforge)
    {
        var entry = FindArchiveEntry(archive, neoforge ? "META-INF/neoforge.mods.toml" : "META-INF/mods.toml");
        if (entry is null)
        {
            return null;
        }

        var content = ReadArchiveEntryText(entry);
        var modsBlock = ReadFirstModsTomlBlock(content);
        if (string.IsNullOrWhiteSpace(modsBlock))
        {
            return null;
        }

        return BuildRecognizedModMetadata(
            Identity: ReadTomlValue(modsBlock, "modId"),
            Title: FirstNonEmpty(ReadTomlValue(modsBlock, "displayName"), ReadTomlValue(modsBlock, "modId")),
            Description: ReadTomlValue(modsBlock, "description"),
            Authors: ReadTomlArrayOrString(content, "authors") ?? ReadTomlArrayOrString(modsBlock, "authors"),
            Version: ReadTomlValue(modsBlock, "version"),
            Website: ReadTomlValue(modsBlock, "displayURL"),
            Loader: neoforge ? "NeoForge" : "Forge",
            IconBytes: TryReadEmbeddedIconBytes(archive, ReadTomlValue(content, "logoFile")));
    }

    private static RecognizedModMetadata? TryReadLiteLoaderMetadata(ZipArchive archive)
    {
        var entry = FindArchiveEntry(archive, "litemod.json");
        if (entry is null)
        {
            return null;
        }

        var root = ReadJsonObject(entry);
        if (root is null)
        {
            return null;
        }

        var name = GetString(root, "name");
        return BuildRecognizedModMetadata(
            Identity: name,
            Title: name,
            Description: GetString(root, "description"),
            Authors: GetString(root, "author"),
            Version: GetString(root, "version"),
            Website: FirstNonEmpty(GetString(root, "updateURI"), GetString(root, "checkUpdateUrl")),
            Loader: "LiteLoader",
            IconBytes: null);
    }

    private static RecognizedModMetadata? BuildRecognizedModMetadata(
        string? Identity,
        string? Title,
        string? Description,
        string? Authors,
        string? Version,
        string? Website,
        string? Loader,
        byte[]? IconBytes)
    {
        if (string.IsNullOrWhiteSpace(Title)
            && string.IsNullOrWhiteSpace(Description)
            && string.IsNullOrWhiteSpace(Version)
            && string.IsNullOrWhiteSpace(Loader))
        {
            return null;
        }

        return new RecognizedModMetadata(
            Identity: Identity?.Trim() ?? string.Empty,
            Title: Title?.Trim() ?? string.Empty,
            Description: Description?.Trim() ?? string.Empty,
            Authors: Authors?.Trim() ?? string.Empty,
            Version: Version?.Trim() ?? string.Empty,
            Website: Website?.Trim() ?? string.Empty,
            Loader: Loader?.Trim() ?? string.Empty,
            IconBytes: IconBytes);
    }

    private static string BuildModSummary(FileInfo file, RecognizedModMetadata? metadata, II18nService? i18n)
    {
        var segments = new List<string>();
        AddIfNotEmpty(segments, metadata?.Authors);
        AddIfNotEmpty(segments, GetWebsiteLabel(metadata?.Website));

        if (segments.Count == 0)
        {
            segments.Add(Text(i18n, "instance.content.resource.summary.archive", "{modified_at} • {file_size}", ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))));
        }

        return string.Join(" • ", segments);
    }

    private static string BuildModMeta(FileInfo file, RecognizedModMetadata? metadata, II18nService? i18n)
    {
        var segments = new List<string>();
        AddIfNotEmpty(segments, metadata?.Loader);
        AddIfNotEmpty(segments, metadata?.Version);

        if (segments.Count == 0)
        {
            var extension = GetModContainerExtension(file.Name);
            AddIfNotEmpty(segments, string.IsNullOrWhiteSpace(extension) ? Text(i18n, "instance.content.resource.kind.mod", "Mod") : extension.ToUpperInvariant());
        }

        return string.Join(" • ", segments);
    }

    private static string DetermineModIconName(string? loader, string fallback)
    {
        return loader switch
        {
            "Fabric" => "Fabric.png",
            "Quilt" => "Quilt.png",
            "NeoForge" => "NeoForge.png",
            "Forge" => "Anvil.png",
            _ => fallback
        };
    }

    private static string GetFallbackModTitle(string fileName)
    {
        var normalizedName = RemoveTrailingSuffix(fileName, ".disabled");
        normalizedName = RemoveTrailingSuffix(normalizedName, ".old");
        return Path.GetFileNameWithoutExtension(normalizedName);
    }

    private static string GetModContainerExtension(string fileName)
    {
        var normalizedName = RemoveTrailingSuffix(fileName, ".disabled");
        normalizedName = RemoveTrailingSuffix(normalizedName, ".old");
        return Path.GetExtension(normalizedName).TrimStart('.');
    }

    private static string RemoveTrailingSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length]
            : value;
    }

    private static string GetWebsiteLabel(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return string.Empty;
        }

        return Uri.TryCreate(website, UriKind.Absolute, out var uri)
            ? uri.Host
            : website;
    }

    private static string NormalizeInlineText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static JsonObject? ReadJsonObject(ZipArchiveEntry entry)
    {
        try
        {
            return JsonNode.Parse(ReadArchiveEntryText(entry)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadArchiveEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string? GetString(JsonObject? root, string key)
    {
        return root?[key]?.GetValue<string>();
    }

    private static string JoinAuthorArray(JsonArray? authors)
    {
        if (authors is null || authors.Count == 0)
        {
            return string.Empty;
        }

        var values = authors
            .Select(node => node switch
            {
                JsonValue value => value.TryGetValue<string>(out var text) ? text : string.Empty,
                JsonObject obj => GetString(obj, "name") ?? string.Empty,
                _ => string.Empty
            })
            .Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join(", ", values);
    }

    private static string JoinJsonArray(JsonArray? values)
    {
        if (values is null || values.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", values
            .Select(node => node?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string JoinContributors(JsonObject? contributors)
    {
        if (contributors is null || contributors.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", contributors
            .Select(pair => string.IsNullOrWhiteSpace(pair.Value?.ToString())
                ? pair.Key
                : $"{pair.Key} ({pair.Value})"));
    }

    private static string? ReadIconReference(JsonNode? node)
    {
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var icon) => icon,
            JsonObject objectValue => objectValue
                .OrderByDescending(pair => int.TryParse(pair.Key, out var size) ? size : 0)
                .Select(pair => pair.Value?.ToString())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => null
        };
    }

    private static byte[]? TryReadEmbeddedIconBytes(ZipArchive archive, string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return null;
        }

        var iconEntry = FindArchiveEntry(archive, entryPath);
        if (iconEntry is null)
        {
            return null;
        }

        if (iconEntry.Length <= 0 || iconEntry.Length > MaxEmbeddedIconBytes)
        {
            return null;
        }

        try
        {
            using var stream = iconEntry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static ZipArchiveEntry? FindArchiveEntry(ZipArchive archive, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var direct = archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (direct is not null)
        {
            return direct;
        }

        var fileName = Path.GetFileName(normalized);
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(Path.GetFileName(entry.FullName), fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadFirstModsTomlBlock(string content)
    {
        const string sectionHeader = "[[mods]]";
        var sectionStart = content.IndexOf(sectionHeader, StringComparison.OrdinalIgnoreCase);
        if (sectionStart < 0)
        {
            return string.Empty;
        }

        var nextSectionMatch = Regex.Match(content[(sectionStart + sectionHeader.Length)..], @"(?m)^\s*\[");
        var sectionEnd = nextSectionMatch.Success
            ? sectionStart + sectionHeader.Length + nextSectionMatch.Index
            : content.Length;
        return content[sectionStart..sectionEnd];
    }

    private static string? ReadTomlValue(string content, string key)
    {
        var raw = ReadTomlRawValue(content, key);
        return string.IsNullOrWhiteSpace(raw) ? null : ParseTomlScalar(raw);
    }

    private static string? ReadTomlArrayOrString(string content, string key)
    {
        var raw = ReadTomlRawValue(content, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        if (raw[0] != '[')
        {
            return ParseTomlScalar(raw);
        }

        var values = new List<string>();
        foreach (Match match in TomlQuotedValueRegex.Matches(raw))
        {
            values.Add(UnescapeTomlString(match.Groups["value"].Value));
        }

        foreach (Match match in TomlSingleQuotedValueRegex.Matches(raw))
        {
            values.Add(match.Groups["value"].Value);
        }

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    private static string? ReadTomlRawValue(string content, string key)
    {
        var matcher = Regex.Match(content, $@"(?m)^\s*{Regex.Escape(key)}\s*=");
        if (!matcher.Success)
        {
            return null;
        }

        var start = matcher.Index + matcher.Length;
        while (start < content.Length && (content[start] == ' ' || content[start] == '\t'))
        {
            start++;
        }

        if (start >= content.Length)
        {
            return null;
        }

        if (content.AsSpan(start).StartsWith("\"\"\"".AsSpan(), StringComparison.Ordinal))
        {
            var endIndex = content.IndexOf("\"\"\"", start + 3, StringComparison.Ordinal);
            return endIndex < 0 ? content[start..] : content[start..(endIndex + 3)];
        }

        if (content.AsSpan(start).StartsWith("'''".AsSpan(), StringComparison.Ordinal))
        {
            var endIndex = content.IndexOf("'''", start + 3, StringComparison.Ordinal);
            return endIndex < 0 ? content[start..] : content[start..(endIndex + 3)];
        }

        var lineEnd = content.IndexOfAny(['\r', '\n'], start);
        return lineEnd < 0 ? content[start..] : content[start..lineEnd];
    }

    private static string? ParseTomlScalar(string raw)
    {
        raw = raw.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.StartsWith("\"\"\"", StringComparison.Ordinal))
        {
            var endIndex = raw.LastIndexOf("\"\"\"", StringComparison.Ordinal);
            if (endIndex > 2)
            {
                return raw[3..endIndex];
            }
        }

        if (raw.StartsWith("'''", StringComparison.Ordinal))
        {
            var endIndex = raw.LastIndexOf("'''", StringComparison.Ordinal);
            if (endIndex > 2)
            {
                return raw[3..endIndex];
            }
        }

        var quoted = TomlQuotedValueRegex.Match(raw);
        if (quoted.Success)
        {
            return UnescapeTomlString(quoted.Groups["value"].Value);
        }

        var singleQuoted = TomlSingleQuotedValueRegex.Match(raw);
        if (singleQuoted.Success)
        {
            return singleQuoted.Groups["value"].Value;
        }

        var commentIndex = raw.IndexOf('#');
        if (commentIndex >= 0)
        {
            raw = raw[..commentIndex];
        }

        return raw.Trim().TrimEnd(',');
    }

    private static string UnescapeTomlString(string value)
    {
        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private sealed record RecognizedModMetadata(
        string Identity,
        string Title,
        string Description,
        string Authors,
        string Version,
        string Website,
        string Loader,
        byte[]? IconBytes);

    private static string? ResolveOverviewIconPath(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        YamlFileProvider instanceConfig)
    {
        var isCustomLogo = ReadValue(instanceConfig, "LogoCustom", false);
        var rawLogoPath = ReadValue(instanceConfig, "Logo", string.Empty);
        if (isCustomLogo)
        {
            var customPath = Path.Combine(selection.InstanceDirectory, "PCL", "Logo.png");
            if (File.Exists(customPath))
            {
                return customPath;
            }
        }

        var mappedLogo = MapStoredLogoPath(rawLogoPath);
        if (mappedLogo is not null)
        {
            return mappedLogo;
        }

        return Path.Combine(
            LauncherRootDirectory,
            "Images",
            "Blocks",
            DetermineInstallIconName(manifestSummary));
    }

    private static int ResolveOverviewIconIndex(YamlFileProvider instanceConfig, FrontendVersionManifestSummary manifestSummary)
    {
        var isCustomLogo = ReadValue(instanceConfig, "LogoCustom", false);
        if (isCustomLogo)
        {
            return 0;
        }

        var rawLogoPath = ReadValue(instanceConfig, "Logo", string.Empty);
        return rawLogoPath switch
        {
            var path when path.Contains("CobbleStone", StringComparison.OrdinalIgnoreCase) => 1,
            var path when path.Contains("CommandBlock", StringComparison.OrdinalIgnoreCase) => 2,
            var path when path.Contains("GoldBlock", StringComparison.OrdinalIgnoreCase) => 3,
            var path when path.Contains("Grass.png", StringComparison.OrdinalIgnoreCase) => 4,
            var path when path.Contains("GrassPath", StringComparison.OrdinalIgnoreCase) => 5,
            var path when path.Contains("Anvil", StringComparison.OrdinalIgnoreCase) => 6,
            var path when path.Contains("RedstoneBlock", StringComparison.OrdinalIgnoreCase) => 7,
            var path when path.Contains("RedstoneLampOn", StringComparison.OrdinalIgnoreCase) => 8,
            var path when path.Contains("RedstoneLampOff", StringComparison.OrdinalIgnoreCase) => 9,
            var path when path.Contains("Egg", StringComparison.OrdinalIgnoreCase) => 10,
            var path when path.Contains("Fabric", StringComparison.OrdinalIgnoreCase) => 11,
            var path when path.Contains("Quilt", StringComparison.OrdinalIgnoreCase) => 12,
            var path when path.Contains("NeoForge", StringComparison.OrdinalIgnoreCase) => 13,
            var path when path.Contains("Cleanroom", StringComparison.OrdinalIgnoreCase) => 14,
            _ => DetermineInstallIconName(manifestSummary) switch
            {
                "Anvil.png" => 6,
                "Fabric.png" => 11,
                "Quilt.png" => 12,
                "NeoForge.png" => 13,
                "Cleanroom.png" => 14,
                "GrassPath.png" => 5,
                "Egg.png" => 10,
                _ => 4
            }
        };
    }

    private static string? MapStoredLogoPath(string rawLogoPath)
    {
        if (string.IsNullOrWhiteSpace(rawLogoPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(rawLogoPath.Replace("pack://application:,,,/images/Blocks/", string.Empty, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.Combine(LauncherRootDirectory, "Images", "Blocks", fileName);
    }

    private static int MapInstanceCategoryIndex(int storedValue)
    {
        return storedValue switch
        {
            <= 0 => 0,
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            _ => 0
        };
    }

    private static string DetermineCategoryLabel(int categoryIndex, II18nService? i18n)
    {
        return categoryIndex switch
        {
            1 => Text(i18n, "instance.overview.categories.hidden", "Hidden instance"),
            2 => Text(i18n, "instance.overview.categories.modable", "Mod-capable"),
            3 => Text(i18n, "instance.overview.categories.regular", "Regular instance"),
            4 => Text(i18n, "instance.overview.categories.rare", "Rarely used instance"),
            5 => Text(i18n, "instance.overview.categories.april_fools", "April Fools version"),
            _ => Text(i18n, "instance.overview.categories.auto", "Auto")
        };
    }

    private static string DisplayVersion(string? version, II18nService? i18n)
    {
        return string.IsNullOrWhiteSpace(version) ? Text(i18n, "instance.common.not_installed", "Not installed") : version;
    }

    private static string DisplayInstalled(bool installed, string? version, II18nService? i18n)
    {
        if (!installed)
        {
            return Text(i18n, "instance.common.not_installed", "Not installed");
        }

        return string.IsNullOrWhiteSpace(version) ? Text(i18n, "instance.common.installed", "Installed") : version;
    }

    private static string PrefixVersion(string title, string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? string.Empty : $"{title} {version}";
    }

    private static string GetRelativeParent(string rootDirectory, string path, II18nService? i18n)
    {
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return Text(i18n, "instance.content.resource.meta.root_directory", "Root directory");
        }

        var relative = Path.GetRelativePath(rootDirectory, parent);
        return string.Equals(relative, ".", StringComparison.Ordinal) ? Text(i18n, "instance.content.resource.meta.root_directory", "Root directory") : relative;
    }

    private static bool IsGlobalJavaPreferenceValue(string rawValue)
    {
        return string.Equals(rawValue, LegacyGlobalJavaPreferenceLabel, StringComparison.Ordinal)
               || string.Equals(rawValue, "Follow global setting", StringComparison.Ordinal)
               || string.Equals(rawValue, "global", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveResourceDirectory(FrontendInstanceSelectionState selection, ResourceKind kind)
    {
        var folderName = kind switch
        {
            ResourceKind.Mods or ResourceKind.DisabledMods => "mods",
            ResourceKind.ResourcePacks => "resourcepacks",
            ResourceKind.Shaders => "shaderpacks",
            ResourceKind.Schematics => "schematics",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(folderName))
        {
            return selection.IndieDirectory;
        }

        if (!selection.HasLabyMod || string.IsNullOrWhiteSpace(selection.VanillaVersion))
        {
            return Path.Combine(selection.IndieDirectory, folderName);
        }

        return Path.Combine(selection.IndieDirectory, "labymod-neo", "fabric", selection.VanillaVersion, folderName);
    }

    private static FrontendVersionManifestSummary ReadManifestSummary(string launcherFolder, string selectedInstanceName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var profile = FrontendVersionManifestInspector.ReadProfile(launcherFolder, selectedInstanceName);
        return new FrontendVersionManifestSummary(
            VanillaVersion: profile.VanillaVersion,
            VersionType: profile.VersionType,
            HasForge: profile.HasForge,
            ForgeVersion: profile.ForgeVersion,
            NeoForgeVersion: profile.NeoForgeVersion,
            CleanroomVersion: profile.CleanroomVersion,
            FabricVersion: profile.FabricVersion,
            LegacyFabricVersion: profile.LegacyFabricVersion,
            QuiltVersion: profile.QuiltVersion,
            OptiFineVersion: profile.OptiFineVersion,
            HasLiteLoader: profile.HasLiteLoader,
            LiteLoaderVersion: profile.LiteLoaderVersion,
            LabyModVersion: profile.LabyModVersion,
            HasLabyMod: profile.HasLabyMod,
            HasFabricApi: profile.HasFabricApi,
            FabricApiVersion: profile.FabricApiVersion,
            HasQsl: profile.HasQsl,
            QslVersion: profile.QslVersion,
            HasOptiFabric: profile.HasOptiFabric,
            OptiFabricVersion: profile.OptiFabricVersion,
            LibraryNames: profile.LibraryNames);
    }

    private static bool ResolveIsolationEnabled(
        YamlFileProvider localConfig,
        YamlFileProvider instanceConfig,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return ReadValue(instanceConfig, "VersionArgumentIndieV2", false);
        }

        var globalMode = ReadValue(localConfig, "LaunchArgumentIndieV2", 4);
        return FrontendIsolationPolicyService.ShouldIsolateByGlobalMode(
            globalMode,
            IsModable(manifestSummary),
            FrontendIsolationPolicyService.IsNonReleaseVersionType(manifestSummary.VersionType));
    }

    private static int ResolveInstanceIsolationIndex(YamlFileProvider instanceConfig)
    {
        if (!instanceConfig.Exists("VersionArgumentIndieV2"))
        {
            return 0;
        }

        return ReadValue(instanceConfig, "VersionArgumentIndieV2", false)
            ? 1
            : 2;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static void AddIfNotEmpty(ICollection<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string FormatFileSize(long length)
    {
        if (length >= 1024L * 1024L * 1024L)
        {
            return $"{length / 1024d / 1024d / 1024d:0.0} GB";
        }

        if (length >= 1024L * 1024L)
        {
            return $"{length / 1024d / 1024d:0.0} MB";
        }

        if (length >= 1024L)
        {
            return $"{length / 1024d:0.0} KB";
        }

        return $"{length} B";
    }

    private static string Text(
        II18nService? i18n,
        string key,
        string fallback,
        params (string Key, object? Value)[] args)
    {
        if (i18n is null)
        {
            return ApplyFallbackArgs(fallback, args);
        }

        if (args.Length == 0)
        {
            return i18n.T(key);
        }

        return i18n.T(
            key,
            args.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }

    private static string ApplyFallbackArgs(string fallback, IReadOnlyList<(string Key, object? Value)> args)
    {
        var result = fallback;
        foreach (var (key, value) in args)
        {
            result = result.Replace("{" + key + "}", value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    private sealed record FrontendJavaEntry(
        string ExecutablePath,
        string DisplayName,
        bool? Is64Bit);

    private sealed record FrontendJavaPreference(
        FrontendJavaPreferenceKind Kind,
        string? Value);

    private sealed record FrontendVersionManifestSummary(
        string VanillaVersion,
        string? VersionType,
        bool HasForge,
        string? ForgeVersion,
        string? NeoForgeVersion,
        string? CleanroomVersion,
        string? FabricVersion,
        string? LegacyFabricVersion,
        string? QuiltVersion,
        string? OptiFineVersion,
        bool HasLiteLoader,
        string? LiteLoaderVersion,
        string? LabyModVersion,
        bool HasLabyMod,
        bool HasFabricApi,
        string? FabricApiVersion,
        bool HasQsl,
        string? QslVersion,
        bool HasOptiFabric,
        string? OptiFabricVersion,
        IReadOnlyList<string> LibraryNames)
    {
        public static FrontendVersionManifestSummary Empty { get; } = new(
            VanillaVersion: "Unknown",
            VersionType: null,
            HasForge: false,
            ForgeVersion: null,
            NeoForgeVersion: null,
            CleanroomVersion: null,
            FabricVersion: null,
            LegacyFabricVersion: null,
            QuiltVersion: null,
            OptiFineVersion: null,
            HasLiteLoader: false,
            LiteLoaderVersion: null,
            LabyModVersion: null,
            HasLabyMod: false,
            HasFabricApi: false,
            FabricApiVersion: null,
            HasQsl: false,
            QslVersion: null,
            HasOptiFabric: false,
            OptiFabricVersion: null,
            LibraryNames: Array.Empty<string>());
    }

    private enum ResourceKind
    {
        Mods = 0,
        DisabledMods = 1,
        ResourcePacks = 2,
        Shaders = 3,
        Schematics = 4
    }

    private enum FrontendJavaPreferenceKind
    {
        Global = 0,
        Auto = 1,
        RelativePath = 2,
        Existing = 3
    }
}
