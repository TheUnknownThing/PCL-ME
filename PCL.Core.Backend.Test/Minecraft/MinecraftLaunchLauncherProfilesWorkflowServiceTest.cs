using System;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchLauncherProfilesWorkflowServiceTest
{
    [TestMethod]
    public void BuildPlanReturnsNoopForNonMicrosoftLogin()
    {
        var result = MinecraftLaunchLauncherProfilesWorkflowService.BuildPlan(
            new MinecraftLaunchLauncherProfilesWorkflowRequest(
                IsMicrosoftLogin: false,
                ExistingProfilesJson: "{}",
                UserName: "Player",
                ClientToken: "token",
                DefaultProfileTimestamp: new DateTime(2026, 4, 2)));

        Assert.IsFalse(result.ShouldWrite);
        Assert.IsNull(result.InitialAttempt);
        Assert.IsNull(result.RetryAttempt);
    }

    [TestMethod]
    public void BuildPlanBuildsInitialAndRetryAttempts()
    {
        const string existingJson = """
            {
              "profiles": {
                "Existing": {
                  "name": "Existing Profile"
                }
              }
            }
            """;

        var result = MinecraftLaunchLauncherProfilesWorkflowService.BuildPlan(
            new MinecraftLaunchLauncherProfilesWorkflowRequest(
                IsMicrosoftLogin: true,
                ExistingProfilesJson: existingJson,
                UserName: "Player",
                ClientToken: "client-token",
                DefaultProfileTimestamp: new DateTime(2026, 4, 2, 9, 8, 7)));

        Assert.IsTrue(result.ShouldWrite);
        Assert.AreEqual("Updated launcher_profiles.json", result.InitialAttempt!.SuccessLogMessage);
        Assert.AreEqual("Updated launcher_profiles.json after deletion", result.RetryAttempt!.SuccessLogMessage);
        Assert.AreEqual("Failed to update launcher_profiles.json; will retry after deleting the file", result.RetryLogMessage);
        Assert.AreEqual("Failed to update launcher_profiles.json", result.FailureLogMessage);

        var retryRoot = JsonNode.Parse(result.RetryAttempt.UpdatedProfilesJson)!.AsObject();
        Assert.AreEqual("PCL", retryRoot["selectedProfile"]!.ToString());
        Assert.AreEqual("PCL", retryRoot["profiles"]!["PCL"]!["name"]!.ToString());
        Assert.AreEqual("Player", retryRoot["authenticationDatabase"]!["00000111112222233333444445555566"]!["username"]!.ToString());
    }
}
