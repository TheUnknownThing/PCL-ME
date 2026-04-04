using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchMicrosoftRequestWorkflowServiceTest
{
    [TestMethod]
    public void BuildDeviceCodeRequestReturnsExpectedEndpointAndFormBody()
    {
        var result = MinecraftLaunchMicrosoftRequestWorkflowService.BuildDeviceCodeRequest();

        Assert.AreEqual("POST", result.Method);
        Assert.AreEqual("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", result.Url);
        Assert.AreEqual("application/x-www-form-urlencoded", result.ContentType);
        StringAssert.Contains(result.Body!, "client_id=00000000402b5328");
        StringAssert.Contains(result.Body!, "scope=XboxLive.signin%20offline_access");
    }

    [TestMethod]
    public void BuildOAuthRefreshRequestReturnsExpectedRefreshEndpoint()
    {
        var result = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOAuthRefreshRequest("refresh-token");

        Assert.AreEqual("https://login.live.com/oauth20_token.srf", result.Url);
        Assert.AreEqual("application/x-www-form-urlencoded", result.ContentType);
        StringAssert.Contains(result.Body!, "refresh_token=refresh-token");
    }

    [TestMethod]
    public void BuildOwnershipAndProfileRequestsCarryBearerToken()
    {
        var ownership = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest("minecraft-token");
        var profile = MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest("minecraft-token");

        Assert.AreEqual("GET", ownership.Method);
        Assert.AreEqual("https://api.minecraftservices.com/entitlements/mcstore", ownership.Url);
        Assert.AreEqual("minecraft-token", ownership.BearerToken);
        Assert.AreEqual("https://api.minecraftservices.com/minecraft/profile", profile.Url);
        Assert.AreEqual("minecraft-token", profile.BearerToken);
    }
}
