using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Tools.I18n;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class I18nYamlEditorTest
{
    [TestMethod]
    public void SetLocaleValueCreatesMissingLeafAndReadReturnsUpdatedValue()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
            """);
        fixture.WriteSchema(
            """
            common:
              actions:
                exit: []
              greeting:
                message:
                  - name
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              actions:
                exit: "Exit"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);
        editor.SetLocaleValue("en-US", "common.greeting.message", "Hello, {name}!");

        Assert.IsTrue(editor.TryReadLocaleValue("en-US", "common.greeting.message", out var value));
        Assert.AreEqual("Hello, {name}!", value);
        Assert.IsTrue(editor.Validate("en-US").IsValid);
    }

    [TestMethod]
    public void SetLocaleValueRejectsPlaceholderMismatch()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
            """);
        fixture.WriteSchema(
            """
            common:
              greeting:
                message:
                  - name
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              greeting:
                message: "Hello, {name}!"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);

        var exception = Assert.ThrowsExactly<InvalidDataException>(
            () => editor.SetLocaleValue("en-US", "common.greeting.message", "Hello, {day}!"));
        StringAssert.Contains(exception.Message, "placeholder mismatch");
    }

    [TestMethod]
    public void ValidateFindsMissingExtraAndPlaceholderIssues()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
              zh-Hans: "简体中文"
            """);
        fixture.WriteSchema(
            """
            common:
              actions:
                exit: []
              greeting:
                message:
                  - name
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              actions:
                exit: "Exit"
              greeting:
                message: "Hello, {day}!"
              extra:
                unused: "x"
            """);
        fixture.WriteLocale(
            "zh-Hans",
            """
            common:
              actions:
                exit: "退出"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);
        var report = editor.Validate();

        Assert.IsFalse(report.IsValid);
        CollectionAssert.AreEquivalent(
            new[] { "locale.key_extra", "locale.key_missing", "locale.placeholder_mismatch" },
            report.Issues.Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void SchemaSetTreeAndRemoveRoundTrip()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
              zh-Hans: "简体中文"
            """);
        fixture.WriteSchema(
            """
            common:
              actions:
                exit: []
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              actions:
                exit: "Exit"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);
        editor.SetSchemaValue("common.greeting.message", ["name", "day"]);

        Assert.IsTrue(editor.TryReadSchemaValue("common.greeting.message", out var placeholders));
        CollectionAssert.AreEqual(new[] { "name", "day" }, placeholders!.ToArray());
        Assert.AreEqual(
            I18nYamlEditor.GetPlaceholderSentinelValue("common.greeting.message"),
            editor.ReadLocaleValues("zh-Hans")["common.greeting.message"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "common",
                "  actions",
                "    exit []",
                "  greeting",
                "    message [name, day]"
            },
            editor.RenderSchemaTree().ToArray());

        Assert.IsTrue(editor.RemoveSchemaValue("common.greeting.message"));
        Assert.IsFalse(editor.TryReadSchemaValue("common.greeting.message", out _));
        Assert.IsFalse(editor.ReadLocaleValues("zh-Hans").ContainsKey("common.greeting.message"));
    }

    [TestMethod]
    public void LocaleTreeRendersNestedEntries()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
            """);
        fixture.WriteSchema(
            """
            common:
              actions:
                exit: []
              greeting:
                message:
                  - name
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              actions:
                exit: "Exit"
              greeting:
                message: "Hello, {name}!"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);

        CollectionAssert.AreEqual(
            new[]
            {
                "common",
                "  greeting",
                "    message = \"Hello, {name}!\""
            },
            editor.RenderLocaleTree("en-US", "common.greeting").ToArray());
    }

    [TestMethod]
    public void ValidateTreatsPlaceholderSentinelAsWarning()
    {
        using var fixture = new I18nYamlEditorFixture();
        fixture.WriteManifest(
            """
            locales:
              en-US: "English"
            """);
        fixture.WriteSchema(
            """
            common:
              greeting:
                message:
                  - name
            """);
        fixture.WriteLocale(
            "en-US",
            """
            common:
              greeting:
                message: "common.greeting.message:placeholder"
            """);

        var editor = new I18nYamlEditor(fixture.LocaleDirectory);
        var report = editor.Validate();

        Assert.IsTrue(report.IsValid);
        Assert.IsFalse(report.HasErrors);
        Assert.IsTrue(report.HasWarnings);
        Assert.AreEqual("locale.placeholder_value", report.Warnings.Single().Code);
    }

    private sealed class I18nYamlEditorFixture : IDisposable
    {
        public I18nYamlEditorFixture()
        {
            RootDirectory = Path.Combine(Path.GetTempPath(), "pcl-i18n-editor-test-" + Guid.NewGuid().ToString("N"));
            LocaleDirectory = Path.Combine(RootDirectory, "Locales");
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

        public void WriteManifest(string content)
        {
            File.WriteAllText(Path.Combine(MetaDirectory, "manifest.yaml"), content + Environment.NewLine);
        }

        public void WriteSchema(string content)
        {
            File.WriteAllText(Path.Combine(MetaDirectory, "schema.yaml"), content + Environment.NewLine);
        }

        public void WriteLocale(string locale, string content)
        {
            File.WriteAllText(Path.Combine(LocaleDirectory, locale + ".yaml"), content + Environment.NewLine);
        }
    }
}
