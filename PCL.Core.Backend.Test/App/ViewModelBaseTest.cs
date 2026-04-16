using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class ViewModelBaseTest
{
    [TestMethod]
    public void TryNormalizeSelectionIndex_IgnoresTransientClearedSelection()
    {
        var result = ViewModelBase.TryNormalizeSelectionIndex(-1, 3, out var normalizedValue);

        Assert.IsFalse(result);
        Assert.AreEqual(0, normalizedValue);
    }

    [TestMethod]
    public void TryNormalizeSelectionIndex_ClampsPositiveSelectionIntoRange()
    {
        var result = ViewModelBase.TryNormalizeSelectionIndex(5, 3, out var normalizedValue);

        Assert.IsTrue(result);
        Assert.AreEqual(2, normalizedValue);
    }
}
