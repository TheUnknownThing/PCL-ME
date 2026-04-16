using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchLoginProfileWorkflowServiceTest
{
    [TestMethod]
    public void ShouldReuseMicrosoftLoginReturnsTrueWithinReuseWindow()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(
            new MinecraftLaunchMicrosoftSessionReuseRequest(
                IsForceRestarting: false,
                AccessToken: "token",
                LastRefreshTick: 1000,
                CurrentTick: 1000 + 1000));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldReuseMicrosoftLoginReturnsFalseWhenForceRestarting()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(
            new MinecraftLaunchMicrosoftSessionReuseRequest(
                IsForceRestarting: true,
                AccessToken: "token",
                LastRefreshTick: 1000,
                CurrentTick: 1000 + 1000));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ResolveMicrosoftProfileMutationReturnsDuplicateUpdatePlanForCreatingProfile()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: true,
                SelectedProfileIndex: 0,
                Profiles:
                [
                    new MinecraftLaunchStoredProfile(
                        MinecraftLaunchStoredProfileKind.Microsoft,
                        "uuid-1",
                        "Player",
                        Server: null,
                        ServerName: null,
                        AccessToken: "old-token",
                        RefreshToken: "old-refresh",
                        LoginName: null,
                        Password: null,
                        ClientToken: null,
                        SkinHeadId: "uuid-1",
                        RawJson: "raw")
                ],
                ResultUuid: "uuid-1",
                ResultUsername: "Player",
                AccessToken: "new-token",
                RefreshToken: "new-refresh",
                ProfileJson: "new-raw"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate, result.Kind);
        Assert.AreEqual(0, result.TargetProfileIndex);
        Assert.AreEqual("你已经添加了这个档案...", result.NoticeMessage);
        Assert.AreEqual("new-token", result.UpdateProfile!.AccessToken);
        Assert.IsFalse(result.ShouldClearCreatingProfile);
    }

    [TestMethod]
    public void ResolveMicrosoftProfileMutationReturnsCreatePlanForNewProfile()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: false,
                SelectedProfileIndex: 0,
                Profiles: new List<MinecraftLaunchStoredProfile>(),
                ResultUuid: "uuid-2",
                ResultUsername: "Player Two",
                AccessToken: "token",
                RefreshToken: "refresh",
                ProfileJson: "raw"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.CreateNew, result.Kind);
        Assert.IsTrue(result.ShouldSelectCreatedProfile);
        Assert.IsTrue(result.ShouldClearCreatingProfile);
        Assert.AreEqual("Player Two", result.CreateProfile!.Username);
    }

    [TestMethod]
    public void ResolveMicrosoftProfileMutationReturnsSelectedUpdateForExistingProfile()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: false,
                SelectedProfileIndex: 1,
                Profiles:
                [
                    new MinecraftLaunchStoredProfile(
                        MinecraftLaunchStoredProfileKind.Offline,
                        "uuid-offline",
                        "Offline",
                        Server: null,
                        ServerName: null,
                        AccessToken: null,
                        RefreshToken: null,
                        LoginName: null,
                        Password: null,
                        ClientToken: null,
                        SkinHeadId: null,
                        RawJson: null),
                    new MinecraftLaunchStoredProfile(
                        MinecraftLaunchStoredProfileKind.Microsoft,
                        "uuid-3",
                        "Player",
                        Server: null,
                        ServerName: null,
                        AccessToken: "old",
                        RefreshToken: "old-refresh",
                        LoginName: null,
                        Password: null,
                        ClientToken: null,
                        SkinHeadId: "uuid-3",
                        RawJson: "old-raw")
                ],
                ResultUuid: "uuid-3",
                ResultUsername: "Player",
                AccessToken: "new-token",
                RefreshToken: "new-refresh",
                ProfileJson: "new-raw"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateSelected, result.Kind);
        Assert.AreEqual(1, result.TargetProfileIndex);
        Assert.AreEqual("new-raw", result.UpdateProfile!.RawJson);
    }

    [TestMethod]
    public void ResolveMicrosoftProfileMutationReturnsSelectedUpdateWhenUsernameChangesButUuidMatches()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                IsCreatingProfile: false,
                SelectedProfileIndex: 1,
                Profiles:
                [
                    new MinecraftLaunchStoredProfile(
                        MinecraftLaunchStoredProfileKind.Offline,
                        "uuid-offline",
                        "Offline",
                        Server: null,
                        ServerName: null,
                        AccessToken: null,
                        RefreshToken: null,
                        LoginName: null,
                        Password: null,
                        ClientToken: null,
                        SkinHeadId: null,
                        RawJson: null),
                    new MinecraftLaunchStoredProfile(
                        MinecraftLaunchStoredProfileKind.Microsoft,
                        "uuid-3",
                        "OldName",
                        Server: null,
                        ServerName: null,
                        AccessToken: "old",
                        RefreshToken: "old-refresh",
                        LoginName: null,
                        Password: null,
                        ClientToken: null,
                        SkinHeadId: "uuid-3",
                        RawJson: "old-raw")
                ],
                ResultUuid: "uuid-3",
                ResultUsername: "NewName",
                AccessToken: "new-token",
                RefreshToken: "new-refresh",
                ProfileJson: "new-raw"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateSelected, result.Kind);
        Assert.AreEqual(1, result.TargetProfileIndex);
        Assert.AreEqual("NewName", result.UpdateProfile!.Username);
        Assert.AreEqual("new-token", result.UpdateProfile.AccessToken);
    }

    [TestMethod]
    public void ResolveAuthProfileMutationReturnsUpdatePlanForExistingProfile()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                IsExistingProfile: true,
                SelectedProfileIndex: 2,
                ServerBaseUrl: "https://example.com/authserver",
                ServerName: "Example",
                ResultUuid: "uuid-auth",
                ResultUsername: "Player",
                AccessToken: "token",
                ClientToken: "client",
                LoginName: "login",
                Password: "password"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateSelected, result.Kind);
        Assert.AreEqual(2, result.TargetProfileIndex);
        Assert.AreEqual("Example", result.UpdateProfile!.ServerName);
    }

    [TestMethod]
    public void ResolveAuthProfileMutationPreservesLoginCredentialsForRefreshUpdate()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                IsExistingProfile: true,
                SelectedProfileIndex: 1,
                ServerBaseUrl: "https://auth.example.com/authserver",
                ServerName: "Example Server",
                ResultUuid: "uuid-refresh",
                ResultUsername: "Refreshed Player",
                AccessToken: "access-refresh",
                ClientToken: "client-refresh",
                LoginName: "account@example.com",
                Password: "password-123"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.UpdateSelected, result.Kind);
        Assert.AreEqual("https://auth.example.com/authserver", result.UpdateProfile!.Server);
        Assert.AreEqual("Refreshed Player", result.UpdateProfile.Username);
        Assert.AreEqual("access-refresh", result.UpdateProfile.AccessToken);
        Assert.AreEqual("client-refresh", result.UpdateProfile.ClientToken);
        Assert.AreEqual("account@example.com", result.UpdateProfile.LoginName);
        Assert.AreEqual("password-123", result.UpdateProfile.Password);
    }

    [TestMethod]
    public void ResolveAuthProfileMutationReturnsCreatePlanForNewProfile()
    {
        var result = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                IsExistingProfile: false,
                SelectedProfileIndex: null,
                ServerBaseUrl: "https://example.com/authserver",
                ServerName: "Example",
                ResultUuid: "uuid-auth",
                ResultUsername: "Player",
                AccessToken: "token",
                ClientToken: "client",
                LoginName: "login",
                Password: "password"));

        Assert.AreEqual(MinecraftLaunchProfileMutationKind.CreateNew, result.Kind);
        Assert.IsTrue(result.ShouldSelectCreatedProfile);
        Assert.AreEqual(MinecraftLaunchStoredProfileKind.Authlib, result.CreateProfile!.Kind);
    }
}
