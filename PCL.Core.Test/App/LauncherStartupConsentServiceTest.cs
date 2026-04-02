using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStartupConsentServiceTest
{
    [TestMethod]
    public void EvaluateReturnsAllRequiredPromptsInOrder()
    {
        var result = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.Debug,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: false,
            IsTelemetryDefault: true));

        CollectionAssert.AreEqual(
            new[] { "特殊版本提示", "协议授权", "启用遥测数据收集" },
            result.Prompts.Select(prompt => prompt.Title).ToArray());
    }

    [TestMethod]
    public void EvaluateSkipsSpecialBuildPromptWhenDisabled()
    {
        var result = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.Ci,
            IsSpecialBuildHintDisabled: true,
            HasAcceptedEula: true,
            IsTelemetryDefault: false));

        Assert.AreEqual(0, result.Prompts.Count);
    }

    [TestMethod]
    public void EvaluateReturnsNonClosingEulaViewButton()
    {
        var result = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.None,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: false,
            IsTelemetryDefault: false));

        var eulaPrompt = result.Prompts.Single();
        Assert.AreEqual("协议授权", eulaPrompt.Title);
        Assert.IsFalse(eulaPrompt.Buttons[2].ClosesPrompt);
        Assert.AreEqual(LauncherStartupPromptActionKind.OpenUrl, eulaPrompt.Buttons[2].Actions.Single().Kind);
    }

    [TestMethod]
    public void EvaluateReturnsSpecialBuildExitPath()
    {
        var result = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.Ci,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: true,
            IsTelemetryDefault: false));

        var prompt = result.Prompts.Single();
        CollectionAssert.AreEqual(
            new[]
            {
                LauncherStartupPromptActionKind.OpenUrl,
                LauncherStartupPromptActionKind.ExitLauncher
            },
            prompt.Buttons[1].Actions.Select(action => action.Kind).ToArray());
    }

    [TestMethod]
    public void EvaluateReturnsTelemetryPromptWithExplicitDecisionActions()
    {
        var result = LauncherStartupConsentService.Evaluate(new LauncherStartupConsentRequest(
            LauncherStartupSpecialBuildKind.None,
            IsSpecialBuildHintDisabled: false,
            HasAcceptedEula: true,
            IsTelemetryDefault: true));

        var telemetryPrompt = result.Prompts.Single();
        CollectionAssert.AreEqual(
            new[]
            {
                bool.TrueString,
                bool.FalseString
            },
            telemetryPrompt.Buttons.Select(button => button.Actions.Single().Value).ToArray());
    }
}
