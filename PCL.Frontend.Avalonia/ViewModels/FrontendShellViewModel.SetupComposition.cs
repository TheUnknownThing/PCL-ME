using System.ComponentModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] LaunchLocalResetKeys =
    [
        "LaunchArgumentTitle",
        "LaunchArgumentInfo",
        "LaunchArgumentIndieV2",
        "LaunchArgumentWindowType",
        "LaunchArgumentWindowWidth",
        "LaunchArgumentWindowHeight",
        "LaunchRamType",
        "LaunchRamCustom",
        "LaunchAdvanceRenderer",
        "LaunchAdvanceJvm",
        "LaunchAdvanceGame",
        "LaunchAdvanceRun",
        "LaunchAdvanceEnvironmentVariables",
        "LaunchAdvanceRunWait",
        "LaunchAdvanceForceX11OnWayland",
        "LaunchAdvanceDisableJLW"
    ];

    private static readonly string[] LaunchSharedResetKeys =
    [
        "LaunchArgumentVisible",
        "LaunchArgumentPriority",
        "LaunchArgumentRam",
        "LaunchAdvanceDisableRW",
        "LaunchAdvanceGraphicCard",
        "LaunchAdvanceNoJavaw",
        "LaunchPreferredIpStack",
        "LaunchArgumentJavaSelect"
    ];

    private static readonly string[] GameManageResetKeys =
    [
        "ToolDownloadThread",
        "ToolDownloadSpeed",
        "ToolDownloadTimeout",
        "ToolDownloadSource",
        "ToolDownloadVersion",
        "ToolDownloadAutoSelectVersion",
        "ToolFixAuthlib",
        "ToolDownloadTranslateV2",
        "ToolDownloadMod",
        "ToolModLocalNameStyle",
        "ToolDownloadIgnoreQuilt",
        "ToolDownloadClipboard",
        "ToolHelpChinese",
        "ToolUpdateRelease",
        "ToolUpdateSnapshot"
    ];

    private static readonly string[] LauncherMiscLocalResetKeys =
    [
        "SystemSystemActivity"
    ];

    private static readonly string[] LauncherMiscSharedResetKeys =
    [
        "UiAniFPS",
        "SystemMaxLog",
        "SystemDisableHardwareAcceleration",
        "SystemNetEnableDoH",
        "SystemHttpProxyType",
        "SystemHttpProxyCustomUsername",
        "SystemHttpProxyCustomPassword",
        "SystemDebugAnim",
        "SystemDebugSkipCopy",
        "SystemDebugMode",
        "SystemDebugDelay"
    ];

    private static readonly string[] LauncherMiscProtectedResetKeys =
    [
        "SystemHttpProxy"
    ];

    private static readonly string[] UpdateLocalResetKeys =
    [
        "SystemUpdateChannel",
        "SystemSystemUpdate"
    ];

    private static readonly string[] UiLocalResetKeys =
    [
        "UiLauncherLogo",
        "UiShowLaunchingHint",
        "UiLogoType",
        "UiLogoText",
        "UiLogoLeft",
        "UiFont",
        "UiMotdFont",
        "UiLauncherTransparent",
        "UiBackgroundColorful",
        "UiBackgroundOpacity",
        "UiBackgroundBlur",
        "UiBackgroundSuit",
        "UiBlur",
        "UiBlurValue",
        "UiBlurSamplingRate",
        "UiBlurType",
        "UiMusicVolume",
        "UiMusicStop",
        "UiMusicStart",
        "UiMusicAuto",
        "UiMusicRandom",
        "UiMusicSMTC",
        "UiCustomType",
        "UiCustomPreset",
        "UiCustomNet",
        "UiHiddenPageDownload",
        "UiHiddenPageSetup",
        "UiHiddenPageTools",
        "UiHiddenSetupLaunch",
        "UiHiddenSetupUi",
        "UiHiddenSetupLauncherMisc",
        "UiHiddenSetupGameManage",
        "UiHiddenSetupJava",
        "UiHiddenSetupUpdate",
        "UiHiddenSetupAbout",
        "UiHiddenSetupFeedback",
        "UiHiddenSetupLog",
        "UiHiddenToolsHelp",
        "UiHiddenToolsTest",
        "UiHiddenVersionEdit",
        "UiHiddenVersionExport",
        "UiHiddenVersionSave",
        "UiHiddenVersionScreenshot",
        "UiHiddenVersionMod",
        "UiHiddenVersionResourcePack",
        "UiHiddenVersionShader",
        "UiHiddenVersionSchematic",
        "UiHiddenVersionServer",
        "UiHiddenFunctionSelect",
        "UiHiddenFunctionModUpdate",
        "UiHiddenFunctionHidden"
    ];

    private static readonly string[] UiSharedResetKeys =
    [
        "UiLockWindowSize",
        "UiDarkMode",
        "UiDarkColor",
        "UiLightColor",
        "UiLightColorCustom",
        "UiDarkColorCustom"
    ];

    private void ApplySetupComposition(FrontendSetupComposition composition, bool initializeAllSurfaces = true)
    {
        _setupComposition = composition;
        _suppressSetupPersistence = true;
        try
        {
            if (initializeAllSurfaces)
            {
                InitializeAboutEntries();
                InitializeLogEntries();
                InitializeUpdateSurface();
                InitializeLaunchSettingsSurface();
                InitializeGameManageSurface();
                InitializeLauncherMiscSurface();
                InitializeJavaSurface();
                InitializeUiSurface();
                RaiseSetupSurfaceProperties();
            }
            else
            {
                InitializeActiveSetupSurface();
                RaiseActiveSetupSurfaceProperties();
            }
        }
        finally
        {
            _suppressSetupPersistence = false;
        }

        ApplyCurrentAppearanceSettings();
    }

    private void ReloadSetupComposition(bool initializeAllSurfaces = true)
    {
        ApplySetupComposition(
            FrontendSetupCompositionService.Compose(_shellActionService.RuntimePaths),
            initializeAllSurfaces);
    }

    private void InitializeActiveSetupSurface()
    {
        switch (_currentRoute.Subpage)
        {
            case LauncherFrontendSubpageKey.SetupAbout:
                InitializeAboutEntries();
                break;
            case LauncherFrontendSubpageKey.SetupFeedback:
                InitializeFeedbackSections();
                break;
            case LauncherFrontendSubpageKey.SetupLog:
                InitializeLogEntries();
                break;
            case LauncherFrontendSubpageKey.SetupUpdate:
                InitializeUpdateSurface();
                break;
            case LauncherFrontendSubpageKey.SetupGameManage:
                InitializeGameManageSurface();
                break;
            case LauncherFrontendSubpageKey.SetupLauncherMisc:
                InitializeLauncherMiscSurface();
                break;
            case LauncherFrontendSubpageKey.SetupJava:
                InitializeJavaSurface();
                break;
            case LauncherFrontendSubpageKey.SetupUI:
                InitializeUiSurface();
                break;
            case LauncherFrontendSubpageKey.SetupLaunch:
            default:
                InitializeLaunchSettingsSurface();
                break;
        }
    }

    private void RaiseActiveSetupSurfaceProperties()
    {
        switch (_currentRoute.Subpage)
        {
            case LauncherFrontendSubpageKey.SetupAbout:
                RaisePropertyChanged(nameof(HasAboutProjectEntries));
                RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
                break;
            case LauncherFrontendSubpageKey.SetupFeedback:
                RaisePropertyChanged(nameof(HasFeedbackSections));
                break;
            case LauncherFrontendSubpageKey.SetupLog:
                break;
            case LauncherFrontendSubpageKey.SetupUpdate:
                RaisePropertyChanged(nameof(SelectedUpdateChannelIndex));
                RaisePropertyChanged(nameof(SelectedUpdateModeIndex));
                RaiseUpdateSurfaceProperties();
                break;
            case LauncherFrontendSubpageKey.SetupGameManage:
                RaisePropertyChanged(nameof(SelectedDownloadSourceIndex));
                RaisePropertyChanged(nameof(SelectedVersionSourceIndex));
                RaisePropertyChanged(nameof(DownloadThreadLimit));
                RaisePropertyChanged(nameof(DownloadThreadLimitLabel));
                RaisePropertyChanged(nameof(DownloadSpeedLimit));
                RaisePropertyChanged(nameof(DownloadSpeedLimitLabel));
                RaisePropertyChanged(nameof(DownloadTimeoutSeconds));
                RaisePropertyChanged(nameof(DownloadTimeoutLabel));
                RaisePropertyChanged(nameof(AutoSelectNewInstance));
                RaisePropertyChanged(nameof(UpgradePartialAuthlib));
                RaisePropertyChanged(nameof(SelectedCommunityDownloadSourceIndex));
                RaisePropertyChanged(nameof(SelectedFileNameFormatIndex));
                RaisePropertyChanged(nameof(SelectedModLocalNameStyleIndex));
                RaisePropertyChanged(nameof(IgnoreQuiltLoader));
                RaisePropertyChanged(nameof(NotifyReleaseUpdates));
                RaisePropertyChanged(nameof(NotifySnapshotUpdates));
                RaisePropertyChanged(nameof(AutoSwitchGameLanguageToChinese));
                RaisePropertyChanged(nameof(DetectClipboardResourceLinks));
                break;
            case LauncherFrontendSubpageKey.SetupLauncherMisc:
                RaisePropertyChanged(nameof(SelectedSystemActivityIndex));
                RaisePropertyChanged(nameof(AnimationFpsLimit));
                RaisePropertyChanged(nameof(AnimationFpsLabel));
                RaisePropertyChanged(nameof(MaxRealTimeLogValue));
                RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
                RaisePropertyChanged(nameof(DisableHardwareAcceleration));
                RaisePropertyChanged(nameof(EnableDoH));
                RaisePropertyChanged(nameof(SelectedHttpProxyTypeIndex));
                RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
                RaisePropertyChanged(nameof(IsNoHttpProxySelected));
                RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
                RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
                RaisePropertyChanged(nameof(HttpProxyAddress));
                RaisePropertyChanged(nameof(HttpProxyUsername));
                RaisePropertyChanged(nameof(HttpProxyPassword));
                RaisePropertyChanged(nameof(DebugAnimationSpeed));
                RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
                RaisePropertyChanged(nameof(SkipCopyDuringDownload));
                RaisePropertyChanged(nameof(DebugModeEnabled));
                RaisePropertyChanged(nameof(DebugDelayEnabled));
                break;
            case LauncherFrontendSubpageKey.SetupJava:
                RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
                RaisePropertyChanged(nameof(IsAutoJavaSelected));
                break;
            case LauncherFrontendSubpageKey.SetupUI:
                RaisePropertyChanged(nameof(SelectedDarkModeIndex));
                RaisePropertyChanged(nameof(SelectedLightColorIndex));
                RaisePropertyChanged(nameof(SelectedDarkColorIndex));
                RaisePropertyChanged(nameof(IsThemeColorSwitchSupported));
                RaisePropertyChanged(nameof(IsThemeColorSwitchUnsupportedNoticeVisible));
                RaisePropertyChanged(nameof(IsLightCustomThemeColorEditorVisible));
                RaisePropertyChanged(nameof(IsDarkCustomThemeColorEditorVisible));
                RaisePropertyChanged(nameof(IsAnyCustomThemeColorEditorVisible));
                RaisePropertyChanged(nameof(CustomThemeColorInputHint));
                RaisePropertyChanged(nameof(CustomLightThemeColorHex));
                RaisePropertyChanged(nameof(CustomDarkThemeColorHex));
                RaisePropertyChanged(nameof(CustomLightThemePreviewBrush));
                RaisePropertyChanged(nameof(CustomDarkThemePreviewBrush));
                RaisePropertyChanged(nameof(IsLightCustomThemeColorInvalid));
                RaisePropertyChanged(nameof(IsDarkCustomThemeColorInvalid));
                RaisePropertyChanged(nameof(LauncherOpacity));
                RaisePropertyChanged(nameof(LauncherOpacityLabel));
                RaisePropertyChanged(nameof(ShowLauncherLogoSetting));
                RaisePropertyChanged(nameof(LockWindowSizeSetting));
                RaisePropertyChanged(nameof(ShowLaunchingHintSetting));
                RaisePropertyChanged(nameof(SelectedGlobalFontIndex));
                RaisePropertyChanged(nameof(SelectedMotdFontIndex));
                RaisePropertyChanged(nameof(BackgroundColorful));
                RaisePropertyChanged(nameof(SelectedBackgroundSuitIndex));
                RaisePropertyChanged(nameof(ShowBackgroundAdvancedSettings));
                RaisePropertyChanged(nameof(BackgroundOpacity));
                RaisePropertyChanged(nameof(BackgroundOpacityLabel));
                RaisePropertyChanged(nameof(BackgroundBlur));
                RaisePropertyChanged(nameof(BackgroundBlurLabel));
                RaisePropertyChanged(nameof(BackgroundCardHeader));
                RaisePropertyChanged(nameof(ShowBackgroundClearAction));
                RaisePropertyChanged(nameof(MusicVolume));
                RaisePropertyChanged(nameof(MusicVolumeLabel));
                RaisePropertyChanged(nameof(MusicRandomPlay));
                RaisePropertyChanged(nameof(MusicAutoStart));
                RaisePropertyChanged(nameof(MusicStartOnGameLaunch));
                RaisePropertyChanged(nameof(MusicStopOnGameLaunch));
                RaisePropertyChanged(nameof(MusicEnableSmtc));
                RaisePropertyChanged(nameof(SelectedLogoTypeIndex));
                RaisePropertyChanged(nameof(IsLogoTypeNoneSelected));
                RaisePropertyChanged(nameof(IsLogoTypeDefaultSelected));
                RaisePropertyChanged(nameof(IsLogoTypeTextSelected));
                RaisePropertyChanged(nameof(IsLogoTypeImageSelected));
                RaisePropertyChanged(nameof(IsLogoLeftVisible));
                RaisePropertyChanged(nameof(LogoAlignLeft));
                RaisePropertyChanged(nameof(IsLogoTextVisible));
                RaisePropertyChanged(nameof(LogoTextValue));
                RaisePropertyChanged(nameof(IsLogoImageActionsVisible));
                RaisePropertyChanged(nameof(SelectedHomepageTypeIndex));
                RaisePropertyChanged(nameof(IsHomepageBlankSelected));
                RaisePropertyChanged(nameof(IsHomepagePresetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalSelected));
                RaisePropertyChanged(nameof(IsHomepageNetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalActionsVisible));
                RaisePropertyChanged(nameof(IsHomepageNetVisible));
                RaisePropertyChanged(nameof(HomepageUrl));
                RaisePropertyChanged(nameof(IsHomepagePresetVisible));
                RaisePropertyChanged(nameof(SelectedHomepagePresetIndex));
                break;
            case LauncherFrontendSubpageKey.SetupLaunch:
            default:
                RaisePropertyChanged(nameof(SelectedLaunchIsolationIndex));
                RaisePropertyChanged(nameof(LaunchWindowTitleSetting));
                RaisePropertyChanged(nameof(LaunchCustomInfoSetting));
                RaisePropertyChanged(nameof(SelectedLaunchVisibilityIndex));
                RaisePropertyChanged(nameof(SelectedLaunchPriorityIndex));
                RaisePropertyChanged(nameof(SelectedLaunchWindowTypeIndex));
                RaisePropertyChanged(nameof(IsCustomLaunchWindowSizeVisible));
                RaisePropertyChanged(nameof(LaunchWindowWidth));
                RaisePropertyChanged(nameof(LaunchWindowHeight));
                RaisePropertyChanged(nameof(UseAutomaticRamAllocation));
                RaisePropertyChanged(nameof(UseCustomRamAllocation));
                RaisePropertyChanged(nameof(IsCustomRamAllocationEnabled));
                RaisePropertyChanged(nameof(CustomRamAllocation));
                RaisePropertyChanged(nameof(CustomRamAllocationLabel));
                RaisePropertyChanged(nameof(UsedRamLabel));
                RaisePropertyChanged(nameof(TotalRamLabel));
                RaisePropertyChanged(nameof(AllocatedRamLabel));
                RaisePropertyChanged(nameof(UsedRamBarWidth));
                RaisePropertyChanged(nameof(AllocatedRamBarWidth));
                RaisePropertyChanged(nameof(FreeRamBarWidth));
                RaisePropertyChanged(nameof(ShowRamAllocationWarning));
                RaisePropertyChanged(nameof(OptimizeMemoryBeforeLaunch));
                RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
                RaisePropertyChanged(nameof(LaunchJvmArguments));
                RaisePropertyChanged(nameof(LaunchGameArguments));
                RaisePropertyChanged(nameof(LaunchBeforeCommand));
                RaisePropertyChanged(nameof(LaunchEnvironmentVariables));
                RaisePropertyChanged(nameof(WaitForLaunchBeforeCommand));
                RaisePropertyChanged(nameof(ForceX11OnWaylandForLaunch));
                RaisePropertyChanged(nameof(DisableJavaLaunchWrapper));
                RaisePropertyChanged(nameof(DisableRetroWrapper));
                RaisePropertyChanged(nameof(RequireDedicatedGpu));
                RaisePropertyChanged(nameof(UseJavaExecutable));
                RaisePropertyChanged(nameof(SelectedLaunchPreferredIpStackIndex));
                break;
        }
    }

    private void PersistSetupSetting(string? propertyName)
    {
        if (_suppressSetupPersistence || string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(SelectedUpdateChannelIndex):
                _shellActionService.PersistLocalValue("SystemUpdateChannel", SelectedUpdateChannelIndex);
                break;
            case nameof(SelectedUpdateModeIndex):
                _shellActionService.PersistLocalValue("SystemSystemUpdate", SelectedUpdateModeIndex);
                break;
            case nameof(SelectedLaunchIsolationIndex):
                _shellActionService.PersistLocalValue("LaunchArgumentIndieV2", SelectedLaunchIsolationIndex);
                break;
            case nameof(LaunchWindowTitleSetting):
                _shellActionService.PersistLocalValue("LaunchArgumentTitle", LaunchWindowTitleSetting);
                break;
            case nameof(LaunchCustomInfoSetting):
                _shellActionService.PersistLocalValue("LaunchArgumentInfo", LaunchCustomInfoSetting);
                break;
            case nameof(SelectedLaunchVisibilityIndex):
                _shellActionService.PersistSharedValue("LaunchArgumentVisible", FrontendSetupCompositionService.MapDisplayLaunchVisibilityToStoredIndex(SelectedLaunchVisibilityIndex));
                break;
            case nameof(SelectedLaunchPriorityIndex):
                _shellActionService.PersistSharedValue("LaunchArgumentPriority", SelectedLaunchPriorityIndex);
                break;
            case nameof(SelectedLaunchWindowTypeIndex):
                _shellActionService.PersistLocalValue("LaunchArgumentWindowType", SelectedLaunchWindowTypeIndex);
                break;
            case nameof(LaunchWindowWidth):
                if (int.TryParse(LaunchWindowWidth, out var width))
                {
                    _shellActionService.PersistLocalValue("LaunchArgumentWindowWidth", width);
                }
                break;
            case nameof(LaunchWindowHeight):
                if (int.TryParse(LaunchWindowHeight, out var height))
                {
                    _shellActionService.PersistLocalValue("LaunchArgumentWindowHeight", height);
                }
                break;
            case nameof(UseAutomaticRamAllocation):
            case nameof(UseCustomRamAllocation):
                _shellActionService.PersistLocalValue("LaunchRamType", UseAutomaticRamAllocation ? 0 : 1);
                break;
            case nameof(CustomRamAllocation):
                _shellActionService.PersistLocalValue("LaunchRamCustom", FrontendSetupCompositionService.MapLaunchRamGbToStoredValue(CustomRamAllocation));
                break;
            case nameof(OptimizeMemoryBeforeLaunch):
                _shellActionService.PersistSharedValue("LaunchArgumentRam", OptimizeMemoryBeforeLaunch);
                break;
            case nameof(SelectedLaunchRendererIndex):
                _shellActionService.PersistLocalValue("LaunchAdvanceRenderer", SelectedLaunchRendererIndex);
                break;
            case nameof(LaunchJvmArguments):
                _shellActionService.PersistLocalValue("LaunchAdvanceJvm", LaunchJvmArguments);
                break;
            case nameof(LaunchGameArguments):
                _shellActionService.PersistLocalValue("LaunchAdvanceGame", LaunchGameArguments);
                break;
            case nameof(LaunchBeforeCommand):
                _shellActionService.PersistLocalValue("LaunchAdvanceRun", LaunchBeforeCommand);
                break;
            case nameof(LaunchEnvironmentVariables):
                _shellActionService.PersistLocalValue("LaunchAdvanceEnvironmentVariables", LaunchEnvironmentVariables);
                break;
            case nameof(WaitForLaunchBeforeCommand):
                _shellActionService.PersistLocalValue("LaunchAdvanceRunWait", WaitForLaunchBeforeCommand);
                break;
            case nameof(ForceX11OnWaylandForLaunch):
                _shellActionService.PersistLocalValue("LaunchAdvanceForceX11OnWayland", ForceX11OnWaylandForLaunch);
                break;
            case nameof(DisableJavaLaunchWrapper):
                _shellActionService.PersistLocalValue("LaunchAdvanceDisableJLW", DisableJavaLaunchWrapper);
                break;
            case nameof(DisableRetroWrapper):
                _shellActionService.PersistSharedValue("LaunchAdvanceDisableRW", DisableRetroWrapper);
                break;
            case nameof(RequireDedicatedGpu):
                _shellActionService.PersistSharedValue("LaunchAdvanceGraphicCard", RequireDedicatedGpu);
                break;
            case nameof(UseJavaExecutable):
                _shellActionService.PersistSharedValue("LaunchAdvanceNoJavaw", UseJavaExecutable);
                break;
            case nameof(SelectedLaunchPreferredIpStackIndex):
                _shellActionService.PersistSharedValue("LaunchPreferredIpStack", SelectedLaunchPreferredIpStackIndex);
                break;
            case nameof(SelectedDownloadSourceIndex):
                _shellActionService.PersistSharedValue("ToolDownloadSource", SelectedDownloadSourceIndex);
                break;
            case nameof(SelectedVersionSourceIndex):
                _shellActionService.PersistSharedValue("ToolDownloadVersion", SelectedVersionSourceIndex);
                break;
            case nameof(DownloadThreadLimit):
                _shellActionService.PersistSharedValue("ToolDownloadThread", (int)Math.Round(DownloadThreadLimit));
                break;
            case nameof(DownloadSpeedLimit):
                _shellActionService.PersistSharedValue("ToolDownloadSpeed", (int)Math.Round(DownloadSpeedLimit));
                break;
            case nameof(DownloadTimeoutSeconds):
                _shellActionService.PersistSharedValue("ToolDownloadTimeout", (int)Math.Round(DownloadTimeoutSeconds));
                break;
            case nameof(AutoSelectNewInstance):
                _shellActionService.PersistSharedValue("ToolDownloadAutoSelectVersion", AutoSelectNewInstance);
                break;
            case nameof(UpgradePartialAuthlib):
                _shellActionService.PersistSharedValue("ToolFixAuthlib", UpgradePartialAuthlib);
                break;
            case nameof(SelectedCommunityDownloadSourceIndex):
                _shellActionService.PersistSharedValue("ToolDownloadMod", SelectedCommunityDownloadSourceIndex);
                break;
            case nameof(SelectedFileNameFormatIndex):
                _shellActionService.PersistSharedValue("ToolDownloadTranslateV2", SelectedFileNameFormatIndex);
                break;
            case nameof(SelectedModLocalNameStyleIndex):
                _shellActionService.PersistSharedValue("ToolModLocalNameStyle", SelectedModLocalNameStyleIndex);
                break;
            case nameof(IgnoreQuiltLoader):
                _shellActionService.PersistSharedValue("ToolDownloadIgnoreQuilt", IgnoreQuiltLoader);
                break;
            case nameof(NotifyReleaseUpdates):
                _shellActionService.PersistSharedValue("ToolUpdateRelease", NotifyReleaseUpdates);
                break;
            case nameof(NotifySnapshotUpdates):
                _shellActionService.PersistSharedValue("ToolUpdateSnapshot", NotifySnapshotUpdates);
                break;
            case nameof(AutoSwitchGameLanguageToChinese):
                _shellActionService.PersistSharedValue("ToolHelpChinese", AutoSwitchGameLanguageToChinese);
                break;
            case nameof(DetectClipboardResourceLinks):
                _shellActionService.PersistSharedValue("ToolDownloadClipboard", DetectClipboardResourceLinks);
                break;
            case nameof(SelectedSystemActivityIndex):
                _shellActionService.PersistLocalValue("SystemSystemActivity", SelectedSystemActivityIndex);
                RefreshLaunchAnnouncements();
                break;
            case nameof(AnimationFpsLimit):
                _shellActionService.PersistSharedValue("UiAniFPS", (int)Math.Round(AnimationFpsLimit));
                FrontendShellActionService.ApplyAnimationPreferences((int)Math.Round(AnimationFpsLimit), DebugAnimationSpeed);
                break;
            case nameof(MaxRealTimeLogValue):
                _shellActionService.PersistSharedValue("SystemMaxLog", (int)Math.Round(MaxRealTimeLogValue));
                break;
            case nameof(DisableHardwareAcceleration):
                _shellActionService.PersistSharedValue("SystemDisableHardwareAcceleration", DisableHardwareAcceleration);
                break;
            case nameof(EnableDoH):
                _shellActionService.PersistSharedValue("SystemNetEnableDoH", EnableDoH);
                break;
            case nameof(SelectedHttpProxyTypeIndex):
                _shellActionService.PersistSharedValue("SystemHttpProxyType", SelectedHttpProxyTypeIndex);
                break;
            case nameof(DebugAnimationSpeed):
                _shellActionService.PersistSharedValue("SystemDebugAnim", (int)Math.Round(DebugAnimationSpeed));
                FrontendShellActionService.ApplyAnimationPreferences((int)Math.Round(AnimationFpsLimit), DebugAnimationSpeed);
                break;
            case nameof(SkipCopyDuringDownload):
                _shellActionService.PersistSharedValue("SystemDebugSkipCopy", SkipCopyDuringDownload);
                break;
            case nameof(DebugModeEnabled):
                _shellActionService.PersistSharedValue("SystemDebugMode", DebugModeEnabled);
                break;
            case nameof(DebugDelayEnabled):
                _shellActionService.PersistSharedValue("SystemDebugDelay", DebugDelayEnabled);
                break;
            case nameof(SelectedDarkModeIndex):
                _shellActionService.PersistSharedValue("UiDarkMode", SelectedDarkModeIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedLightColorIndex):
                _shellActionService.PersistSharedValue("UiLightColor", SelectedLightColorIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedDarkColorIndex):
                _shellActionService.PersistSharedValue("UiDarkColor", SelectedDarkColorIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(CustomLightThemeColorHex):
                PersistCustomThemeColor("UiLightColorCustom", CustomLightThemeColorHex);
                break;
            case nameof(CustomDarkThemeColorHex):
                PersistCustomThemeColor("UiDarkColorCustom", CustomDarkThemeColorHex);
                break;
            case nameof(LauncherOpacity):
                _shellActionService.PersistLocalValue("UiLauncherTransparent", (int)Math.Round(LauncherOpacity));
                break;
            case nameof(ShowLauncherLogoSetting):
                _shellActionService.PersistLocalValue("UiLauncherLogo", ShowLauncherLogoSetting);
                break;
            case nameof(LockWindowSizeSetting):
                _shellActionService.PersistSharedValue("UiLockWindowSize", LockWindowSizeSetting);
                break;
            case nameof(ShowLaunchingHintSetting):
                _shellActionService.PersistLocalValue("UiShowLaunchingHint", ShowLaunchingHintSetting);
                break;
            case nameof(SelectedGlobalFontIndex):
                _shellActionService.PersistLocalValue("UiFont", MapFontIndexToConfigValue(SelectedGlobalFontIndex));
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedMotdFontIndex):
                _shellActionService.PersistLocalValue("UiMotdFont", MapFontIndexToConfigValue(SelectedMotdFontIndex));
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(BackgroundColorful):
                _shellActionService.PersistLocalValue("UiBackgroundColorful", BackgroundColorful);
                break;
            case nameof(SelectedBackgroundSuitIndex):
                _shellActionService.PersistLocalValue("UiBackgroundSuit", SelectedBackgroundSuitIndex);
                break;
            case nameof(BackgroundOpacity):
                _shellActionService.PersistLocalValue("UiBackgroundOpacity", (int)Math.Round(BackgroundOpacity));
                break;
            case nameof(BackgroundBlur):
                _shellActionService.PersistLocalValue("UiBackgroundBlur", (int)Math.Round(BackgroundBlur));
                break;
            case nameof(MusicVolume):
                _shellActionService.PersistLocalValue("UiMusicVolume", (int)Math.Round(MusicVolume));
                break;
            case nameof(MusicRandomPlay):
                _shellActionService.PersistLocalValue("UiMusicRandom", MusicRandomPlay);
                break;
            case nameof(MusicAutoStart):
                _shellActionService.PersistLocalValue("UiMusicAuto", MusicAutoStart);
                break;
            case nameof(MusicStartOnGameLaunch):
                _shellActionService.PersistLocalValue("UiMusicStart", MusicStartOnGameLaunch);
                break;
            case nameof(MusicStopOnGameLaunch):
                _shellActionService.PersistLocalValue("UiMusicStop", MusicStopOnGameLaunch);
                break;
            case nameof(MusicEnableSmtc):
                _shellActionService.PersistLocalValue("UiMusicSMTC", MusicEnableSmtc);
                break;
            case nameof(SelectedLogoTypeIndex):
                _shellActionService.PersistLocalValue("UiLogoType", SelectedLogoTypeIndex);
                break;
            case nameof(LogoAlignLeft):
                _shellActionService.PersistLocalValue("UiLogoLeft", LogoAlignLeft);
                break;
            case nameof(LogoTextValue):
                _shellActionService.PersistLocalValue("UiLogoText", LogoTextValue);
                break;
            case nameof(SelectedHomepageTypeIndex):
                _shellActionService.PersistLocalValue("UiCustomType", MapHomepageDisplayIndexToStoredValue(SelectedHomepageTypeIndex));
                RefreshLaunchHomepage(forceRefresh: false);
                break;
            case nameof(HomepageUrl):
                _shellActionService.PersistLocalValue("UiCustomNet", HomepageUrl);
                RefreshLaunchHomepage(forceRefresh: false);
                break;
            case nameof(SelectedHomepagePresetIndex):
                _shellActionService.PersistLocalValue("UiCustomPreset", SelectedHomepagePresetIndex);
                RefreshLaunchHomepage(forceRefresh: false);
                break;
        }
    }

    private void PersistUiToggle(string key, bool value)
    {
        if (_suppressSetupPersistence)
        {
            return;
        }

        _shellActionService.PersistLocalValue(key, value);
        ReloadSetupComposition(initializeAllSurfaces: false);
        RefreshShell(value ? $"已隐藏 {key}。" : $"已恢复 {key}。");
        RaiseUiVisibilityProperties();
    }

    private static string MapFontIndexToConfigValue(int index)
    {
        return FrontendAppearanceService.MapFontIndexToConfigValue(index, FrontendAppearanceService.GetFontOptions());
    }

    private static int MapHomepageDisplayIndexToStoredValue(int displayIndex)
    {
        return Math.Clamp(displayIndex, 0, 3) switch
        {
            0 => 0,
            1 => 3,
            2 => 1,
            _ => 2
        };
    }

    private void ApplyCurrentAppearanceSettings()
    {
        _shellActionService.ApplyAppearance(
            SelectedDarkModeIndex,
            SelectedLightColorIndex,
            SelectedDarkColorIndex,
            CustomLightThemeColorHex,
            CustomDarkThemeColorHex,
            MapFontIndexToConfigValue(SelectedGlobalFontIndex),
            MapFontIndexToConfigValue(SelectedMotdFontIndex));
    }

    private void PersistCustomThemeColor(string key, string rawValue)
    {
        if (!FrontendAppearanceService.TryParseCustomThemeColor(rawValue, out var color))
        {
            return;
        }

        _shellActionService.PersistSharedValue(key, FrontendAppearanceService.FormatCustomThemeColor(color));
        ApplyCurrentAppearanceSettings();
    }

    private void RaiseSetupSurfaceProperties()
    {
        RaisePropertyChanged(nameof(HasAboutProjectEntries));
        RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
        RaisePropertyChanged(nameof(HasFeedbackSections));
        RaisePropertyChanged(nameof(SelectedUpdateChannelIndex));
        RaisePropertyChanged(nameof(SelectedUpdateModeIndex));
        RaiseUpdateSurfaceProperties();
        RaisePropertyChanged(nameof(SelectedLaunchIsolationIndex));
        RaisePropertyChanged(nameof(LaunchWindowTitleSetting));
        RaisePropertyChanged(nameof(LaunchCustomInfoSetting));
        RaisePropertyChanged(nameof(SelectedLaunchVisibilityIndex));
        RaisePropertyChanged(nameof(SelectedLaunchPriorityIndex));
        RaisePropertyChanged(nameof(SelectedLaunchWindowTypeIndex));
        RaisePropertyChanged(nameof(IsCustomLaunchWindowSizeVisible));
        RaisePropertyChanged(nameof(LaunchWindowWidth));
        RaisePropertyChanged(nameof(LaunchWindowHeight));
        RaisePropertyChanged(nameof(UseAutomaticRamAllocation));
        RaisePropertyChanged(nameof(UseCustomRamAllocation));
        RaisePropertyChanged(nameof(IsCustomRamAllocationEnabled));
        RaisePropertyChanged(nameof(CustomRamAllocation));
        RaisePropertyChanged(nameof(CustomRamAllocationLabel));
        RaisePropertyChanged(nameof(UsedRamLabel));
        RaisePropertyChanged(nameof(TotalRamLabel));
        RaisePropertyChanged(nameof(AllocatedRamLabel));
        RaisePropertyChanged(nameof(UsedRamBarWidth));
        RaisePropertyChanged(nameof(AllocatedRamBarWidth));
        RaisePropertyChanged(nameof(FreeRamBarWidth));
        RaisePropertyChanged(nameof(ShowRamAllocationWarning));
        RaisePropertyChanged(nameof(OptimizeMemoryBeforeLaunch));
        RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
        RaisePropertyChanged(nameof(LaunchJvmArguments));
        RaisePropertyChanged(nameof(LaunchGameArguments));
        RaisePropertyChanged(nameof(LaunchBeforeCommand));
        RaisePropertyChanged(nameof(LaunchEnvironmentVariables));
        RaisePropertyChanged(nameof(WaitForLaunchBeforeCommand));
        RaisePropertyChanged(nameof(ForceX11OnWaylandForLaunch));
        RaisePropertyChanged(nameof(DisableJavaLaunchWrapper));
        RaisePropertyChanged(nameof(DisableRetroWrapper));
        RaisePropertyChanged(nameof(RequireDedicatedGpu));
        RaisePropertyChanged(nameof(UseJavaExecutable));
        RaisePropertyChanged(nameof(SelectedLaunchPreferredIpStackIndex));
        RaisePropertyChanged(nameof(SelectedDownloadSourceIndex));
        RaisePropertyChanged(nameof(SelectedVersionSourceIndex));
        RaisePropertyChanged(nameof(DownloadThreadLimit));
        RaisePropertyChanged(nameof(DownloadThreadLimitLabel));
        RaisePropertyChanged(nameof(DownloadSpeedLimit));
        RaisePropertyChanged(nameof(DownloadSpeedLimitLabel));
        RaisePropertyChanged(nameof(DownloadTimeoutSeconds));
        RaisePropertyChanged(nameof(DownloadTimeoutLabel));
        RaisePropertyChanged(nameof(AutoSelectNewInstance));
        RaisePropertyChanged(nameof(UpgradePartialAuthlib));
        RaisePropertyChanged(nameof(SelectedCommunityDownloadSourceIndex));
        RaisePropertyChanged(nameof(SelectedFileNameFormatIndex));
        RaisePropertyChanged(nameof(SelectedModLocalNameStyleIndex));
        RaisePropertyChanged(nameof(IgnoreQuiltLoader));
        RaisePropertyChanged(nameof(NotifyReleaseUpdates));
        RaisePropertyChanged(nameof(NotifySnapshotUpdates));
        RaisePropertyChanged(nameof(AutoSwitchGameLanguageToChinese));
        RaisePropertyChanged(nameof(DetectClipboardResourceLinks));
        RaisePropertyChanged(nameof(SelectedSystemActivityIndex));
        RaisePropertyChanged(nameof(AnimationFpsLimit));
        RaisePropertyChanged(nameof(AnimationFpsLabel));
        RaisePropertyChanged(nameof(MaxRealTimeLogValue));
        RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
        RaisePropertyChanged(nameof(DisableHardwareAcceleration));
        RaisePropertyChanged(nameof(EnableDoH));
        RaisePropertyChanged(nameof(SelectedHttpProxyTypeIndex));
        RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
        RaisePropertyChanged(nameof(IsNoHttpProxySelected));
        RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
        RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
        RaisePropertyChanged(nameof(HttpProxyAddress));
        RaisePropertyChanged(nameof(HttpProxyUsername));
        RaisePropertyChanged(nameof(HttpProxyPassword));
        RaisePropertyChanged(nameof(DebugAnimationSpeed));
        RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
        RaisePropertyChanged(nameof(SkipCopyDuringDownload));
        RaisePropertyChanged(nameof(DebugModeEnabled));
        RaisePropertyChanged(nameof(DebugDelayEnabled));
        RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
        RaisePropertyChanged(nameof(IsAutoJavaSelected));
        RaisePropertyChanged(nameof(SelectedDarkModeIndex));
        RaisePropertyChanged(nameof(SelectedLightColorIndex));
        RaisePropertyChanged(nameof(SelectedDarkColorIndex));
        RaisePropertyChanged(nameof(IsThemeColorSwitchSupported));
        RaisePropertyChanged(nameof(IsThemeColorSwitchUnsupportedNoticeVisible));
        RaisePropertyChanged(nameof(IsLightCustomThemeColorEditorVisible));
        RaisePropertyChanged(nameof(IsDarkCustomThemeColorEditorVisible));
        RaisePropertyChanged(nameof(IsAnyCustomThemeColorEditorVisible));
        RaisePropertyChanged(nameof(CustomThemeColorInputHint));
        RaisePropertyChanged(nameof(CustomLightThemeColorHex));
        RaisePropertyChanged(nameof(CustomDarkThemeColorHex));
        RaisePropertyChanged(nameof(CustomLightThemePreviewBrush));
        RaisePropertyChanged(nameof(CustomDarkThemePreviewBrush));
        RaisePropertyChanged(nameof(IsLightCustomThemeColorInvalid));
        RaisePropertyChanged(nameof(IsDarkCustomThemeColorInvalid));
        RaisePropertyChanged(nameof(LauncherOpacity));
        RaisePropertyChanged(nameof(LauncherOpacityLabel));
        RaisePropertyChanged(nameof(ShowLauncherLogoSetting));
        RaisePropertyChanged(nameof(LockWindowSizeSetting));
        RaisePropertyChanged(nameof(ShowLaunchingHintSetting));
        RaisePropertyChanged(nameof(SelectedGlobalFontIndex));
        RaisePropertyChanged(nameof(SelectedMotdFontIndex));
        RaisePropertyChanged(nameof(BackgroundColorful));
        RaisePropertyChanged(nameof(SelectedBackgroundSuitIndex));
        RaisePropertyChanged(nameof(ShowBackgroundAdvancedSettings));
        RaisePropertyChanged(nameof(BackgroundOpacity));
        RaisePropertyChanged(nameof(BackgroundOpacityLabel));
        RaisePropertyChanged(nameof(BackgroundBlur));
        RaisePropertyChanged(nameof(BackgroundBlurLabel));
        RaisePropertyChanged(nameof(BackgroundCardHeader));
        RaisePropertyChanged(nameof(ShowBackgroundClearAction));
        RaisePropertyChanged(nameof(MusicVolume));
        RaisePropertyChanged(nameof(MusicVolumeLabel));
        RaisePropertyChanged(nameof(MusicRandomPlay));
        RaisePropertyChanged(nameof(MusicAutoStart));
        RaisePropertyChanged(nameof(MusicStartOnGameLaunch));
        RaisePropertyChanged(nameof(MusicStopOnGameLaunch));
        RaisePropertyChanged(nameof(MusicEnableSmtc));
        RaisePropertyChanged(nameof(SelectedLogoTypeIndex));
        RaisePropertyChanged(nameof(IsLogoTypeNoneSelected));
        RaisePropertyChanged(nameof(IsLogoTypeDefaultSelected));
        RaisePropertyChanged(nameof(IsLogoTypeTextSelected));
        RaisePropertyChanged(nameof(IsLogoTypeImageSelected));
        RaisePropertyChanged(nameof(IsLogoLeftVisible));
        RaisePropertyChanged(nameof(LogoAlignLeft));
        RaisePropertyChanged(nameof(IsLogoTextVisible));
        RaisePropertyChanged(nameof(LogoTextValue));
        RaisePropertyChanged(nameof(IsLogoImageActionsVisible));
        RaisePropertyChanged(nameof(SelectedHomepageTypeIndex));
        RaisePropertyChanged(nameof(IsHomepageBlankSelected));
        RaisePropertyChanged(nameof(IsHomepagePresetSelected));
        RaisePropertyChanged(nameof(IsHomepageLocalSelected));
        RaisePropertyChanged(nameof(IsHomepageNetSelected));
        RaisePropertyChanged(nameof(IsHomepageLocalActionsVisible));
        RaisePropertyChanged(nameof(IsHomepageNetVisible));
        RaisePropertyChanged(nameof(HomepageUrl));
        RaisePropertyChanged(nameof(IsHomepagePresetVisible));
        RaisePropertyChanged(nameof(SelectedHomepagePresetIndex));
    }
}
