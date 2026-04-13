namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public IReadOnlyList<string> DarkModeOptions { get; } =
    [
        "浅色",
        "深色",
        "跟随系统"
    ];

    public IReadOnlyList<string> ThemeColorOptions { get; } =
    [
        "龙猫蓝",
        "甜柠青",
        "小草绿",
        "菠萝黄",
        "橡木棕"
    ];

    public IReadOnlyList<string> BlurTypeOptions { get; } =
    [
        "高斯模糊",
        "方框模糊"
    ];

    public IReadOnlyList<string> FontOptions { get; } =
    [
        "默认字体",
        "思源黑体",
        "霞鹜文楷",
        "JetBrains Mono"
    ];

    public IReadOnlyList<string> HomepagePresetOptions { get; } =
    [
        "你知道吗？",
        "Minecraft 新闻（作者：最亮的信标）",
        "简单主页（作者：MFn233）",
        "每日整合包推荐（作者：wkea）",
        "Minecraft 皮肤推荐（作者：wkea）",
        "OpenBMCLAPI 仪表盘 Lite（作者：Silverteal、Mxmilu666）",
        "PCL 主页市场（作者：凌云）",
        "PCL 新闻速报（作者：Joker2184）",
        "PCL 新功能说明书（作者：WForst-Breeze）",
        "杂志主页（作者：CreeperIsASpy）",
        "PCL GitHub 仪表盘（作者：Deep-Dark-Forest）",
        "Minecraft 更新摘要（作者：pynickle，部分由 AI 生成）",
        "PCL CE 公告栏",
        "Minecraft 官方信息流"
    ];

    public int SelectedDarkModeIndex
    {
        get => _selectedDarkModeIndex;
        set => SetProperty(ref _selectedDarkModeIndex, Math.Clamp(value, 0, DarkModeOptions.Count - 1));
    }

    public int SelectedLightColorIndex
    {
        get => _selectedLightColorIndex;
        set => SetProperty(ref _selectedLightColorIndex, Math.Clamp(value, 0, ThemeColorOptions.Count - 1));
    }

    public int SelectedDarkColorIndex
    {
        get => _selectedDarkColorIndex;
        set => SetProperty(ref _selectedDarkColorIndex, Math.Clamp(value, 0, ThemeColorOptions.Count - 1));
    }

    public double LauncherOpacity
    {
        get => _launcherOpacity;
        set
        {
            if (SetProperty(ref _launcherOpacity, value))
            {
                RaisePropertyChanged(nameof(LauncherOpacityLabel));
            }
        }
    }

    public string LauncherOpacityLabel => $"{Math.Round(LauncherOpacity / 10)}%";

    public bool ShowLauncherLogoSetting
    {
        get => _showLauncherLogo;
        set => SetProperty(ref _showLauncherLogo, value);
    }

    public bool LockWindowSizeSetting
    {
        get => _lockWindowSize;
        set => SetProperty(ref _lockWindowSize, value);
    }

    public bool ShowLaunchingHintSetting
    {
        get => _showLaunchingHint;
        set => SetProperty(ref _showLaunchingHint, value);
    }

    public bool EnableAdvancedMaterial
    {
        get => _enableAdvancedMaterial;
        set => SetProperty(ref _enableAdvancedMaterial, value);
    }

    public double BlurRadius
    {
        get => _blurRadius;
        set
        {
            if (SetProperty(ref _blurRadius, value))
            {
                RaisePropertyChanged(nameof(BlurRadiusLabel));
            }
        }
    }

    public string BlurRadiusLabel => $"{Math.Round(BlurRadius)}";

    public double BlurSamplingRate
    {
        get => _blurSamplingRate;
        set
        {
            if (SetProperty(ref _blurSamplingRate, value))
            {
                RaisePropertyChanged(nameof(BlurSamplingRateLabel));
            }
        }
    }

    public string BlurSamplingRateLabel => $"{Math.Round(BlurSamplingRate)}%";

    public int SelectedBlurTypeIndex
    {
        get => _selectedBlurTypeIndex;
        set => SetProperty(ref _selectedBlurTypeIndex, Math.Clamp(value, 0, BlurTypeOptions.Count - 1));
    }

    public int SelectedGlobalFontIndex
    {
        get => _selectedGlobalFontIndex;
        set => SetProperty(ref _selectedGlobalFontIndex, Math.Clamp(value, 0, FontOptions.Count - 1));
    }

    public int SelectedMotdFontIndex
    {
        get => _selectedMotdFontIndex;
        set => SetProperty(ref _selectedMotdFontIndex, Math.Clamp(value, 0, FontOptions.Count - 1));
    }

    public bool AutoPauseVideo
    {
        get => _autoPauseVideo;
        set => SetProperty(ref _autoPauseVideo, value);
    }

    public bool BackgroundColorful
    {
        get => _backgroundColorful;
        set => SetProperty(ref _backgroundColorful, value);
    }

    public double MusicVolume
    {
        get => _musicVolume;
        set
        {
            if (SetProperty(ref _musicVolume, value))
            {
                RaisePropertyChanged(nameof(MusicVolumeLabel));
            }
        }
    }

    public string MusicVolumeLabel => $"{Math.Round(MusicVolume / 10)}%";

    public bool MusicRandomPlay
    {
        get => _musicRandomPlay;
        set => SetProperty(ref _musicRandomPlay, value);
    }

    public bool MusicAutoStart
    {
        get => _musicAutoStart;
        set => SetProperty(ref _musicAutoStart, value);
    }

    public bool MusicStartOnGameLaunch
    {
        get => _musicStartOnGameLaunch;
        set => SetProperty(ref _musicStartOnGameLaunch, value);
    }

    public bool MusicStopOnGameLaunch
    {
        get => _musicStopOnGameLaunch;
        set => SetProperty(ref _musicStopOnGameLaunch, value);
    }

    public bool MusicEnableSmtc
    {
        get => _musicEnableSmtc;
        set => SetProperty(ref _musicEnableSmtc, value);
    }

    public int SelectedLogoTypeIndex
    {
        get => _selectedLogoTypeIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _selectedLogoTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsLogoTypeNoneSelected));
                RaisePropertyChanged(nameof(IsLogoTypeDefaultSelected));
                RaisePropertyChanged(nameof(IsLogoTypeTextSelected));
                RaisePropertyChanged(nameof(IsLogoTypeImageSelected));
                RaisePropertyChanged(nameof(IsLogoLeftVisible));
                RaisePropertyChanged(nameof(IsLogoTextVisible));
                RaisePropertyChanged(nameof(IsLogoImageActionsVisible));
            }
        }
    }

    public bool IsLogoTypeNoneSelected
    {
        get => SelectedLogoTypeIndex == 0;
        set { if (value) SelectedLogoTypeIndex = 0; }
    }

    public bool IsLogoTypeDefaultSelected
    {
        get => SelectedLogoTypeIndex == 1;
        set { if (value) SelectedLogoTypeIndex = 1; }
    }

    public bool IsLogoTypeTextSelected
    {
        get => SelectedLogoTypeIndex == 2;
        set { if (value) SelectedLogoTypeIndex = 2; }
    }

    public bool IsLogoTypeImageSelected
    {
        get => SelectedLogoTypeIndex == 3;
        set { if (value) SelectedLogoTypeIndex = 3; }
    }

    public bool IsLogoLeftVisible => SelectedLogoTypeIndex != 0;

    public bool LogoAlignLeft
    {
        get => _logoAlignLeft;
        set => SetProperty(ref _logoAlignLeft, value);
    }

    public bool IsLogoTextVisible => SelectedLogoTypeIndex == 2;

    public string LogoTextValue
    {
        get => _logoText;
        set => SetProperty(ref _logoText, value);
    }

    public bool IsLogoImageActionsVisible => SelectedLogoTypeIndex == 3;

    public int SelectedHomepageTypeIndex
    {
        get => _selectedHomepageTypeIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, 3);
            if (SetProperty(ref _selectedHomepageTypeIndex, clamped))
            {
                RaisePropertyChanged(nameof(IsHomepageBlankSelected));
                RaisePropertyChanged(nameof(IsHomepagePresetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalSelected));
                RaisePropertyChanged(nameof(IsHomepageNetSelected));
                RaisePropertyChanged(nameof(IsHomepageLocalActionsVisible));
                RaisePropertyChanged(nameof(IsHomepageNetVisible));
                RaisePropertyChanged(nameof(IsHomepagePresetVisible));
            }
        }
    }

    public bool IsHomepageBlankSelected
    {
        get => SelectedHomepageTypeIndex == 0;
        set { if (value) SelectedHomepageTypeIndex = 0; }
    }

    public bool IsHomepagePresetSelected
    {
        get => SelectedHomepageTypeIndex == 1;
        set { if (value) SelectedHomepageTypeIndex = 1; }
    }

    public bool IsHomepageLocalSelected
    {
        get => SelectedHomepageTypeIndex == 2;
        set { if (value) SelectedHomepageTypeIndex = 2; }
    }

    public bool IsHomepageNetSelected
    {
        get => SelectedHomepageTypeIndex == 3;
        set { if (value) SelectedHomepageTypeIndex = 3; }
    }

    public bool IsHomepageLocalActionsVisible => SelectedHomepageTypeIndex == 2;

    public bool IsHomepageNetVisible => SelectedHomepageTypeIndex == 3;

    public string HomepageUrl
    {
        get => _homepageUrl;
        set => SetProperty(ref _homepageUrl, value);
    }

    public bool IsHomepagePresetVisible => SelectedHomepageTypeIndex == 1;

    public int SelectedHomepagePresetIndex
    {
        get => _selectedHomepagePresetIndex;
        set => SetProperty(ref _selectedHomepagePresetIndex, Math.Clamp(value, 0, HomepagePresetOptions.Count - 1));
    }

    public bool HasJavaRuntimeEntries => JavaRuntimeEntries.Count > 0;

    public bool IsAutoJavaSelected => _selectedJavaRuntimeKey == "auto";
}
