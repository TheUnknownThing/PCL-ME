using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendAppearanceService
{
    private const string CustomThemeColorName = "custom";
    private const string DefaultFontOptionName = "Default font";
    private const string LaunchFontFamilyResourceKey = "LaunchFontFamily";
    private const string LaunchMotdFontFamilyResourceKey = "LaunchMotdFontFamily";
    private const string BundledHarmonyOsSansFontFamily = "avares://PCL.Frontend.Avalonia/Assets/Fonts#HarmonyOS Sans";
    private const string DefaultUiFontFamily =
        $"{BundledHarmonyOsSansFontFamily}, Microsoft YaHei UI, PingFang SC, Hiragino Sans GB, Noto Sans CJK SC, Source Han Sans CN, WenQuanYi Micro Hei, sans-serif";
    private const double LauncherOpacitySliderMinimum = 0d;
    private const double LauncherOpacitySliderMaximum = 600d;
    private const double LauncherWindowOpacityMinimum = 0.4d;
    private const double LauncherWindowOpacityRange = 0.6d;
    private static readonly Lock FontOptionSync = new();
    private static Application? _subscribedApplication;
    private static FrontendAppearanceSelection _currentSelection = new(2, 0, 0, null, null, null, null);
    private static IReadOnlyList<string>? _fontOptions;
    private static bool _isReapplyingForThemeVariantChange;

    public static event Action? AppearanceChanged;

    private static readonly FrontendPaletteDefinition[] LightPalettes =
    [
        new("cat_blue", Color.Parse("#1370F3")),
        new("lemon_cyan", Color.Parse("#159E95")),
        new("grass_green", Color.Parse("#459A44")),
        new("pineapple_yellow", Color.Parse("#C48910")),
        new("oak_brown", Color.Parse("#9A6B42"))
    ];

    private static readonly FrontendPaletteDefinition[] DarkPalettes =
    [
        new("cat_blue", Color.Parse("#33BBFF")),
        new("lemon_cyan", Color.Parse("#50DED4")),
        new("grass_green", Color.Parse("#C4F1C4")),
        new("pineapple_yellow", Color.Parse("#F0C44A")),
        new("oak_brown", Color.Parse("#D8A06C"))
    ];

    public static IReadOnlyList<string> ThemeColorOptions { get; } =
    [
        .. LightPalettes.Select(palette => palette.Name),
        CustomThemeColorName
    ];

    public static IReadOnlyList<string> GetFontOptions()
    {
        lock (FontOptionSync)
        {
            if (_fontOptions is not null)
            {
                return _fontOptions;
            }

            IReadOnlyList<string> systemFonts;
            try
            {
                systemFonts = FontManager.Current.SystemFonts
                    .Select(font => font.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
            catch
            {
                systemFonts = [];
            }

            _fontOptions =
            [
                DefaultFontOptionName,
                .. systemFonts
            ];
            return _fontOptions;
        }
    }

    public static IReadOnlyList<string> BuildDisplayFontOptions(string? defaultFontOptionLabel)
    {
        var options = GetFontOptions();
        if (options.Count == 0 || string.IsNullOrWhiteSpace(defaultFontOptionLabel))
        {
            return options;
        }

        if (options.Count == 1)
        {
            return [defaultFontOptionLabel];
        }

        var displayOptions = new string[options.Count];
        displayOptions[0] = defaultFontOptionLabel;
        for (var index = 1; index < options.Count; index++)
        {
            displayOptions[index] = options[index];
        }

        return displayOptions;
    }

    public static int CustomThemeColorIndex => ThemeColorOptions.Count - 1;

    public static bool IsThemeColorSwitchSupported
    {
        get
        {
#if PCL_ENABLE_THEME_COLOR_SWITCH
            return true;
#else
            return false;
#endif
        }
    }

    public static int NormalizeThemeColorIndex(int index, int optionCount)
    {
        if (!IsThemeColorSwitchSupported || optionCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, optionCount - 1);
    }

    public static bool IsCustomThemeColorSelected(int index)
    {
        return NormalizeThemeColorIndex(index, ThemeColorOptions.Count) == CustomThemeColorIndex;
    }

    public static string GetBuiltInAccentHex(bool useDarkPalette, int index)
    {
        var palettes = useDarkPalette ? DarkPalettes : LightPalettes;
        var paletteIndex = Math.Clamp(index, 0, palettes.Length - 1);
        return FormatCustomThemeColor(palettes[paletteIndex].Accent);
    }

    public static bool TryParseCustomThemeColor(string? rawValue, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length != 6 ||
            !uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        color = Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
        return true;
    }

    public static string FormatCustomThemeColor(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static int MapFontConfigValueToIndex(string? value)
    {
        var normalizedValue = NormalizeStoredFontValue(value);
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return 0;
        }

        var options = GetFontOptions();
        for (var index = 1; index < options.Count; index++)
        {
            if (string.Equals(options[index], normalizedValue, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return 0;
    }

    public static string MapFontIndexToConfigValue(int index, IReadOnlyList<string> fontOptions)
    {
        if (fontOptions.Count == 0)
        {
            return string.Empty;
        }

        var normalizedIndex = Math.Clamp(index, 0, fontOptions.Count - 1);
        return normalizedIndex == 0 ? string.Empty : fontOptions[normalizedIndex];
    }

    public static double NormalizeLauncherOpacity(double value)
    {
        return Math.Clamp(value, LauncherOpacitySliderMinimum, LauncherOpacitySliderMaximum);
    }

    public static string FormatLauncherOpacityLabel(double value)
    {
        var normalized = NormalizeLauncherOpacity(value);
        var percentage = normalized <= LauncherOpacitySliderMinimum
            ? 0d
            : (normalized / LauncherOpacitySliderMaximum) * 100d;
        return $"{Math.Round(percentage)}%";
    }

    public static double MapLauncherOpacityToWindowOpacity(double value)
    {
        var normalized = NormalizeLauncherOpacity(value);
        return LauncherWindowOpacityMinimum + ((normalized / LauncherOpacitySliderMaximum) * LauncherWindowOpacityRange);
    }

    public static void ApplyStoredAppearance(Application application, FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var darkModeIndex = 2;
        var lightColorIndex = 0;
        var darkColorIndex = 0;
        string? lightCustomColor = null;
        string? darkCustomColor = null;
        string? globalFontConfigValue = null;
        string? motdFontConfigValue = null;

        if (File.Exists(runtimePaths.SharedConfigPath))
        {
            var provider = new JsonFileProvider(runtimePaths.SharedConfigPath);
            if (provider.Exists("UiDarkMode"))
            {
                darkModeIndex = provider.Get<int>("UiDarkMode");
            }

            if (provider.Exists("UiLightColor"))
            {
                lightColorIndex = provider.Get<int>("UiLightColor");
            }

            if (provider.Exists("UiDarkColor"))
            {
                darkColorIndex = provider.Get<int>("UiDarkColor");
            }

            if (provider.Exists("UiLightColorCustom"))
            {
                lightCustomColor = provider.Get<string>("UiLightColorCustom");
            }

            if (provider.Exists("UiDarkColorCustom"))
            {
                darkCustomColor = provider.Get<string>("UiDarkColorCustom");
            }
        }

        if (File.Exists(runtimePaths.LocalConfigPath))
        {
            var provider = runtimePaths.OpenLocalConfigProvider();
            if (provider.Exists("UiFont"))
            {
                globalFontConfigValue = provider.Get<string>("UiFont");
            }

            if (provider.Exists("UiMotdFont"))
            {
                motdFontConfigValue = provider.Get<string>("UiMotdFont");
            }
        }

        ApplyAppearance(application, new FrontendAppearanceSelection(
            darkModeIndex,
            lightColorIndex,
            darkColorIndex,
            lightCustomColor,
            darkCustomColor,
            globalFontConfigValue,
            motdFontConfigValue));
    }

    public static void ApplyAppearance(Application? application, FrontendAppearanceSelection selection)
    {
        if (application is null)
        {
            return;
        }

        EnsureThemeVariantTracking(application);
        _currentSelection = selection;
        application.RequestedThemeVariant = selection.DarkModeIndex switch
        {
            0 => ThemeVariant.Light,
            1 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        var effectiveVariant = ResolveEffectiveThemeVariant(application, selection.DarkModeIndex);
        var useDarkPalette = effectiveVariant == ThemeVariant.Dark;

        var accent = ResolveAccent(useDarkPalette, selection);
        var palette = useDarkPalette
            ? CreateDarkPalette(accent)
            : CreateLightPalette(accent);

        ApplyFontResources(application, selection);
        ApplyPaletteResources(application, palette, useDarkPalette);
        ApplyInputResources(application, palette, useDarkPalette);
        ApplyFluentAccentResources(application, palette, useDarkPalette);
        AppearanceChanged?.Invoke();
    }

    public static void ReapplyCurrentAppearance(Application? application)
    {
        if (application is null)
        {
            return;
        }

        if (_isReapplyingForThemeVariantChange)
        {
            return;
        }

        try
        {
            _isReapplyingForThemeVariantChange = true;
            ApplyAppearance(application, _currentSelection);
        }
        finally
        {
            _isReapplyingForThemeVariantChange = false;
        }
    }

    public static ThemeVariant ResolveCurrentThemeVariant(Application? application)
    {
        if (application is null)
        {
            return ThemeVariant.Light;
        }

        return ResolveEffectiveThemeVariant(application, _currentSelection.DarkModeIndex);
    }

    public static bool IsDarkTheme(Application? application)
    {
        return ResolveCurrentThemeVariant(application) == ThemeVariant.Dark;
    }

    private static FrontendResolvedPalette CreateLightPalette(Color accent)
    {
        var foreground = Color.Parse("#343D4A");
        var accentStrong = Mix(accent, Colors.Black, 0.18);
        var accentHover = Mix(accent, Colors.White, 0.20);
        var accentFaint = Mix(accent, Colors.White, 0.56);
        var border = Mix(accent, Colors.White, 0.80);
        var hover = Mix(accent, Colors.White, 0.87);
        var selected = Mix(accent, Colors.White, 0.92);

        return new FrontendResolvedPalette(
            Foreground: foreground,
            AccentStrong: accentStrong,
            Accent: accent,
            AccentHover: accentHover,
            AccentFaint: accentFaint,
            SurfaceBorder: border,
            SurfaceHover: hover,
            SurfaceSelected: selected,
            BackgroundPrimary: accentFaint,
            BackgroundOverlay: WithAlpha(hover, 0xBE),
            EntryHoverBackground: hover,
            EntrySelectedBackground: selected,
            EntrySelectedHoverBackground: border,
            EntrySecondarySelected: Mix(accent, foreground, 0.55),
            EntrySecondaryIdle: Color.Parse("#7D8897"),
            EntryChevronIdle: Mix(accent, Colors.White, 0.45),
            TitleBarForeground: ChooseOverlayForeground(accent, preferWhite: true),
            TitleBarHoverBackground: default,
            TitleBarSelectionBackground: default);
    }

    private static FrontendResolvedPalette CreateDarkPalette(Color accent)
    {
        var foreground = Color.Parse("#EBEBEB");
        var surfaceBase = Color.Parse("#202325");
        var accentStrong = Mix(accent, Colors.White, 0.14);
        var accentHover = Mix(accent, Colors.White, 0.10);
        var accentFaint = Mix(accent, surfaceBase, 0.22);
        var border = Mix(accent, surfaceBase, 0.58);
        var hover = Mix(accent, surfaceBase, 0.70);
        var selected = Mix(accent, surfaceBase, 0.80);

        return new FrontendResolvedPalette(
            Foreground: foreground,
            AccentStrong: accentStrong,
            Accent: accent,
            AccentHover: accentHover,
            AccentFaint: accentFaint,
            SurfaceBorder: border,
            SurfaceHover: hover,
            SurfaceSelected: selected,
            BackgroundPrimary: accentFaint,
            BackgroundOverlay: Color.Parse("#B4363636"),
            EntryHoverBackground: hover,
            EntrySelectedBackground: selected,
            EntrySelectedHoverBackground: border,
            EntrySecondarySelected: Mix(accent, Colors.White, 0.42),
            EntrySecondaryIdle: Color.Parse("#A7B2BE"),
            EntryChevronIdle: Mix(accent, Colors.White, 0.40),
            TitleBarForeground: accentStrong,
            TitleBarHoverBackground: default,
            TitleBarSelectionBackground: default);
    }

    private static void ApplyPaletteResources(Application application, FrontendResolvedPalette palette, bool useDarkPalette)
    {
        var useLightOverlay = palette.TitleBarForeground == Colors.White;
        var titleBarHover = useDarkPalette
            ? WithAlpha(palette.TitleBarForeground, 0x1A)
            : WithAlpha(
                palette.TitleBarForeground,
                useLightOverlay ? (byte)0x32 : (byte)0x14);
        var titleBarSelection = useDarkPalette
            ? WithAlpha(palette.TitleBarForeground, 0x26)
            : WithAlpha(
                palette.TitleBarForeground,
                useLightOverlay ? (byte)0x46 : (byte)0x20);
        var titleBarBackground = useDarkPalette
            ? Mix(palette.Accent, Color.Parse("#FF050608"), 0.92)
            : palette.AccentStrong;
        var titleBarBadgeBackground = useDarkPalette
            ? Mix(palette.Accent, titleBarBackground, 0.82)
            : palette.TitleBarForeground;
        var titleBarBadgeForeground = useDarkPalette
            ? palette.TitleBarForeground
            : palette.AccentStrong;
        var cardBase = useDarkPalette
            ? Color.Parse("#38262626")
            : WithAlpha(Colors.White, 0xD2);
        var cardHover = useDarkPalette
            ? Color.Parse("#562E2E2E")
            : WithAlpha(Mix(palette.SurfaceSelected, Colors.White, 0.08), 0xD8);
        var cardHoverBorder = useDarkPalette
            ? Color.Parse("#3AB8C1CA")
            : WithAlpha(palette.AccentFaint, 0x78);
        var cardEdge = useDarkPalette
            ? Color.Parse("#38FFFFFF")
            : Color.Parse("#66FFFFFF");
        var cardSheenStart = useDarkPalette
            ? Color.Parse("#22FFFFFF")
            : Color.Parse("#26FFFFFF");
        var cardSheenEnd = useDarkPalette
            ? Color.Parse("#05FFFFFF")
            : Color.Parse("#08FFFFFF");
        var surfaceBorderBrush = useDarkPalette
            ? WithAlpha(Mix(palette.SurfaceBorder, Color.Parse("#FF3A3A3A"), 0.60), 0x68)
            : palette.SurfaceBorder;

        SetBrushResource(application, "ColorBrush1", palette.Foreground);
        SetBrushResource(application, "ColorBrush2", palette.AccentStrong);
        SetBrushResource(application, "ColorBrush3", palette.Accent);
        SetBrushResource(application, "ColorBrush4", palette.AccentHover);
        SetBrushResource(application, "ColorBrush5", palette.AccentFaint);
        SetBrushResource(application, "ColorBrush6", surfaceBorderBrush);
        SetBrushResource(application, "ColorBrush7", palette.SurfaceHover);
        SetBrushResource(application, "ColorBrush8", palette.SurfaceSelected);
        SetBrushResource(application, "ColorBrushBg0", palette.BackgroundPrimary);
        SetBrushResource(application, "ColorBrushBg1", palette.BackgroundOverlay);
        SetBrushResource(application, "ColorBrushEntryHoverBackground", palette.EntryHoverBackground);
        SetBrushResource(application, "ColorBrushEntrySelectedBackground", palette.EntrySelectedBackground);
        SetBrushResource(application, "ColorBrushEntrySelectedHoverBackground", palette.EntrySelectedHoverBackground);
        SetBrushResource(application, "ColorBrushEntrySecondarySelected", palette.EntrySecondarySelected);
        SetBrushResource(application, "ColorBrushEntrySecondaryIdle", palette.EntrySecondaryIdle);
        SetBrushResource(application, "ColorBrushEntryChevronIdle", palette.EntryChevronIdle);
        SetBrushResource(application, "ColorBrushTitleBarBackground", titleBarBackground);
        SetBrushResource(application, "ColorBrushTitleBarForeground", palette.TitleBarForeground);
        SetBrushResource(application, "ColorBrushTitleBarHoverBackground", titleBarHover);
        SetBrushResource(application, "ColorBrushTitleBarSelectionBackground", titleBarSelection);
        SetBrushResource(application, "ColorBrushTitleBarSelectedForeground", palette.TitleBarForeground);
        SetBrushResource(application, "ColorBrushTitleBarBadgeBackground", titleBarBadgeBackground);
        SetBrushResource(application, "ColorBrushTitleBarBadgeForeground", titleBarBadgeForeground);
        SetBrushResource(application, "ColorBrushMyCard", cardBase);
        SetBrushResource(application, "ColorBrushMyCardMouseOver", cardHover);
        SetBrushResource(application, "ColorBrushMyCardBorderMouseOver", cardHoverBorder);
        SetBrushResource(application, "ColorBrushMyCardEdge", cardEdge);

        SetColorResource(application, "ColorObject1", palette.Foreground);
        SetColorResource(application, "ColorObject2", palette.AccentStrong);
        SetColorResource(application, "ColorObject3", palette.Accent);
        SetColorResource(application, "ColorObject4", palette.AccentHover);
        SetColorResource(application, "ColorObject5", palette.AccentFaint);
        SetColorResource(application, "ColorObject6", palette.SurfaceBorder);
        SetColorResource(application, "ColorObject7", palette.SurfaceHover);
        SetColorResource(application, "ColorObject8", palette.SurfaceSelected);
        SetColorResource(application, "ColorObjectBg0", palette.BackgroundPrimary);
        SetColorResource(application, "ColorObjectBg1", palette.BackgroundOverlay);
        SetColorResource(application, "ColorObjectMyCardSheenStart", cardSheenStart);
        SetColorResource(application, "ColorObjectMyCardSheenEnd", cardSheenEnd);
    }

    private static void ApplyInputResources(Application application, FrontendResolvedPalette palette, bool useDarkPalette)
    {
        var disabledBackground = useDarkPalette ? Color.Parse("#FF4A4A4A") : Color.Parse("#FFEBEBEB");
        var disabledForeground = useDarkPalette ? Color.Parse("#FFB0B0B0") : Color.Parse("#FFA6A6A6");
        var dropdownBackground = useDarkPalette ? Color.Parse("#FF2B2B2B") : Colors.White;
        var transparent = Color.Parse("#00FFFFFF");
        var inputBorder = useDarkPalette
            ? WithAlpha(Mix(palette.AccentFaint, Color.Parse("#FF3B3B3B"), 0.58), 0x66)
            : palette.AccentFaint;
        var inputBorderHover = useDarkPalette
            ? WithAlpha(Mix(palette.AccentHover, Color.Parse("#FF3B3B3B"), 0.46), 0x78)
            : palette.AccentHover;
        var inputBorderFocus = useDarkPalette
            ? WithAlpha(Mix(palette.Accent, Color.Parse("#FF444444"), 0.34), 0x88)
            : palette.Accent;
        var inputBorderPressed = useDarkPalette
            ? WithAlpha(Mix(palette.Accent, Color.Parse("#FF3E3E3E"), 0.42), 0x80)
            : palette.Accent;

        SetBrushResource(application, "ComboBoxBackground", WithAlpha(useDarkPalette ? Color.Parse("#FF5A5A5A") : Colors.White, 0x55));
        SetBrushResource(application, "ComboBoxBackgroundPointerOver", palette.SurfaceHover);
        SetBrushResource(application, "ComboBoxBackgroundPressed", palette.SurfaceHover);
        SetBrushResource(application, "ComboBoxBackgroundDisabled", disabledBackground);
        SetBrushResource(application, "ComboBoxBackgroundUnfocused", dropdownBackground);
        SetBrushResource(application, "ComboBoxBackgroundBorderBrushFocused", inputBorderFocus);
        SetBrushResource(application, "ComboBoxBackgroundBorderBrushUnfocused", inputBorder);
        SetBrushResource(application, "ComboBoxBorderBrush", inputBorder);
        SetBrushResource(application, "ComboBoxBorderBrushPointerOver", inputBorderHover);
        SetBrushResource(application, "ComboBoxBorderBrushPressed", inputBorderPressed);
        SetBrushResource(application, "ComboBoxBorderBrushDisabled", Color.Parse("#FFCCCCCC"));
        SetBrushResource(application, "ComboBoxForeground", palette.Foreground);
        SetBrushResource(application, "ComboBoxForegroundDisabled", disabledForeground);
        SetBrushResource(application, "ComboBoxForegroundFocused", palette.Foreground);
        SetBrushResource(application, "ComboBoxForegroundFocusedPressed", palette.Foreground);
        SetBrushResource(application, "ComboBoxPlaceHolderForeground", palette.EntrySecondaryIdle);
        SetBrushResource(application, "ComboBoxPlaceHolderForegroundFocusedPressed", palette.EntrySecondaryIdle);
        SetBrushResource(application, "ComboBoxDropDownGlyphForeground", palette.EntrySecondaryIdle);
        SetBrushResource(application, "ComboBoxEditableDropDownGlyphForeground", palette.EntrySecondaryIdle);
        SetBrushResource(application, "ComboBoxDropDownGlyphForegroundDisabled", disabledForeground);
        SetBrushResource(application, "ComboBoxDropDownGlyphForegroundFocused", palette.Accent);
        SetBrushResource(application, "ComboBoxDropDownGlyphForegroundFocusedPressed", palette.AccentStrong);
        SetBrushResource(application, "ComboBoxDropDownBackground", dropdownBackground);
        SetBrushResource(application, "ComboBoxDropDownForeground", palette.Foreground);
        SetBrushResource(application, "ComboBoxDropDownBorderBrush", inputBorder);
        SetBrushResource(application, "ComboBoxDropDownBackgroundPointerOver", palette.SurfaceSelected);
        SetBrushResource(application, "ComboBoxDropDownBackgroundPointerPressed", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxFocusedDropDownBackgroundPointerOver", palette.SurfaceSelected);
        SetBrushResource(application, "ComboBoxFocusedDropDownBackgroundPointerPressed", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxItemForeground", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundPointerOver", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundPressed", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundDisabled", disabledForeground);
        SetBrushResource(application, "ComboBoxItemForegroundSelected", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundSelectedUnfocused", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundSelectedPressed", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundSelectedPointerOver", palette.Foreground);
        SetBrushResource(application, "ComboBoxItemForegroundSelectedDisabled", disabledForeground);
        SetBrushResource(application, "ComboBoxItemBackground", transparent);
        SetBrushResource(application, "ComboBoxItemBackgroundPointerOver", palette.SurfaceSelected);
        SetBrushResource(application, "ComboBoxItemBackgroundPressed", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxItemBackgroundDisabled", transparent);
        SetBrushResource(application, "ComboBoxItemBackgroundSelected", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxItemBackgroundSelectedUnfocused", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxItemBackgroundSelectedPressed", palette.SurfaceBorder);
        SetBrushResource(application, "ComboBoxItemBackgroundSelectedPointerOver", palette.SurfaceHover);
        SetBrushResource(application, "ComboBoxItemBackgroundSelectedDisabled", disabledBackground);
        SetBrushResource(application, "ComboBoxItemBorderBrush", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushPointerOver", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushPressed", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushDisabled", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushSelected", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushSelectedUnfocused", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushSelectedPressed", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushSelectedPointerOver", transparent);
        SetBrushResource(application, "ComboBoxItemBorderBrushSelectedDisabled", transparent);

        SetBrushResource(application, "TextFieldBackground", WithAlpha(useDarkPalette ? Color.Parse("#FF5A5A5A") : Colors.White, 0x55));
        SetBrushResource(application, "TextFieldBackgroundPointerOver", palette.SurfaceHover);
        SetBrushResource(application, "TextFieldBackgroundFocused", palette.SurfaceHover);
        SetBrushResource(application, "TextFieldBackgroundDisabled", disabledBackground);
        SetBrushResource(application, "TextFieldBorderBrush", inputBorder);
        SetBrushResource(application, "TextFieldBorderBrushPointerOver", inputBorderHover);
        SetBrushResource(application, "TextFieldBorderBrushFocused", inputBorderFocus);
        SetBrushResource(application, "TextFieldBorderBrushDisabled", Color.Parse("#FFCCCCCC"));
        SetBrushResource(application, "TextFieldForeground", palette.Foreground);
        SetBrushResource(application, "TextFieldForegroundDisabled", disabledForeground);
        SetBrushResource(application, "TextFieldSelectionBrush", palette.Accent);
    }

    private static void ApplyFluentAccentResources(Application application, FrontendResolvedPalette palette, bool useDarkPalette)
    {
        var accent = palette.Accent;
        var accentDark1 = Mix(accent, Colors.Black, 0.14);
        var accentDark2 = Mix(accent, Colors.Black, 0.25);
        var accentDark3 = Mix(accent, Colors.Black, 0.36);
        var accentLight1 = Mix(accent, Colors.White, 0.16);
        var accentLight2 = Mix(accent, Colors.White, 0.30);
        var accentLight3 = Mix(accent, Colors.White, 0.42);
        var disabledBase = useDarkPalette ? Color.Parse("#FF4A4A4A") : Color.Parse("#FFEBEBEB");
        var disabledAccent = WithAlpha(Mix(accent, disabledBase, 0.68), 0xA0);
        var sliderTrackFill = useDarkPalette
            ? WithAlpha(palette.SurfaceBorder, 0x78)
            : WithAlpha(palette.SurfaceBorder, 0xCC);
        var checkBoxActiveFill = useDarkPalette
            ? Mix(accent, Colors.White, 0.18)
            : accent;
        var checkBoxActiveFillPointerOver = useDarkPalette
            ? Mix(accent, Colors.White, 0.28)
            : accentLight1;
        var checkBoxActiveFillPressed = useDarkPalette
            ? Mix(accent, Colors.White, 0.12)
            : accentDark1;
        var checkBoxActiveGlyph = useDarkPalette
            ? Color.Parse("#FF09141C")
            : ChooseOverlayForeground(checkBoxActiveFill, preferWhite: true);

        SetColorResource(application, "SystemAccentColor", accent);
        SetColorResource(application, "SystemAccentColorDark1", accentDark1);
        SetColorResource(application, "SystemAccentColorDark2", accentDark2);
        SetColorResource(application, "SystemAccentColorDark3", accentDark3);
        SetColorResource(application, "SystemAccentColorLight1", accentLight1);
        SetColorResource(application, "SystemAccentColorLight2", accentLight2);
        SetColorResource(application, "SystemAccentColorLight3", accentLight3);

        SetBrushResource(application, "SystemControlBackgroundAccentBrush", accentDark1);
        SetBrushResource(application, "SystemControlForegroundAccentBrush", accentDark2);
        SetBrushResource(application, "SystemControlHighlightAccentBrush", accent);
        SetBrushResource(application, "SystemControlHighlightAltAccentBrush", accentLight1);
        SetBrushResource(application, "SystemControlHighlightListAccentLowBrush", accentLight2);
        SetBrushResource(application, "SystemControlHighlightListAccentMediumBrush", accentLight1);
        SetBrushResource(application, "SystemControlHighlightListAccentHighBrush", accent);
        SetBrushResource(application, "SystemControlHighlightAltListAccentLowBrush", accentLight3);
        SetBrushResource(application, "SystemControlHighlightAltListAccentMediumBrush", accentLight2);
        SetBrushResource(application, "SystemControlHighlightAltListAccentHighBrush", accentLight1);
        SetBrushResource(application, "SystemControlDisabledAccentBrush", disabledAccent);
        SetBrushResource(application, "SystemControlHighlightAccentRevealBackgroundBrush", accent);
        SetBrushResource(application, "SystemControlHighlightAccent2RevealBackgroundBrush", accentLight1);
        SetBrushResource(application, "SystemControlHighlightAccent3RevealBackgroundBrush", accentLight2);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillChecked", checkBoxActiveFill);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillCheckedPointerOver", checkBoxActiveFillPointerOver);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillCheckedPressed", checkBoxActiveFillPressed);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillIndeterminate", checkBoxActiveFill);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillIndeterminatePointerOver", checkBoxActiveFillPointerOver);
        SetBrushResource(application, "CheckBoxCheckBackgroundFillIndeterminatePressed", checkBoxActiveFillPressed);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeChecked", checkBoxActiveFill);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeCheckedPointerOver", checkBoxActiveFillPointerOver);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeCheckedPressed", checkBoxActiveFillPressed);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeIndeterminate", checkBoxActiveFill);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", checkBoxActiveFillPointerOver);
        SetBrushResource(application, "CheckBoxCheckBackgroundStrokeIndeterminatePressed", checkBoxActiveFillPressed);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundChecked", checkBoxActiveGlyph);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundCheckedPointerOver", checkBoxActiveGlyph);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundCheckedPressed", checkBoxActiveGlyph);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundIndeterminate", checkBoxActiveGlyph);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundIndeterminatePointerOver", checkBoxActiveGlyph);
        SetBrushResource(application, "CheckBoxCheckGlyphForegroundIndeterminatePressed", checkBoxActiveGlyph);

        SetBrushResource(application, "SliderThumbBackground", accent);
        SetBrushResource(application, "SliderThumbBackgroundPointerOver", accentLight1);
        SetBrushResource(application, "SliderThumbBackgroundPressed", accentDark1);
        SetBrushResource(application, "SliderThumbBackgroundDisabled", disabledAccent);
        SetBrushResource(application, "SliderTrackValueFill", accent);
        SetBrushResource(application, "SliderTrackValueFillPointerOver", accentLight1);
        SetBrushResource(application, "SliderTrackValueFillPressed", accentDark1);
        SetBrushResource(application, "SliderTrackValueFillDisabled", disabledAccent);
        SetBrushResource(application, "SliderTrackFill", sliderTrackFill);
        SetBrushResource(application, "SliderTrackFillPointerOver", WithAlpha(palette.SurfaceHover, 0xD8));
        SetBrushResource(application, "SliderTrackFillPressed", WithAlpha(palette.SurfaceHover, 0xEE));
        SetBrushResource(application, "SliderTrackFillDisabled", WithAlpha(disabledBase, 0xA0));
        SetBrushResource(application, "SliderTickBarFill", accentDark2);
        SetBrushResource(application, "SliderTickBarFillDisabled", disabledAccent);
        SetBrushResource(application, "SliderInlineTickBarFill", accentDark1);
    }

    private static void SetBrushResource(Application application, string key, Color color)
    {
        application.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetFontFamilyResource(Application application, string key, string fontFamily)
    {
        application.Resources[key] = new FontFamily(fontFamily);
    }

    private static void SetColorResource(Application application, string key, Color color)
    {
        application.Resources[key] = color;
    }

    private static Color ResolveAccent(bool useDarkPalette, FrontendAppearanceSelection selection)
    {
        var palettes = useDarkPalette ? DarkPalettes : LightPalettes;
        var optionIndex = NormalizeThemeColorIndex(
            useDarkPalette ? selection.DarkColorIndex : selection.LightColorIndex,
            ThemeColorOptions.Count);

        if (optionIndex == CustomThemeColorIndex &&
            TryParseCustomThemeColor(
                useDarkPalette ? selection.DarkCustomColorHex : selection.LightCustomColorHex,
                out var customAccent))
        {
            return customAccent;
        }

        var builtInIndex = Math.Clamp(optionIndex, 0, palettes.Length - 1);
        return palettes[builtInIndex].Accent;
    }

    private static void ApplyFontResources(Application application, FrontendAppearanceSelection selection)
    {
        SetFontFamilyResource(
            application,
            LaunchFontFamilyResourceKey,
            ResolveFontFamily(selection.GlobalFontConfigValue));
        SetFontFamilyResource(
            application,
            LaunchMotdFontFamilyResourceKey,
            ResolveFontFamily(selection.MotdFontConfigValue));
    }

    private static string ResolveFontFamily(string? configValue)
    {
        var normalizedValue = NormalizeStoredFontValue(configValue);
        return string.IsNullOrWhiteSpace(normalizedValue)
            ? DefaultUiFontFamily
            : $"{normalizedValue}, {DefaultUiFontFamily}";
    }

    private static string NormalizeStoredFontValue(string? configValue)
    {
        var trimmed = configValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || string.Equals(trimmed, "MiSans", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return trimmed switch
        {
            "SourceHanSansCN-Regular" => FindInstalledFontName("Source Han Sans CN", "Source Han Sans SC", "Noto Sans CJK SC"),
            "LXGW WenKai" => FindInstalledFontName("LXGW WenKai", "LXGW WenKai GB"),
            "JetBrains Mono" => FindInstalledFontName("JetBrains Mono", "JetBrainsMono Nerd Font", "JetBrains Mono NL"),
            _ => FindInstalledFontName(trimmed)
        } ?? string.Empty;
    }

    private static string? FindInstalledFontName(params string[] candidateNames)
    {
        var options = GetFontOptions();
        foreach (var candidateName in candidateNames)
        {
            for (var index = 1; index < options.Count; index++)
            {
                if (string.Equals(options[index], candidateName, StringComparison.OrdinalIgnoreCase))
                {
                    return options[index];
                }
            }
        }

        return null;
    }

    private static void EnsureThemeVariantTracking(Application application)
    {
        if (ReferenceEquals(_subscribedApplication, application))
        {
            return;
        }

        if (_subscribedApplication is not null)
        {
            _subscribedApplication.ActualThemeVariantChanged -= OnApplicationActualThemeVariantChanged;
        }

        application.ActualThemeVariantChanged += OnApplicationActualThemeVariantChanged;
        _subscribedApplication = application;
    }

    private static void OnApplicationActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ReapplyCurrentAppearance(sender as Application);
    }

    private static ThemeVariant ResolveEffectiveThemeVariant(Application application, int darkModeIndex)
    {
        return darkModeIndex switch
        {
            0 => ThemeVariant.Light,
            1 => ThemeVariant.Dark,
            _ => application.PlatformSettings?.GetColorValues().ThemeVariant switch
            {
                PlatformThemeVariant.Dark => ThemeVariant.Dark,
                PlatformThemeVariant.Light => ThemeVariant.Light,
                _ => application.ActualThemeVariant == ThemeVariant.Dark
                    ? ThemeVariant.Dark
                    : ThemeVariant.Light
            }
        };
    }

    private static Color Mix(Color left, Color right, double rightWeight)
    {
        var clamped = Math.Clamp(rightWeight, 0, 1);
        var leftWeight = 1 - clamped;
        return Color.FromArgb(
            (byte)Math.Round(left.A * leftWeight + right.A * clamped),
            (byte)Math.Round(left.R * leftWeight + right.R * clamped),
            (byte)Math.Round(left.G * leftWeight + right.G * clamped),
            (byte)Math.Round(left.B * leftWeight + right.B * clamped));
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color ChooseOverlayForeground(Color accent, bool preferWhite)
    {
        var whiteContrast = GetContrastRatio(Colors.White, accent);
        var blackContrast = GetContrastRatio(Colors.Black, accent);

        if (preferWhite)
        {
            // Light themes keep the title bar closer to the original white styling unless
            // black is overwhelmingly better for readability.
            return blackContrast >= 9.5 && whiteContrast < 2.1
                ? Colors.Black
                : Colors.White;
        }

        return blackContrast > whiteContrast * 2.2
            ? Colors.Black
            : Colors.White;
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        static double ToLinear(byte channel)
        {
            var normalized = channel / 255d;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * ToLinear(color.R)
             + 0.7152 * ToLinear(color.G)
             + 0.0722 * ToLinear(color.B);
    }

    private readonly record struct FrontendPaletteDefinition(string Name, Color Accent);

    private readonly record struct FrontendResolvedPalette(
        Color Foreground,
        Color AccentStrong,
        Color Accent,
        Color AccentHover,
        Color AccentFaint,
        Color SurfaceBorder,
        Color SurfaceHover,
        Color SurfaceSelected,
        Color BackgroundPrimary,
        Color BackgroundOverlay,
        Color EntryHoverBackground,
        Color EntrySelectedBackground,
        Color EntrySelectedHoverBackground,
        Color EntrySecondarySelected,
        Color EntrySecondaryIdle,
        Color EntryChevronIdle,
        Color TitleBarForeground,
        Color TitleBarHoverBackground,
        Color TitleBarSelectionBackground);
}

internal readonly record struct FrontendAppearanceSelection(
    int DarkModeIndex,
    int LightColorIndex,
    int DarkColorIndex,
    string? LightCustomColorHex,
    string? DarkCustomColorHex,
    string? GlobalFontConfigValue,
    string? MotdFontConfigValue);
