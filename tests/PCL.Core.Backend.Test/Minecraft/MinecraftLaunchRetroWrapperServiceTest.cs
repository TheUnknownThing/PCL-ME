using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchRetroWrapperServiceTest
{
    [TestMethod]
    public void ShouldUseMatchesLauncherLegacyRules()
    {
        Assert.IsTrue(MinecraftLaunchRetroWrapperService.ShouldUse(
            new MinecraftLaunchRetroWrapperRequest(
                new DateTime(2013, 6, 25),
                99,
                false,
                false)));

        Assert.IsTrue(MinecraftLaunchRetroWrapperService.ShouldUse(
            new MinecraftLaunchRetroWrapperRequest(
                new DateTime(2012, 1, 1),
                20,
                false,
                false)));

        Assert.IsFalse(MinecraftLaunchRetroWrapperService.ShouldUse(
            new MinecraftLaunchRetroWrapperRequest(
                new DateTime(2012, 1, 1),
                20,
                true,
                false)));
    }
}
