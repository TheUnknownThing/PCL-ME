using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchMicrosoftDeviceCodePromptServiceTest
{
    [TestMethod]
    public void BuildPromptPlanUsesCompleteVerificationUrlWhenAvailable()
    {
        const string json = """
            {
              "device_code": "device-code",
              "user_code": "ABCD-EFGH",
              "verification_uri": "https://microsoft.com/devicelogin",
              "verification_uri_complete": "https://microsoft.com/devicelogin?otc=ABCD-EFGH",
              "expires_in": 900,
              "interval": 5
            }
            """;

        var result = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(json);

        Assert.AreEqual("Sign in to Minecraft", result.Title);
        Assert.AreEqual("ABCD-EFGH", result.UserCode);
        Assert.AreEqual("device-code", result.DeviceCode);
        Assert.AreEqual("https://microsoft.com/devicelogin?otc=ABCD-EFGH", result.OpenBrowserUrl);
        Assert.AreEqual(MinecraftLaunchMicrosoftDeviceCodePromptService.DefaultPollUrl, result.PollUrl);
        Assert.AreEqual(5, result.PollIntervalSeconds);
        Assert.AreEqual(900, result.ExpiresInSeconds);
        StringAssert.Contains(result.Message, "code will be filled in automatically");
        StringAssert.Contains(result.LogMessage, result.OpenBrowserUrl);
    }

    [TestMethod]
    public void BuildPromptPlanFallsBackToManualVerificationUrlWhenCompleteUrlMissing()
    {
        const string json = """
            {
              "device_code": "device-code",
              "user_code": "ABCD-EFGH",
              "verification_uri": "https://microsoft.com/devicelogin",
              "expires_in": 900,
              "interval": 5
            }
            """;

        var result = MinecraftLaunchMicrosoftDeviceCodePromptService.BuildPromptPlan(json, "https://login.example.invalid/token");

        Assert.AreEqual("https://microsoft.com/devicelogin", result.OpenBrowserUrl);
        Assert.AreEqual("https://login.example.invalid/token", result.PollUrl);
        StringAssert.Contains(result.Message, "Enter code");
        StringAssert.Contains(result.Message, result.OpenBrowserUrl);
    }
}
