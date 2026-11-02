using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Testing;

internal sealed class DictionaryI18nService(
    IReadOnlyDictionary<string, string>? messages = null,
    string locale = "en-US") : II18nService
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyArgs =
        new Dictionary<string, object?>(0, StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, string> _messages =
        messages ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public string Locale { get; private set; } = locale;

    public IReadOnlyList<string> AvailableLocales => [Locale];

    public event Action? Changed;

    public string T(string key)
    {
        return T(key, EmptyArgs);
    }

    public string T(string key, IReadOnlyDictionary<string, object?> args)
    {
        var template = _messages.TryGetValue(key, out var message) ? message : key;
        var result = template;

        foreach (var (argumentKey, argumentValue) in args)
        {
            result = result.Replace(
                "{" + argumentKey + "}",
                argumentValue?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
        }

        return result;
    }

    public string T(I18nText text)
    {
        return T(
            text.Key,
            text.Arguments?.ToDictionary(
                argument => argument.Name,
                argument => argument.GetValue(),
                StringComparer.Ordinal) ?? EmptyArgs);
    }

    public bool SetLocale(string locale)
    {
        if (string.Equals(Locale, locale, StringComparison.Ordinal))
        {
            return false;
        }

        Locale = locale;
        Changed?.Invoke();
        return true;
    }

    public bool ReloadLocaleFromSettings()
    {
        return false;
    }

    public bool ReloadCurrentLocale()
    {
        Changed?.Invoke();
        return true;
    }
}
