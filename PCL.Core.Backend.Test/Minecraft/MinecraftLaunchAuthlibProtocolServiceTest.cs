using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;
using System.Linq;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchAuthlibProtocolServiceTest
{
    [TestMethod]
    public void BuildAuthenticateRequestJsonIncludesMinecraftAgentAndCredentials()
    {
        var json = MinecraftLaunchAuthlibProtocolService.BuildAuthenticateRequestJson("player@example.com", "secret");

        StringAssert.Contains(json, "\"username\":\"player@example.com\"");
        StringAssert.Contains(json, "\"password\":\"secret\"");
        StringAssert.Contains(json, "\"name\":\"Minecraft\"");
        StringAssert.Contains(json, "\"version\":1");
        StringAssert.Contains(json, "\"requestUser\":true");
    }

    [TestMethod]
    public void ParseRefreshResponseJsonExtractsSelectedProfile()
    {
        const string json = """
            {
              "accessToken": "new-access",
              "clientToken": "new-client",
              "selectedProfile": {
                "id": "uuid-123",
                "name": "Player"
              }
            }
            """;

        var result = MinecraftLaunchAuthlibProtocolService.ParseRefreshResponseJson(json);

        Assert.AreEqual("new-access", result.AccessToken);
        Assert.AreEqual("new-client", result.ClientToken);
        Assert.AreEqual("uuid-123", result.SelectedProfileId);
        Assert.AreEqual("Player", result.SelectedProfileName);
    }

    [TestMethod]
    public void BuildRefreshRequestJsonIncludesClientTokenAndOmitsSelectedProfileWhenNotProvided()
    {
        var json = MinecraftLaunchAuthlibProtocolService.BuildRefreshRequestJson(
            "access-token",
            "client-token");

        StringAssert.Contains(json, "\"accessToken\":\"access-token\"");
        StringAssert.Contains(json, "\"clientToken\":\"client-token\"");
        StringAssert.Contains(json, "\"requestUser\":true");
        Assert.IsFalse(json.Contains("selectedProfile", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParseAuthenticateResponseJsonExtractsAvailableProfilesAndSelectedProfile()
    {
        const string json = """
            {
              "accessToken": "access",
              "clientToken": "client",
              "selectedProfile": {
                "id": "selected-uuid",
                "name": "Selected"
              },
              "availableProfiles": [
                {
                  "id": "selected-uuid",
                  "name": "Selected"
                },
                {
                  "id": "other-uuid",
                  "name": "Other"
                }
              ]
            }
            """;

        var result = MinecraftLaunchAuthlibProtocolService.ParseAuthenticateResponseJson(json);

        Assert.AreEqual("access", result.AccessToken);
        Assert.AreEqual("client", result.ClientToken);
        Assert.AreEqual("selected-uuid", result.SelectedProfileId);
        CollectionAssert.AreEqual(
            new[] { "Selected", "Other" },
            result.AvailableProfiles.Select(profile => profile.Name).ToArray());
    }

    [TestMethod]
    public void ParseServerNameFromMetadataJsonReadsServerName()
    {
        const string json = """
            {
              "meta": {
                "serverName": "Blessing Skin"
              }
            }
            """;

        var result = MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(json);

        Assert.AreEqual("Blessing Skin", result);
    }
}
