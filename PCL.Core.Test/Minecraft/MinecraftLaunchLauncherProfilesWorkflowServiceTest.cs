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
        Assert.AreEqual("已更新 launcher_profiles.json", result.InitialAttempt!.SuccessLogMessage);
        Assert.AreEqual("已在删除后更新 launcher_profiles.json", result.RetryAttempt!.SuccessLogMessage);
        Assert.AreEqual("更新 launcher_profiles.json 失败，将在删除文件后重试", result.RetryLogMessage);
        Assert.AreEqual("更新 launcher_profiles.json 失败", result.FailureLogMessage);

        var retryRoot = JsonNode.Parse(result.RetryAttempt.UpdatedProfilesJson)!.AsObject();
        Assert.AreEqual("PCL", retryRoot["selectedProfile"]!.ToString());
        Assert.AreEqual("PCL", retryRoot["profiles"]!["PCL"]!["name"]!.ToString());
        Assert.AreEqual("Player", retryRoot["authenticationDatabase"]!["00000111112222233333444445555566"]!["username"]!.ToString());
    }
}
