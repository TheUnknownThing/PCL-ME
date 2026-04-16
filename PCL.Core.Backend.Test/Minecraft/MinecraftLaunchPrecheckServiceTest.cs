using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchPrecheckServiceTest
{
    [TestMethod]
    public void EvaluateReturnsFailureForInvalidInstancePathCharacter()
    {
        var request = CreateRequest() with { InstancePath = @"C:\Games\Bad;Path\" };

        var result = MinecraftLaunchPrecheckService.Evaluate(request);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(MinecraftLaunchPrecheckFailureKind.InstancePathContainsReservedCharacters, result.Failure?.Kind);
        Assert.AreEqual(@"C:\Games\Bad;Path\", result.Failure?.Path);
    }

    [TestMethod]
    public void EvaluateReturnsFailureForProfileRequirementMismatch()
    {
        var request = CreateRequest() with
        {
            HasLabyMod = true,
            SelectedProfileKind = MinecraftLaunchProfileKind.Legacy
        };

        var result = MinecraftLaunchPrecheckService.Evaluate(request);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(MinecraftLaunchPrecheckFailureKind.MicrosoftProfileRequired, result.Failure?.Kind);
    }

    [TestMethod]
    public void EvaluateAddsNonAsciiPathPromptWithPersistAction()
    {
        var request = CreateRequest() with
        {
            IsUtf8CodePage = true,
            IsInstancePathAscii = false
        };

        var result = MinecraftLaunchPrecheckService.Evaluate(request);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Prompts.Count);
        var pathPrompt = result.Prompts[0];
        Assert.AreEqual("launch.prompts.non_ascii_path.title", pathPrompt.Title.Key);
        Assert.AreEqual(3, pathPrompt.Buttons.Count);
        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchPromptActionKind.PersistNonAsciiPathWarningDisabled,
                MinecraftLaunchPromptActionKind.Continue
            },
            pathPrompt.Buttons[2].Actions.Select(action => action.Kind).ToArray());
    }

    [TestMethod]
    public void EvaluateAddsRequiredPurchasePromptForRestrictedRegionFlow()
    {
        var request = CreateRequest() with { IsRestrictedFeatureAllowed = false };

        var result = MinecraftLaunchPrecheckService.Evaluate(request);

        Assert.IsTrue(result.IsSuccess);
        var purchasePrompt = result.Prompts.Single();
        Assert.AreEqual("launch.prompts.purchase.required.title", purchasePrompt.Title.Key);
        Assert.IsFalse(purchasePrompt.Buttons[0].ClosesPrompt);
        CollectionAssert.AreEqual(
            new[]
            {
                MinecraftLaunchPromptActionKind.AppendLaunchArgument,
                MinecraftLaunchPromptActionKind.Continue
            },
            purchasePrompt.Buttons[1].Actions.Select(action => action.Kind).ToArray());
        Assert.AreEqual(MinecraftLaunchPromptActionKind.Abort, purchasePrompt.Buttons[2].Actions.Single().Kind);
    }

    [TestMethod]
    public void EvaluateSkipsPurchasePromptWhenMicrosoftProfileExists()
    {
        var request = CreateRequest() with { HasMicrosoftProfile = true };

        var result = MinecraftLaunchPrecheckService.Evaluate(request);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Prompts.Count);
    }

    private static MinecraftLaunchPrecheckRequest CreateRequest()
    {
        return new MinecraftLaunchPrecheckRequest(
            "Example",
            @"C:\Games\Instance\",
            @"C:\Games\Instance\",
            IsInstanceSelected: true,
            IsInstanceError: false,
            InstanceErrorDescription: null,
            IsUtf8CodePage: false,
            IsNonAsciiPathWarningDisabled: false,
            IsInstancePathAscii: true,
            ProfileValidationMessage: string.Empty,
            SelectedProfileKind: MinecraftLaunchProfileKind.Microsoft,
            HasLabyMod: false,
            LoginRequirement: MinecraftLaunchLoginRequirement.None,
            RequiredAuthServer: null,
            SelectedAuthServer: null,
            HasMicrosoftProfile: false,
            IsRestrictedFeatureAllowed: true);
    }
}
