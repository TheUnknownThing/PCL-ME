using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.ResourceProject.Curseforge;
using PCL.Core.Minecraft.ResourceProject.Modrinth;

namespace PCL.Core.Test.Project;

[TestClass]
public class ResourceProjectSerializationTest
{
    [TestMethod]
    public void ModrinthProjectRoundTrip()
    {
        var project = new ModrinthProject(
            "sodium",
            "Sodium",
            "Performance mod",
            ["optimization"],
            "required",
            "optional",
            "body",
            "approved",
            null,
            ["fabric"],
            "https://example.com/issues",
            "https://example.com/source",
            null,
            null,
            [new ModrinthDonationUrl("patreon", "Patreon", "https://example.com/patreon")],
            "mod",
            1000,
            "https://example.com/icon.png",
            123456,
            "thread",
            "monetized",
            "project-id",
            "team-id",
            "https://example.com/body",
            new ModrinthModeratorMessage("ok", null),
            "2024-01-01T00:00:00Z",
            "2024-01-02T00:00:00Z",
            null,
            null,
            200,
            new ModrinthLicense("MIT", "MIT", "https://example.com/license"),
            ["1.0.0"],
            ["1.20.1"],
            ["fabric"],
            []);

        var json = JsonSerializer.Serialize(project);
        var restored = JsonSerializer.Deserialize<ModrinthProject>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(project.slug, restored.slug);
        Assert.AreEqual(project.license.id, restored.license.id);
        Assert.AreEqual(project.donation_urls[0].platform, restored.donation_urls[0].platform);
    }

    [TestMethod]
    public void CurseforgeProjectRoundTrip()
    {
        var project = new CurseforgeProject(
            1,
            432,
            "Example",
            "example",
            new CurseforgeLinks("https://example.com", "", "", ""),
            "summary",
            4,
            500,
            true,
            6,
            [new CurseforgeCategories(6, 432, "Tech", "tech", "https://example.com/category", "https://example.com/icon.png", "2024-01-01", false, 0, 0, 0)],
            6,
            [new CurseforgeAuthors(1, "Author", "https://example.com/author")],
            new CurseforgePictures(1, 1, "logo", "", "https://example.com/thumb.png", "https://example.com/logo.png"),
            [new CurseforgePictures(2, 1, "shot", "", "https://example.com/shot-thumb.png", "https://example.com/shot.png")],
            10,
            new Dictionary<string, object> { ["id"] = 10 });

        var json = JsonSerializer.Serialize(project);
        var restored = JsonSerializer.Deserialize<CurseforgeProject>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(project.id, restored.id);
        Assert.AreEqual(project.links.websiteUrl, restored.links.websiteUrl);
        Assert.AreEqual(project.authors[0].name, restored.authors[0].name);
    }
}
