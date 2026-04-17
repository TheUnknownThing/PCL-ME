using Avalonia.Media;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public IReadOnlyList<string> DarkModeOptions => SetupText.Ui.DarkModeOptions;

    public IReadOnlyList<string> ThemeColorOptions => SetupText.Ui.ThemeColorOptions;

    public IReadOnlyList<string> FontOptions => FrontendAppearanceService.BuildDisplayFontOptions(
        SetupText.Ui.FontOptions.FirstOrDefault());

    public IReadOnlyList<string> HomepagePresetOptions => HomepagePresetCatalog.Select(item => item.Title).ToArray();

    public int SelectedDarkModeIndex
    {
        get => _selectedDarkModeIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, DarkModeOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedDarkModeIndex, normalizedValue);
            }
        }
    }

    public int SelectedLightColorIndex
    {
        get => _selectedLightColorIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, ThemeColorOptions.Count, out var normalizedValue))
            {
                return;
            }

            var normalized = FrontendAppearanceService.NormalizeThemeColorIndex(normalizedValue, ThemeColorOptions.Count);
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
            if (!TryNormalizeSelectionIndex(value, ThemeColorOptions.Count, out var normalizedValue))
            {
                return;
            }

            var normalized = FrontendAppearanceService.NormalizeThemeColorIndex(normalizedValue, ThemeColorOptions.Count);
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

    public string CustomThemeColorInputHint => SetupText.Ui.CustomThemeHint;

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
        set
        {
            if (TryNormalizeSelectionIndex(value, FontOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedGlobalFontIndex, normalizedValue);
            }
        }
    }

    public int SelectedMotdFontIndex
    {
        get => _selectedMotdFontIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, FontOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedMotdFontIndex, normalizedValue);
            }
        }
    }

    public bool BackgroundColorful
    {
        get => _backgroundColorful;
        set => SetProperty(ref _backgroundColorful, value);
    }

    public int SelectedLogoTypeIndex
    {
        get => _selectedLogoTypeIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, 4, out var clamped))
            {
                return;
            }

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
            if (!TryNormalizeSelectionIndex(value, 4, out var clamped))
            {
                return;
            }

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
        set
        {
            if (TryNormalizeSelectionIndex(value, HomepagePresetOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedHomepagePresetIndex, normalizedValue);
            }
        }
    }

    public bool HasJavaRuntimeEntries => JavaRuntimeEntries.Count > 0;

    public bool IsAutoJavaSelected => _selectedJavaRuntimeKey == "auto";
}
