using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchAuthlibRequestWorkflowServiceTest
{
    [TestMethod]
    public void BuildAuthenticateRequestReturnsJsonRequestPlan()
    {
        var result = MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
            "https://auth.example.invalid/authserver",
            "demo@example.invalid",
            "demo-password");

        Assert.AreEqual("POST", result.Method);
        Assert.AreEqual("https://auth.example.invalid/authserver/authenticate", result.Url);
        Assert.AreEqual("application/json", result.ContentType);
        Assert.AreEqual("zh-CN", result.Headers!["Accept-Language"]);
        Assert.AreEqual("demo@example.invalid", JsonNode.Parse(result.Body!)?["username"]?.ToString());
    }

    [TestMethod]
    public void BuildRefreshRequestReturnsRefreshEndpointAndBody()
    {
        var result = MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
            "https://auth.example.invalid/authserver",
            "CachedDemo",
            "cached-id",
            "token");

        Assert.AreEqual("https://auth.example.invalid/authserver/refresh", result.Url);
        Assert.AreEqual("application/json", result.ContentType);
        Assert.AreEqual("CachedDemo", JsonNode.Parse(result.Body!)?["selectedProfile"]?["name"]?.ToString());
    }

    [TestMethod]
    public void BuildMetadataRequestStripsAuthserverSuffix()
    {
        var result = MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(
            "https://auth.example.invalid/authserver");

        Assert.AreEqual("GET", result.Method);
        Assert.AreEqual("https://auth.example.invalid", result.Url);
        Assert.IsNull(result.ContentType);
    }
}
