using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchProfileStorageServiceTest
{
    [TestMethod]
    public void ParseDocumentDecryptsSensitiveMicrosoftAndAuthlibFields()
    {
        const string json = """
                            {
                              "lastUsed": 2,
                              "profiles": [
                                {
                                  "type": "microsoft",
                                  "uuid": "ms-uuid",
                                  "username": "Microsoft Player",
                                  "accessToken": "enc-access",
                                  "refreshToken": "enc-refresh",
                                  "expires": 123456,
                                  "desc": "ms-desc",
                                  "rawJson": "enc-raw",
                                  "skinHeadId": "ms-skin"
                                },
                                {
                                  "type": "authlib",
                                  "uuid": "auth-uuid",
                                  "username": "Auth Player",
                                  "accessToken": "auth-access",
                                  "refreshToken": "auth-refresh",
                                  "expires": 456789,
                                  "server": "https://example.com/authserver",
                                  "serverName": "Example",
                                  "name": "auth-name",
                                  "password": "auth-password",
                                  "clientToken": "auth-client",
                                  "desc": "auth-desc",
                                  "skinHeadId": "auth-skin"
                                }
                              ]
                            }
                            """;

        var result = MinecraftLaunchProfileStorageService.ParseDocument(json, value => $"dec:{value}");

        Assert.AreEqual(2, result.LastUsedProfile);
        Assert.AreEqual(2, result.Profiles.Count);

        var microsoftProfile = result.Profiles[0];
        Assert.AreEqual(MinecraftLaunchStoredProfileKind.Microsoft, microsoftProfile.Kind);
        Assert.AreEqual("dec:enc-access", microsoftProfile.AccessToken);
        Assert.AreEqual("dec:enc-refresh", microsoftProfile.RefreshToken);
        Assert.AreEqual("dec:enc-raw", microsoftProfile.RawJson);

        var authProfile = result.Profiles[1];
        Assert.AreEqual(MinecraftLaunchStoredProfileKind.Authlib, authProfile.Kind);
        Assert.AreEqual("https://example.com/authserver", authProfile.Server);
        Assert.AreEqual("dec:auth-name", authProfile.LoginName);
        Assert.AreEqual("dec:auth-password", authProfile.Password);
        Assert.AreEqual("dec:auth-client", authProfile.ClientToken);
    }

    [TestMethod]
    public void ParseDocumentTreatsUnknownProfileTypeAsOffline()
    {
        const string json = """
                            {
                              "lastUsed": 0,
                              "profiles": [
                                {
                                  "type": "mystery",
                                  "uuid": "offline-uuid",
                                  "username": "Offline Player",
                                  "desc": "offline-desc",
                                  "skinHeadId": "offline-skin"
                                }
                              ]
                            }
                            """;

        var result = MinecraftLaunchProfileStorageService.ParseDocument(json, value => $"ignored:{value}");

        var profile = result.Profiles.Single();
        Assert.AreEqual(MinecraftLaunchStoredProfileKind.Offline, profile.Kind);
        Assert.AreEqual("Offline Player", profile.Username);
        Assert.IsNull(profile.AccessToken);
    }

    [TestMethod]
    public void SerializeDocumentEncryptsOnlySensitiveFields()
    {
        var document = new MinecraftLaunchProfileDocument(
            LastUsedProfile: 1,
            Profiles:
            [
                new MinecraftLaunchPersistedProfile(
                    MinecraftLaunchStoredProfileKind.Microsoft,
                    Uuid: "ms-uuid",
                    Username: "Microsoft Player",
                    Desc: "ms-desc",
                    SkinHeadId: "ms-skin",
                    Expires: 100,
                    Server: null,
                    ServerName: null,
                    AccessToken: "access",
                    RefreshToken: "refresh",
                    LoginName: null,
                    Password: null,
                    ClientToken: null,
                    RawJson: "raw"),
                new MinecraftLaunchPersistedProfile(
                    MinecraftLaunchStoredProfileKind.Authlib,
                    Uuid: "auth-uuid",
                    Username: "Auth Player",
                    Desc: "auth-desc",
                    SkinHeadId: "auth-skin",
                    Expires: 200,
                    Server: "https://example.com/authserver",
                    ServerName: "Example",
                    AccessToken: "auth-access",
                    RefreshToken: "auth-refresh",
                    LoginName: "login",
                    Password: "password",
                    ClientToken: "client",
                    RawJson: null),
                new MinecraftLaunchPersistedProfile(
                    MinecraftLaunchStoredProfileKind.Offline,
                    Uuid: "offline-uuid",
                    Username: "Offline Player",
                    Desc: "offline-desc",
                    SkinHeadId: "offline-skin",
                    Expires: 0,
                    Server: null,
                    ServerName: null,
                    AccessToken: null,
                    RefreshToken: null,
                    LoginName: null,
                    Password: null,
                    ClientToken: null,
                    RawJson: null)
            ]);

        var json = MinecraftLaunchProfileStorageService.SerializeDocument(document, value => $"enc:{value}");
        var root = JsonNode.Parse(json)!.AsObject();
        var profiles = root["profiles"]!.AsArray();

        Assert.AreEqual(1, root["lastUsed"]!.GetValue<int>());
        Assert.AreEqual("enc:access", profiles[0]!["accessToken"]!.GetValue<string>());
        Assert.AreEqual("enc:raw", profiles[0]!["rawJson"]!.GetValue<string>());
        Assert.AreEqual("enc:login", profiles[1]!["name"]!.GetValue<string>());
        Assert.AreEqual("enc:password", profiles[1]!["password"]!.GetValue<string>());
        Assert.AreEqual("offline", profiles[2]!["type"]!.GetValue<string>());
        Assert.IsNull(profiles[2]!["accessToken"]);
    }

    [TestMethod]
    public void DeleteProfileKeepsCurrentSelectionWhenDeletingEarlierEntry()
    {
        var document = new MinecraftLaunchProfileDocument(
            2,
            [
                CreateProfile("Offline-A"),
                CreateProfile("Offline-B"),
                CreateProfile("Offline-C")
            ]);

        var result = MinecraftLaunchProfileStorageService.DeleteProfile(document, 0);

        Assert.AreEqual(1, result.LastUsedProfile);
        Assert.AreEqual(2, result.Profiles.Count);
        Assert.AreEqual("Offline-C", result.Profiles[result.LastUsedProfile].Username);
    }

    [TestMethod]
    public void DeleteProfileSelectsNearestRemainingEntryWhenDeletingCurrentSelection()
    {
        var document = new MinecraftLaunchProfileDocument(
            1,
            [
                CreateProfile("Offline-A"),
                CreateProfile("Offline-B"),
                CreateProfile("Offline-C")
            ]);

        var result = MinecraftLaunchProfileStorageService.DeleteProfile(document, 1);

        Assert.AreEqual(1, result.LastUsedProfile);
        Assert.AreEqual(2, result.Profiles.Count);
        Assert.AreEqual("Offline-C", result.Profiles[result.LastUsedProfile].Username);
    }

    [TestMethod]
    public void DeleteProfileReturnsZeroSelectionWhenRemovingLastProfile()
    {
        var document = new MinecraftLaunchProfileDocument(
            0,
            [
                CreateProfile("Offline-A")
            ]);

        var result = MinecraftLaunchProfileStorageService.DeleteProfile(document, 0);

        Assert.AreEqual(0, result.LastUsedProfile);
        Assert.AreEqual(0, result.Profiles.Count);
    }

    private static MinecraftLaunchPersistedProfile CreateProfile(string userName)
    {
        return new MinecraftLaunchPersistedProfile(
            MinecraftLaunchStoredProfileKind.Offline,
            Uuid: $"{userName}-uuid",
            Username: userName,
            Desc: null,
            SkinHeadId: null,
            Expires: 0,
            Server: null,
            ServerName: null,
            AccessToken: null,
            RefreshToken: null,
            LoginName: null,
            Password: null,
            ClientToken: null,
            RawJson: null);
    }
}
