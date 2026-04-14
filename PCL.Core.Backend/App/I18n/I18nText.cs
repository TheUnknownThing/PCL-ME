namespace PCL.Core.App.I18n;

public sealed record I18nText(
    string Key,
    IReadOnlyList<I18nTextArgument>? Arguments = null)
{
    public static I18nText Plain(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new I18nText(key);
    }

    public static I18nText WithArgs(string key, params I18nTextArgument[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);
        return new I18nText(key, arguments);
    }
}

public sealed record I18nTextArgument(
    string Name,
    string? StringValue = null,
    int? IntValue = null,
    bool? BoolValue = null)
{
    public object? GetValue()
    {
        if (IntValue.HasValue)
        {
            return IntValue.Value;
        }

        if (BoolValue.HasValue)
        {
            return BoolValue.Value;
        }

        return StringValue;
    }

    public static I18nTextArgument String(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new I18nTextArgument(name, StringValue: value);
    }

    public static I18nTextArgument Int(string name, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new I18nTextArgument(name, IntValue: value);
    }

    public static I18nTextArgument Bool(string name, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new I18nTextArgument(name, BoolValue: value);
    }
}
