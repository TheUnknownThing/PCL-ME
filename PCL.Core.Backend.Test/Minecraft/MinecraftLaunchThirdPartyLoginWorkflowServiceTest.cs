using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchThirdPartyLoginWorkflowServiceTest
{
    [TestMethod]
    public void GetValidationTimeoutFailureBuildsWrappedTimeoutMessage()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetValidationTimeoutFailure("gateway timeout");

        Assert.AreEqual("第三方验证失败", result.DialogTitle);
        StringAssert.Contains(result.DialogMessage, "连接登录服务器超时");
        StringAssert.Contains(result.DialogMessage, "gateway timeout");
        Assert.AreEqual(result.DialogMessage, result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void ResolveValidationHttpFailureReturnsWrappedTimeoutFailureForTimeouts()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveValidationHttpFailure("request timeout", "gateway timeout");

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped, result.Kind);
        Assert.IsNotNull(result.Failure);
        StringAssert.Contains(result.WrappedExceptionMessage!, "连接登录服务器超时");
    }

    [TestMethod]
    public void ResolveValidationHttpFailureAdvancesToRefreshForNonTimeoutHttpFailures()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveValidationHttpFailure("403 forbidden", "blocked");

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.AdvanceToStep, result.Kind);
        Assert.IsNotNull(result.NextStep);
        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession, result.NextStep.Kind);
    }

    [TestMethod]
    public void GetValidationFailureUsesValidationPrefix()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetValidationFailure("boom");

        Assert.AreEqual("验证登录失败: boom", result.DialogMessage);
        Assert.IsNull(result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void ResolveValidationFailureRequestsPromptThenRethrow()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveValidationFailure("boom");

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndRethrow, result.Kind);
        Assert.IsNotNull(result.Failure);
    }

    [TestMethod]
    public void GetRefreshFailureUsesRefreshPrefix()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetRefreshFailure("boom");

        Assert.AreEqual("刷新登录失败: boom", result.DialogMessage);
        Assert.IsNull(result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void ResolveRefreshFailureAdvancesToAuthenticateOnFirstFailure()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveRefreshFailure("boom", hasRetriedRefresh: false);

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndAdvance, result.Kind);
        Assert.IsNotNull(result.NextStep);
        Assert.AreEqual(MinecraftLaunchThirdPartyLoginStepKind.Authenticate, result.NextStep.Kind);
    }

    [TestMethod]
    public void ResolveRefreshFailureThrowsWrappedMessageAfterRetriedFailure()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveRefreshFailure("boom", hasRetriedRefresh: true);

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped, result.Kind);
        Assert.AreEqual("二轮刷新登录失败", result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void GetLoginHttpFailurePrefersParsedErrorMessage()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetLoginHttpFailure(
            "raw exception",
            """{"errorMessage":"Invalid password"}""");

        Assert.AreEqual("刷新登录失败: raw exception", result.DialogMessage);
        Assert.AreEqual("$登录失败：Invalid password", result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void ResolveLoginHttpFailureReturnsWrappedFailure()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveLoginHttpFailure(
            "raw exception",
            """{"errorMessage":"Invalid password"}""");

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped, result.Kind);
        Assert.AreEqual("$登录失败：Invalid password", result.WrappedExceptionMessage);
    }

    [TestMethod]
    public void GetLoginHttpFailureFallsBackToGenericNetworkMessage()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetLoginHttpFailure(
            "raw exception",
            "not json");

        Assert.AreEqual("刷新登录失败: raw exception", result.DialogMessage);
        StringAssert.Contains(result.WrappedExceptionMessage!, "第三方验证登录失败");
        StringAssert.Contains(result.WrappedExceptionMessage!, "not json");
    }

    [TestMethod]
    public void GetLoginFailureWrapsGenericFailureMessage()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.GetLoginFailure("raw exception");

        Assert.AreEqual("刷新登录失败: raw exception", result.DialogMessage);
        StringAssert.Contains(result.WrappedExceptionMessage!, "第三方验证登录失败");
        StringAssert.Contains(result.WrappedExceptionMessage!, "raw exception");
    }

    [TestMethod]
    public void ResolveLoginFailureReturnsWrappedFailure()
    {
        var result = MinecraftLaunchThirdPartyLoginWorkflowService.ResolveLoginFailure("raw exception");

        Assert.AreEqual(MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped, result.Kind);
        StringAssert.Contains(result.WrappedExceptionMessage!, "第三方验证登录失败");
    }
}
