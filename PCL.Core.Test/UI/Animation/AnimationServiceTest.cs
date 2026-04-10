using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.Test.UI.Animation;

[TestClass]
public sealed class AnimationServiceTest
{
    [TestMethod]
    public void NormalizeFpsSettingMatchesLauncherSliderStorage()
    {
        var cases = new (int StoredFpsLimit, int ExpectedFps)[]
        {
            (0, 1),
            (9, 10),
            (59, 60)
        };

        foreach (var (storedFpsLimit, expectedFps) in cases)
        {
            Assert.AreEqual(expectedFps, AnimationService.NormalizeFpsSetting(storedFpsLimit));
        }
    }

    [TestMethod]
    public void NormalizeSpeedSettingMatchesGoldLauncherMapping()
    {
        var cases = new (double StoredSpeedSetting, double ExpectedScale)[]
        {
            (0d, 0.1d),
            (9d, 1.0d),
            (29d, 3.0d),
            (30d, 200d)
        };

        foreach (var (storedSpeedSetting, expectedScale) in cases)
        {
            Assert.AreEqual(expectedScale, AnimationService.NormalizeSpeedSetting(storedSpeedSetting), 0.0001d);
        }
    }
}
