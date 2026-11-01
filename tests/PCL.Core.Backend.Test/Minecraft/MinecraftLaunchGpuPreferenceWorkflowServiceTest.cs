using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchGpuPreferenceWorkflowServiceTest
{
    [TestMethod]
    public void BuildFailurePlanReturnsDirectLoggingWhenAlreadyAdmin()
    {
        var result = MinecraftLaunchGpuPreferenceWorkflowService.BuildFailurePlan(
            new MinecraftLaunchGpuPreferenceFailureRequest(
                @"C:\Java\bin\javaw.exe",
                WantHighPerformance: true,
                IsRunningAsAdmin: true));

        Assert.AreEqual(MinecraftLaunchGpuPreferenceFailureActionKind.LogDirectFailure, result.ActionKind);
        Assert.IsNull(result.AdminRetryArguments);
    }

    [TestMethod]
    public void BuildFailurePlanReturnsDirectLoggingWhenHighPerformanceIsDisabled()
    {
        var result = MinecraftLaunchGpuPreferenceWorkflowService.BuildFailurePlan(
            new MinecraftLaunchGpuPreferenceFailureRequest(
                @"C:\Java\bin\javaw.exe",
                WantHighPerformance: false,
                IsRunningAsAdmin: false));

        Assert.AreEqual(MinecraftLaunchGpuPreferenceFailureActionKind.LogDirectFailure, result.ActionKind);
        Assert.IsNull(result.RetryLogMessage);
    }

    [TestMethod]
    public void BuildFailurePlanReturnsAdminRetryArgumentsWhenEligible()
    {
        var result = MinecraftLaunchGpuPreferenceWorkflowService.BuildFailurePlan(
            new MinecraftLaunchGpuPreferenceFailureRequest(
                @"C:\Java\bin\javaw.exe",
                WantHighPerformance: true,
                IsRunningAsAdmin: false));

        Assert.AreEqual(MinecraftLaunchGpuPreferenceFailureActionKind.RetryAsAdmin, result.ActionKind);
        Assert.AreEqual("--gpu \"C:\\Java\\bin\\javaw.exe\"", result.AdminRetryArguments);
        Assert.IsNotNull(result.RetryFailureHintMessage);
    }
}
