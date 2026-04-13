using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class FrontendFontDiagnostics
{
    private const int CjkProbeCodePoint = '汉';

    public static bool ShouldWarnAboutMissingCjkFont(Application app, bool forceWarning)
    {
        if (forceWarning)
        {
            return true;
        }

        if (!app.TryFindResource("LaunchFontFamily", out var resource)
            || resource is not FontFamily launchFontFamily)
        {
            return false;
        }

        return !FontManager.Current.TryMatchCharacter(
            CjkProbeCodePoint,
            FontStyle.Normal,
            FontWeight.Normal,
            FontStretch.Normal,
            launchFontFamily,
            CultureInfo.GetCultureInfo("zh-CN"),
            out _);
    }

    public static async Task ShowMissingCjkFontWarningAsync(FrontendShellActionService shellActionService)
    {
        await shellActionService.ConfirmAsync(
            "Missing CJK System Font",
            "No suitable localized CJK system font was found. Install a Chinese, Japanese, or Korean UI font for text rendering, then restart the launcher. Until this is resolved, CJK text may render incorrectly or with missing glyphs.",
            "Continue",
            isDanger: false);
    }
}
