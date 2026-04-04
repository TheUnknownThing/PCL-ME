using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchMicrosoftProtocolServiceTest
{
    [TestMethod]
    public void ParseDeviceCodeResponseJsonExtractsPromptFields()
    {
        const string json = """
            {
              "device_code": "device-code",
              "user_code": "user-code",
              "verification_uri": "https://microsoft.com/devicelogin",
              "verification_uri_complete": "https://microsoft.com/devicelogin?otc=user-code",
              "expires_in": 900,
              "interval": 5
            }
            """;

        var result = MinecraftLaunchMicrosoftProtocolService.ParseDeviceCodeResponseJson(json);

        Assert.AreEqual("device-code", result.DeviceCode);
        Assert.AreEqual("user-code", result.UserCode);
        Assert.AreEqual("https://microsoft.com/devicelogin", result.VerificationUrl);
        Assert.AreEqual("https://microsoft.com/devicelogin?otc=user-code", result.VerificationUrlComplete);
        Assert.AreEqual(900, result.ExpiresInSeconds);
        Assert.AreEqual(5, result.IntervalSeconds);
    }

    [TestMethod]
    public void BuildXboxLiveTokenRequestFormatsRpsTicket()
    {
        var result = MinecraftLaunchMicrosoftProtocolService.BuildXboxLiveTokenRequest("oauth-token");

        Assert.AreEqual("RPS", result.Properties.AuthMethod);
        Assert.AreEqual("user.auth.xboxlive.com", result.Properties.SiteName);
        Assert.AreEqual("d=oauth-token", result.Properties.RpsTicket);
        Assert.AreEqual("http://auth.xboxlive.com", result.RelyingParty);
    }

    [TestMethod]
    public void ParseXstsTokenResponseJsonExtractsTokenAndUserHash()
    {
        const string json = """
            {
              "Token": "xsts-token",
              "DisplayClaims": {
                "xui": [
                  {
                    "uhs": "user-hash"
                  }
                ]
              }
            }
            """;

        var result = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(json);

        Assert.AreEqual("xsts-token", result.Token);
        Assert.AreEqual("user-hash", result.UserHash);
    }

    [TestMethod]
    public void BuildMinecraftAccessTokenRequestFormatsIdentityToken()
    {
        var result = MinecraftLaunchMicrosoftProtocolService.BuildMinecraftAccessTokenRequest("uhs", "xsts");

        Assert.AreEqual("XBL3.0 x=uhs;xsts", result["identityToken"]);
    }

    [TestMethod]
    public void HasMinecraftOwnershipDetectsOwnedProduct()
    {
        const string json = """
            {
              "items": [
                { "name": "product_minecraft" }
              ]
            }
            """;

        Assert.IsTrue(MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(json));
    }

    [TestMethod]
    public void ParseMinecraftProfileResponseJsonExtractsProfileFields()
    {
        const string json = """
            {
              "id": "uuid-123",
              "name": "Steve"
            }
            """;

        var result = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(json);

        Assert.AreEqual("uuid-123", result.Uuid);
        Assert.AreEqual("Steve", result.UserName);
        Assert.AreEqual(json, result.ProfileJson);
    }
}
