using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class FrontendRealTimeLogSettingsServiceTest
{
    [TestMethod]
    public void ResolveLineLimitMapsLegacySliderBreakpoints()
    {
        Assert.AreEqual(50, FrontendRealTimeLogSettingsService.ResolveLineLimit(0));
        Assert.AreEqual(100, FrontendRealTimeLogSettingsService.ResolveLineLimit(5));
        Assert.AreEqual(500, FrontendRealTimeLogSettingsService.ResolveLineLimit(13));
        Assert.AreEqual(2000, FrontendRealTimeLogSettingsService.ResolveLineLimit(28));
        Assert.IsNull(FrontendRealTimeLogSettingsService.ResolveLineLimit(29));
    }

    [TestMethod]
    public void FormatLineLimitLabelMatchesResolvedLimit()
    {
        Assert.AreEqual("500", FrontendRealTimeLogSettingsService.FormatLineLimitLabel(13));
        Assert.AreEqual("Unlimited", FrontendRealTimeLogSettingsService.FormatLineLimitLabel(29));
    }
}
