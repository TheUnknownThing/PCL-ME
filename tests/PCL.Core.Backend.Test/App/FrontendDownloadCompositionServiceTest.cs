using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Testing;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendDownloadCompositionServiceTest
{
    [TestMethod]
    [DataRow("Default favorites")]
    [DataRow("Default Favorites")]
    [DataRow("Default Favourites")]
    [DataRow("Standardfavoriten")]
    public void ResolveFavoriteTargetDisplayName_LocalizesKnownDefaultNames(string storedName)
    {
        var i18n = new DictionaryI18nService(new Dictionary<string, string>
        {
            ["download.favorites.targets.default_name"] = "Localized default"
        });

        var displayName = FrontendDownloadCompositionService.ResolveFavoriteTargetDisplayName(
            storedName,
            "default",
            i18n);

        Assert.AreEqual("Localized default", displayName);
    }

    [TestMethod]
    public void ResolveFavoriteTargetDisplayName_PreservesCustomDefaultTargetName()
    {
        var i18n = new DictionaryI18nService(new Dictionary<string, string>
        {
            ["download.favorites.targets.default_name"] = "Localized default"
        });

        var displayName = FrontendDownloadCompositionService.ResolveFavoriteTargetDisplayName(
            "My mod list",
            "default",
            i18n);

        Assert.AreEqual("My mod list", displayName);
    }
}
