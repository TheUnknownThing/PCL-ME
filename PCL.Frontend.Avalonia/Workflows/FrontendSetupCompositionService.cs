using System.Diagnostics;
using System.Security.Cryptography;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Java;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendSetupCompositionService
{
    private const int MaxLogEntries = 8;

    public static FrontendSetupComposition Compose(FrontendRuntimePaths paths, II18nService i18n)
    {
        var sharedConfig = paths.OpenSharedConfigProvider();
        var localConfig = paths.OpenLocalConfigProvider();

        return new FrontendSetupComposition(
            BuildAboutState(i18n),
            BuildLogState(paths, i18n),
            BuildUpdateState(sharedConfig, localConfig),
            BuildLaunchState(sharedConfig, localConfig),
            BuildGameManageState(sharedConfig),
            BuildLauncherMiscState(paths, sharedConfig, localConfig),
            BuildJavaState(sharedConfig, localConfig, i18n),
            BuildUiState(sharedConfig, localConfig, i18n));
    }

    public static FrontendSetupComposition ComposeInitial(
        FrontendRuntimePaths paths,
        II18nService i18n,
        LauncherFrontendSubpageKey activeSubpage)
    {
        var sharedConfig = paths.OpenSharedConfigProvider();
        var localConfig = paths.OpenLocalConfigProvider();

        var composition = CreateInactiveComposition(sharedConfig, localConfig, i18n);
        return ComposeActiveSurface(paths, i18n, composition, activeSubpage);
    }

    public static FrontendSetupComposition ComposeActiveSurface(
        FrontendRuntimePaths paths,
        II18nService i18n,
        FrontendSetupComposition current,
        LauncherFrontendSubpageKey subpage)
    {
        ArgumentNullException.ThrowIfNull(current);

        var sharedConfig = paths.OpenSharedConfigProvider();
        var localConfig = paths.OpenLocalConfigProvider();

        return subpage switch
        {
            LauncherFrontendSubpageKey.SetupAbout => current with
            {
                About = BuildAboutState(i18n)
            },
            LauncherFrontendSubpageKey.SetupLog => current with
            {
                Log = BuildLogState(paths, i18n)
            },
            LauncherFrontendSubpageKey.SetupUpdate => current with
            {
                Update = BuildUpdateState(sharedConfig, localConfig)
            },
            LauncherFrontendSubpageKey.SetupLaunch or LauncherFrontendSubpageKey.SetupLink => current with
            {
                Launch = BuildLaunchState(sharedConfig, localConfig)
            },
            LauncherFrontendSubpageKey.SetupGameManage => current with
            {
                GameManage = BuildGameManageState(sharedConfig)
            },
            LauncherFrontendSubpageKey.SetupLauncherMisc => current with
            {
                LauncherMisc = BuildLauncherMiscState(paths, sharedConfig, localConfig)
            },
            LauncherFrontendSubpageKey.SetupJava => current with
            {
                Java = BuildJavaState(sharedConfig, localConfig, i18n)
            },
            LauncherFrontendSubpageKey.SetupUI => current with
            {
                Ui = BuildUiState(sharedConfig, localConfig, i18n)
            },
            _ => current
        };
    }

    private static FrontendSetupComposition CreateInactiveComposition(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        II18nService i18n)
    {
        return new FrontendSetupComposition(
            new FrontendSetupAboutState(string.Empty),
            new FrontendSetupLogState([]),
            new FrontendSetupUpdateState(0, 1),
            new FrontendSetupLaunchState(
                IsolationIndex: 4,
                WindowTitle: string.Empty,
                CustomInfo: "PCLME",
                VisibilityIndex: 4,
                PriorityIndex: 1,
                WindowTypeIndex: 1,
                WindowWidth: "854",
                WindowHeight: "480",
                UseAutomaticRamAllocation: true,
                CustomRamAllocationGb: MapStoredLaunchRamToGb(15),
                RendererIndex: 0,
                WrapperCommand: string.Empty,
                JvmArguments: string.Empty,
                GameArguments: string.Empty,
                BeforeCommand: string.Empty,
                EnvironmentVariables: string.Empty,
                WaitForBeforeCommand: true,
                ForceX11OnWayland: false,
                DisableJavaLaunchWrapper: true,
                DisableRetroWrapper: false,
                RequireDedicatedGpu: true,
                UseJavaExecutable: false,
                PreferredIpStackIndex: 1),
            BuildGameManageState(sharedConfig),
            new FrontendSetupLauncherMiscState(
                SystemActivityIndex: 0,
                MaxRealTimeLogValue: ReadValue(sharedConfig, "SystemMaxLog", 13),
                IsHardwareAccelerationToggleAvailable: false,
                DisableHardwareAcceleration: false,
                SecureDnsModeIndex: (int)FrontendSecureDnsMode.System,
                SecureDnsProviderIndex: (int)FrontendSecureDnsProvider.Auto,
                HttpProxyTypeIndex: 1,
                HttpProxyAddress: string.Empty,
                HttpProxyUsername: string.Empty,
                HttpProxyPassword: string.Empty,
                DebugAnimationSpeed: ReadValue(sharedConfig, "SystemDebugAnim", 9),
                DebugModeEnabled: ReadValue(sharedConfig, "SystemDebugMode", false)),
            new FrontendSetupJavaState("auto", []),
            BuildUiState(sharedConfig, localConfig, i18n));
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

    private static FrontendSetupAboutState BuildAboutState(II18nService i18n)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? AppContext.BaseDirectory);
        var versionText = string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
            ? typeof(FrontendSetupCompositionService).Assembly.GetName().Version?.ToString() ?? "unknown"
            : versionInfo.ProductVersion;
        return new FrontendSetupAboutState(
            i18n.T(
                "setup.about.version_summary",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = versionText
                }));
    }

    private static FrontendSetupLogState BuildLogState(FrontendRuntimePaths paths, II18nService i18n)
    {
        var logDirectories = new[]
        {
            Path.Combine(paths.LauncherAppDataDirectory, "Log"),
            Path.Combine(paths.DataDirectory, "Log"),
            paths.FrontendArtifactDirectory
        };

        var entries = EnumerateRecentLogFiles(logDirectories)
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
                    i18n.T("setup.log.empty.title"),
                    i18n.T("setup.log.empty.summary"),
                    paths.LauncherAppDataDirectory)
            ];
        }

        return new FrontendSetupLogState(entries);
    }

    private static IReadOnlyList<FileInfo> EnumerateRecentLogFiles(IEnumerable<string> logDirectories)
    {
        List<FileInfo> recentFiles = [];
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0
        };

        foreach (var directory in logDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*", options))
                {
                    if (!IsLogArtifact(path))
                    {
                        continue;
                    }

                    try
                    {
                        AddRecentFile(recentFiles, new FileInfo(path));
                    }
                    catch
                    {
                        // Ignore files that disappear or become inaccessible during log discovery.
                    }
                }
            }
            catch
            {
                // Ignore log roots that disappear or become inaccessible while enumerating.
            }
        }

        return recentFiles;
    }

    private static bool IsLogArtifact(string path)
    {
        return path.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddRecentFile(List<FileInfo> recentFiles, FileInfo file)
    {
        var insertIndex = recentFiles.FindIndex(existing => file.LastWriteTimeUtc > existing.LastWriteTimeUtc);
        if (insertIndex < 0)
        {
            if (recentFiles.Count < MaxLogEntries)
            {
                recentFiles.Add(file);
            }

            return;
        }

        recentFiles.Insert(insertIndex, file);
        if (recentFiles.Count > MaxLogEntries)
        {
            recentFiles.RemoveAt(recentFiles.Count - 1);
        }
    }

    private static FrontendSetupUpdateState BuildUpdateState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        return new FrontendSetupUpdateState(
            UpdateChannelIndex: ReadValue(localConfig, "SystemUpdateChannel", 0),
            UpdateModeIndex: ReadValue(localConfig, "SystemSystemUpdate", 1));
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
            CustomInfo: ReadValue(localConfig, "LaunchArgumentInfo", "PCLME"),
            VisibilityIndex: MapStoredLaunchVisibilityToDisplayIndex(storedVisibility),
            PriorityIndex: ReadValue(sharedConfig, "LaunchArgumentPriority", 1),
            WindowTypeIndex: ReadValue(localConfig, "LaunchArgumentWindowType", 1),
            WindowWidth: ReadValue(localConfig, "LaunchArgumentWindowWidth", 854).ToString(),
            WindowHeight: ReadValue(localConfig, "LaunchArgumentWindowHeight", 480).ToString(),
            UseAutomaticRamAllocation: ReadValue(localConfig, "LaunchRamType", 0) == 0,
            CustomRamAllocationGb: MapStoredLaunchRamToGb(storedCustomRam),
            RendererIndex: ReadValue(localConfig, "LaunchAdvanceRenderer", 0),
            WrapperCommand: ReadValue(localConfig, "LaunchAdvanceWrapper", string.Empty),
            JvmArguments: ReadValue(localConfig, "LaunchAdvanceJvm", "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true"),
            GameArguments: ReadValue(localConfig, "LaunchAdvanceGame", string.Empty),
            BeforeCommand: ReadValue(localConfig, "LaunchAdvanceRun", string.Empty),
            EnvironmentVariables: ReadValue(localConfig, "LaunchAdvanceEnvironmentVariables", string.Empty),
            WaitForBeforeCommand: ReadValue(localConfig, "LaunchAdvanceRunWait", true),
            ForceX11OnWayland: ReadValue(localConfig, "LaunchAdvanceForceX11OnWayland", false),
            DisableJavaLaunchWrapper: ReadValue(localConfig, "LaunchAdvanceDisableJLW", true),
            DisableRetroWrapper: ReadValue(sharedConfig, "LaunchAdvanceDisableRW", false),
            RequireDedicatedGpu: ReadValue(sharedConfig, "LaunchAdvanceGraphicCard", true),
            UseJavaExecutable: ReadValue(sharedConfig, "LaunchAdvanceNoJavaw", false),
            PreferredIpStackIndex: ReadValue(sharedConfig, "LaunchPreferredIpStack", 1));
    }

    private static FrontendSetupGameManageState BuildGameManageState(JsonFileProvider sharedConfig)
    {
        return new FrontendSetupGameManageState(
            DownloadSourceIndex: ReadValue(sharedConfig, "ToolDownloadSource", 1),
            VersionSourceIndex: ReadValue(sharedConfig, "ToolDownloadVersion", 1),
            DownloadThreadLimit: ReadValue(sharedConfig, "ToolDownloadThread", 63),
            DownloadSpeedLimit: ReadValue(sharedConfig, "ToolDownloadSpeed", 42),
            DownloadTimeoutSeconds: ReadValue(sharedConfig, "ToolDownloadTimeout", 8),
            AutoSelectNewInstance: ReadValue(sharedConfig, "ToolDownloadAutoSelectVersion", true),
            CommunityDownloadSourceIndex: ReadValue(sharedConfig, "ToolDownloadMod", 1),
            FileNameFormatIndex: ReadValue(sharedConfig, "ToolDownloadTranslateV2", 1),
            ModLocalNameStyleIndex: ReadValue(sharedConfig, "ToolModLocalNameStyle", 0),
            IgnoreQuiltLoader: ReadValue(sharedConfig, "ToolDownloadIgnoreQuilt", false),
            AutoSwitchGameLanguageToChinese: ReadValue(sharedConfig, "ToolHelpChinese", true),
            DetectClipboardResourceLinks: ReadValue(sharedConfig, "ToolDownloadClipboard", false));
    }

    private static FrontendSetupLauncherMiscState BuildLauncherMiscState(
        FrontendRuntimePaths paths,
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        var secureDnsConfiguration = FrontendHttpProxyService.ReadConfiguredSecureDnsConfiguration(paths);
        var renderingConfiguration = FrontendStartupRenderingService.Resolve(
            paths,
            new FrontendPlatformAdapter().GetDesktopPlatformKind());

        return new FrontendSetupLauncherMiscState(
            SystemActivityIndex: ReadValue(localConfig, "SystemSystemActivity", 0),
            MaxRealTimeLogValue: ReadValue(sharedConfig, "SystemMaxLog", 13),
            IsHardwareAccelerationToggleAvailable: renderingConfiguration.IsHardwareAccelerationToggleAvailable,
            DisableHardwareAcceleration: renderingConfiguration.DisableHardwareAcceleration,
            SecureDnsModeIndex: (int)secureDnsConfiguration.Mode,
            SecureDnsProviderIndex: (int)secureDnsConfiguration.Provider,
            HttpProxyTypeIndex: ReadValue(sharedConfig, "SystemHttpProxyType", 1),
            HttpProxyAddress: FrontendHttpProxyService.ReadConfiguredProxyAddress(paths),
            HttpProxyUsername: FrontendHttpProxyService.ReadConfiguredProxyUsername(paths),
            HttpProxyPassword: FrontendHttpProxyService.ReadConfiguredProxyPassword(paths),
            DebugAnimationSpeed: ReadValue(sharedConfig, "SystemDebugAnim", 9),
            DebugModeEnabled: ReadValue(sharedConfig, "SystemDebugMode", false));
    }

    private static FrontendSetupJavaState BuildJavaState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        II18nService i18n)
    {
        var selectedJava = ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty);
        var rawJavaList = ReadValue(localConfig, "LaunchArgumentJavaUser", "[]");
        var storageItems = FrontendJavaInventoryService.ParseStorageItems(rawJavaList);
        var storageSourceMap = storageItems.ToDictionary(
            item => item.Path,
            item => item.Source,
            StringComparer.OrdinalIgnoreCase);
        var entries = FrontendJavaInventoryService.ParseAvailableRuntimes(rawJavaList)
            .Select(runtime => BuildJavaRuntimeEntry(
                runtime,
                storageSourceMap.TryGetValue(runtime.ExecutablePath, out var source) ? source : null,
                i18n))
            .OrderByDescending(item => item.IsEnabled)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        LogWrapper.Trace(
            "SetupJava",
            $"BuildJavaState: selected='{(string.IsNullOrWhiteSpace(selectedJava) ? "auto" : selectedJava)}', rawLength={rawJavaList.Length}, storedItems={storageItems.Count}, resolvedEntries={entries.Length}.");

        return new FrontendSetupJavaState(
            string.IsNullOrWhiteSpace(selectedJava) ? "auto" : selectedJava,
            entries);
    }

    private static FrontendSetupJavaRuntimeEntry BuildJavaRuntimeEntry(
        FrontendStoredJavaRuntime runtime,
        JavaSource? source,
        II18nService i18n)
    {
        var folder = ResolveJavaFolder(runtime.ExecutablePath);
        var title = string.IsNullOrWhiteSpace(runtime.DisplayName)
            ? ResolveJavaTitle(folder, runtime.ExecutablePath)
            : runtime.DisplayName;
        var tags = new List<string>
        {
            runtime.IsEnabled
                ? i18n.T("setup.java.tags.enabled")
                : i18n.T("setup.java.tags.disabled"),
            source switch
            {
                JavaSource.AutoInstalled => i18n.T("setup.java.tags.auto_installed"),
                JavaSource.ManualAdded => i18n.T("setup.java.tags.manual_added"),
                _ => i18n.T("setup.java.tags.auto_scanned")
            }
        };

        return new FrontendSetupJavaRuntimeEntry(
            runtime.ExecutablePath,
            title,
            folder,
            tags,
            runtime.IsEnabled);
    }

    private static FrontendSetupUiState BuildUiState(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        II18nService i18n)
    {
        return new FrontendSetupUiState(
            DarkModeIndex: ReadValue(sharedConfig, "UiDarkMode", 2),
            LightColorIndex: ReadValue(sharedConfig, "UiLightColor", 0),
            DarkColorIndex: ReadValue(sharedConfig, "UiDarkColor", 0),
            LightCustomColorHex: ReadValue(sharedConfig, "UiLightColorCustom", string.Empty),
            DarkCustomColorHex: ReadValue(sharedConfig, "UiDarkColorCustom", string.Empty),
            LauncherOpacity: ReadValue(localConfig, "UiLauncherTransparent", 600),
            UiScaleFactor: FrontendStartupScalingService.ReadStoredUiScaleFactor(localConfig),
            ShowLauncherLogo: ReadValue(localConfig, "UiLauncherLogo", true),
            LockWindowSize: ReadValue(sharedConfig, "UiLockWindowSize", false),
            ShowLaunchingHint: ReadValue(localConfig, "UiShowLaunchingHint", true),
            GlobalFontIndex: MapFontToIndex(ReadValue(localConfig, "UiFont", string.Empty)),
            MotdFontIndex: MapFontToIndex(ReadValue(localConfig, "UiMotdFont", string.Empty)),
            BackgroundColorful: ReadValue(localConfig, "UiBackgroundColorful", true),
            BackgroundOpacity: ReadValue(localConfig, "UiBackgroundOpacity", 1000),
            BackgroundBlur: ReadValue(localConfig, "UiBackgroundBlur", 0),
            BackgroundSuitIndex: ReadValue(localConfig, "UiBackgroundSuit", 0),
            LogoTypeIndex: ReadValue(localConfig, "UiLogoType", 1),
            LogoAlignLeft: ReadValue(localConfig, "UiLogoLeft", false),
            LogoText: ReadValue(localConfig, "UiLogoText", string.Empty),
            HomepageTypeIndex: MapHomepageTypeToDisplayIndex(ReadValue(localConfig, "UiCustomType", 0)),
            HomepageUrl: ReadValue(localConfig, "UiCustomNet", string.Empty),
            HomepagePresetIndex: ReadValue(localConfig, "UiCustomPreset", 11),
            ToggleGroups: BuildUiToggleGroups(localConfig, i18n));
    }

    private static IReadOnlyList<FrontendSetupUiToggleGroup> BuildUiToggleGroups(YamlFileProvider localConfig, II18nService i18n)
    {
        return
        [
            new FrontendSetupUiToggleGroup(
                i18n.T("setup.ui.hidden_features.groups.main_pages"),
                [
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.download"), "UiHiddenPageDownload"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup"), "UiHiddenPageSetup"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.tools"), "UiHiddenPageTools")
                ]),
            new FrontendSetupUiToggleGroup(
                i18n.T("setup.ui.hidden_features.groups.setup_subpages"),
                [
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_launch"), "UiHiddenSetupLaunch"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_java"), "UiHiddenSetupJava"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_game_manage"), "UiHiddenSetupGameManage"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_ui"), "UiHiddenSetupUi"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_launcher_misc"), "UiHiddenSetupLauncherMisc"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_update"), "UiHiddenSetupUpdate"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_about"), "UiHiddenSetupAbout"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_feedback"), "UiHiddenSetupFeedback"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.setup_log"), "UiHiddenSetupLog")
                ]),
            new FrontendSetupUiToggleGroup(
                i18n.T("setup.ui.hidden_features.groups.tools_subpages"),
                [
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.tools_test"), "UiHiddenToolsTest"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.tools_help"), "UiHiddenToolsHelp")
                ]),
            new FrontendSetupUiToggleGroup(
                i18n.T("setup.ui.hidden_features.groups.instance_subpages"),
                [
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_install"), "UiHiddenVersionEdit"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_export"), "UiHiddenVersionExport"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_save"), "UiHiddenVersionSave"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_screenshot"), "UiHiddenVersionScreenshot"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_mod"), "UiHiddenVersionMod"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_resource_pack"), "UiHiddenVersionResourcePack"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_shader"), "UiHiddenVersionShader"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_schematic"), "UiHiddenVersionSchematic"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.instance_server"), "UiHiddenVersionServer")
                ]),
            new FrontendSetupUiToggleGroup(
                i18n.T("setup.ui.hidden_features.groups.features"),
                [
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.feature_instance_select"), "UiHiddenFunctionSelect"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.feature_mod_update"), "UiHiddenFunctionModUpdate"),
                    CreateUiToggle(localConfig, i18n.T("setup.ui.hidden_features.items.feature_hidden"), "UiHiddenFunctionHidden")
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
        return FrontendAppearanceService.MapFontConfigValueToIndex(value);
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
