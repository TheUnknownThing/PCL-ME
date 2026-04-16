using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.I18n;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherUpdateLogServiceTest
{
    [TestMethod]
    public void BuildPromptUsesProvidedChangelogContent()
    {
        var result = LauncherUpdateLogService.BuildPrompt(new LauncherUpdateLogRequest(
            "# Changes",
            "CE",
            "2.0"));

        Assert.AreEqual("# Changes", result.MarkdownContent);
        Assert.AreEqual("startup.prompts.update_log.title", result.Title.Key);
        Assert.AreEqual("CE", result.Title.Arguments![0].StringValue);
        Assert.AreEqual("2.0", result.Title.Arguments![1].StringValue);
        Assert.AreEqual("startup.prompts.update_log.actions.confirm", result.ConfirmLabel.Key);
        Assert.AreEqual("startup.prompts.update_log.actions.full_changelog", result.FullChangelogLabel.Key);
        Assert.AreEqual("https://github.com/TheUnknownThing/PCL-ME/releases", result.FullChangelogUrl);
    }

    [TestMethod]
    public void BuildPromptFallsBackWhenChangelogIsMissing()
    {
        var result = LauncherUpdateLogService.BuildPrompt(new LauncherUpdateLogRequest(
            null,
            "Release",
            "1.2.3"));

        Assert.AreEqual("Welcome.", result.MarkdownContent);
        Assert.AreEqual("startup.prompts.update_log.title", result.Title.Key);
    }
}
