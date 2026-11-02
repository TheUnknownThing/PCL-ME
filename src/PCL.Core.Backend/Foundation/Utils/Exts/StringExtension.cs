using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PCL.Core.Utils.Exts;

public static class StringConvertExtension
{
    public static object? Convert(string? value, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (targetType == typeof(string)) return value;

        if (value is null)
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null) return null;
            return Activator.CreateInstance(targetType);
        }

        var converter = TypeDescriptor.GetConverter(targetType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            return converter.ConvertFromInvariantString(value);
        }

        if (typeof(IConvertible).IsAssignableFrom(targetType))
        {
            return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        if (targetType.IsEnum) return Enum.Parse(targetType, value, ignoreCase: true);

        var parse = targetType.GetMethod(
            "Parse",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        if (parse is not null) return parse.Invoke(null, [value]);

        throw new NotSupportedException($"Cannot convert a string to type {targetType.FullName}.");
    }

    public static T? Convert<T>(this string? value)
    {
        var obj = Convert(value, typeof(T));
        if (obj is null) return default;
        return (T)obj;
    }
}

public static class StringExtension
{
    public static string? ConvertToString(object? obj)
    {
        if (obj == null) return null;
        if (obj is string s) return s;

        var converter = TypeDescriptor.GetConverter(obj.GetType());
        if (converter.CanConvertTo(typeof(string)))
        {
            object? o = converter.ConvertToInvariantString(obj);
            return o as string;
        }

        if (obj is IFormattable fmt) return fmt.ToString(null, CultureInfo.InvariantCulture);

        return obj.ToString();
    }

    public static string? ConvertToString<T>(this T? value) => ConvertToString((object?)value);

    extension([NotNullWhen(false)] string? value)
    {
        /// <summary>
        /// <see cref="string.IsNullOrEmpty"/> 的扩展方法。
        /// </summary>
        public bool IsNullOrEmpty() => string.IsNullOrEmpty(value);

        /// <summary>
        /// <see cref="string.IsNullOrWhiteSpace"/> 的扩展方法。
        /// </summary>
        public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(value);
    }

    /// <param name="input">文本</param>
    extension(string? input)
    {
        /// <summary>
        /// 替换指定文本中的所有换行符。
        /// </summary>
        /// <param name="replacement">用于替换的文本</param>
        /// <returns>替换后的文本</returns>
        public string ReplaceLineBreak(string replacement = " ")
            => input?.Replace(RegexPatterns.NewLine, replacement) ?? string.Empty;

        /// <summary>
        /// 替换指定文本中所有匹配正则表达式的部分。
        /// </summary>
        /// <param name="regex">正则表达式</param>
        /// <param name="replacement">用于替换的文本</param>
        /// <returns>替换后的文本</returns>
        [return: NotNullIfNotNull(nameof(input))]
        public string? Replace(Regex regex, string replacement)
            => input == null ? null : regex.Replace(input, replacement);

        /// <summary>
        /// 判断指定文本是否能成功匹配正则表达式。
        /// </summary>
        /// <param name="regex">正则表达式</param>
        /// <returns>若匹配成功则为 <c>true</c>，若文本为 <c>null</c> 或匹配不成功则为 <c>false</c></returns>
        public bool IsMatch(Regex regex)
            => input != null && regex.IsMatch(input);
    }

    extension(string str)
    {
        public bool StartsWithF(string prefix, bool ignoreCase = false)
            => str.StartsWith(prefix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public bool EndsWithF(string suffix, bool ignoreCase = false)
            => str.EndsWith(suffix, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public bool ContainsF(string subStr, bool ignoreCase = false)
            => str.Contains(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int IndexOfF(string subStr, bool ignoreCase = false)
            => str.IndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int IndexOfF(string subStr, int startIndex, bool ignoreCase = false)
            => str.IndexOf(subStr, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int LastIndexOfF(string subStr, bool ignoreCase = false)
            => str.LastIndexOf(subStr, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        public int LastIndexOfF(string subStr, int startIndex, bool ignoreCase = false)
            => str.LastIndexOf(subStr, startIndex, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
