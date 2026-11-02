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

        Assert.AreEqual("Password sign-in required", result.Title);
        Assert.AreEqual("launch.profile.microsoft.prompts.password_login.title", result.TitleText.Key);
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

        Assert.AreEqual("Account refresh failed", result.Title);
        Assert.AreEqual("launch.profile.microsoft.prompts.refresh_network_error.title", result.TitleText.Key);
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
        Assert.AreEqual("Sign-in failed", result.Title);
        Assert.AreEqual("launch.profile.microsoft.prompts.xsts.ban.title", result.TitleText.Key);
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
        StringAssert.Contains(result.Message, "VPN or accelerator");
        Assert.AreEqual("launch.profile.microsoft.prompts.xsts.region_restricted.message", result.MessageText.Key);
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
        StringAssert.Contains(result.Options[2].Followup!.Message, "opened page");
        Assert.AreEqual("launch.profile.microsoft.prompts.xsts.underage.followup.open_page.message", result.Options[2].Followup!.MessageText.Key);
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

        Assert.AreEqual("Sign-in failed", result.Title);
        Assert.AreEqual("launch.profile.microsoft.prompts.ownership.title", result.TitleText.Key);
        CollectionAssert.AreEqual(
            new[]
            {
                "Buy Minecraft",
                "Cancel"
            },
            result.Options.Select(option => option.Label).ToArray());
        Assert.AreEqual("launch.profile.microsoft.prompts.ownership.actions.buy_minecraft", result.Options[0].LabelText.Key);
    }

    [TestMethod]
    public void GetCreateProfilePromptReturnsProfileCreationLink()
    {
        var result = MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt();

        Assert.AreEqual("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile", result.Options[0].Url);
        Assert.AreEqual(MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, result.Options[0].Decision);
        Assert.AreEqual("Create profile", result.Options[0].Label);
        Assert.AreEqual("launch.profile.microsoft.prompts.create_profile.actions.create_profile", result.Options[0].LabelText.Key);
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
        Assert.AreEqual("$You have not created a profile yet. Please create one and try again!", result.FailureMessage);
        Assert.AreEqual("You have not created a profile yet, so it cannot be changed.", result.NoticeMessage);
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
        Assert.AreEqual("Your account only has one profile, so it cannot be changed.", result.NoticeMessage);
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
        Assert.AreEqual("Select a profile", result.PromptTitle);
        Assert.AreEqual("launch.profile.selection.prompt_title", result.PromptTitleText!.Key);
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
