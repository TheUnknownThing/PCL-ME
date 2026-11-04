using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.ExtensionRuntimeTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
public sealed class FrontendExtensionInstallServiceTest
{
    private static ExtensionManifest Manifest(string id = "test.remote", string version = "1.0.0") => new()
    {
        Id = id, Name = "Remote extension", Version = version,
        EntryAssembly = "Remote.dll", EntryType = typeof(ExternalTestExtension).FullName
    };

    [TestMethod]
    [DataRow("https://github.com/owner/project", "owner/project")]
    [DataRow("https://github.com/owner/project.git", "owner/project")]
    [DataRow("https://github.com/owner/project/releases", "owner/project")]
    [DataRow("https://gitlab.example/team/subgroup/project/-/releases", "team/subgroup/project")]
    [DataRow("https://codeberg.org/owner/project", "owner/project")]
    public void RepositoryUrls_PreserveHostsAndSubgroups(string url, string project)
    {
        var repository = FrontendExtensionInstallService.ParseRepository(FrontendExtensionInstallService.ParseUrl(url));
        Assert.AreEqual(project, repository.Project);
        Assert.AreEqual(new Uri(url).Host, repository.Url.Host);
        Assert.IsTrue(repository.Url.AbsolutePath.EndsWith(project + ".git", StringComparison.Ordinal)
            || repository.Url.AbsolutePath.EndsWith(project, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("file:///tmp/package.zip")]
    [DataRow("ssh://git@example.com/owner/project")]
    [DataRow("https://user:password@example.com/owner/project")]
    [DataRow("not a URL")]
    public void PackageUrls_RejectUnsupportedSchemesAndEmbeddedCredentials(string url)
        => Assert.Throws<ExtensionInstallException>(() => FrontendExtensionInstallService.ParseUrl(url));

    [TestMethod]
    public async Task GitHubReleases_SeparateChannels_IgnoreDraftsAndSourceArchives()
    {
        using var handler = new RepositoryHandler(request => Json(new[]
        {
            Release("v2.0.0-alpha.1"), Release("v1.5.0-beta.2"), Release("v1.4.0", prerelease: true),
            Release("v1.0.0"), Release("v3.0.0", draft: true)
        }));
        using var http = new HttpClient(handler);
        var service = new FrontendExtensionInstallService(http);
        var releases = await service.GetReleasesAsync(Repository("https://github.com/owner/project"), default);
        Assert.AreEqual(4, releases.Count);
        Assert.AreEqual(1, releases.Count(release => release.Channel == ExtensionReleaseChannel.Alpha));
        Assert.AreEqual(2, releases.Count(release => release.Channel == ExtensionReleaseChannel.Beta));
        Assert.AreEqual(1, releases.Count(release => release.Channel == ExtensionReleaseChannel.Stable));
        Assert.IsTrue(handler.Requests.Single().StartsWith("https://api.github.com/repos/owner/project/releases?", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GitLabReleases_UseNestedProjectIdAndDirectAssets()
    {
        using var handler = new RepositoryHandler(_ => Json(new[] { new
        {
            tag_name = "v1.0.0-beta.1", name = "Beta", assets = new { links = new[]
            {
                new { name = "extension.pclext", url = "https://files.example/fallback", direct_asset_url = "https://files.example/extension.pclext" }
            } }
        } }));
        using var http = new HttpClient(handler);
        var releases = await new FrontendExtensionInstallService(http).GetReleasesAsync(Repository("https://git.example/team/group/project"), default);
        Assert.AreEqual("https://files.example/extension.pclext", releases.Single().Url.AbsoluteUri);
        StringAssert.Contains(handler.Requests.Single(), "team%2Fgroup%2Fproject");
        Assert.AreEqual(ExtensionReleaseChannel.Beta, releases.Single().Channel);
    }

    [TestMethod]
    public async Task ForgejoReleases_FallBackToGiteaApi()
    {
        using var handler = new RepositoryHandler(request => request.RequestUri!.AbsolutePath.StartsWith("/api/v4/", StringComparison.Ordinal)
            ? new(HttpStatusCode.NotFound) : Json(new[] { Release("v1.0.0") }));
        using var http = new HttpClient(handler);
        var releases = await new FrontendExtensionInstallService(http).GetReleasesAsync(Repository("https://codeberg.org/owner/project"), default);
        Assert.AreEqual(1, releases.Count);
        StringAssert.Contains(handler.Requests.Last(), "/api/v1/repos/owner/project/releases");
    }

    [TestMethod]
    public async Task ReleasePagination_ReachesOlderStableAssets()
    {
        using var handler = new RepositoryHandler(request => Json(request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
            ? new[] { Release("v1.0.0") } : Enumerable.Range(0, 100).Select(index => Release("v2.0.0-alpha." + index)).ToArray()));
        using var http = new HttpClient(handler);
        var releases = await new FrontendExtensionInstallService(http).GetReleasesAsync(Repository("https://github.com/owner/project"), default);
        Assert.AreEqual(101, releases.Count);
        Assert.AreEqual(ExtensionReleaseChannel.Stable, releases.Last().Channel);
    }

    [TestMethod]
    public async Task DirectPackage_InstallsDisabled_RejectsWrongUpdate_AndUpdatesCorrectExtension()
    {
        await using var fixture = new RuntimeFixture();
        var bytes = File.ReadAllBytes(MakePackage(fixture.Root, Manifest()));
        using var handler = new RepositoryHandler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        using var http = new HttpClient(handler);
        var service = new FrontendExtensionInstallService(http);
        var installed = await service.InstallUrlAsync(fixture.Host, new("https://files.example/extension.pclext"), null, default);
        Assert.IsFalse(installed.IsEnabled);
        bytes = File.ReadAllBytes(MakePackage(fixture.Root, Manifest("test.other", "2.0.0")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallUrlAsync(fixture.Host,
            new("https://files.example/update.pclext"), installed.Manifest.Id, default));
        Assert.AreEqual(1, fixture.Host.Extensions.Count);
        Assert.AreEqual("1.0.0", installed.Manifest.Version);
        bytes = File.ReadAllBytes(MakePackage(fixture.Root, Manifest(version: "2.0.0")));
        await service.InstallUrlAsync(fixture.Host, new("https://files.example/update.pclext"), installed.Manifest.Id, default);
        Assert.AreEqual("2.0.0", installed.Manifest.Version);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(Path.Combine(fixture.Root, "Staging")).Length);
    }

    [TestMethod]
    public async Task OversizedDownload_IsRejectedBeforeInstallation()
    {
        await using var fixture = new RuntimeFixture();
        using var handler = new RepositoryHandler(_ =>
        {
            var content = new ByteArrayContent([]);
            content.Headers.ContentLength = 256L * 1024 * 1024 + 1;
            return new(HttpStatusCode.OK) { Content = content };
        });
        using var http = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ExtensionInstallException>(() => new FrontendExtensionInstallService(http)
            .InstallUrlAsync(fixture.Host, new("https://files.example/extension.pclext"), null, default));
        Assert.AreEqual("package_too_large", error.Key);
        Assert.AreEqual(0, fixture.Host.Extensions.Count);
    }

    [TestMethod]
    public async Task SourceInstall_UsesSelectedBranchAndReallyPublishesTheProject()
    {
        await using var fixture = new RuntimeFixture();
        using var http = new HttpClient();
        string? checkout = null;
        var service = new FrontendExtensionInstallService(http, async (executable, arguments, directory, token) =>
        {
            if (executable == "dotnet") return await FrontendExtensionInstallService.RunProcessAsync(executable, arguments, directory, token);
            Assert.AreEqual("git", executable);
            if (arguments[0] == "ls-remote") return "abc\trefs/heads/feature/my-branch\nabc\trefs/heads/main\n";
            CollectionAssert.Contains(arguments, "feature/my-branch");
            checkout = arguments.Last();
            Directory.CreateDirectory(checkout);
            await File.WriteAllTextAsync(Path.Combine(checkout, "Remote.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup><Content Include="extension.json" CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
                </Project>
                """, token);
            await File.WriteAllTextAsync(Path.Combine(checkout, "Marker.cs"), "public class Marker { }", token);
            File.SetAttributes(Path.Combine(checkout, "Marker.cs"), FileAttributes.ReadOnly);
            await File.WriteAllTextAsync(Path.Combine(checkout, "extension.json"), JsonSerializer.Serialize(Manifest()), token);
            return "";
        });
        var repository = Repository("https://other-git.example/owner/project");
        CollectionAssert.AreEqual(new[] { "main", "feature/my-branch" }, (await service.GetBranchesAsync(repository, default)).ToArray());
        var installed = await service.InstallSourceAsync(fixture.Host, repository, "feature/my-branch", null, null, default);
        Assert.IsFalse(installed.IsEnabled);
        Assert.IsTrue(File.Exists(Path.Combine(installed.Directory, "Remote.dll")));
        Assert.IsFalse(Directory.Exists(checkout));
    }

    [TestMethod]
    public async Task CanceledSourceCheckout_LeavesNoPartialPackageOrTemporaryDirectory()
    {
        await using var fixture = new RuntimeFixture();
        using var http = new HttpClient();
        string? temporary = null;
        var service = new FrontendExtensionInstallService(http, (_, _, directory, _) =>
        {
            temporary = directory;
            throw new OperationCanceledException();
        });
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.InstallSourceAsync(fixture.Host,
            Repository("https://example.com/owner/project"), "main", null, null, default));
        Assert.IsFalse(Directory.Exists(temporary));
        Assert.AreEqual(0, fixture.Host.Extensions.Count);
    }

    [TestMethod]
    public async Task SourceProjectSelection_RejectsTraversalAndRequiresDisambiguation()
    {
        await using var fixture = new RuntimeFixture();
        foreach (var name in new[] { "One", "Two" })
        {
            var directory = Path.Combine(fixture.Root, name);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "extension.json"), JsonSerializer.Serialize(Manifest("test." + name.ToLowerInvariant())));
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".csproj"), "<Project />");
        }
        Assert.Throws<ExtensionInstallException>(() => FrontendExtensionInstallService.FindProject(fixture.Root, null, null));
        Assert.Throws<InvalidDataException>(() => FrontendExtensionInstallService.FindProject(fixture.Root, "../outside.csproj", null));
        Assert.AreEqual(Path.Combine(fixture.Root, "Two", "Two.csproj"), FrontendExtensionInstallService.FindProject(fixture.Root, null, "test.two"));
    }

    private static ExtensionRepository Repository(string url) => FrontendExtensionInstallService.ParseRepository(new(url));
    private static object Release(string tag, bool prerelease = false, bool draft = false) => new
    {
        tag_name = tag, name = tag, prerelease, draft, zipball_url = "https://example.com/source.zip",
        assets = new[] { new { name = "extension.pclext", browser_download_url = "https://files.example/" + tag + ".pclext" } }
    };
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };
    private sealed class RepositoryHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond(request));
        }
    }
}
