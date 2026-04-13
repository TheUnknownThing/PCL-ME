using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchAccountWorkflowServiceTest
{
    [TestMethod]
    public void GetPasswordLoginPromptReturnsRetryAndPasswordResetChoices()
    {
        var result = MinecraftLaunchAccountWorkflowService.GetPasswordLoginPrompt();

        Assert.AreEqual("需要使用密码登录", result.Title);
        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchAccountDecisionKind.Retry,
                MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                MinecraftLaunchAccountDecisionKind.Abort
            },
            result.Options.Select(option => option.Decision).ToArray());
        Assert.AreEqual("https://account.live.com/password/Change", result.Options[1].Url);
    }

    [TestMethod]
    public void GetMicrosoftRefreshNetworkErrorPromptReturnsIgnoreAndAbortOptions()
    {
        var result = MinecraftLaunchAccountWorkflowService.GetMicrosoftRefreshNetworkErrorPrompt("Step 4");

        Assert.AreEqual("账号信息获取失败", result.Title);
        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchAccountDecisionKind.IgnoreAndContinue,
                MinecraftLaunchAccountDecisionKind.Abort
            },
            result.Options.Select(option => option.Decision).ToArray());
        StringAssert.Contains(result.Message, "(Step 4)");
    }

    [TestMethod]
    public void TryGetMicrosoftXstsErrorPromptMapsBanResponse()
    {
        var result = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt("2148916227");

        Assert.IsNotNull(result);
        Assert.AreEqual("登录失败", result.Title);
        Assert.AreEqual(MinecraftLaunchAccountDecisionKind.Abort, result.Options.Single().Decision);
        Assert.IsTrue(result.IsWarning);
    }

    [TestMethod]
    public void TryGetMicrosoftXstsErrorPromptMapsMissingXboxSignup()
    {
        var result = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt("2148916233");

        Assert.IsNotNull(result);
        Assert.AreEqual("https://signup.live.com/signup", result.Options[0].Url);
        Assert.AreEqual(MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, result.Options[0].Decision);
    }

    [TestMethod]
    public void TryGetMicrosoftXstsErrorPromptMapsRestrictedRegionMessage()
    {
        var result = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt("2148916235");

        Assert.IsNotNull(result);
        StringAssert.Contains(result.Message, "加速器或 VPN");
        Assert.AreEqual(MinecraftLaunchAccountDecisionKind.Abort, result.Options.Single().Decision);
    }

    [TestMethod]
    public void TryGetMicrosoftXstsErrorPromptMapsUnderageFlowWithFollowup()
    {
        var result = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt("2148916238");

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Options.Count);
        Assert.AreEqual("https://account.live.com/editprof.aspx", result.Options[0].Url);
        Assert.IsNotNull(result.Options[0].Followup);
        StringAssert.Contains(result.Options[2].Followup!.Message, "根据打开的网页的说明");
    }

    [TestMethod]
    public void TryGetMicrosoftXstsErrorPromptReturnsNullForUnhandledResponse()
    {
        var result = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt("some other error");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetOwnershipPromptReturnsPurchaseAndCancelOptions()
    {
        var result = MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt();

        Assert.AreEqual("登录失败", result.Title);
        CollectionAssert.AreEqual(
            new[]
            {
                "购买 Minecraft",
                "取消"
            },
            result.Options.Select(option => option.Label).ToArray());
    }

    [TestMethod]
    public void GetCreateProfilePromptReturnsProfileCreationLink()
    {
        var result = MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt();

        Assert.AreEqual("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile", result.Options[0].Url);
        Assert.AreEqual(MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, result.Options[0].Decision);
    }

    [TestMethod]
    public void ResolveAuthProfileSelectionReturnsFailureWhenNoProfilesExist()
    {
        var result = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                ForceReselectProfile: true,
                CachedProfileId: null,
                ServerSelectedProfileId: null,
                AvailableProfiles: []));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.Fail, result.Kind);
        Assert.AreEqual("$你还没有创建角色，请在创建角色后再试！", result.FailureMessage);
        Assert.AreEqual("你还没有创建角色，无法更换！", result.NoticeMessage);
    }

    [TestMethod]
    public void ResolveAuthProfileSelectionReturnsOnlyProfileForForcedReselect()
    {
        var result = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                ForceReselectProfile: true,
                CachedProfileId: null,
                ServerSelectedProfileId: "solo",
                AvailableProfiles:
                [
                    new MinecraftLaunchAuthProfileOption("solo", "Only")
                ]));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.Resolved, result.Kind);
        Assert.AreEqual("solo", result.SelectedProfileId);
        Assert.AreEqual("你的账户中只有一个角色，无法更换！", result.NoticeMessage);
    }

    [TestMethod]
    public void ResolveAuthProfileSelectionUsesCachedProfileWhenServerRequiresSelection()
    {
        var result = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                ForceReselectProfile: false,
                CachedProfileId: "cached",
                ServerSelectedProfileId: null,
                AvailableProfiles:
                [
                    new MinecraftLaunchAuthProfileOption("cached", "Cached Role"),
                    new MinecraftLaunchAuthProfileOption("other", "Other Role")
                ]));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.Resolved, result.Kind);
        Assert.IsTrue(result.NeedsRefresh);
        Assert.AreEqual("cached", result.SelectedProfileId);
    }

    [TestMethod]
    public void ResolveAuthProfileSelectionRequestsPromptWhenCacheDoesNotMatch()
    {
        var result = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                ForceReselectProfile: true,
                CachedProfileId: "missing",
                ServerSelectedProfileId: "selected",
                AvailableProfiles:
                [
                    new MinecraftLaunchAuthProfileOption("first", "First"),
                    new MinecraftLaunchAuthProfileOption("second", "Second")
                ]));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.PromptForSelection, result.Kind);
        Assert.IsTrue(result.NeedsRefresh);
        Assert.AreEqual("选择使用的角色", result.PromptTitle);
        Assert.AreEqual(2, result.PromptOptions.Count);
    }

    [TestMethod]
    public void ResolveAuthProfileSelectionUsesServerSelectedProfileWhenPromptNotRequired()
    {
        var result = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                ForceReselectProfile: false,
                CachedProfileId: "cached",
                ServerSelectedProfileId: "server",
                AvailableProfiles:
                [
                    new MinecraftLaunchAuthProfileOption("server", "Server Role"),
                    new MinecraftLaunchAuthProfileOption("cached", "Cached Role")
                ]));

        Assert.AreEqual(MinecraftLaunchAuthProfileSelectionKind.Resolved, result.Kind);
        Assert.IsFalse(result.NeedsRefresh);
        Assert.AreEqual("server", result.SelectedProfileId);
        Assert.AreEqual("Server Role", result.SelectedProfileName);
    }
}
