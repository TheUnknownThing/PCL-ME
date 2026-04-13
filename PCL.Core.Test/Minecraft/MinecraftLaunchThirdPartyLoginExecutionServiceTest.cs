using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchThirdPartyLoginExecutionServiceTest
{
    [TestMethod]
    public void GetInitialStepStartsWithValidateWhenCachedSessionRecoveryIsAllowed()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetInitialStep(
            new MinecraftLaunchThirdPartyLoginExecutionRequest(ShouldSkipCachedSessionRecovery: false));

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.ValidateCachedSession, result.Kind);
        Assert.AreEqual(0.05, result.Progress, 0.0001);
        Assert.IsFalse(result.HasRetriedRefresh);
    }

    [TestMethod]
    public void GetInitialStepStartsWithAuthenticateWhenCachedSessionRecoveryIsSkipped()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetInitialStep(
            new MinecraftLaunchThirdPartyLoginExecutionRequest(ShouldSkipCachedSessionRecovery: true));

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.Authenticate, result.Kind);
        Assert.AreEqual(0.05, result.Progress, 0.0001);
    }

    [TestMethod]
    public void GetStepAfterValidateFailureMovesToRefresh()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterValidateFailure();

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession, result.Kind);
        Assert.AreEqual(0.25, result.Progress, 0.0001);
        Assert.IsFalse(result.HasRetriedRefresh);
    }

    [TestMethod]
    public void GetStepAfterRefreshFailureFallsBackToAuthenticateBeforeRetry()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshFailure(hasRetriedRefresh: false);

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.Authenticate, result.Kind);
        Assert.AreEqual(0.45, result.Progress, 0.0001);
        Assert.IsFalse(result.HasRetriedRefresh);
    }

    [TestMethod]
    public void GetStepAfterLoginSuccessRequestsSecondRefreshWhenNeeded()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterLoginSuccess(needsRefresh: true);

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession, result.Kind);
        Assert.AreEqual(0.65, result.Progress, 0.0001);
        Assert.IsTrue(result.HasRetriedRefresh);
    }

    [TestMethod]
    public void GetStepAfterRefreshFailureFailsAfterSecondRefreshAttempt()
    {
        var result = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshFailure(hasRetriedRefresh: true);

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.Fail, result.Kind);
        Assert.AreEqual("二轮刷新登录失败", result.FailureMessage);
        Assert.IsTrue(result.HasRetriedRefresh);
    }
}
