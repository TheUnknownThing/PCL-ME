using System.ComponentModel;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.ViewModels;

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
        "LaunchAdvanceRunWait",
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
        "LoginMsAuthType",
        "LaunchPreferredIpStack",
        "LaunchArgumentJavaSelect"
    ];

    private static readonly string[] GameLinkResetKeys =
    [
        "LinkUsername",
        "LinkProtocolPreference",
        "LinkLatencyFirstMode",
        "LinkTryPunchSym",
        "LinkEnableIPv6",
        "LinkEnableCliOutput"
    ];

    private static readonly string[] GameManageResetKeys =
    [
        "ToolDownloadThread",
        "ToolDownloadSpeed",
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
        "SystemTelemetry",
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

    private static readonly string[] UpdateProtectedResetKeys =
    [
        "SystemMirrorChyanKey"
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
        "UiAutoPauseVideo",
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
        "UiHiddenSetupGameLink",
        "UiHiddenSetupAbout",
        "UiHiddenSetupFeedback",
        "UiHiddenSetupLog",
        "UiHiddenToolsGameLink",
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
        "UiLightColor"
    ];

    private void ApplySetupComposition(FrontendSetupComposition composition)
    {
        _setupComposition = composition;
        _suppressSetupPersistence = true;
        try
        {
            InitializeAboutEntries();
            InitializeLogEntries();
            InitializeUpdateSurface();
            InitializeLaunchSettingsSurface();
            InitializeGameLinkSurface();
            InitializeGameManageSurface();
            InitializeLauncherMiscSurface();
            InitializeJavaSurface();
            InitializeUiSurface();
            RaiseSetupSurfaceProperties();
        }
        finally
        {
            _suppressSetupPersistence = false;
        }
    }

    private void ReloadSetupComposition()
    {
        ApplySetupComposition(FrontendSetupCompositionService.Compose(_shellActionService.RuntimePaths));
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
            case nameof(MirrorCdk):
                _shellActionService.PersistProtectedSharedValue("SystemMirrorChyanKey", MirrorCdk);
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
            case nameof(WaitForLaunchBeforeCommand):
                _shellActionService.PersistLocalValue("LaunchAdvanceRunWait", WaitForLaunchBeforeCommand);
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
            case nameof(SelectedLaunchMicrosoftAuthIndex):
                _shellActionService.PersistSharedValue("LoginMsAuthType", SelectedLaunchMicrosoftAuthIndex);
                break;
            case nameof(SelectedLaunchPreferredIpStackIndex):
                _shellActionService.PersistSharedValue("LaunchPreferredIpStack", SelectedLaunchPreferredIpStackIndex);
                break;
            case nameof(LinkUsername):
                _shellActionService.PersistSharedValue("LinkUsername", LinkUsername);
                break;
            case nameof(SelectedProtocolPreferenceIndex):
                _shellActionService.PersistSharedValue("LinkProtocolPreference", SelectedProtocolPreferenceIndex);
                break;
            case nameof(PreferLowestLatencyPath):
                _shellActionService.PersistSharedValue("LinkLatencyFirstMode", PreferLowestLatencyPath);
                break;
            case nameof(TryPunchSymmetricNat):
                _shellActionService.PersistSharedValue("LinkTryPunchSym", TryPunchSymmetricNat);
                break;
            case nameof(AllowIpv6Communication):
                _shellActionService.PersistSharedValue("LinkEnableIPv6", AllowIpv6Communication);
                break;
            case nameof(EnableLinkCliOutput):
                _shellActionService.PersistSharedValue("LinkEnableCliOutput", EnableLinkCliOutput);
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
                break;
            case nameof(AnimationFpsLimit):
                _shellActionService.PersistSharedValue("UiAniFPS", (int)Math.Round(AnimationFpsLimit));
                break;
            case nameof(MaxRealTimeLogValue):
                _shellActionService.PersistSharedValue("SystemMaxLog", (int)Math.Round(MaxRealTimeLogValue));
                break;
            case nameof(DisableHardwareAcceleration):
                _shellActionService.PersistSharedValue("SystemDisableHardwareAcceleration", DisableHardwareAcceleration);
                break;
            case nameof(EnableTelemetry):
                _shellActionService.PersistSharedValue("SystemTelemetry", EnableTelemetry);
                break;
            case nameof(EnableDoH):
                _shellActionService.PersistSharedValue("SystemNetEnableDoH", EnableDoH);
                break;
            case nameof(SelectedHttpProxyTypeIndex):
                _shellActionService.PersistSharedValue("SystemHttpProxyType", SelectedHttpProxyTypeIndex);
                break;
            case nameof(DebugAnimationSpeed):
                _shellActionService.PersistSharedValue("SystemDebugAnim", (int)Math.Round(DebugAnimationSpeed));
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
                break;
            case nameof(SelectedLightColorIndex):
                _shellActionService.PersistSharedValue("UiLightColor", SelectedLightColorIndex);
                break;
            case nameof(SelectedDarkColorIndex):
                _shellActionService.PersistSharedValue("UiDarkColor", SelectedDarkColorIndex);
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
            case nameof(EnableAdvancedMaterial):
                _shellActionService.PersistLocalValue("UiBlur", EnableAdvancedMaterial);
                break;
            case nameof(BlurRadius):
                _shellActionService.PersistLocalValue("UiBlurValue", (int)Math.Round(BlurRadius));
                break;
            case nameof(BlurSamplingRate):
                _shellActionService.PersistLocalValue("UiBlurSamplingRate", (int)Math.Round(BlurSamplingRate));
                break;
            case nameof(SelectedBlurTypeIndex):
                _shellActionService.PersistLocalValue("UiBlurType", SelectedBlurTypeIndex);
                break;
            case nameof(SelectedGlobalFontIndex):
                _shellActionService.PersistLocalValue("UiFont", MapFontIndexToConfigValue(SelectedGlobalFontIndex));
                break;
            case nameof(SelectedMotdFontIndex):
                _shellActionService.PersistLocalValue("UiMotdFont", MapFontIndexToConfigValue(SelectedMotdFontIndex));
                break;
            case nameof(AutoPauseVideo):
                _shellActionService.PersistLocalValue("UiAutoPauseVideo", AutoPauseVideo);
                break;
            case nameof(BackgroundColorful):
                _shellActionService.PersistLocalValue("UiBackgroundColorful", BackgroundColorful);
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
                break;
            case nameof(HomepageUrl):
                _shellActionService.PersistLocalValue("UiCustomNet", HomepageUrl);
                break;
            case nameof(SelectedHomepagePresetIndex):
                _shellActionService.PersistLocalValue("UiCustomPreset", SelectedHomepagePresetIndex);
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
    }

    private static string MapFontIndexToConfigValue(int index)
    {
        return index switch
        {
            1 => "SourceHanSansCN-Regular",
            2 => "LXGW WenKai",
            3 => "JetBrains Mono",
            _ => string.Empty
        };
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

    private void RaiseSetupSurfaceProperties()
    {
        RaisePropertyChanged(nameof(HasAboutProjectEntries));
        RaisePropertyChanged(nameof(HasAboutAcknowledgementEntries));
        RaisePropertyChanged(nameof(HasFeedbackSections));
        RaisePropertyChanged(nameof(SelectedUpdateChannelIndex));
        RaisePropertyChanged(nameof(SelectedUpdateModeIndex));
        RaisePropertyChanged(nameof(MirrorCdk));
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
        RaisePropertyChanged(nameof(AllocatedRamLabel));
        RaisePropertyChanged(nameof(ShowRamAllocationWarning));
        RaisePropertyChanged(nameof(OptimizeMemoryBeforeLaunch));
        RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
        RaisePropertyChanged(nameof(LaunchJvmArguments));
        RaisePropertyChanged(nameof(LaunchGameArguments));
        RaisePropertyChanged(nameof(LaunchBeforeCommand));
        RaisePropertyChanged(nameof(WaitForLaunchBeforeCommand));
        RaisePropertyChanged(nameof(DisableJavaLaunchWrapper));
        RaisePropertyChanged(nameof(DisableRetroWrapper));
        RaisePropertyChanged(nameof(RequireDedicatedGpu));
        RaisePropertyChanged(nameof(UseJavaExecutable));
        RaisePropertyChanged(nameof(SelectedLaunchMicrosoftAuthIndex));
        RaisePropertyChanged(nameof(SelectedLaunchPreferredIpStackIndex));
        RaisePropertyChanged(nameof(LinkUsername));
        RaisePropertyChanged(nameof(SelectedProtocolPreferenceIndex));
        RaisePropertyChanged(nameof(PreferLowestLatencyPath));
        RaisePropertyChanged(nameof(TryPunchSymmetricNat));
        RaisePropertyChanged(nameof(AllowIpv6Communication));
        RaisePropertyChanged(nameof(EnableLinkCliOutput));
        RaisePropertyChanged(nameof(SelectedDownloadSourceIndex));
        RaisePropertyChanged(nameof(SelectedVersionSourceIndex));
        RaisePropertyChanged(nameof(DownloadThreadLimit));
        RaisePropertyChanged(nameof(DownloadThreadLimitLabel));
        RaisePropertyChanged(nameof(DownloadSpeedLimit));
        RaisePropertyChanged(nameof(DownloadSpeedLimitLabel));
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
        RaisePropertyChanged(nameof(EnableTelemetry));
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
        RaisePropertyChanged(nameof(LauncherOpacity));
        RaisePropertyChanged(nameof(LauncherOpacityLabel));
        RaisePropertyChanged(nameof(ShowLauncherLogoSetting));
        RaisePropertyChanged(nameof(LockWindowSizeSetting));
        RaisePropertyChanged(nameof(ShowLaunchingHintSetting));
        RaisePropertyChanged(nameof(EnableAdvancedMaterial));
        RaisePropertyChanged(nameof(BlurRadius));
        RaisePropertyChanged(nameof(BlurRadiusLabel));
        RaisePropertyChanged(nameof(BlurSamplingRate));
        RaisePropertyChanged(nameof(BlurSamplingRateLabel));
        RaisePropertyChanged(nameof(SelectedBlurTypeIndex));
        RaisePropertyChanged(nameof(SelectedGlobalFontIndex));
        RaisePropertyChanged(nameof(SelectedMotdFontIndex));
        RaisePropertyChanged(nameof(AutoPauseVideo));
        RaisePropertyChanged(nameof(BackgroundColorful));
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
