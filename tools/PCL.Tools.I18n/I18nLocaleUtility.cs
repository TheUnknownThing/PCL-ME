using System.Text;

namespace PCL.Tools.I18n;

public static class I18nLocaleUtility
{
    public static string? NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var rawSegments = locale
            .Trim()
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rawSegments.Length == 0 || rawSegments.Any(segment => !segment.All(char.IsLetterOrDigit)))
        {
            return null;
        }

        var builder = new StringBuilder(locale.Length);
        for (var i = 0; i < rawSegments.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('-');
            }

            var segment = rawSegments[i];
            if (i == 0)
            {
                builder.Append(segment.ToLowerInvariant());
                continue;
            }

            if (segment.Length == 4 && segment.All(char.IsLetter))
            {
                builder.Append(char.ToUpperInvariant(segment[0]));
                builder.Append(segment[1..].ToLowerInvariant());
                continue;
            }

            if ((segment.Length == 2 || segment.Length == 3) && segment.All(char.IsLetter))
            {
                builder.Append(segment.ToUpperInvariant());
                continue;
            }

            builder.Append(segment);
        }

        return builder.ToString();
    }
}
