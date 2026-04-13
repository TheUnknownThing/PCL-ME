using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchGameArgumentServiceTest
{
    [TestMethod]
    public void BuildLegacyPlanAddsResolutionArgsAndFixesOptiFineTweaker()
    {
        var result = MinecraftLaunchGameArgumentService.BuildLegacyPlan(
            new MinecraftLaunchLegacyGameArgumentRequest(
                MinecraftArguments: "--username player --tweakClass optifine.OptiFineTweaker",
                UseRetroWrapper: true,
                HasForgeOrLiteLoader: true,
                HasOptiFine: true));

        StringAssert.Contains(result.Arguments, "--height ${resolution_height} --width ${resolution_width}");
        StringAssert.Contains(result.Arguments, "--tweakClass com.zero.retrowrapper.RetroTweaker");
        StringAssert.EndsWith(result.Arguments, "--tweakClass optifine.OptiFineForgeTweaker");
        Assert.IsTrue(result.ShouldRewriteOptiFineTweakerInJson);
    }

    [TestMethod]
    public void BuildModernPlanDeduplicatesMergedFlags()
    {
        var result = MinecraftLaunchGameArgumentService.BuildModernPlan(
            new MinecraftLaunchModernGameArgumentRequest(
                ["--tweakClass", "foo.Bar", "--tweakClass", "foo.Bar", "--username", "player"],
                HasForgeOrLiteLoader: false,
                HasOptiFine: false));

        Assert.AreEqual("--tweakClass foo.Bar --username player", result.Arguments);
    }
}
