using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal static class FrontendThemeResourceResolver
{
    public static Color GetColor(string resourceKey, string? fallback = null)
    {
        if (TryGetResource(resourceKey, out var resource))
        {
            if (resource is Color color)
            {
                return color;
            }

            if (resource is ISolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return Color.Parse(fallback);
        }

        return Colors.Transparent;
    }

    public static IBrush GetBrush(string resourceKey, string? fallback = null)
    {
        if (TryGetResource(resourceKey, out var resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return Brush.Parse(fallback);
        }

        return Brushes.Transparent;
    }

    public static BoxShadows GetBoxShadows(string resourceKey, string? fallback = null)
    {
        if (TryGetResource(resourceKey, out var resource))
        {
            if (resource is BoxShadows boxShadows)
            {
                return boxShadows;
            }

            if (resource is string boxShadowText)
            {
                return BoxShadows.Parse(boxShadowText);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return BoxShadows.Parse(fallback);
        }

        return default;
    }

    private static bool TryGetResource(string resourceKey, out object? resource)
    {
        var application = Application.Current;
        if (application is null)
        {
            resource = null;
            return false;
        }

        var themeVariant = application.ActualThemeVariant ?? ThemeVariant.Default;
        return application.TryGetResource(resourceKey, themeVariant, out resource)
            || application.TryFindResource(resourceKey, out resource);
    }
}
