using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchMicrosoftLoginExecutionServiceTest
{
    [TestMethod]
    public void GetInitialStepReturnsCachedFinishWhenSessionCanBeReused()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetInitialStep(
            new MinecraftLaunchMicrosoftLoginExecutionRequest(
                ShouldReuseCachedSession: true,
                HasRefreshToken: true));

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession, result.Kind);
        Assert.AreEqual(0.05, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetInitialStepPrefersRefreshWhenRefreshTokenExists()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetInitialStep(
            new MinecraftLaunchMicrosoftLoginExecutionRequest(
                ShouldReuseCachedSession: false,
                HasRefreshToken: true));

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.RefreshOAuthTokens, result.Kind);
        Assert.AreEqual(0.05, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterRefreshOAuthRequestsReloginWhenRefreshTokenExpires()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.RequireRelogin);

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.RequestDeviceCodeOAuthTokens, result.Kind);
        Assert.AreEqual(0.05, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterRefreshOAuthUsesCachedSessionWhenUserIgnoresNetworkFailure()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.IgnoreAndContinue);

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession, result.Kind);
        Assert.AreEqual(0.99, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterXboxSecurityTokenAdvancesToMinecraftAccessToken()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxSecurityToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftAccessToken, result.Kind);
        Assert.AreEqual(0.55, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterMinecraftProfileMovesToMutationApplication()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftProfile(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.ApplyProfileMutation, result.Kind);
        Assert.AreEqual(0.98, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterMinecraftAccessTokenUsesCachedSessionWhenUserContinuesWithoutRefresh()
    {
        var result = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftAccessToken(
            MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue);

        Assert.AreEqual(MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession, result.Kind);
        Assert.AreEqual(0.99, result.Progress, 0.0001);
    }
}
