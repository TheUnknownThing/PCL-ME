using System.ComponentModel;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
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
        "LaunchAdvanceWrapper",
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
        "ToolDownloadTranslateV2",
        "ToolDownloadMod",
        "ToolModLocalNameStyle",
        "ToolDownloadIgnoreQuilt",
        "ToolDownloadClipboard",
        "ToolHelpChinese"
    ];

    private static readonly string[] LauncherMiscLocalResetKeys =
    [
        "SystemSystemActivity"
    ];

    private static readonly string[] LauncherMiscSharedResetKeys =
    [
        "SystemLocale",
        "SystemMaxLog",
        FrontendStartupRenderingService.DisableHardwareAccelerationConfigKey,
        "SystemNetDnsMode",
        "SystemNetDnsProvider",
        "SystemNetEnableDoH",
        "SystemHttpProxyType",
        "SystemDebugAnim",
        "SystemDebugMode"
    ];

    private static readonly string[] LauncherMiscProtectedResetKeys =
    [
        "SystemHttpProxy",
        "SystemHttpProxyCustomUsername",
        "SystemHttpProxyCustomPassword"
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
        FrontendStartupScalingService.UiScaleFactorConfigKey,
        "UiBackgroundColorful",
        "UiBackgroundOpacity",
        "UiBackgroundBlur",
        "UiBackgroundSuit",
        "UiBlur",
        "UiBlurValue",
        "UiBlurSamplingRate",
        "UiBlurType",
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

    private void ApplySetupComposition(
        FrontendSetupComposition composition,
        bool initializeAllSurfaces = true,
        bool applyAppearance = true)
    {
        _setupComposition = composition;
        _suppressSetupPersistence = true;
        try
        {
            if (!initializeAllSurfaces)
            {
                InitializeGlobalSetupSettings();
            }

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
            else if (_currentRoute.Page == LauncherFrontendPageKey.Setup)
            {
                InitializeActiveSetupSurface();
                RaiseActiveSetupSurfaceProperties();
            }
            else
            {
                InitializeUiSurface(
                    refreshFeatureToggleGroups: false,
                    backgroundContentRefreshMode: BackgroundContentRefreshMode.Deferred);
            }
        }
        finally
        {
            _suppressSetupPersistence = false;
        }

        if (applyAppearance)
        {
            ApplyCurrentAppearanceSettings();
        }
    }

    private void ReloadSetupComposition(bool initializeAllSurfaces = false, bool applyAppearance = true)
    {
        FrontendHttpProxyService.ApplyStoredProxySettings(_launcherActionService.RuntimePaths);
        FrontendHttpProxyService.ApplyStoredDnsSettings(_launcherActionService.RuntimePaths);
        var composition = initializeAllSurfaces
            ? FrontendSetupCompositionService.Compose(_launcherActionService.RuntimePaths, _i18n)
            : FrontendSetupCompositionService.ComposeInitial(
                _launcherActionService.RuntimePaths,
                _i18n,
                _currentRoute.Page == LauncherFrontendPageKey.Setup
                    ? _currentRoute.Subpage
                    : LauncherFrontendSubpageKey.Default);
        ApplySetupComposition(
            composition,
            initializeAllSurfaces,
            applyAppearance);
    }

    private void ReloadActiveSetupSurface(bool applyAppearance = true)
    {
        FrontendHttpProxyService.ApplyStoredProxySettings(_launcherActionService.RuntimePaths);
        FrontendHttpProxyService.ApplyStoredDnsSettings(_launcherActionService.RuntimePaths);
        ApplySetupComposition(
            FrontendSetupCompositionService.ComposeActiveSurface(
                _launcherActionService.RuntimePaths,
                _i18n,
                _setupComposition,
                _currentRoute.Subpage),
            initializeAllSurfaces: false,
            applyAppearance);
    }

    private void RefreshSetupLocalizationState()
    {
        RefreshSetupLocalizationCatalog();
        _selectedLauncherLocaleIndex = ResolveLauncherLocaleIndex(_i18n.Locale);
        _setupComposition = FrontendSetupCompositionService.Compose(_launcherActionService.RuntimePaths, _i18n);
        _suppressSetupPersistence = true;
        try
        {
            InitializeAboutEntries();
            InitializeLogEntries();
            InitializeJavaSurface();
            RefreshUiFeatureToggleGroups();
            RaisePropertyChanged(nameof(SelectedLauncherLocaleIndex));
        }
        finally
        {
            _suppressSetupPersistence = false;
        }
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
                RaisePropertyChanged(nameof(SelectedCommunityDownloadSourceIndex));
                RaisePropertyChanged(nameof(SelectedFileNameFormatIndex));
                RaisePropertyChanged(nameof(SelectedModLocalNameStyleIndex));
                RaisePropertyChanged(nameof(IgnoreQuiltLoader));
                RaisePropertyChanged(nameof(AutoSwitchGameLanguageToChinese));
                RaisePropertyChanged(nameof(DetectClipboardResourceLinks));
                break;
            case LauncherFrontendSubpageKey.SetupLauncherMisc:
                RaisePropertyChanged(nameof(SelectedSystemActivityIndex));
                RaisePropertyChanged(nameof(MaxRealTimeLogValue));
                RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
                RaisePropertyChanged(nameof(IsHardwareAccelerationToggleVisible));
                RaisePropertyChanged(nameof(DisableHardwareAcceleration));
                RaisePropertyChanged(nameof(SecureDnsModeOptions));
                RaisePropertyChanged(nameof(SecureDnsProviderOptions));
                RaisePropertyChanged(nameof(SelectedSecureDnsModeIndex));
                RaisePropertyChanged(nameof(SelectedSecureDnsProviderIndex));
                RaisePropertyChanged(nameof(IsSecureDnsProviderSelectionEnabled));
                RaisePropertyChanged(nameof(SelectedHttpProxyTypeIndex));
                RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
                RaisePropertyChanged(nameof(IsNoHttpProxySelected));
                RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
                RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
                RaisePropertyChanged(nameof(HttpProxyAddress));
                RaisePropertyChanged(nameof(HttpProxyUsername));
                RaisePropertyChanged(nameof(HttpProxyPassword));
                RaisePropertyChanged(nameof(ProxyTestFeedbackText));
                RaisePropertyChanged(nameof(IsProxyTestFeedbackVisible));
                RaisePropertyChanged(nameof(IsProxyTestSuccessVisible));
                RaisePropertyChanged(nameof(IsProxyTestFailureVisible));
                RaisePropertyChanged(nameof(DebugAnimationSpeed));
                RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
                RaisePropertyChanged(nameof(DebugModeEnabled));
                break;
            case LauncherFrontendSubpageKey.SetupJava:
                RaisePropertyChanged(nameof(HasJavaRuntimeEntries));
                RaisePropertyChanged(nameof(IsAutoJavaSelected));
                break;
            case LauncherFrontendSubpageKey.SetupUI:
                RaisePropertyChanged(nameof(LauncherLocaleOptions));
                RaisePropertyChanged(nameof(SelectedLauncherLocaleIndex));
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
                RaisePropertyChanged(nameof(UiScaleOptions));
                RaisePropertyChanged(nameof(SelectedUiScaleFactorIndex));
                RaisePropertyChanged(nameof(UiScaleFactor));
                RaisePropertyChanged(nameof(UiScaleFactorLabel));
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
                RaisePropertyChanged(nameof(ShowLaunch32BitJavaWarning));
                RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
                RaisePropertyChanged(nameof(LaunchWrapperCommand));
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
                _launcherActionService.PersistLocalValue("SystemUpdateChannel", SelectedUpdateChannelIndex);
                break;
            case nameof(SelectedUpdateModeIndex):
                _launcherActionService.PersistLocalValue("SystemSystemUpdate", SelectedUpdateModeIndex);
                break;
            case nameof(SelectedLaunchIsolationIndex):
                _launcherActionService.PersistLocalValue("LaunchArgumentIndieV2", SelectedLaunchIsolationIndex);
                break;
            case nameof(LaunchWindowTitleSetting):
                _launcherActionService.PersistLocalValue("LaunchArgumentTitle", LaunchWindowTitleSetting);
                break;
            case nameof(LaunchCustomInfoSetting):
                _launcherActionService.PersistLocalValue("LaunchArgumentInfo", LaunchCustomInfoSetting);
                break;
            case nameof(SelectedLaunchVisibilityIndex):
                _launcherActionService.PersistSharedValue("LaunchArgumentVisible", FrontendSetupCompositionService.MapDisplayLaunchVisibilityToStoredIndex(SelectedLaunchVisibilityIndex));
                break;
            case nameof(SelectedLaunchPriorityIndex):
                _launcherActionService.PersistSharedValue("LaunchArgumentPriority", SelectedLaunchPriorityIndex);
                break;
            case nameof(SelectedLaunchWindowTypeIndex):
                _launcherActionService.PersistLocalValue("LaunchArgumentWindowType", SelectedLaunchWindowTypeIndex);
                break;
            case nameof(LaunchWindowWidth):
                if (int.TryParse(LaunchWindowWidth, out var width))
                {
                    _launcherActionService.PersistLocalValue("LaunchArgumentWindowWidth", width);
                }
                break;
            case nameof(LaunchWindowHeight):
                if (int.TryParse(LaunchWindowHeight, out var height))
                {
                    _launcherActionService.PersistLocalValue("LaunchArgumentWindowHeight", height);
                }
                break;
            case nameof(UseAutomaticRamAllocation):
            case nameof(UseCustomRamAllocation):
                _launcherActionService.PersistLocalValue("LaunchRamType", UseAutomaticRamAllocation ? 0 : 1);
                break;
            case nameof(CustomRamAllocation):
                _launcherActionService.PersistLocalValue("LaunchRamCustom", FrontendSetupCompositionService.MapLaunchRamGbToStoredValue(CustomRamAllocation));
                break;
            case nameof(SelectedLaunchRendererIndex):
                _launcherActionService.PersistLocalValue("LaunchAdvanceRenderer", SelectedLaunchRendererIndex);
                break;
            case nameof(LaunchWrapperCommand):
                _launcherActionService.PersistLocalValue("LaunchAdvanceWrapper", LaunchWrapperCommand);
                break;
            case nameof(LaunchJvmArguments):
                _launcherActionService.PersistLocalValue("LaunchAdvanceJvm", LaunchJvmArguments);
                break;
            case nameof(LaunchGameArguments):
                _launcherActionService.PersistLocalValue("LaunchAdvanceGame", LaunchGameArguments);
                break;
            case nameof(LaunchBeforeCommand):
                _launcherActionService.PersistLocalValue("LaunchAdvanceRun", LaunchBeforeCommand);
                break;
            case nameof(LaunchEnvironmentVariables):
                _launcherActionService.PersistLocalValue("LaunchAdvanceEnvironmentVariables", LaunchEnvironmentVariables);
                break;
            case nameof(WaitForLaunchBeforeCommand):
                _launcherActionService.PersistLocalValue("LaunchAdvanceRunWait", WaitForLaunchBeforeCommand);
                break;
            case nameof(ForceX11OnWaylandForLaunch):
                _launcherActionService.PersistLocalValue("LaunchAdvanceForceX11OnWayland", ForceX11OnWaylandForLaunch);
                break;
            case nameof(DisableJavaLaunchWrapper):
                _launcherActionService.PersistLocalValue("LaunchAdvanceDisableJLW", DisableJavaLaunchWrapper);
                break;
            case nameof(DisableRetroWrapper):
                _launcherActionService.PersistSharedValue("LaunchAdvanceDisableRW", DisableRetroWrapper);
                break;
            case nameof(RequireDedicatedGpu):
                _launcherActionService.PersistSharedValue("LaunchAdvanceGraphicCard", RequireDedicatedGpu);
                break;
            case nameof(UseJavaExecutable):
                _launcherActionService.PersistSharedValue("LaunchAdvanceNoJavaw", UseJavaExecutable);
                break;
            case nameof(SelectedLaunchPreferredIpStackIndex):
                _launcherActionService.PersistSharedValue("LaunchPreferredIpStack", SelectedLaunchPreferredIpStackIndex);
                break;
            case nameof(SelectedDownloadSourceIndex):
                _launcherActionService.PersistSharedValue("ToolDownloadSource", SelectedDownloadSourceIndex);
                break;
            case nameof(SelectedVersionSourceIndex):
                _launcherActionService.PersistSharedValue("ToolDownloadVersion", SelectedVersionSourceIndex);
                break;
            case nameof(DownloadThreadLimit):
                _launcherActionService.PersistSharedValue("ToolDownloadThread", (int)Math.Round(DownloadThreadLimit));
                break;
            case nameof(DownloadSpeedLimit):
                _launcherActionService.PersistSharedValue("ToolDownloadSpeed", (int)Math.Round(DownloadSpeedLimit));
                break;
            case nameof(DownloadTimeoutSeconds):
                _launcherActionService.PersistSharedValue("ToolDownloadTimeout", (int)Math.Round(DownloadTimeoutSeconds));
                break;
            case nameof(AutoSelectNewInstance):
                _launcherActionService.PersistSharedValue("ToolDownloadAutoSelectVersion", AutoSelectNewInstance);
                break;
            case nameof(SelectedCommunityDownloadSourceIndex):
                _launcherActionService.PersistSharedValue("ToolDownloadMod", SelectedCommunityDownloadSourceIndex);
                break;
            case nameof(SelectedFileNameFormatIndex):
                _launcherActionService.PersistSharedValue("ToolDownloadTranslateV2", SelectedFileNameFormatIndex);
                break;
            case nameof(SelectedModLocalNameStyleIndex):
                _launcherActionService.PersistSharedValue("ToolModLocalNameStyle", SelectedModLocalNameStyleIndex);
                break;
            case nameof(IgnoreQuiltLoader):
                _launcherActionService.PersistSharedValue("ToolDownloadIgnoreQuilt", IgnoreQuiltLoader);
                break;
            case nameof(AutoSwitchGameLanguageToChinese):
                _launcherActionService.PersistSharedValue("ToolHelpChinese", AutoSwitchGameLanguageToChinese);
                break;
            case nameof(DetectClipboardResourceLinks):
                _launcherActionService.PersistSharedValue("ToolDownloadClipboard", DetectClipboardResourceLinks);
                break;
            case nameof(SelectedSystemActivityIndex):
                _launcherActionService.PersistLocalValue("SystemSystemActivity", SelectedSystemActivityIndex);
                RefreshLaunchAnnouncements();
                break;
            case nameof(MaxRealTimeLogValue):
                _launcherActionService.PersistSharedValue("SystemMaxLog", (int)Math.Round(MaxRealTimeLogValue));
                break;
            case nameof(DisableHardwareAcceleration):
                _launcherActionService.PersistSharedValue(
                    FrontendStartupRenderingService.DisableHardwareAccelerationConfigKey,
                    DisableHardwareAcceleration);
                AddActivity(
                    _i18n.T(DisableHardwareAcceleration
                        ? "setup.launcher_misc.activities.disable_hardware_acceleration"
                        : "setup.launcher_misc.activities.enable_hardware_acceleration"),
                    _i18n.T("setup.launcher_misc.hints.disable_hardware_acceleration_restart"));
                break;
            case nameof(SelectedSecureDnsModeIndex):
                _launcherActionService.PersistSharedValue("SystemNetDnsMode", SelectedSecureDnsModeIndex);
                _launcherActionService.PersistSharedValue(
                    "SystemNetEnableDoH",
                    SelectedSecureDnsModeIndex == (int)FrontendSecureDnsMode.DnsOverHttps);
                FrontendHttpProxyService.ApplySecureDnsConfiguration(
                    FrontendHttpProxyService.BuildSecureDnsConfiguration(
                        SelectedSecureDnsModeIndex,
                        SelectedSecureDnsProviderIndex));
                break;
            case nameof(SelectedSecureDnsProviderIndex):
                _launcherActionService.PersistSharedValue("SystemNetDnsProvider", SelectedSecureDnsProviderIndex);
                FrontendHttpProxyService.ApplySecureDnsConfiguration(
                    FrontendHttpProxyService.BuildSecureDnsConfiguration(
                        SelectedSecureDnsModeIndex,
                        SelectedSecureDnsProviderIndex));
                break;
            case nameof(SelectedHttpProxyTypeIndex):
                _launcherActionService.PersistSharedValue("SystemHttpProxyType", SelectedHttpProxyTypeIndex);
                FrontendHttpProxyService.ApplyStoredProxySettings(_launcherActionService.RuntimePaths);
                break;
            case nameof(DebugAnimationSpeed):
                _launcherActionService.PersistSharedValue("SystemDebugAnim", (int)Math.Round(DebugAnimationSpeed));
                LauncherActionService.ApplyAnimationPreferences(DebugAnimationSpeed);
                break;
            case nameof(DebugModeEnabled):
                _launcherActionService.PersistSharedValue("SystemDebugMode", DebugModeEnabled);
                RefreshDebugModeSurface();
                AddActivity(
                    _i18n.T("setup.launcher_misc.flags.debug_mode"),
                    _i18n.T(DebugModeEnabled
                        ? "setup.launcher_misc.messages.debug_mode_enabled"
                        : "setup.launcher_misc.messages.debug_mode_disabled"));
                break;
            case nameof(SelectedDarkModeIndex):
                _launcherActionService.PersistSharedValue("UiDarkMode", SelectedDarkModeIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedLightColorIndex):
                _launcherActionService.PersistSharedValue("UiLightColor", SelectedLightColorIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedDarkColorIndex):
                _launcherActionService.PersistSharedValue("UiDarkColor", SelectedDarkColorIndex);
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(CustomLightThemeColorHex):
                PersistCustomThemeColor("UiLightColorCustom", CustomLightThemeColorHex);
                break;
            case nameof(CustomDarkThemeColorHex):
                PersistCustomThemeColor("UiDarkColorCustom", CustomDarkThemeColorHex);
                break;
            case nameof(LauncherOpacity):
                _launcherActionService.PersistLocalValue("UiLauncherTransparent", (int)Math.Round(LauncherOpacity));
                break;
            case nameof(UiScaleFactor):
                if (_suppressUiScalePersistence)
                {
                    break;
                }

                _launcherActionService.PersistLocalValue(
                    FrontendStartupScalingService.UiScaleFactorConfigKey,
                    UiScaleFactor);
                break;
            case nameof(ShowLauncherLogoSetting):
                _launcherActionService.PersistLocalValue("UiLauncherLogo", ShowLauncherLogoSetting);
                break;
            case nameof(LockWindowSizeSetting):
                _launcherActionService.PersistSharedValue("UiLockWindowSize", LockWindowSizeSetting);
                break;
            case nameof(ShowLaunchingHintSetting):
                _launcherActionService.PersistLocalValue("UiShowLaunchingHint", ShowLaunchingHintSetting);
                break;
            case nameof(SelectedGlobalFontIndex):
                _launcherActionService.PersistLocalValue("UiFont", MapFontIndexToConfigValue(SelectedGlobalFontIndex));
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(SelectedMotdFontIndex):
                _launcherActionService.PersistLocalValue("UiMotdFont", MapFontIndexToConfigValue(SelectedMotdFontIndex));
                ApplyCurrentAppearanceSettings();
                break;
            case nameof(BackgroundColorful):
                _launcherActionService.PersistLocalValue("UiBackgroundColorful", BackgroundColorful);
                break;
            case nameof(SelectedBackgroundSuitIndex):
                _launcherActionService.PersistLocalValue("UiBackgroundSuit", SelectedBackgroundSuitIndex);
                break;
            case nameof(BackgroundOpacity):
                _launcherActionService.PersistLocalValue("UiBackgroundOpacity", (int)Math.Round(BackgroundOpacity));
                break;
            case nameof(BackgroundBlur):
                _launcherActionService.PersistLocalValue("UiBackgroundBlur", (int)Math.Round(BackgroundBlur));
                break;
            case nameof(SelectedLogoTypeIndex):
                _launcherActionService.PersistLocalValue("UiLogoType", SelectedLogoTypeIndex);
                break;
            case nameof(LogoAlignLeft):
                _launcherActionService.PersistLocalValue("UiLogoLeft", LogoAlignLeft);
                break;
            case nameof(LogoTextValue):
                _launcherActionService.PersistLocalValue("UiLogoText", LogoTextValue);
                break;
            case nameof(SelectedHomepageTypeIndex):
                _launcherActionService.PersistLocalValue("UiCustomType", MapHomepageDisplayIndexToStoredValue(SelectedHomepageTypeIndex));
                RefreshLaunchHomepage(forceRefresh: false);
                break;
            case nameof(HomepageUrl):
                _launcherActionService.PersistLocalValue("UiCustomNet", HomepageUrl);
                RefreshLaunchHomepage(forceRefresh: false);
                break;
            case nameof(SelectedHomepagePresetIndex):
                _launcherActionService.PersistLocalValue("UiCustomPreset", SelectedHomepagePresetIndex);
                RefreshLaunchHomepage(forceRefresh: false);
                break;
        }
    }

    private void PersistUiToggle(string key, string title, bool value)
    {
        if (_suppressSetupPersistence)
        {
            return;
        }

        _launcherActionService.PersistLocalValue(key, value);
        ReloadActiveSetupSurface();
        RefreshLauncherState(_i18n.T(
            value
                ? "setup.ui.hidden_features.reactions.hidden"
                : "setup.ui.hidden_features.reactions.restored",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = title
            }));
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
        _launcherActionService.ApplyAppearance(
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

        _launcherActionService.PersistSharedValue(key, FrontendAppearanceService.FormatCustomThemeColor(color));
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
        RaisePropertyChanged(nameof(ShowLaunch32BitJavaWarning));
        RaisePropertyChanged(nameof(SelectedLaunchRendererIndex));
        RaisePropertyChanged(nameof(LaunchWrapperCommand));
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
        RaisePropertyChanged(nameof(SelectedCommunityDownloadSourceIndex));
        RaisePropertyChanged(nameof(SelectedFileNameFormatIndex));
        RaisePropertyChanged(nameof(SelectedModLocalNameStyleIndex));
        RaisePropertyChanged(nameof(IgnoreQuiltLoader));
        RaisePropertyChanged(nameof(AutoSwitchGameLanguageToChinese));
        RaisePropertyChanged(nameof(DetectClipboardResourceLinks));
        RaisePropertyChanged(nameof(LauncherLocaleOptions));
        RaisePropertyChanged(nameof(SelectedLauncherLocaleIndex));
        RaisePropertyChanged(nameof(SelectedSystemActivityIndex));
        RaisePropertyChanged(nameof(MaxRealTimeLogValue));
        RaisePropertyChanged(nameof(MaxRealTimeLogLabel));
        RaisePropertyChanged(nameof(DisableHardwareAcceleration));
        RaisePropertyChanged(nameof(SecureDnsModeOptions));
        RaisePropertyChanged(nameof(SecureDnsProviderOptions));
        RaisePropertyChanged(nameof(SelectedSecureDnsModeIndex));
        RaisePropertyChanged(nameof(SelectedSecureDnsProviderIndex));
        RaisePropertyChanged(nameof(IsSecureDnsProviderSelectionEnabled));
        RaisePropertyChanged(nameof(SelectedHttpProxyTypeIndex));
        RaisePropertyChanged(nameof(IsCustomHttpProxyEnabled));
        RaisePropertyChanged(nameof(IsNoHttpProxySelected));
        RaisePropertyChanged(nameof(IsSystemHttpProxySelected));
        RaisePropertyChanged(nameof(IsCustomHttpProxySelected));
        RaisePropertyChanged(nameof(HttpProxyAddress));
        RaisePropertyChanged(nameof(HttpProxyUsername));
        RaisePropertyChanged(nameof(HttpProxyPassword));
        RaisePropertyChanged(nameof(ProxyTestFeedbackText));
        RaisePropertyChanged(nameof(IsProxyTestFeedbackVisible));
        RaisePropertyChanged(nameof(IsProxyTestSuccessVisible));
        RaisePropertyChanged(nameof(IsProxyTestFailureVisible));
        RaisePropertyChanged(nameof(DebugAnimationSpeed));
        RaisePropertyChanged(nameof(DebugAnimationSpeedLabel));
        RaisePropertyChanged(nameof(DebugModeEnabled));
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
        RaisePropertyChanged(nameof(UiScaleFactor));
        RaisePropertyChanged(nameof(UiScaleFactorLabel));
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
