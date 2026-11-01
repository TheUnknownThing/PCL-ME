using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherAnnouncementServiceTest
{
    [TestMethod]
    public void FilterSkipsShownAnnouncementsAndHonorsPreference()
    {
        LauncherAnnouncement[] announcements =
        [
            new LauncherAnnouncement("general", "General", "body", null, LauncherAnnouncementSeverity.General),
            new LauncherAnnouncement("important", "Important", "body", null, LauncherAnnouncementSeverity.Important)
        ];

        var result = LauncherAnnouncementService.Filter(
            announcements,
            LauncherAnnouncementPreference.ImportantOnly,
            "general");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("important", result[0].Id);
    }

    [TestMethod]
    public void MergeShownAnnouncementStateAddsIdsWithoutDuplicates()
    {
        var result = LauncherAnnouncementService.MergeShownAnnouncementState(
            "b|a",
            ["a", "c"]);

        CollectionAssert.AreEqual(
            new[] { "a", "b", "c" },
            result.Split('|').ToArray());
    }
}
