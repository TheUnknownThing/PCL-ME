using System;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLauncherProfilesFileServiceTest
{
    [TestMethod]
    public void CreateDefaultProfilesJsonBuildsExpectedSeedDocument()
    {
        var result = MinecraftLauncherProfilesFileService.CreateDefaultProfilesJson(new DateTime(2026, 4, 2, 9, 8, 7));

        var root = JsonNode.Parse(result)!.AsObject();
        Assert.AreEqual("PCL", root["selectedProfile"]!.ToString());
        Assert.AreEqual("23323323323323323323323323323333", root["clientToken"]!.ToString());
        Assert.AreEqual("Grass", root["profiles"]!["PCL"]!["icon"]!.ToString());
        Assert.AreEqual("2026-04-02T09:08:07.0000Z", root["profiles"]!["PCL"]!["lastUsed"]!.ToString());
    }
}
