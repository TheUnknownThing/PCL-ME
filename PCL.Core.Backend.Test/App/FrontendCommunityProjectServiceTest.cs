using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendCommunityProjectServiceTest
{
    [TestMethod]
    public void TryParseClipboardProjectLink_RecognizesModrinthProjectLinks()
    {
        var parsed = FrontendCommunityProjectService.TryParseClipboardProjectLink(
            "https://modrinth.com/mod/sodium",
            out var link);

        Assert.IsTrue(parsed);
        Assert.AreEqual("Modrinth", link.Source);
        Assert.AreEqual("sodium", link.Identifier);
        Assert.AreEqual(LauncherFrontendSubpageKey.DownloadMod, link.Route);
    }

    [TestMethod]
    public void TryParseClipboardProjectLink_RecognizesCurseForgeProjectLinks()
    {
        var parsed = FrontendCommunityProjectService.TryParseClipboardProjectLink(
            "https://www.curseforge.com/minecraft/texture-packs/faithful-32x",
            out var link);

        Assert.IsTrue(parsed);
        Assert.AreEqual("CurseForge", link.Source);
        Assert.AreEqual("faithful-32x", link.Identifier);
        Assert.AreEqual(LauncherFrontendSubpageKey.DownloadResourcePack, link.Route);
    }

    [TestMethod]
    public void TryParseClipboardProjectLink_IgnoresUnsupportedUrls()
    {
        var parsed = FrontendCommunityProjectService.TryParseClipboardProjectLink(
            "https://example.com/not-a-project",
            out _);

        Assert.IsFalse(parsed);
    }
}
