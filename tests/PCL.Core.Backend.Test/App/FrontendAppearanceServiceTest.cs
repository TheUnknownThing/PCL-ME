using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendAppearanceServiceTest
{
    [TestMethod]
    public void BuildDisplayFontOptions_KeepsDynamicFontOrderWhileLocalizingDefaultEntry()
    {
        var sourceOptions = FrontendAppearanceService.GetFontOptions();
        var displayOptions = FrontendAppearanceService.BuildDisplayFontOptions("默认字体");

        Assert.AreEqual(sourceOptions.Count, displayOptions.Count);
        Assert.IsTrue(displayOptions.Count > 0);
        Assert.AreEqual("默认字体", displayOptions[0]);

        CollectionAssert.AreEqual(
            sourceOptions.Skip(1).ToArray(),
            displayOptions.Skip(1).ToArray());
    }
}
