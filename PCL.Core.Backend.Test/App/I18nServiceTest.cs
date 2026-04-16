using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;
using PCL.Core.App.Configuration;
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class I18nServiceTest
{
    [TestMethod]
    public void T_FlattensYamlAndFormatsEscapedTemplates()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteLocale(
            "en-US",
            """
            common:
              actions:
                exit: "Exit"
              greeting:
                value: "Hello, {name}! Today is {{ {day} }}."
            """);

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);

        Assert.AreEqual("Exit", service.T("common.actions.exit"));
        Assert.AreEqual(
            "Hello, Alex! Today is { Monday }.",
            service.T(
                "common.greeting.value",
                new Dictionary<string, object?>
                {
                    ["name"] = "Alex",
                    ["day"] = "Monday"
                }));
    }

    [TestMethod]
    public void LocaleChanged_ReloadsTargetSnapshotAndRaisesChanged()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteLocale("en-US", "greeting: \"Hello\"");
        fixture.WriteLocale("zh-Hans", "greeting: \"你好\"");

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);
        var changeCount = 0;
        service.Changed += () => changeCount++;

        Assert.AreEqual("Hello", service.T("greeting"));
        Assert.IsTrue(settingsManager.SetLocale("zh-Hans"));
        Assert.AreEqual("你好", service.T("greeting"));
        Assert.AreEqual(1, changeCount);
    }

    [TestMethod]
    public void ReloadCurrentLocale_BypassesCacheAndRefreshesStrings()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteLocale("en-US", "greeting: \"Hello\"");

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);
        var changeCount = 0;
        service.Changed += () => changeCount++;

        fixture.WriteLocale("en-US", "greeting: \"Updated\"");

        Assert.IsTrue(service.ReloadCurrentLocale());
        Assert.AreEqual("Updated", service.T("greeting"));
        Assert.AreEqual(1, changeCount);
    }

    [TestMethod]
    public void T_MissingKeyLogsWarningWithSchemaPreviewOnce()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteSchema(
            """
            launch:
              profile:
                kinds:
                  authlib: []
                  offline: []
            """);
        fixture.WriteLocale(
            "en-US",
            """
            launch:
              profile:
                kinds:
                  authlib: "Authlib-Injector"
                  offline: "Offline"
            """);

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);
        var warnings = new List<string>();

        void HandleLog(LogLevel level, string message, string? module, Exception? exception)
        {
            if (level == LogLevel.Warning && string.Equals(module, "I18n", StringComparison.Ordinal))
            {
                warnings.Add(message);
            }
        }

        LogWrapper.OnLog += HandleLog;
        try
        {
            Assert.AreEqual("launch.profile.kinds.microsoft", service.T("launch.profile.kinds.microsoft"));
            Assert.AreEqual("launch.profile.kinds.microsoft", service.T("launch.profile.kinds.microsoft"));
        }
        finally
        {
            LogWrapper.OnLog -= HandleLog;
        }

        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "Missing translation key 'launch.profile.kinds.microsoft'");
        StringAssert.Contains(warnings[0], "Expected schema near");
        StringAssert.Contains(warnings[0], "launch");
        StringAssert.Contains(warnings[0], "microsoft");
        StringAssert.Contains(warnings[0], "authlib []");
    }

    private sealed class LocaleFixture : IDisposable
    {
        public LocaleFixture()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-i18n-test-" + Guid.NewGuid().ToString("N"));
            LocaleDirectory = Path.Combine(RootDirectory, "Locales");
            Directory.CreateDirectory(LocaleDirectory);
            MetaDirectory = Path.Combine(LocaleDirectory, "Meta");
            Directory.CreateDirectory(MetaDirectory);
        }

        public string RootDirectory { get; }

        public string LocaleDirectory { get; }

        public string MetaDirectory { get; }

        public void Dispose()
        {
            Directory.Delete(RootDirectory, recursive: true);
        }

        public void WriteLocale(string locale, string content)
        {
            File.WriteAllText(
                Path.Combine(LocaleDirectory, locale + ".yaml"),
                content + Environment.NewLine);
        }

        public void WriteSchema(string content)
        {
            File.WriteAllText(
                Path.Combine(MetaDirectory, "schema.yaml"),
                content + Environment.NewLine);
        }
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
    }
}
