using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.Models;
using System.Text.Json.Nodes;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendCommunityProjectServiceTest
{
    [TestMethod]
    public void Dependencies_RemainRequiredWhenDecorativeProjectMetadataIsUnavailable()
    {
        var modrinth = FrontendCommunityProjectService.BuildModrinthDependencyEntries(
            JsonNode.Parse("""[{"project_id":"dependency","version_id":"pinned","dependency_type":"required"},{"project_id":"conflict","dependency_type":"incompatible"}]""")!.AsArray(),
            new Dictionary<string, FrontendCommunityProjectSummary>());
        Assert.HasCount(1, modrinth);
        Assert.AreEqual("dependency", modrinth[0].ProjectId);
        Assert.AreEqual("pinned", modrinth[0].ReleaseId);
        Assert.AreEqual(FrontendCommunityProjectDependencyKind.Required, modrinth[0].Kind);

        var curseforge = FrontendCommunityProjectService.BuildCurseForgeDependencyEntries(
            JsonNode.Parse("""[{"modId":42,"relationType":3},{"modId":7,"relationType":5}]""")!.AsArray(),
            new Dictionary<string, FrontendCommunityProjectSummary>());
        Assert.HasCount(1, curseforge);
        Assert.AreEqual("42", curseforge[0].ProjectId);
        Assert.AreEqual(FrontendCommunityProjectDependencyKind.Required, curseforge[0].Kind);
    }

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
