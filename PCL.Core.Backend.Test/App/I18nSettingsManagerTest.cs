using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;
using PCL.Core.App.Configuration;
using PCL.Core.App.I18n;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class I18nSettingsManagerTest
{
    [TestMethod]
    public void Constructor_UsesStoredLocaleWhenValid()
    {
        var provider = new InMemoryConfigProvider();
        provider.SetValue("SystemLocale", "zh_hans");

        var manager = new I18nSettingsManager(provider);

        Assert.AreEqual("zh-Hans", manager.Locale);
    }

    [TestMethod]
    public void SetLocale_NormalizesPersistsAndRaisesOnlyOnEffectiveChange()
    {
        var provider = new InMemoryConfigProvider();
        var manager = new I18nSettingsManager(provider);
        var eventCount = 0;
        var changedLocale = string.Empty;
        manager.LocaleChanged += locale =>
        {
            eventCount++;
            changedLocale = locale;
        };

        Assert.IsTrue(manager.SetLocale("zh_hans"));
        Assert.IsFalse(manager.SetLocale("zh-Hans"));

        Assert.AreEqual("zh-Hans", manager.Locale);
        Assert.AreEqual("zh-Hans", changedLocale);
        Assert.AreEqual(1, eventCount);
        Assert.AreEqual("zh-Hans", provider.GetStoredValue("SystemLocale"));
    }

    private sealed class InMemoryConfigProvider : IConfigProvider
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument = null)
        {
            if (_values.TryGetValue(key, out var rawValue) && rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public void SetValue<T>(string key, T value, object? argument = null)
        {
            _values[key] = value;
        }

        public void Delete(string key, object? argument = null)
        {
            _values.Remove(key);
        }

        public bool Exists(string key, object? argument = null)
        {
            return _values.ContainsKey(key);
        }

        public string? GetStoredValue(string key)
        {
            return _values.TryGetValue(key, out var value) ? value as string : null;
        }
    }
}
