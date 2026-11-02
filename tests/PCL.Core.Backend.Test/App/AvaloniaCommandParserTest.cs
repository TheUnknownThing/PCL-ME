using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Tools.I18n;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class I18nToolCommandParserTest
{
    [TestMethod]
    public void ParseSupportsSetCommand()
    {
        var result = I18nToolCommandParser.Parse([
            "set",
            "--locale",
            "en-US",
            "--key",
            "common.actions.exit",
            "--value",
            "Quit"
        ]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(I18nToolCommandKind.Set, result.Options.Kind);
        Assert.AreEqual("en-US", result.Options.Locale);
        Assert.AreEqual("common.actions.exit", result.Options.Key);
        Assert.AreEqual("Quit", result.Options.Value);
    }

    [TestMethod]
    public void ParseRejectsIncompleteGetCommand()
    {
        var result = I18nToolCommandParser.Parse(["get", "--locale", "en-US"]);

        Assert.IsNull(result.Options);
        Assert.IsFalse(result.ShowHelp);
        Assert.AreEqual("The get command requires --locale and --key.", result.ErrorMessage);
    }

    [TestMethod]
    public void ParseSupportsSchemaSetCommand()
    {
        var result = I18nToolCommandParser.Parse([
            "schema",
            "set",
            "--key",
            "common.greeting.message",
            "--placeholders",
            "name,day"
        ]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.ShowHelp);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(I18nToolCommandKind.SchemaSet, result.Options.Kind);
        Assert.AreEqual("common.greeting.message", result.Options.Key);
        CollectionAssert.AreEqual(new[] { "name", "day" }, result.Options.Placeholders!.ToArray());
    }

    [TestMethod]
    public void ParseSupportsLocaleTreeCommand()
    {
        var result = I18nToolCommandParser.Parse([
            "tree",
            "--locale",
            "en-US",
            "--prefix",
            "common.greeting"
        ]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(I18nToolCommandKind.Tree, result.Options.Kind);
        Assert.AreEqual("en-US", result.Options.Locale);
        Assert.AreEqual("common.greeting", result.Options.Prefix);
    }

    [TestMethod]
    public void ParseSupportsValidateMsBuildFormat()
    {
        var result = I18nToolCommandParser.Parse([
            "validate",
            "--format",
            "msbuild"
        ]);

        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual(I18nToolCommandKind.Validate, result.Options.Kind);
        Assert.AreEqual(I18nToolOutputFormat.MsBuild, result.Options.OutputFormat);
    }
}
