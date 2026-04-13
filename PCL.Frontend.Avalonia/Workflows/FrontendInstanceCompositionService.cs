using System.Text.Json;
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

    public static FrontendInstanceComposition Compose(FrontendRuntimePaths runtimePaths)
    {
        var sharedConfig = new JsonFileProvider(runtimePaths.SharedConfigPath);
        var localConfig = new YamlFileProvider(runtimePaths.LocalConfigPath);
        var launcherDirectory = FrontendLauncherPathService.ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstanceName = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return CreateEmptyComposition(launcherDirectory);
        }

        var instanceDirectory = Path.Combine(launcherDirectory, "versions", selectedInstanceName);
        if (!Directory.Exists(instanceDirectory))
        {
            return CreateEmptyComposition(launcherDirectory, selectedInstanceName);
        }

        var instanceConfig = OpenInstanceConfigProvider(instanceDirectory);
        var manifestSummary = ReadManifestSummary(launcherDirectory, selectedInstanceName);
        var isIndie = ResolveIsolationEnabled(localConfig, instanceConfig, manifestSummary);
        var indieDirectory = isIndie ? instanceDirectory : launcherDirectory;
        var vanillaVersion = manifestSummary.VanillaVersion?.ToString() ?? ReadValue(instanceConfig, "VersionVanillaName", "Unknown");
        var selection = new FrontendInstanceSelectionState(
            HasSelection: true,
            InstanceName: selectedInstanceName,
            InstanceDirectory: instanceDirectory,
            IndieDirectory: indieDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: isIndie,
            IsModable: IsModable(manifestSummary),
            HasLabyMod: manifestSummary.HasLabyMod,
            VanillaVersion: vanillaVersion);
        var javaEntries = ParseJavaEntries(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));
        var setupState = BuildSetupState(selection, manifestSummary, sharedConfig, localConfig, instanceConfig, javaEntries);

        return new FrontendInstanceComposition(
            selection,
            BuildOverviewState(selection, manifestSummary, instanceConfig, setupState),
            setupState,
            BuildExportState(selection, manifestSummary),
            BuildInstallState(selection, manifestSummary),
            new FrontendInstanceContentState(BuildWorldEntries(selection)),
            new FrontendInstanceScreenshotState(BuildScreenshotEntries(selection)),
            new FrontendInstanceServerState(BuildServerEntries(selection)),
            new FrontendInstanceResourceState(BuildResourceEntries(selection, ResourceKind.Mods)),
            new FrontendInstanceResourceState(BuildResourceEntries(selection, ResourceKind.DisabledMods)),
            new FrontendInstanceResourceState(BuildResourceEntries(selection, ResourceKind.ResourcePacks)),
            new FrontendInstanceResourceState(BuildResourceEntries(selection, ResourceKind.Shaders)),
            new FrontendInstanceResourceState(BuildResourceEntries(selection, ResourceKind.Schematics)));
    }

    private static FrontendInstanceComposition CreateEmptyComposition(string launcherDirectory, string instanceName = "未选择实例")
    {
        var selection = new FrontendInstanceSelectionState(
            HasSelection: false,
            InstanceName: instanceName,
            InstanceDirectory: string.Empty,
            IndieDirectory: launcherDirectory,
            LauncherDirectory: launcherDirectory,
            IsIndie: false,
            IsModable: false,
            HasLabyMod: false,
            VanillaVersion: "Unknown");
        var setup = new FrontendInstanceSetupState(
            IsolationIndex: 0,
            WindowTitle: string.Empty,
            UseDefaultWindowTitle: false,
            CustomInfo: string.Empty,
            JavaOptions:
            [
                new FrontendInstanceJavaOption("global", "跟随全局设置"),
                new FrontendInstanceJavaOption("auto", "自动选择合适的 Java")
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
            WaitForPreLaunchCommand: true,
            IgnoreJavaCompatibilityWarning: false,
            DisableFileValidation: false,
            FollowLauncherProxy: false,
            DisableJavaLaunchWrapper: false,
            DisableRetroWrapper: false,
            UseDebugLog4jConfig: false);

        return new FrontendInstanceComposition(
            selection,
            new FrontendInstanceOverviewState(
                instanceName,
                "请选择一个实例后再查看详细内容。",
                null,
                0,
                0,
                false,
                [],
                []),
            setup,
            new FrontendInstanceExportState(instanceName, "1.0.0", false, false, false, []),
            new FrontendInstanceInstallState(instanceName, "未选择实例", "Minecraft", "Grass.png", [], []),
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
        FrontendInstanceSetupState setupState)
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
            "启动次数",
            launchCount == 0 ? "从未启动" : $"已启动 {launchCount} 次"));

        if (!string.IsNullOrWhiteSpace(modpackVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("整合包版本", modpackVersion));
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
            infoEntries.Add(new FrontendInstanceInfoEntry("LiteLoader", "已安装"));
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
            infoEntries.Add(new FrontendInstanceInfoEntry("实例描述", customInfo));
        }

        var tags = new List<string>();
        AddIfNotEmpty(tags, DeterminePrimaryLoaderLabel(manifestSummary));
        if (selection.IsModable)
        {
            tags.Add("支持 Mod");
        }

        if (isStarred)
        {
            tags.Add("已收藏");
        }
        else
        {
            AddIfNotEmpty(tags, DetermineCategoryLabel(categoryIndex));
        }

        var iconPath = ResolveOverviewIconPath(selection, manifestSummary, instanceConfig);
        return new FrontendInstanceOverviewState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary),
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
        IReadOnlyList<FrontendJavaEntry> javaEntries)
    {
        var javaPreference = ReadJavaPreference(ReadValue(instanceConfig, "VersionArgumentJavaSelect", "使用全局设置"));
        var javaOptions = BuildJavaOptions(javaEntries, selection.LauncherDirectory, javaPreference);
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
            WaitForPreLaunchCommand: ReadValue(instanceConfig, "VersionAdvanceRunWait", true),
            IgnoreJavaCompatibilityWarning: ReadValue(instanceConfig, "VersionAdvanceJava", false),
            DisableFileValidation: ReadValue(instanceConfig, "VersionAdvanceAssetsV2", false),
            FollowLauncherProxy: ReadValue(instanceConfig, "VersionAdvanceUseProxyV2", false),
            DisableJavaLaunchWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableJLW", false),
            DisableRetroWrapper: ReadValue(instanceConfig, "VersionAdvanceDisableRW", false),
            UseDebugLog4jConfig: ReadValue(instanceConfig, "VersionUseDebugLog4j2Config", false));
    }

    private static FrontendInstanceExportState BuildExportState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary)
    {
        var resourcePackEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.ResourcePacks), ArchiveExtensions, allowDirectories: true, recursive: false);
        var shaderEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Shaders), ArchiveExtensions, allowDirectories: true, recursive: false);
        var schematicEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Schematics), SchematicExtensions, allowDirectories: false, recursive: true);
        var saveEntries = BuildExportWorldOptions(selection);
        var screenshotEntries = BuildScreenshotEntries(selection);
        var replayEntries = BuildFolderEntries(Path.Combine(selection.IndieDirectory, "replay_recordings"), [".mcpr"], allowDirectories: false, recursive: false);
        var hasServers = File.Exists(Path.Combine(selection.IndieDirectory, "servers.dat"));
        var hasLauncherContent = Directory.Exists(Path.Combine(selection.InstanceDirectory, "PCL"));

        var groups = new List<FrontendInstanceExportOptionGroup>
        {
            new(
                "游戏本体",
                string.Empty,
                true,
                [
                    CreateExportOption("游戏本体设置", File.Exists(Path.Combine(selection.IndieDirectory, "options.txt")) ? "检测到 options.txt" : "未检测到配置文件", File.Exists(Path.Combine(selection.IndieDirectory, "options.txt"))),
                    CreateExportOption("游戏本体个人信息", File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt")) ? "检测到 OptiFine 设置" : "未检测到个人设置", File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt"))),
                    CreateExportOption(
                        "OptiFine 设置",
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion) ? "当前实例包含 OptiFine" : "当前实例未安装 OptiFine",
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion))
                ]),
            new(
                "Mod",
                "模组",
                selection.IsModable,
                [
                    CreateExportOption("已禁用的 Mod", $"{BuildResourceEntries(selection, ResourceKind.DisabledMods).Count} 项", BuildResourceEntries(selection, ResourceKind.DisabledMods).Count > 0),
                    CreateExportOption("整合包重要数据", Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? "检测到 config 文件夹" : "未检测到 config 文件夹", Directory.Exists(Path.Combine(selection.IndieDirectory, "config"))),
                    CreateExportOption("Mod 设置", Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? "检测到配置目录" : "未检测到配置目录", Directory.Exists(Path.Combine(selection.IndieDirectory, "config")))
                ]),
            new("资源包", "纹理包 / 材质包", resourcePackEntries.Count > 0, resourcePackEntries.Select(ToExportOption).ToArray()),
            new("光影包", string.Empty, shaderEntries.Count > 0, shaderEntries.Select(ToExportOption).ToArray()),
            new("截图", string.Empty, screenshotEntries.Count > 0, []),
            new("导出的结构", "schematics 文件夹", schematicEntries.Count > 0, schematicEntries.Select(ToExportOption).ToArray()),
            new("录像回放", "Replay Mod 的录像文件", replayEntries.Count > 0, replayEntries.Select(ToExportOption).ToArray()),
            new("单人游戏存档", "世界 / 地图", false, saveEntries),
            new("多人游戏服务器列表", string.Empty, hasServers, []),
            new(
                "PCL 启动器程序",
                "打包跨平台版 PCL，以便没有启动器的玩家安装整合包",
                hasLauncherContent,
                [
                    CreateExportOption("PCL 个性化内容", hasLauncherContent ? "检测到实例 PCL 配置目录" : "未检测到实例 PCL 配置目录", hasLauncherContent)
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

    private static FrontendInstanceInstallState BuildInstallState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary)
    {
        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifestSummary.FabricVersion) && !manifestSummary.HasFabricApi)
        {
            hints.Add("你尚未选择安装 Fabric API，这会导致大多数 Mod 无法使用！");
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion) && !manifestSummary.HasQsl)
        {
            hints.Add("你尚未选择安装 QFAPI / QSL，这会导致大多数 Mod 无法使用！");
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion)
            && !string.IsNullOrWhiteSpace(manifestSummary.FabricVersion)
            && !manifestSummary.HasOptiFabric)
        {
            hints.Add("你尚未选择安装 OptiFabric，这会导致 OptiFine 无法使用！");
        }

        return new FrontendInstanceInstallState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary),
            $"Minecraft {selection.VanillaVersion}",
            DetermineInstallIconName(manifestSummary),
            hints,
            [
                new FrontendInstanceInstallOption("Forge", DisplayVersion(manifestSummary.ForgeVersion), "Anvil.png"),
                new FrontendInstanceInstallOption("Cleanroom", DisplayVersion(manifestSummary.CleanroomVersion), "Cleanroom.png"),
                new FrontendInstanceInstallOption("NeoForge", DisplayVersion(manifestSummary.NeoForgeVersion), "NeoForge.png"),
                new FrontendInstanceInstallOption("Fabric", DisplayVersion(manifestSummary.FabricVersion), "Fabric.png"),
                new FrontendInstanceInstallOption("Legacy Fabric", DisplayVersion(manifestSummary.LegacyFabricVersion), "Fabric.png"),
                new FrontendInstanceInstallOption("Fabric API", DisplayInstalled(manifestSummary.HasFabricApi, manifestSummary.FabricApiVersion), "Fabric.png"),
                new FrontendInstanceInstallOption("QFAPI / QSL", DisplayInstalled(manifestSummary.HasQsl, manifestSummary.QslVersion), "Quilt.png"),
                new FrontendInstanceInstallOption("Quilt", DisplayVersion(manifestSummary.QuiltVersion), "Quilt.png"),
                new FrontendInstanceInstallOption("LabyMod", DisplayVersion(manifestSummary.LabyModVersion), "LabyMod.png"),
                new FrontendInstanceInstallOption("OptiFine", DisplayVersion(manifestSummary.OptiFineVersion), "GrassPath.png"),
                new FrontendInstanceInstallOption("OptiFabric", DisplayInstalled(manifestSummary.HasOptiFabric, manifestSummary.OptiFabricVersion), "OptiFabric.png"),
                new FrontendInstanceInstallOption("LiteLoader", DisplayInstalled(manifestSummary.HasLiteLoader, manifestSummary.LiteLoaderVersion), "Egg.png")
            ]);
    }

    private static IReadOnlyList<FrontendInstanceDirectoryEntry> BuildWorldEntries(FrontendInstanceSelectionState selection)
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
                $"创建时间：{directory.CreationTime:yyyy/MM/dd}，最后修改时间：{directory.LastWriteTime:yyyy/MM/dd}",
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
                directory.Name,
                directory.LastWriteTime.ToString("yyyy/MM/dd HH:mm"),
                true))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceScreenshotEntry> BuildScreenshotEntries(FrontendInstanceSelectionState selection)
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
                $"{file.CreationTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                file.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceServerEntry> BuildServerEntries(FrontendInstanceSelectionState selection)
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
                    Title: server.Get<NbtString>("name")?.Value ?? "Minecraft服务器",
                    Address: server.Get<NbtString>("ip")?.Value ?? string.Empty,
                    Status: "已保存服务器"))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildResourceEntries(
        FrontendInstanceSelectionState selection,
        ResourceKind kind)
    {
        return kind switch
        {
            ResourceKind.Mods => BuildFileResourceEntries(
                ResolveResourceDirectory(selection, kind),
                recursive: false,
                fileFilter: path => EnabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                metaPrefix: "已启用",
                defaultIconName: DetermineInstallIconNameFromExtension("mods", selection)),
            ResourceKind.DisabledMods => BuildFileResourceEntries(
                ResolveResourceDirectory(selection, ResourceKind.Mods),
                recursive: false,
                fileFilter: path => DisabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                metaPrefix: "已禁用",
                defaultIconName: "RedstoneBlock.png",
                isEnabled: false),
            ResourceKind.ResourcePacks => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), "资源包", "Grass.png"),
            ResourceKind.Shaders => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), "光影包", "RedstoneLampOn.png"),
            ResourceKind.Schematics => BuildFileResourceEntries(
                ResolveResourceDirectory(selection, kind),
                recursive: true,
                fileFilter: path => SchematicExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                metaPrefix: "投影文件",
                defaultIconName: "CommandBlock.png"),
            _ => []
        };
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFileResourceEntries(
        string directory,
        bool recursive,
        Func<string, bool> fileFilter,
        string metaPrefix,
        string defaultIconName,
        bool isEnabled = true)
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
                Summary: $"{GetRelativeParent(directory, file.FullName)} • {file.LastWriteTime:yyyy/MM/dd HH:mm}",
                Meta: $"{metaPrefix} • {file.Extension.TrimStart('.').ToUpperInvariant()}",
                Path: file.FullName,
                IconName: defaultIconName,
                IsEnabled: isEnabled))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFolderAndArchiveEntries(string directory, string metaPrefix, string iconName)
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
                Summary: $"{file.LastWriteTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                Meta: $"{metaPrefix} • 压缩包",
                Path: file.FullName,
                IconName: iconName));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .Select(folder => new FrontendInstanceResourceEntry(
                Title: folder.Name,
                Summary: $"{folder.LastWriteTime:yyyy/MM/dd HH:mm} • 文件夹",
                Meta: $"{metaPrefix} • 文件夹",
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
        return new FrontendInstanceExportOptionEntry(entry.Title, entry.Summary, true);
    }

    private static FrontendInstanceExportOptionEntry CreateExportOption(string title, string description, bool isChecked)
    {
        return new FrontendInstanceExportOptionEntry(title, description, isChecked);
    }

    private static string ReadVersionFallback(string instanceDirectory)
    {
        var iniPath = Path.Combine(instanceDirectory, "PCL", "config.v1.yml");
        return File.Exists(iniPath) ? "1.0.0" : "1.0.0";
    }

    private static List<FrontendInstanceJavaOption> BuildJavaOptions(
        IReadOnlyList<FrontendJavaEntry> javaEntries,
        string launcherDirectory,
        FrontendJavaPreference preference)
    {
        var options = new List<FrontendInstanceJavaOption>
        {
            new("global", "跟随全局设置"),
            new("auto", "自动选择合适的 Java")
        };

        if (preference.Kind == FrontendJavaPreferenceKind.RelativePath && !string.IsNullOrWhiteSpace(preference.Value))
        {
            options.Add(new FrontendInstanceJavaOption(
                $"relative:{preference.Value}",
                $"启动器目录下的 Java | {preference.Value}"));
        }
        else
        {
            options.Add(new FrontendInstanceJavaOption("relative", "选择启动器目录下的 Java"));
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
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "使用全局设置", StringComparison.Ordinal))
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

    private static string BuildInstanceSubtitle(FrontendInstanceSelectionState selection, FrontendVersionManifestSummary manifestSummary)
    {
        var parts = new List<string>();
        AddIfNotEmpty(parts, DeterminePrimaryLoaderLabel(manifestSummary));
        AddIfNotEmpty(parts, $"Minecraft {selection.VanillaVersion}");
        parts.Add(selection.IsIndie ? "独立实例" : "共用实例");
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

    private static string DetermineCategoryLabel(int categoryIndex)
    {
        return categoryIndex switch
        {
            1 => "隐藏实例",
            2 => "可安装 Mod",
            3 => "常规实例",
            4 => "不常用实例",
            5 => "愚人节版本",
            _ => "自动"
        };
    }

    private static string DisplayVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? "未安装" : version;
    }

    private static string DisplayInstalled(bool installed, string? version)
    {
        if (!installed)
        {
            return "未安装";
        }

        return string.IsNullOrWhiteSpace(version) ? "已安装" : version;
    }

    private static string PrefixVersion(string title, string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? string.Empty : $"{title} {version}";
    }

    private static string GetRelativeParent(string rootDirectory, string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return "根目录";
        }

        var relative = Path.GetRelativePath(rootDirectory, parent);
        return string.Equals(relative, ".", StringComparison.Ordinal) ? "根目录" : relative;
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

    private static YamlFileProvider OpenInstanceConfigProvider(string instanceDirectory)
    {
        var pclDirectory = Path.Combine(instanceDirectory, "PCL");
        var configPath = Path.Combine(pclDirectory, "config.v1.yml");
        if (!File.Exists(configPath))
        {
            var legacyPath = Path.Combine(pclDirectory, "Setup.ini");
            if (File.Exists(legacyPath))
            {
                Directory.CreateDirectory(pclDirectory);
                var provider = new YamlFileProvider(configPath);
                foreach (var line in File.ReadLines(legacyPath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var splitIndex = line.IndexOf(':');
                    if (splitIndex <= 0)
                    {
                        continue;
                    }

                    provider.Set(line[..splitIndex], line[(splitIndex + 1)..]);
                }

                provider.Sync();
            }
        }

        return new YamlFileProvider(configPath);
    }

    private static FrontendVersionManifestSummary ReadManifestSummary(string launcherFolder, string selectedInstanceName)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        return ReadManifestSummaryRecursive(launcherFolder, selectedInstanceName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static FrontendVersionManifestSummary ReadManifestSummaryRecursive(
        string launcherFolder,
        string versionName,
        ISet<string> visited)
    {
        if (!visited.Add(versionName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var manifestPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.json");
        if (!File.Exists(manifestPath))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var parentVersion = GetString(root, "inheritsFrom");
        var parentSummary = string.IsNullOrWhiteSpace(parentVersion)
            ? FrontendVersionManifestSummary.Empty
            : ReadManifestSummaryRecursive(launcherFolder, parentVersion, visited);
        var currentLibraries = ParseLibraryNames(root);
        var allLibraries = parentSummary.LibraryNames.Concat(currentLibraries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var vanillaVersion = TryParseVanillaVersion(FirstNonEmpty(parentVersion, GetString(root, "id")))?.ToString() ?? parentSummary.VanillaVersion;

        return new FrontendVersionManifestSummary(
            VanillaVersion: vanillaVersion,
            VersionType: FirstNonEmpty(GetString(root, "type"), parentSummary.VersionType),
            HasForge: parentSummary.HasForge || ContainsLibrary(allLibraries, "net.minecraftforge:forge"),
            ForgeVersion: parentSummary.ForgeVersion ?? ExtractLibraryVersion(allLibraries, "net.minecraftforge:forge"),
            NeoForgeVersion: parentSummary.NeoForgeVersion ?? ExtractLibraryVersion(allLibraries, "net.neoforged:neoforge"),
            CleanroomVersion: parentSummary.CleanroomVersion ?? ExtractLibraryVersion(allLibraries, "com.cleanroommc"),
            FabricVersion: parentSummary.FabricVersion ?? ExtractLibraryVersion(allLibraries, "net.fabricmc:fabric-loader"),
            LegacyFabricVersion: parentSummary.LegacyFabricVersion ?? ExtractLibraryVersion(allLibraries, "net.legacyfabric"),
            QuiltVersion: parentSummary.QuiltVersion ?? ExtractLibraryVersion(allLibraries, "org.quiltmc:quilt-loader"),
            OptiFineVersion: parentSummary.OptiFineVersion ?? ExtractOptiFineVersion(allLibraries),
            HasLiteLoader: parentSummary.HasLiteLoader || ContainsLibrary(allLibraries, "liteloader"),
            LiteLoaderVersion: parentSummary.LiteLoaderVersion ?? ExtractLibraryVersion(allLibraries, "com.mumfrey:liteloader"),
            LabyModVersion: parentSummary.LabyModVersion ?? ExtractLibraryVersion(allLibraries, "net.labymod"),
            HasLabyMod: parentSummary.HasLabyMod || ContainsLibrary(allLibraries, "labymod"),
            HasFabricApi: parentSummary.HasFabricApi || ContainsLibrary(allLibraries, "fabric-api"),
            FabricApiVersion: parentSummary.FabricApiVersion ?? ExtractLibraryVersion(allLibraries, "net.fabricmc.fabric-api:fabric-api"),
            HasQsl: parentSummary.HasQsl || ContainsLibrary(allLibraries, "quilted-fabric-api") || ContainsLibrary(allLibraries, ":qsl"),
            QslVersion: parentSummary.QslVersion
                        ?? ExtractLibraryVersion(allLibraries, "org.quiltmc.quilted-fabric-api")
                        ?? ExtractLibraryVersion(allLibraries, "org.quiltmc:qsl"),
            HasOptiFabric: parentSummary.HasOptiFabric || ContainsLibrary(allLibraries, "optifabric"),
            OptiFabricVersion: parentSummary.OptiFabricVersion ?? ExtractLibraryVersion(allLibraries, "optifabric"),
            LibraryNames: allLibraries);
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

    private static IReadOnlyList<string> ParseLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return libraries.EnumerateArray()
            .Select(library => GetString(library, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
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

    private static bool ContainsLibrary(IEnumerable<string> libraries, string searchText)
    {
        return libraries.Any(library => library.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractLibraryVersion(IEnumerable<string> libraries, string prefix)
    {
        var match = libraries.FirstOrDefault(library => library.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase));
        return match?.Split(':').LastOrDefault();
    }

    private static string? ExtractOptiFineVersion(IEnumerable<string> libraries)
    {
        var match = libraries.FirstOrDefault(library => library.Contains("optifine", StringComparison.OrdinalIgnoreCase));
        return match?.Split(':').LastOrDefault();
    }

    private static Version? TryParseVanillaVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var candidate = rawValue.Trim();
        if (candidate.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[1..];
        }

        var filtered = new string(candidate.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        return Version.TryParse(filtered, out var version) ? version : null;
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

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static bool? GetNestedBoolean(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : null;
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
