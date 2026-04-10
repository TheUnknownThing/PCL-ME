using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.Text.Json.Nodes;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchLauncherProfilesServiceTest
{
    [TestMethod]
    public void BuildUpdatePlanReturnsNoopForNonMicrosoftLogins()
    {
        var result = MinecraftLaunchLauncherProfilesService.BuildUpdatePlan(new MinecraftLaunchLauncherProfilesUpdateRequest(
            IsMicrosoftLogin: false,
            ExistingProfilesJson: "{}",
            UserName: "Player",
            ClientToken: "token"));

        Assert.IsFalse(result.ShouldWrite);
        Assert.IsNull(result.UpdatedProfilesJson);
    }

    [TestMethod]
    public void BuildUpdatePlanMergesMicrosoftAccountIntoExistingLauncherProfiles()
    {
        const string existingJson = """
            {
              "profiles": {
                "existing": {
                  "name": "Existing Profile"
                }
              },
              "selectedUser": {
                "account": "legacy-account"
              }
            }
            """;

        var result = MinecraftLaunchLauncherProfilesService.BuildUpdatePlan(new MinecraftLaunchLauncherProfilesUpdateRequest(
            IsMicrosoftLogin: true,
            ExistingProfilesJson: existingJson,
            UserName: "Player\"Name",
            ClientToken: "client-token"));

        Assert.IsTrue(result.ShouldWrite);
        var root = JsonNode.Parse(result.UpdatedProfilesJson!)!.AsObject();

        Assert.AreEqual("Existing Profile", root["profiles"]!["existing"]!["name"]!.ToString());
        Assert.AreEqual("Player-Name", root["authenticationDatabase"]!["00000111112222233333444445555566"]!["username"]!.ToString());
        Assert.AreEqual("Player\"Name", root["authenticationDatabase"]!["00000111112222233333444445555566"]!["profiles"]!["66666555554444433333222221111100"]!["displayName"]!.ToString());
        Assert.AreEqual("client-token", root["clientToken"]!.ToString());
        Assert.AreEqual("00000111112222233333444445555566", root["selectedUser"]!["account"]!.ToString());
        Assert.AreEqual("66666555554444433333222221111100", root["selectedUser"]!["profile"]!.ToString());
    }
}
