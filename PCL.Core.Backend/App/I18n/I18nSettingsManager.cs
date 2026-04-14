using System.Text;
using PCL.Core.App.Configuration;

namespace PCL.Core.App.I18n;

public interface II18nSettingsManager
{
    string Locale { get; }

    bool SetLocale(string locale);

    event Action<string>? LocaleChanged;
}

public sealed class I18nSettingsManager : II18nSettingsManager
{
    public const string DefaultLocale = "en-US";
    public const string DefaultConfigKey = "SystemLocale";

    private readonly IConfigProvider _configProvider;
    private readonly string _configKey;
    private string _locale;

    public I18nSettingsManager(
        IConfigProvider configProvider,
        string configKey = DefaultConfigKey,
        string fallbackLocale = DefaultLocale)
    {
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(configKey);

        _configProvider = configProvider;
        _configKey = configKey;
        _locale = ReadInitialLocale(configProvider, configKey, fallbackLocale);
    }

    public string Locale => Volatile.Read(ref _locale);

    public event Action<string>? LocaleChanged;

    public bool SetLocale(string locale)
    {
        var normalizedLocale = NormalizeLocale(locale);
        if (normalizedLocale is null)
        {
            return false;
        }

        var currentLocale = Volatile.Read(ref _locale);
        if (string.Equals(currentLocale, normalizedLocale, StringComparison.Ordinal))
        {
            return false;
        }

        _configProvider.SetValue(_configKey, normalizedLocale);
        Volatile.Write(ref _locale, normalizedLocale);
        LocaleChanged?.Invoke(normalizedLocale);
        return true;
    }

    internal static string? NormalizeLocale(string? locale)
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

    private static string ReadInitialLocale(
        IConfigProvider configProvider,
        string configKey,
        string fallbackLocale)
    {
        var normalizedFallback = NormalizeLocale(fallbackLocale)
                                 ?? throw new ArgumentException("Fallback locale is invalid.", nameof(fallbackLocale));

        if (configProvider.GetValue<string>(configKey, out var storedLocale) &&
            NormalizeLocale(storedLocale) is { } normalizedStoredLocale)
        {
            return normalizedStoredLocale;
        }

        return normalizedFallback;
    }
}
