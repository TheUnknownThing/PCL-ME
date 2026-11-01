using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FrontendGameManagementServiceTest
{
    [TestMethod]
    public void ResolveCommunityResourceFileNameAppliesConfiguredProjectPrefix()
    {
        var result = FrontendGameManagementService.ResolveCommunityResourceFileName(
            "机械动力",
            "create-1.21.1-6.0.4.jar",
            "Create 6.0.4",
            0);

        Assert.AreEqual("[机械动力] create-1.21.1-6.0.4.jar", result);
    }

    [TestMethod]
    public void ResolveCommunityResourceFileNameKeepsExistingNameWhenProjectTitleAlreadyExists()
    {
        var result = FrontendGameManagementService.ResolveCommunityResourceFileName(
            "Create",
            "create-1.21.1-6.0.4.jar",
            "Create 6.0.4",
            2);

        Assert.AreEqual("create-1.21.1-6.0.4.jar", result);
    }

    [TestMethod]
    public void ResolveCommunityResourceFileNameDoesNotTreatSubstringMatchesAsExistingProjectTitle()
    {
        var result = FrontendGameManagementService.ResolveCommunityResourceFileName(
            "Create",
            "recreated-worlds.zip",
            "Recreated Worlds",
            0);

        Assert.AreEqual("[Create] recreated-worlds.zip", result);
    }

    [TestMethod]
    public void ResolveLocalModDisplayCanPromoteFileNameToPrimaryLine()
    {
        var entry = new FrontendInstanceResourceEntry(
            Title: "机械动力",
            Summary: "simibubi • modrinth.com",
            Meta: "Fabric • 6.0.4",
            Path: "/tmp/create-1.21.1-6.0.4.jar.disabled",
            IconName: "Fabric.png");

        var result = FrontendGameManagementService.ResolveLocalModDisplay(entry, 1);

        Assert.AreEqual("create-1.21.1-6.0.4", result.Title);
        Assert.AreEqual("机械动力 • simibubi • modrinth.com", result.Summary);
    }

    [TestMethod]
    public void ResolveLocalModDisplayAddsFileNameToSecondaryLineByDefault()
    {
        var entry = new FrontendInstanceResourceEntry(
            Title: "机械动力",
            Summary: "simibubi • modrinth.com",
            Meta: "Fabric • 6.0.4",
            Path: "/tmp/create-1.21.1-6.0.4.jar",
            IconName: "Fabric.png");

        var result = FrontendGameManagementService.ResolveLocalModDisplay(entry, 0);

        Assert.AreEqual("机械动力", result.Title);
        Assert.AreEqual("create-1.21.1-6.0.4 • simibubi • modrinth.com", result.Summary);
    }
}
