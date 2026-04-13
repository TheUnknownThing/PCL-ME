using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft.Java;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendSetupCompositionService
{
    private const int ObsoleteLaunchVisibilityIndex = 1;

    public static FrontendSetupComposition Compose(FrontendRuntimePaths paths)
    {
        var sharedConfig = new JsonFileProvider(paths.SharedConfigPath);
        var localConfig = new YamlFileProvider(paths.LocalConfigPath);

        return new FrontendSetupComposition(
            BuildAboutState(),
            BuildLogState(paths),
            BuildUpdateState(paths, sharedConfig, localConfig),
            BuildLaunchState(sharedConfig, localConfig),
            BuildGameLinkState(sharedConfig),
            BuildGameManageState(sharedConfig),
            BuildLauncherMiscState(paths, sharedConfig, localConfig),
            BuildJavaState(sharedConfig, localConfig),
            BuildUiState(sharedConfig, localConfig));
    }

    public static int MapStoredLaunchVisibilityToDisplayIndex(int storedValue)
    {
        return storedValue switch
        {
            <= 0 => 0,
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 3,
            _ => 4
        };
    }

    public static int MapDisplayLaunchVisibilityToStoredIndex(int displayValue)
    {
        return Math.Clamp(displayValue, 0, 4) switch
        {
            0 => 0,
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 5
        };
    }

    public static double MapStoredLaunchRamToGb(int storedValue)
    {
        if (storedValue <= 12)
        {
            return Math.Round(storedValue * 0.1 + 0.3, 1);
        }

        if (storedValue <= 25)
        {
            return Math.Round((storedValue - 12) * 0.5 + 1.5, 1);
        }

        if (storedValue <= 33)
        {
            return Math.Round((double)(storedValue - 25 + 8), 1);
        }

        return Math.Round((double)((storedValue - 33) * 2 + 16), 1);
    }

    public static int MapLaunchRamGbToStoredValue(double gb)
    {
        var clamped = Math.Clamp(gb, 0.4, 32.0);

        if (clamped <= 1.5)
        {
            return Math.Clamp((int)Math.Round((clamped - 0.3) / 0.1), 1, 12);
        }

        if (clamped <= 8)
        {
            return Math.Clamp((int)Math.Round((clamped - 1.5) / 0.5) + 12, 13, 25);
        }

        if (clamped <= 16)
        {
            return Math.Clamp((int)Math.Round(clamped - 8) + 25, 26, 33);
        }

        return Math.Max(34, (int)Math.Round((clamped - 16) / 2) + 33);
    }

    private static FrontendSetupAboutState BuildAboutState()
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? AppContext.BaseDirectory);
        var versionText = string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
            ? typeof(FrontendSetupCompositionService).Assembly.GetName().Version?.ToString() ?? "unknown"
            : versionInfo.ProductVersion;
        return new FrontendSetupAboutState($"当前版本: {versionText}");
    }

    private static FrontendSetupLogState BuildLogState(FrontendRuntimePaths paths)
    {
        var logDirectories = new[]
        {
            Path.Combine(paths.LauncherAppDataDirectory, "Log"),
            Path.Combine(paths.ExecutableDirectory, "PCL", "Log"),
            paths.FrontendArtifactDirectory
        };

        var entries = logDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Take(8)
            .Select(file => new FrontendSetupLogEntry(
                file.Name,
                $"{file.DirectoryName} • {file.LastWriteTime:yyyy-MM-dd HH:mm}",
                file.FullName))
            .ToArray();

        if (entries.Length == 0)
        {
            entries =
            [
                new FrontendSetupLogEntry(
                    "暂无日志文件",
                    "当前运行目录下还没有可供设置页展示的日志文件。",
                    paths.LauncherAppDataDirectory)
            ];
        }

        return new FrontendSetupLogState(entries);
    }

    private static FrontendSetupUpdateState BuildUpdateState(
        FrontendRuntimePaths paths,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        return new FrontendSetupUpdateState(
            UpdateChannelIndex: ReadValue(localConfig, "SystemUpdateChannel", 0),
            UpdateModeIndex: ReadValue(localConfig, "SystemSystemUpdate", 1),
            MirrorCdk: ReadProtectedValue(paths, "SystemMirrorChyanKey"));
    }

    private static FrontendSetupLaunchState BuildLaunchState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        var storedVisibility = ReadValue(sharedConfig, "LaunchArgumentVisible", 5);
        var storedCustomRam = ReadValue(localConfig, "LaunchRamCustom", 15);

        return new FrontendSetupLaunchState(
            IsolationIndex: ReadValue(localConfig, "LaunchArgumentIndieV2", 4),
            WindowTitle: ReadValue(localConfig, "LaunchArgumentTitle", string.Empty),
            CustomInfo: ReadValue(localConfig, "LaunchArgumentInfo", "PCLCE"),
            VisibilityIndex: MapStoredLaunchVisibilityToDisplayIndex(storedVisibility),
            PriorityIndex: ReadValue(sharedConfig, "LaunchArgumentPriority", 1),
            WindowTypeIndex: ReadValue(localConfig, "LaunchArgumentWindowType", 1),
            WindowWidth: ReadValue(localConfig, "LaunchArgumentWindowWidth", 854).ToString(),
            WindowHeight: ReadValue(localConfig, "LaunchArgumentWindowHeight", 480).ToString(),
            UseAutomaticRamAllocation: ReadValue(localConfig, "LaunchRamType", 0) == 0,
            CustomRamAllocationGb: MapStoredLaunchRamToGb(storedCustomRam),
            OptimizeMemoryBeforeLaunch: ReadValue(sharedConfig, "LaunchArgumentRam", false),
            RendererIndex: ReadValue(localConfig, "LaunchAdvanceRenderer", 0),
            JvmArguments: ReadValue(localConfig, "LaunchAdvanceJvm", "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true"),
            GameArguments: ReadValue(localConfig, "LaunchAdvanceGame", string.Empty),
            BeforeCommand: ReadValue(localConfig, "LaunchAdvanceRun", string.Empty),
            WaitForBeforeCommand: ReadValue(localConfig, "LaunchAdvanceRunWait", true),
            DisableJavaLaunchWrapper: ReadValue(localConfig, "LaunchAdvanceDisableJLW", true),
            DisableRetroWrapper: ReadValue(sharedConfig, "LaunchAdvanceDisableRW", false),
            RequireDedicatedGpu: ReadValue(sharedConfig, "LaunchAdvanceGraphicCard", true),
            UseJavaExecutable: ReadValue(sharedConfig, "LaunchAdvanceNoJavaw", false),
            MicrosoftAuthIndex: ReadValue(sharedConfig, "LoginMsAuthType", 1),
            PreferredIpStackIndex: ReadValue(sharedConfig, "LaunchPreferredIpStack", 1));
    }

    private static FrontendSetupGameLinkState BuildGameLinkState(JsonFileProvider sharedConfig)
    {
        return new FrontendSetupGameLinkState(
            Username: ReadValue(sharedConfig, "LinkUsername", string.Empty),
            ProtocolPreferenceIndex: ReadValue(sharedConfig, "LinkProtocolPreference", 0),
            PreferLowestLatencyPath: ReadValue(sharedConfig, "LinkLatencyFirstMode", true),
            TryPunchSymmetricNat: ReadValue(sharedConfig, "LinkTryPunchSym", true),
            AllowIpv6Communication: ReadValue(sharedConfig, "LinkEnableIPv6", true),
            EnableCliOutput: ReadValue(sharedConfig, "LinkEnableCliOutput", false));
    }

    private static FrontendSetupGameManageState BuildGameManageState(JsonFileProvider sharedConfig)
    {
        return new FrontendSetupGameManageState(
            DownloadSourceIndex: ReadValue(sharedConfig, "ToolDownloadSource", 1),
            VersionSourceIndex: ReadValue(sharedConfig, "ToolDownloadVersion", 1),
            DownloadThreadLimit: ReadValue(sharedConfig, "ToolDownloadThread", 63),
            DownloadSpeedLimit: ReadValue(sharedConfig, "ToolDownloadSpeed", 42),
            AutoSelectNewInstance: ReadValue(sharedConfig, "ToolDownloadAutoSelectVersion", true),
            UpgradePartialAuthlib: ReadValue(sharedConfig, "ToolFixAuthlib", true),
            CommunityDownloadSourceIndex: ReadValue(sharedConfig, "ToolDownloadMod", 1),
            FileNameFormatIndex: ReadValue(sharedConfig, "ToolDownloadTranslateV2", 1),
            ModLocalNameStyleIndex: ReadValue(sharedConfig, "ToolModLocalNameStyle", 0),
            IgnoreQuiltLoader: ReadValue(sharedConfig, "ToolDownloadIgnoreQuilt", false),
            NotifyReleaseUpdates: ReadValue(sharedConfig, "ToolUpdateRelease", false),
            NotifySnapshotUpdates: ReadValue(sharedConfig, "ToolUpdateSnapshot", false),
            AutoSwitchGameLanguageToChinese: ReadValue(sharedConfig, "ToolHelpChinese", true),
            DetectClipboardResourceLinks: ReadValue(sharedConfig, "ToolDownloadClipboard", false));
    }

    private static FrontendSetupLauncherMiscState BuildLauncherMiscState(
        FrontendRuntimePaths paths,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        return new FrontendSetupLauncherMiscState(
            SystemActivityIndex: ReadValue(localConfig, "SystemSystemActivity", 0),
            AnimationFpsLimit: ReadValue(sharedConfig, "UiAniFPS", 59),
            MaxRealTimeLogValue: ReadValue(sharedConfig, "SystemMaxLog", 13),
            DisableHardwareAcceleration: ReadValue(sharedConfig, "SystemDisableHardwareAcceleration", false),
            EnableTelemetry: ReadValue(sharedConfig, "SystemTelemetry", false),
            EnableDoH: ReadValue(sharedConfig, "SystemNetEnableDoH", true),
            HttpProxyTypeIndex: ReadValue(sharedConfig, "SystemHttpProxyType", 1),
            HttpProxyAddress: ReadProtectedValue(paths, "SystemHttpProxy"),
            HttpProxyUsername: ReadValue(sharedConfig, "SystemHttpProxyCustomUsername", string.Empty),
            HttpProxyPassword: ReadValue(sharedConfig, "SystemHttpProxyCustomPassword", string.Empty),
            DebugAnimationSpeed: ReadValue(sharedConfig, "SystemDebugAnim", 9),
            SkipCopyDuringDownload: ReadValue(sharedConfig, "SystemDebugSkipCopy", false),
            DebugModeEnabled: ReadValue(sharedConfig, "SystemDebugMode", false),
            DebugDelayEnabled: ReadValue(sharedConfig, "SystemDebugDelay", false));
    }

    private static FrontendSetupJavaState BuildJavaState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        var selectedJava = ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty);
        var rawJavaList = ReadValue(localConfig, "LaunchArgumentJavaUser", "[]");
        JavaStorageItem[] items;

        try
        {
            items = JsonSerializer.Deserialize<JavaStorageItem[]>(rawJavaList) ?? [];
        }
        catch
        {
            items = [];
        }

        var entries = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .Select(item => BuildJavaRuntimeEntry(item))
            .OrderByDescending(item => item.IsEnabled)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FrontendSetupJavaState(
            string.IsNullOrWhiteSpace(selectedJava) ? "auto" : selectedJava,
            entries);
    }

    private static FrontendSetupJavaRuntimeEntry BuildJavaRuntimeEntry(JavaStorageItem item)
    {
        var folder = ResolveJavaFolder(item.Path);
        var title = ResolveJavaTitle(folder, item.Path);
        var tags = new List<string>
        {
            item.IsEnable ? "已启用" : "已禁用",
            item.Source switch
            {
                JavaSource.AutoInstalled => "自动安装",
                JavaSource.ManualAdded => "手动添加",
                _ => "自动扫描"
            }
        };

        return new FrontendSetupJavaRuntimeEntry(
            item.Path,
            title,
            folder,
            tags,
            item.IsEnable);
    }

    private static FrontendSetupUiState BuildUiState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        return new FrontendSetupUiState(
            DarkModeIndex: ReadValue(sharedConfig, "UiDarkMode", 2),
            LightColorIndex: ReadValue(sharedConfig, "UiLightColor", 1),
            DarkColorIndex: ReadValue(sharedConfig, "UiDarkColor", 1),
            LauncherOpacity: ReadValue(localConfig, "UiLauncherTransparent", 600),
            ShowLauncherLogo: ReadValue(localConfig, "UiLauncherLogo", true),
            LockWindowSize: ReadValue(sharedConfig, "UiLockWindowSize", false),
            ShowLaunchingHint: ReadValue(localConfig, "UiShowLaunchingHint", true),
            EnableAdvancedMaterial: ReadValue(localConfig, "UiBlur", false),
            BlurRadius: ReadValue(localConfig, "UiBlurValue", 16),
            BlurSamplingRate: ReadValue(localConfig, "UiBlurSamplingRate", 70),
            BlurTypeIndex: ReadValue(localConfig, "UiBlurType", 0),
            GlobalFontIndex: MapFontToIndex(ReadValue(localConfig, "UiFont", string.Empty)),
            MotdFontIndex: MapFontToIndex(ReadValue(localConfig, "UiMotdFont", string.Empty)),
            AutoPauseVideo: ReadValue(localConfig, "UiAutoPauseVideo", true),
            BackgroundColorful: ReadValue(localConfig, "UiBackgroundColorful", true),
            MusicVolume: ReadValue(localConfig, "UiMusicVolume", 500),
            MusicRandomPlay: ReadValue(localConfig, "UiMusicRandom", true),
            MusicAutoStart: ReadValue(localConfig, "UiMusicAuto", true),
            MusicStartOnGameLaunch: ReadValue(localConfig, "UiMusicStart", false),
            MusicStopOnGameLaunch: ReadValue(localConfig, "UiMusicStop", false),
            MusicEnableSmtc: ReadValue(localConfig, "UiMusicSMTC", true),
            LogoTypeIndex: ReadValue(localConfig, "UiLogoType", 1),
            LogoAlignLeft: ReadValue(localConfig, "UiLogoLeft", false),
            LogoText: ReadValue(localConfig, "UiLogoText", string.Empty),
            HomepageTypeIndex: MapHomepageTypeToDisplayIndex(ReadValue(localConfig, "UiCustomType", 0)),
            HomepageUrl: ReadValue(localConfig, "UiCustomNet", string.Empty),
            HomepagePresetIndex: ReadValue(localConfig, "UiCustomPreset", 12),
            ToggleGroups: BuildUiToggleGroups(localConfig));
    }

    private static IReadOnlyList<FrontendSetupUiToggleGroup> BuildUiToggleGroups(YamlFileProvider localConfig)
    {
        return
        [
            new FrontendSetupUiToggleGroup(
                "主页面",
                [
                    CreateUiToggle(localConfig, "下载", "UiHiddenPageDownload"),
                    CreateUiToggle(localConfig, "设置", "UiHiddenPageSetup"),
                    CreateUiToggle(localConfig, "工具", "UiHiddenPageTools")
                ]),
            new FrontendSetupUiToggleGroup(
                "子页面 设置",
                [
                    CreateUiToggle(localConfig, "启动", "UiHiddenSetupLaunch"),
                    CreateUiToggle(localConfig, "Java", "UiHiddenSetupJava"),
                    CreateUiToggle(localConfig, "管理", "UiHiddenSetupGameManage"),
                    CreateUiToggle(localConfig, "联机", "UiHiddenSetupGameLink"),
                    CreateUiToggle(localConfig, "个性化", "UiHiddenSetupUi"),
                    CreateUiToggle(localConfig, "杂项", "UiHiddenSetupLauncherMisc"),
                    CreateUiToggle(localConfig, "软件更新", "UiHiddenSetupUpdate"),
                    CreateUiToggle(localConfig, "关于", "UiHiddenSetupAbout"),
                    CreateUiToggle(localConfig, "反馈", "UiHiddenSetupFeedback"),
                    CreateUiToggle(localConfig, "查看日志", "UiHiddenSetupLog")
                ]),
            new FrontendSetupUiToggleGroup(
                "子页面 工具",
                [
                    CreateUiToggle(localConfig, "联机", "UiHiddenToolsGameLink"),
                    CreateUiToggle(localConfig, "百宝箱", "UiHiddenToolsTest"),
                    CreateUiToggle(localConfig, "帮助", "UiHiddenToolsHelp")
                ]),
            new FrontendSetupUiToggleGroup(
                "子页面 实例设置",
                [
                    CreateUiToggle(localConfig, "修改", "UiHiddenVersionEdit"),
                    CreateUiToggle(localConfig, "导出", "UiHiddenVersionExport"),
                    CreateUiToggle(localConfig, "存档", "UiHiddenVersionSave"),
                    CreateUiToggle(localConfig, "截图", "UiHiddenVersionScreenshot"),
                    CreateUiToggle(localConfig, "Mod", "UiHiddenVersionMod"),
                    CreateUiToggle(localConfig, "资源包", "UiHiddenVersionResourcePack"),
                    CreateUiToggle(localConfig, "光影包", "UiHiddenVersionShader"),
                    CreateUiToggle(localConfig, "投影原理图", "UiHiddenVersionSchematic"),
                    CreateUiToggle(localConfig, "服务器", "UiHiddenVersionServer")
                ]),
            new FrontendSetupUiToggleGroup(
                "特定功能",
                [
                    CreateUiToggle(localConfig, "实例管理", "UiHiddenFunctionSelect"),
                    CreateUiToggle(localConfig, "Mod 更新", "UiHiddenFunctionModUpdate"),
                    CreateUiToggle(localConfig, "功能隐藏", "UiHiddenFunctionHidden")
                ])
        ];
    }

    private static FrontendSetupUiToggleItem CreateUiToggle(YamlFileProvider localConfig, string title, string key)
    {
        return new FrontendSetupUiToggleItem(title, key, ReadValue(localConfig, key, false));
    }

    private static int MapHomepageTypeToDisplayIndex(int storedValue)
    {
        return storedValue switch
        {
            0 => 0,
            3 => 1,
            1 => 2,
            2 => 3,
            _ => 0
        };
    }

    private static string ReadProtectedValue(FrontendRuntimePaths paths, string key)
    {
        return LauncherFrontendRuntimeStateService.TryReadProtectedString(paths.SharedConfigDirectory, paths.SharedConfigPath, key) ?? string.Empty;
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

    private static int MapFontToIndex(string value)
    {
        return value.Trim() switch
        {
            "" => 0,
            "MiSans" => 0,
            "SourceHanSansCN-Regular" => 1,
            "LXGW WenKai" => 2,
            "JetBrains Mono" => 3,
            _ => 0
        };
    }

    private static string ResolveJavaFolder(string javaPath)
    {
        if (string.IsNullOrWhiteSpace(javaPath))
        {
            return string.Empty;
        }

        var executableDirectory = Path.GetDirectoryName(javaPath);
        if (string.IsNullOrWhiteSpace(executableDirectory))
        {
            return javaPath;
        }

        var directory = new DirectoryInfo(executableDirectory);
        if (directory.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) && directory.Parent is not null)
        {
            directory = directory.Parent;
        }

        return directory.FullName;
    }

    private static string ResolveJavaTitle(string folder, string javaPath)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Path.GetFileNameWithoutExtension(javaPath);
        }

        var directory = new DirectoryInfo(folder);
        if (directory.Name.Equals("Home", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent?.Name.Equals("Contents", StringComparison.OrdinalIgnoreCase) == true &&
            directory.Parent.Parent is not null)
        {
            return directory.Parent.Parent.Name;
        }

        return directory.Name;
    }
}
