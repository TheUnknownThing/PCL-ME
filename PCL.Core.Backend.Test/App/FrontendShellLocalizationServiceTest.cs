using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Essentials;
using PCL.Core.App.I18n;
using PCL.Frontend.Avalonia.Workflows;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FrontendShellLocalizationServiceTest
{
    [TestMethod]
    public void LocalizeNavigationView_UsesLaunchLabelForSetupLaunchSubpage()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteLocale("en-US", BuildNavigationLocaleYaml("Interface"));

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);
        var navigation = FrontendShellLocalizationService.LocalizeNavigationView(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupLaunch))),
            service);

        Assert.AreEqual("Settings", navigation.CurrentPage.Title);
        Assert.AreEqual("Launch", navigation.CurrentPage.SidebarItemTitle);
        Assert.AreEqual(
            "Launch",
            navigation.SidebarEntries.Single(entry => entry.IsSelected).Title);
    }

    [TestMethod]
    public void LocalizeNavigationView_UsesDistinctLabelForSetupUiSubpage()
    {
        using var fixture = new LocaleFixture();
        fixture.WriteLocale("en-US", BuildNavigationLocaleYaml("Interface"));

        var settingsManager = new I18nSettingsManager(new InMemoryConfigProvider());
        using var service = new I18nService(fixture.LocaleDirectory, settingsManager);
        var navigation = FrontendShellLocalizationService.LocalizeNavigationView(
            LauncherFrontendNavigationService.BuildView(new LauncherFrontendNavigationViewRequest(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Setup, LauncherFrontendSubpageKey.SetupUI))),
            service);

        Assert.AreEqual("Settings", navigation.CurrentPage.Title);
        Assert.AreEqual("Interface", navigation.CurrentPage.SidebarItemTitle);
        Assert.AreEqual(
            "Interface",
            navigation.SidebarEntries.Single(entry => entry.IsSelected).Title);
        Assert.AreNotEqual("Settings", navigation.CurrentPage.SidebarItemTitle);
    }

    private static string BuildNavigationLocaleYaml(string setupUiTitle)
    {
        return $$"""
        shell:
          navigation:
            pages:
              launch:
                title: "Launch"
                summary: "Launch summary"
              download:
                title: "Download"
                summary: "Download summary"
              setup:
                title: "Settings"
                summary: "Settings summary"
              tools:
                title: "Tools"
                summary: "Tools summary"
            subpages:
              setup_launch:
                title: "Launch"
                summary: "Launch settings"
              setup_u_i:
                title: "{{setupUiTitle}}"
                summary: "Appearance settings"
              setup_game_manage:
                title: "Game Management"
                summary: "Game management"
              setup_about:
                title: "About"
                summary: "About summary"
              setup_log:
                title: "Logs"
                summary: "Logs summary"
              setup_feedback:
                title: "Feedback"
                summary: "Feedback summary"
              setup_update:
                title: "Updates"
                summary: "Updates summary"
              setup_java:
                title: "Java"
                summary: "Java summary"
              setup_launcher_misc:
                title: "Launcher Misc"
                summary: "Launcher misc summary"
            sidebar_groups:
              setup: "Settings workspace"
            utilities:
              back: "Back"
              back_target: "Back to {target}"
              task_manager: "Task Manager"
              game_log: "Game Log"
        """;
    }

    private sealed class LocaleFixture : IDisposable
    {
        public LocaleFixture()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-shell-l10n-test-" + Guid.NewGuid().ToString("N"));
            LocaleDirectory = Path.Combine(RootDirectory, "Locales");
            Directory.CreateDirectory(LocaleDirectory);
        }

        public string RootDirectory { get; }

        public string LocaleDirectory { get; }

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
