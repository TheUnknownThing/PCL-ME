using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchMicrosoftFailureWorkflowServiceTest
{
    [TestMethod]
    public void ResolveOAuthRefreshFailureReturnsRequireReloginForExpiredPasswordMessages()
    {
        var result = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveOAuthRefreshFailure(
            "AADSTS: user must sign in again because password expired");

        Assert.AreEqual(MinecraftLaunchMicrosoftFailureResolutionKind.RequireRelogin, result.Kind);
    }

    [TestMethod]
    public void ResolveOAuthRefreshFailureReturnsRetryableFailureForGenericNetworkErrors()
    {
        var result = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveOAuthRefreshFailure("network timeout");

        Assert.AreEqual(MinecraftLaunchMicrosoftFailureResolutionKind.OfferIgnoreAndContinue, result.Kind);
        Assert.IsNull(result.StepLabel);
    }

    [TestMethod]
    public void ResolveXstsFailureReturnsPromptForKnownXstsCodes()
    {
        var result = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveXstsFailure("2148916233");

        Assert.AreEqual(MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort, result.Kind);
        Assert.IsNotNull(result.Prompt);
        Assert.AreEqual("Sign-in notice", result.Prompt.Title);
    }

    [TestMethod]
    public void ResolveMinecraftAccessTokenFailureReturnsWrappedMessageForForbidden()
    {
        var result = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveMinecraftAccessTokenFailure(HttpStatusCode.Forbidden);

        Assert.AreEqual(MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException, result.Kind);
        StringAssert.Contains(result.WrappedExceptionMessage, "VPN or accelerator");
    }

    [TestMethod]
    public void TryGetOwnershipFailurePromptReturnsPromptWhenOwnershipIsMissing()
    {
        const string json = """
                            {
                              "items": []
                            }
                            """;

        var result = MinecraftLaunchMicrosoftFailureWorkflowService.TryGetOwnershipFailurePrompt(json);

        Assert.IsNotNull(result);
        Assert.AreEqual("Sign-in failed", result.Title);
    }

    [TestMethod]
    public void ResolveMinecraftProfileFailureReturnsCreateProfilePromptForMissingProfile()
    {
        var result = MinecraftLaunchMicrosoftFailureWorkflowService.ResolveMinecraftProfileFailure(HttpStatusCode.NotFound);

        Assert.AreEqual(MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort, result.Kind);
        Assert.IsNotNull(result.Prompt);
        Assert.AreEqual("Sign-in failed", result.Prompt.Title);
        Assert.AreEqual("Create profile", result.Prompt.Options[0].Label);
    }
}
