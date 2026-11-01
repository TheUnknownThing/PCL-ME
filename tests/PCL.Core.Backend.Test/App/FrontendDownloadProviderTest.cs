using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendDownloadProviderTest
{
    [TestMethod]
    public void MirrorPreferredUsesBmclFirstForMinecraftLibraries()
    {
        var provider = FrontendDownloadProvider.FromPreference(0);

        var result = provider.GetPreferredUrls("https://libraries.minecraft.net/com/example/demo/1.0/demo-1.0.jar");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/libraries/com/example/demo/1.0/demo-1.0.jar",
                "https://libraries.minecraft.net/com/example/demo/1.0/demo-1.0.jar"
            },
            result.ToArray());
    }

    [TestMethod]
    public void OfficialPreferredFallsBackToMirrorForMinecraftLibraries()
    {
        var provider = FrontendDownloadProvider.FromPreference(1);

        var result = provider.GetPreferredUrls("https://libraries.minecraft.net/com/example/demo/1.0/demo-1.0.jar");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://libraries.minecraft.net/com/example/demo/1.0/demo-1.0.jar",
                "https://bmclapi2.bangbang93.com/libraries/com/example/demo/1.0/demo-1.0.jar"
            },
            result.ToArray());
    }

    [TestMethod]
    public void OfficialOnlySkipsMirrorCandidates()
    {
        var provider = FrontendDownloadProvider.FromPreference(2);

        var result = provider.GetPreferredUrls("https://piston-data.mojang.com/v1/objects/client.jar");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://piston-data.mojang.com/v1/objects/client.jar"
            },
            result.ToArray());
    }

    [TestMethod]
    public void AssetObjectsFollowConfiguredOrdering()
    {
        var provider = FrontendDownloadProvider.FromPreference(0);

        var result = provider.GetAssetObjectUrls("ab/abcdef1234567890");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/assets/ab/abcdef1234567890",
                "https://resources.download.minecraft.net/ab/abcdef1234567890"
            },
            result.ToArray());
    }

    [TestMethod]
    public void FallbackMirrorsAreIncludedForCommunityCdnUrls()
    {
        var provider = FrontendDownloadProvider.FromPreference(1);

        var result = provider.GetPreferredUrls("https://cdn.modrinth.com/data/demo/demo.jar");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://cdn.modrinth.com/data/demo/demo.jar",
                "https://mod.mcimirror.top/data/demo/demo.jar"
            },
            result.ToArray());
    }

    [TestMethod]
    public void MirrorPreferredRewritesNeoForgeMetadataApis()
    {
        var provider = FrontendDownloadProvider.FromPreference(0);

        var result = provider.GetPreferredUrls("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
                "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"
            },
            result.ToArray());
    }

    [TestMethod]
    public void MirrorPreferredRewritesHttpsLiteLoaderMetadata()
    {
        var provider = FrontendDownloadProvider.FromPreference(0);

        var result = provider.GetPreferredUrls("https://dl.liteloader.com/versions/versions.json");

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json",
                "https://dl.liteloader.com/versions/versions.json"
            },
            result.ToArray());
    }
}
