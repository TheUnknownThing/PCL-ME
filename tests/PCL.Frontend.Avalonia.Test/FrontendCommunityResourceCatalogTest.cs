using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;
using static PCL.Frontend.Avalonia.Test.FrontendProfileEditingTest;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendCommunityResourceCatalogTest
{
    private static readonly FrontendCommunityResourceQuery Query = new("", "", "", "", "", "");
    private static readonly FrontendInstanceComposition Composition = new(
        new(false, "", "", "", "", false, false, false, ""),
        null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    [TestMethod]
    public async Task Pagination_FetchesEachProviderOffsetOnceAndReusesBufferedResults()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        FrontendCommunityResourceQueryResult? previous = null;
        for (var target = 80; target <= 480; target += 40)
        {
            var result = await Search(catalog, Query, target);
            Assert.HasCount(target, result.State.Entries);
            Assert.IsTrue(result.State.HasMoreEntries);
            if (previous is not null)
            {
                CollectionAssert.AreEqual(previous.State.Entries.ToArray(), result.State.Entries.Take(previous.State.Entries.Count).ToArray());
            }

            previous = result;
        }

        foreach (var source in new[] { "Modrinth", "CurseForge" })
        {
            CollectionAssert.AreEqual(new[] { 0, 40, 80, 120, 160, 200 },
                handler.Requests.Where(request => request.Source == source).Select(request => request.Offset).ToArray());
        }

        Assert.AreSame(previous, await Search(catalog, Query, 80), "Returning to a loaded page must use the cache.");
        Assert.HasCount(12, handler.Requests);
    }

    [TestMethod]
    public async Task Pagination_DrainsBufferedResultsAfterBothSourcesAreExhausted()
    {
        using var handler = new CatalogHandler { TotalCount = 65 };
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var first = await Search(catalog, Query, 120);
        Assert.HasCount(120, first.State.Entries);
        Assert.IsTrue(first.State.HasMoreEntries);
        Assert.HasCount(4, handler.Requests);

        var last = await Search(catalog, Query, 160);
        Assert.HasCount(130, last.State.Entries);
        Assert.IsFalse(last.State.HasMoreEntries);
        Assert.AreEqual(130, last.State.TotalEntryCount);
        Assert.HasCount(4, handler.Requests, "Buffered matches must not require more provider requests.");
    }

    [TestMethod]
    public async Task CancellationWhileLoadingVersions_DoesNotPublishResultsOrTryAMirror()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.BeforeVersions = async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        using var cancellation = new CancellationTokenSource();
        var query = Query with { Source = "Modrinth" };
        var pending = Search(catalog, query, 40, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, handler.VersionRequests);

        handler.BeforeVersions = null;
        Assert.HasCount(40, (await Search(catalog, query, 40)).State.Entries);
        CollectionAssert.AreEqual(new[] { 0, 0 }, handler.Requests.Select(request => request.Offset).ToArray());
        Assert.AreEqual(2, handler.VersionRequests);
    }

    [TestMethod]
    [DataRow("Modrinth")]
    [DataRow("CurseForge")]
    public async Task Pagination_ContinuesFromRawOffsetsWhenClientFiltersDiscardEntries(string source)
    {
        using var handler = new CatalogHandler { SparseLoaderMatches = true };
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var query = Query with { Source = source, Loader = "Fabric" };
        var first = await Search(catalog, query, 40);
        var next = await Search(catalog, query, 80);

        Assert.HasCount(40, first.State.Entries);
        Assert.HasCount(80, next.State.Entries);
        Assert.IsTrue(next.State.Entries.All(entry => entry.Loader == "Fabric"));
        CollectionAssert.AreEqual(Enumerable.Range(0, 8).Select(index => index * 40).ToArray(),
            handler.Requests.Select(request => request.Offset).ToArray());
        CollectionAssert.AreEqual(first.State.Entries.ToArray(), next.State.Entries.Take(40).ToArray());
    }

    [TestMethod]
    [DataRow("Modrinth")]
    [DataRow("CurseForge")]
    public async Task Pagination_StopsRequestingAnExhaustedSource(string source)
    {
        using var handler = new CatalogHandler { TotalCount = 55 };
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var query = Query with { Source = source };
        await Search(catalog, query, 40);
        var last = await Search(catalog, query, 80);

        Assert.HasCount(55, last.State.Entries);
        Assert.AreEqual(55, last.State.TotalEntryCount);
        Assert.IsFalse(last.State.HasMoreEntries);
        Assert.AreSame(last, await Search(catalog, query, 120));
        CollectionAssert.AreEqual(new[] { 0, 40 }, handler.Requests.Select(request => request.Offset).ToArray());
    }

    [TestMethod]
    [DataRow("Modrinth")]
    [DataRow("CurseForge")]
    public async Task CancelledContinuation_StopsHttpWithoutFallbackAndPreservesThePreviousSnapshot(string source)
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var query = Query with { Source = source };
        var first = await Search(catalog, query, 80);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.BeforeSearch = async (_, offset, token) =>
        {
            Assert.AreEqual(80, offset);
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        };
        using var cancellation = new CancellationTokenSource();
        var pending = Search(catalog, query, 160, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.HasCount(3, handler.Requests, "Cancellation must not try another endpoint.");
        Assert.AreSame(first, await Search(catalog, query, 80));

        handler.BeforeSearch = null;
        var retried = await Search(catalog, query, 160);
        Assert.HasCount(160, retried.State.Entries);
        CollectionAssert.AreEqual(new[] { 0, 40, 80, 80, 120 }, handler.Requests.Select(request => request.Offset).ToArray());
        CollectionAssert.AreEqual(first.State.Entries.ToArray(), retried.State.Entries.Take(80).ToArray());
    }

    [TestMethod]
    public async Task ConcurrentQueries_SerializeContinuationsWithoutDownloadingTheSamePagesAgain()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.BeforeSearch = async (_, offset, token) =>
        {
            if (offset != 0) return;
            started.SetResult();
            await release.Task.WaitAsync(token);
        };
        var query = Query with { Source = "Modrinth" };
        var first = Search(catalog, query, 80);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var next = Search(catalog, query, 160);
        using var cancellation = new CancellationTokenSource();
        var cancelledWaiter = Search(catalog, query, 240, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => cancelledWaiter);
        release.SetResult();

        Assert.HasCount(80, (await first.WaitAsync(TimeSpan.FromSeconds(5))).State.Entries);
        Assert.HasCount(160, (await next.WaitAsync(TimeSpan.FromSeconds(5))).State.Entries);
        CollectionAssert.AreEqual(new[] { 0, 40, 80, 120 }, handler.Requests.Select(request => request.Offset).ToArray());
    }

    [TestMethod]
    public async Task FailedContinuation_CanRetryTheFailedOffsetWithoutLosingLoadedPages()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var query = Query with { Source = "Modrinth" };
        var first = await Search(catalog, query, 80);
        handler.FailSearch = true;
        var failed = await Search(catalog, query, 120);
        Assert.HasCount(80, failed.State.Entries);
        Assert.IsTrue(failed.State.HasMoreEntries);
        StringAssert.Contains(failed.State.HintText, "partial_results");

        handler.FailSearch = false;
        var retried = await Search(catalog, query, 120);
        Assert.HasCount(120, retried.State.Entries);
        Assert.AreEqual("", retried.State.HintText);
        CollectionAssert.AreEqual(first.State.Entries.ToArray(), retried.State.Entries.Take(80).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 40, 80, 80, 80 }, handler.Requests.Select(request => request.Offset).ToArray());
    }

    [TestMethod]
    public async Task DifferentFiltersAndRoutes_StartIndependentProviderCursors()
    {
        using var handler = new CatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new FrontendCommunityResourceCatalogService(client);
        var query = Query with { Source = "Modrinth" };
        foreach (var changed in new[] { query, query with { SearchText = "project" }, query with { Sort = "downloads" },
                     query with { Loader = "Fabric" }, query with { Version = "1.20.1" }, query with { Tag = "utility" } })
        {
            Assert.HasCount(40, (await Search(catalog, changed, 40)).State.Entries);
        }

        await catalog.QueryResourcesAsync(LauncherFrontendSubpageKey.DownloadShader, query, Composition, 0, 40);
        await catalog.QueryResourcesAsync(LauncherFrontendSubpageKey.DownloadMod, query, Composition, 1, 40);
        Assert.HasCount(8, handler.Requests);
        Assert.IsTrue(handler.Requests.All(request => request.Offset == 0));
    }

    [TestMethod]
    public async Task Loading_DisablesEveryPageCommandAndIgnoresRepeatedNavigation()
    {
        await FrontendMotionTest.RunAsync(() =>
        {
            using var fixture = new ProfileFixture();
            var launcher = fixture.Launcher;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            void Set(string name, object value) => typeof(LauncherViewModel).GetField(name, flags)!.SetValue(launcher, value);
            void Invoke(string name, params object[] arguments) => typeof(LauncherViewModel).GetMethod(name, flags)!.Invoke(launcher, arguments);
            Set("_downloadResourcePageIndex", 1);
            Set("_downloadResourceTotalPages", 5);
            Set("_downloadResourceHasMoreEntries", true);
            Invoke("SetDownloadResourceLoading", false);
            Invoke("RebuildDownloadResourcePaginationItems");
            var jump = launcher.DownloadResourcePaginationItems.Last().Command!;
            using var cancellation = new CancellationTokenSource();
            Set("_downloadResourceRefreshCts", cancellation);
            var changes = new List<string?>();
            launcher.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
            var commandNotifications = 0;
            launcher.NextDownloadResourcePageCommand.CanExecuteChanged += (_, _) => commandNotifications++;

            Invoke("SetDownloadResourceLoading", true);
            Assert.IsTrue(launcher.ShowDownloadResourceLoadingCard);
            Assert.IsFalse(launcher.ShowDownloadResourceContent);
            Assert.IsFalse(launcher.CanGoToPreviousDownloadResourcePage);
            Assert.IsFalse(launcher.CanGoToNextDownloadResourcePage);
            foreach (var command in new[] { launcher.FirstDownloadResourcePageCommand, launcher.PreviousDownloadResourcePageCommand,
                         launcher.NextDownloadResourcePageCommand, jump })
            {
                Assert.IsFalse(command.CanExecute(null));
                command.Execute(null);
            }

            Assert.IsTrue(launcher.DownloadResourcePaginationItems.Where(item => item.Command is not null).All(item => !item.Command!.CanExecute(null)));
            Assert.AreEqual("2", launcher.DownloadResourcePageLabel);
            Assert.IsFalse(cancellation.IsCancellationRequested, "Repeated page clicks must not cancel the pending request.");
            Invoke("SetDownloadResourceLoading", false);
            Assert.IsTrue(launcher.CanGoToPreviousDownloadResourcePage);
            Assert.IsTrue(launcher.CanGoToNextDownloadResourcePage);
            Assert.IsTrue(jump.CanExecute(null));
            Assert.AreEqual(2, commandNotifications);
            CollectionAssert.Contains(changes, nameof(LauncherViewModel.CanGoToNextDownloadResourcePage));
            Set("_downloadResourceRefreshCts", null!);
            return Task.CompletedTask;
        });
    }

    private static Task<FrontendCommunityResourceQueryResult> Search(FrontendCommunityResourceCatalogService catalog,
        FrontendCommunityResourceQuery query, int target, CancellationToken cancellationToken = default) =>
        catalog.QueryResourcesAsync(LauncherFrontendSubpageKey.DownloadMod, query, Composition, 0, target, cancellationToken);

    private sealed class CatalogHandler : HttpMessageHandler
    {
        public ConcurrentQueue<(string Source, int Offset)> Requests { get; } = new();
        public int TotalCount { get; init; } = 10000;
        public bool SparseLoaderMatches { get; init; }
        public bool FailSearch { get; set; }
        public Func<string, int, CancellationToken, Task>? BeforeSearch { get; set; }
        public Func<CancellationToken, Task>? BeforeVersions { get; set; }
        public int VersionRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            if (!uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
            {
                VersionRequests++;
                if (BeforeVersions is { } beforeVersions) await beforeVersions(cancellationToken);
                return Json(new JsonObject { ["versions"] = new JsonArray(new JsonObject { ["id"] = "1.20.1", ["type"] = "release" }) });
            }

            var parameters = uri.Query.TrimStart('?').Split('&').Select(part => part.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
            var source = uri.Host.Contains("modrinth", StringComparison.Ordinal) || uri.AbsolutePath.Contains("modrinth", StringComparison.Ordinal)
                ? "Modrinth" : "CurseForge";
            var offset = int.Parse(parameters.GetValueOrDefault(source == "Modrinth" ? "offset" : "index", "0"));
            Requests.Enqueue((source, offset));
            if (BeforeSearch is { } before) await before(source, offset, cancellationToken);
            if (FailSearch) return new HttpResponseMessage(HttpStatusCode.Forbidden);
            var entries = new JsonArray();
            for (var index = offset; index < Math.Min(offset + 40, TotalCount); index++)
            {
                var fabric = !SparseLoaderMatches || index % 4 == 0;
                var downloads = 100000 - index * 2 - (source == "Modrinth" ? 0 : 1);
                entries.Add(source == "Modrinth"
                    ? new JsonObject
                    {
                        ["project_id"] = "modrinth-" + index, ["title"] = "project " + index, ["project_type"] = "mod",
                        ["downloads"] = downloads, ["categories"] = new JsonArray("utility", fabric ? "fabric" : "forge"),
                        ["versions"] = new JsonArray("1.20.1")
                    }
                    : new JsonObject
                    {
                        ["id"] = index + 1, ["name"] = "project " + index, ["downloadCount"] = downloads,
                        ["latestFilesIndexes"] = new JsonArray(new JsonObject { ["gameVersion"] = "1.20.1", ["modLoader"] = fabric ? 4 : 1 })
                    });
            }

            return Json(source == "Modrinth"
                ? new JsonObject { ["hits"] = entries, ["total_hits"] = TotalCount }
                : new JsonObject { ["data"] = entries, ["pagination"] = new JsonObject { ["index"] = offset, ["resultCount"] = entries.Count, ["totalCount"] = TotalCount } });
        }

        private static HttpResponseMessage Json(JsonObject content) => new(HttpStatusCode.OK) { Content = new StringContent(content.ToJsonString()) };
    }
}
