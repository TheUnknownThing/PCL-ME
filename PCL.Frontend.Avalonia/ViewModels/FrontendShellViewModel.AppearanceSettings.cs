using Avalonia.Media;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public IReadOnlyList<string> DarkModeOptions { get; } =
    [
        "浅色",
        "深色",
        "跟随系统"
    ];

    public IReadOnlyList<string> ThemeColorOptions => FrontendAppearanceService.ThemeColorOptions;

    public IReadOnlyList<string> FontOptions { get; } = FrontendAppearanceService.GetFontOptions();

    public IReadOnlyList<string> HomepagePresetOptions { get; } = HomepagePresetCatalog.Select(item => item.Title).ToArray();

    public int SelectedDarkModeIndex
    {
        get => _selectedDarkModeIndex;
        set => SetProperty(ref _selectedDarkModeIndex, Math.Clamp(value, 0, DarkModeOptions.Count - 1));
    }

    public int SelectedLightColorIndex
    {
        get => _selectedLightColorIndex;
        set
        {
            var normalized = FrontendAppearanceService.NormalizeThemeColorIndex(value, ThemeColorOptions.Count);
            if (_selectedLightColorIndex == normalized)
            {
                return;
            }

            var previousIndex = _selectedLightColorIndex;
            SeedCustomThemeColorIfNeeded(isDarkPalette: false, normalized, previousIndex);
            _selectedLightColorIndex = normalized;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsLightCustomThemeColorEditorVisible));
            RaisePropertyChanged(nameof(IsAnyCustomThemeColorEditorVisible));
            RaisePropertyChanged(nameof(IsLightCustomThemeColorInvalid));
        }
    }

    public int SelectedDarkColorIndex
    {
        get => _selectedDarkColorIndex;
        set
        {
            var normalized = FrontendAppearanceService.NormalizeThemeColorIndex(value, ThemeColorOptions.Count);
            if (_selectedDarkColorIndex == normalized)
            {
                return;
            }

            var previousIndex = _selectedDarkColorIndex;
            SeedCustomThemeColorIfNeeded(isDarkPalette: true, normalized, previousIndex);
            _selectedDarkColorIndex = normalized;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(IsDarkCustomThemeColorEditorVisible));
            RaisePropertyChanged(nameof(IsAnyCustomThemeColorEditorVisible));
            RaisePropertyChanged(nameof(IsDarkCustomThemeColorInvalid));
        }
    }

    public bool IsThemeColorSwitchSupported => FrontendAppearanceService.IsThemeColorSwitchSupported;

    public bool IsThemeColorSwitchUnsupportedNoticeVisible => !IsThemeColorSwitchSupported;

    public bool IsLightCustomThemeColorEditorVisible =>
        IsThemeColorSwitchSupported &&
        FrontendAppearanceService.IsCustomThemeColorSelected(SelectedLightColorIndex);

    public bool IsDarkCustomThemeColorEditorVisible =>
        IsThemeColorSwitchSupported &&
        FrontendAppearanceService.IsCustomThemeColorSelected(SelectedDarkColorIndex);

    public bool IsAnyCustomThemeColorEditorVisible =>
        IsLightCustomThemeColorEditorVisible || IsDarkCustomThemeColorEditorVisible;

    public string CustomThemeColorInputHint => "支持 #RRGGBB，例如 #159E95";

    public string CustomLightThemeColorHex
    {
        get => _customLightThemeColorHex;
        set
        {
            if (SetProperty(ref _customLightThemeColorHex, value))
            {
                RaisePropertyChanged(nameof(CustomLightThemePreviewBrush));
                RaisePropertyChanged(nameof(IsLightCustomThemeColorInvalid));
            }
        }
    }

    public string CustomDarkThemeColorHex
    {
        get => _customDarkThemeColorHex;
        set
        {
            if (SetProperty(ref _customDarkThemeColorHex, value))
            {
                RaisePropertyChanged(nameof(CustomDarkThemePreviewBrush));
                RaisePropertyChanged(nameof(IsDarkCustomThemeColorInvalid));
            }
        }
    }

    public IBrush CustomLightThemePreviewBrush => CreateCustomThemePreviewBrush(CustomLightThemeColorHex, Colors.White);

    public IBrush CustomDarkThemePreviewBrush => CreateCustomThemePreviewBrush(CustomDarkThemeColorHex, Color.Parse("#FF2B2B2B"));

    public bool IsLightCustomThemeColorInvalid =>
        IsLightCustomThemeColorEditorVisible &&
        !FrontendAppearanceService.TryParseCustomThemeColor(CustomLightThemeColorHex, out _);

    public bool IsDarkCustomThemeColorInvalid =>
        IsDarkCustomThemeColorEditorVisible &&
        !FrontendAppearanceService.TryParseCustomThemeColor(CustomDarkThemeColorHex, out _);

    private void SeedCustomThemeColorIfNeeded(bool isDarkPalette, int newIndex, int previousIndex)
    {
        if (!FrontendAppearanceService.IsCustomThemeColorSelected(newIndex))
        {
            return;
        }

        var currentValue = isDarkPalette ? _customDarkThemeColorHex : _customLightThemeColorHex;
        if (FrontendAppearanceService.TryParseCustomThemeColor(currentValue, out _))
        {
            return;
        }

        var seedIndex = FrontendAppearanceService.IsCustomThemeColorSelected(previousIndex)
            ? 0
            : previousIndex;
        var seed = FrontendAppearanceService.GetBuiltInAccentHex(isDarkPalette, seedIndex);
        if (isDarkPalette)
        {
            CustomDarkThemeColorHex = seed;
        }
        else
        {
            CustomLightThemeColorHex = seed;
        }
    }

    private static IBrush CreateCustomThemePreviewBrush(string rawColor, Color fallbackColor)
    {
        var previewColor = FrontendAppearanceService.TryParseCustomThemeColor(rawColor, out var color)
            ? color
            : fallbackColor;
        return new SolidColorBrush(previewColor);
    }

    public double LauncherOpacity
    {
        get => _launcherOpacity;
        set
        {
            var normalized = FrontendAppearanceService.NormalizeLauncherOpacity(value);
            if (SetProperty(ref _launcherOpacity, normalized))
            {
                RaisePropertyChanged(nameof(LauncherOpacityLabel));
            }
        }
    }

    public string LauncherOpacityLabel => FrontendAppearanceService.FormatLauncherOpacityLabel(LauncherOpacity);

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
                RaiseTitleBarAppearanceProperties();
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

    public bool IsLogoLeftVisible => SelectedLogoTypeIndex == 0;

    public bool LogoAlignLeft
    {
        get => _logoAlignLeft;
        set
        {
            if (SetProperty(ref _logoAlignLeft, value))
            {
                RaiseTitleBarAppearanceProperties();
            }
        }
    }

    public bool IsLogoTextVisible => SelectedLogoTypeIndex == 2;

    public string LogoTextValue
    {
        get => _logoText;
        set
        {
            if (SetProperty(ref _logoText, value))
            {
                RaiseTitleBarAppearanceProperties();
            }
        }
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
                RaisePropertyChanged(nameof(ShowLaunchHomepageRefreshAction));
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
