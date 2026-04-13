using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
public sealed class FrontendSetupUpdateStatusServiceTest
{
    [TestMethod]
    public void SelectLatestGithubRelease_StableChannelIgnoresPrereleases()
    {
        var releases = new[]
        {
            new FrontendSetupUpdateStatusService.GithubRelease(
                "v2.14.5-beta.1",
                "https://example.invalid/releases/v2.14.5-beta.1",
                "beta",
                Draft: false,
                Prerelease: true,
                PublishedAt: new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero),
                Assets:
                [
                    new FrontendSetupUpdateStatusService.GithubReleaseAsset(
                        "PCL-ME-win-x64.zip",
                        "https://example.invalid/downloads/beta-win-x64.zip")
                ]),
            new FrontendSetupUpdateStatusService.GithubRelease(
                "v2.14.4",
                "https://example.invalid/releases/v2.14.4",
                "stable",
                Draft: false,
                Prerelease: false,
                PublishedAt: new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero),
                Assets:
                [
                    new FrontendSetupUpdateStatusService.GithubReleaseAsset(
                        "PCL-ME-win-x64.zip",
                        "https://example.invalid/downloads/stable-win-x64.zip")
                ])
        };

        var result = FrontendSetupUpdateStatusService.SelectLatestGithubRelease(
            releases,
            FrontendSetupUpdateStatusService.UpdateChannel.Stable,
            "win-x64");

        Assert.AreEqual("2.14.4", result.VersionName);
        Assert.AreEqual("https://example.invalid/downloads/stable-win-x64.zip", result.DownloadUrl);
        Assert.AreEqual("https://example.invalid/releases/v2.14.4", result.ReleaseUrl);
        Assert.AreEqual("GitHub", result.SourceName);
    }

    [TestMethod]
    public void SelectLatestGithubRelease_BetaChannelPrefersNewestVersionAndMatchingAsset()
    {
        var releases = new[]
        {
            new FrontendSetupUpdateStatusService.GithubRelease(
                "v2.14.5-beta.1",
                "https://example.invalid/releases/v2.14.5-beta.1",
                "beta",
                Draft: false,
                Prerelease: true,
                PublishedAt: new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero),
                Assets:
                [
                    new FrontendSetupUpdateStatusService.GithubReleaseAsset(
                        "PCL-ME-linux-x64.tar.gz",
                        "https://example.invalid/downloads/beta-linux-x64.tar.gz"),
                    new FrontendSetupUpdateStatusService.GithubReleaseAsset(
                        "PCL-ME-win-x64.zip",
                        "https://example.invalid/downloads/beta-win-x64.zip")
                ]),
            new FrontendSetupUpdateStatusService.GithubRelease(
                "v2.14.4",
                "https://example.invalid/releases/v2.14.4",
                "stable",
                Draft: false,
                Prerelease: false,
                PublishedAt: new DateTimeOffset(2026, 4, 13, 0, 0, 0, TimeSpan.Zero),
                Assets:
                [
                    new FrontendSetupUpdateStatusService.GithubReleaseAsset(
                        "PCL-ME-win-x64.zip",
                        "https://example.invalid/downloads/stable-win-x64.zip")
                ])
        };

        var result = FrontendSetupUpdateStatusService.SelectLatestGithubRelease(
            releases,
            FrontendSetupUpdateStatusService.UpdateChannel.Beta,
            "win-x64");

        Assert.AreEqual("2.14.5-beta.1", result.VersionName);
        Assert.AreEqual("https://example.invalid/downloads/beta-win-x64.zip", result.DownloadUrl);
        Assert.AreEqual("https://example.invalid/releases/v2.14.5-beta.1", result.ReleaseUrl);
    }
}
