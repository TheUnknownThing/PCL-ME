using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchAuthlibLoginWorkflowServiceTest
{
    [TestMethod]
    public void ResolveRefreshBuildsSessionAndMutationPlan()
    {
        const string json = """
                            {
                              "accessToken": "refresh-access",
                              "clientToken": "refresh-client",
                              "selectedProfile": {
                                "id": "profile-2",
                                "name": "Alex"
                              }
                            }
                            """;

        var result = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
            new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                json,
                SelectedProfileIndex: 3,
                ServerBaseUrl: "https://auth.example",
                ServerName: "ExampleAuth",
                LoginName: "player@example.com",
                Password: "secret"));

        Assert.AreEqual("refresh-access", result.Session.AccessToken);
        Assert.AreEqual("profile-2", result.Session.ProfileId);
        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateSelected, result.MutationPlan.Kind);
        Assert.AreEqual("ExampleAuth", result.MutationPlan.UpdateProfile!.ServerName);
    }

    [TestMethod]
    public void PlanAuthenticateReturnsFailureWhenNoProfilesExist()
    {
        const string json = """
                            {
                              "accessToken": "access",
                              "clientToken": "client",
                              "availableProfiles": []
                            }
                            """;

        var result = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                ForceReselectProfile: false,
                CachedProfileId: null,
                AuthenticateResponseJson: json));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.Fail, result.Kind);
        Assert.AreEqual("$你还没有创建角色，请在创建角色后再试！", result.FailureMessage);
    }

    [TestMethod]
    public void PlanAuthenticateReturnsPromptWhenMultipleProfilesNeedSelection()
    {
        const string json = """
                            {
                              "accessToken": "access",
                              "clientToken": "client",
                              "availableProfiles": [
                                { "id": "profile-1", "name": "Steve" },
                                { "id": "profile-2", "name": "Alex" }
                              ]
                            }
                            """;

        var result = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                ForceReselectProfile: true,
                CachedProfileId: null,
                AuthenticateResponseJson: json));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.PromptForSelection, result.Kind);
        Assert.AreEqual(2, result.PromptOptions.Count);
        Assert.AreEqual("选择使用的角色", result.PromptTitle);
    }

    [TestMethod]
    public void ResolveAuthenticateUsesSelectedPromptProfileAndBuildsMutationPlan()
    {
        const string authenticateJson = """
                                        {
                                          "accessToken": "access",
                                          "clientToken": "client",
                                          "availableProfiles": [
                                            { "id": "profile-1", "name": "Steve" },
                                            { "id": "profile-2", "name": "Alex" }
                                          ]
                                        }
                                        """;
        const string metadataJson = """
                                    {
                                      "meta": {
                                        "serverName": "LittleSkin"
                                      }
                                    }
                                    """;

        var plan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                ForceReselectProfile: true,
                CachedProfileId: null,
                AuthenticateResponseJson: authenticateJson));
        var result = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
            new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                plan,
                metadataJson,
                IsExistingProfile: false,
                SelectedProfileIndex: -1,
                ServerBaseUrl: "https://auth.example",
                LoginName: "player@example.com",
                Password: "secret",
                SelectedProfileId: "profile-2"));

        Assert.AreEqual("profile-2", result.Session.ProfileId);
        Assert.AreEqual("Alex", result.Session.ProfileName);
        Assert.AreEqual("LittleSkin", result.ServerName);
        Assert.AreEqual(MinecraftLaunchProfileMutationKind.CreateNew, result.MutationPlan.Kind);
    }
}
