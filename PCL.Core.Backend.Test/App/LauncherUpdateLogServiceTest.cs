using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        Assert.AreEqual("PCL-ME 已更新至 CE 2.0", result.Title);
        Assert.AreEqual("确定", result.ConfirmLabel);
        Assert.AreEqual("完整更新日志", result.FullChangelogLabel);
        Assert.AreEqual("https://github.com/TheUnknownThing/PCL-ME/releases", result.FullChangelogUrl);
    }

    [TestMethod]
    public void BuildPromptFallsBackWhenChangelogIsMissing()
    {
        var result = LauncherUpdateLogService.BuildPrompt(new LauncherUpdateLogRequest(
            null,
            "Release",
            "1.2.3"));

        Assert.AreEqual("欢迎使用呀~", result.MarkdownContent);
        Assert.AreEqual("PCL-ME 已更新至 Release 1.2.3", result.Title);
    }
}
