using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendDownloadRemoteCatalogService
{
    private static RemotePayload<JsonObject> FetchJsonObject(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<JsonObject>(
                    sources[index],
                    JsonNode.Parse(FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)))?.AsObject()
                    ?? throw new InvalidOperationException($"Unable to parse JSON object: {sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static RemotePayload<JsonArray> FetchJsonArray(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<JsonArray>(
                    sources[index],
                    JsonNode.Parse(FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)))?.AsArray()
                    ?? throw new InvalidOperationException($"Unable to parse JSON array: {sources[index].Url}"));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static RemotePayload<string> FetchString(IReadOnlyList<RemoteSource> sources, int versionSourceIndex)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            try
            {
                return new RemotePayload<string>(
                    sources[index],
                    FetchStringContent(sources[index], GetRequestTimeout(versionSourceIndex, index)));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote catalog: {sources.FirstOrDefault()?.Url ?? "unknown"}", lastError);
    }

    private static string FetchStringContent(RemoteSource source, TimeSpan timeout)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
        request.Headers.UserAgent.ParseAdd("PCL-ME-Frontend");
        using var cts = new CancellationTokenSource(timeout);
        using var response = HttpClient.Send(request, cts.Token);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
    }

    private static TimeSpan GetRequestTimeout(int versionSourceIndex, int sourceAttemptIndex)
    {
        return versionSourceIndex switch
        {
            0 => sourceAttemptIndex == 0 ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(10),
            1 => sourceAttemptIndex == 0 ? TimeSpan.FromSeconds(6) : TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(15)
        };
    }

    private static IReadOnlyList<RemoteSource> CreateClientSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Mojang Official", "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", false));
    }

    private static IReadOnlyList<RemoteSource> CreateOptiFineSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("OptiFine Official", "https://optifine.net/downloads", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/optifine/versionList", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeListSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge Official", "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/forge/minecraft", false));
    }

    private static IReadOnlyList<RemoteSource> CreateForgeVersionSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = minecraftVersion.Replace("-", "_", StringComparison.Ordinal);
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Forge Official", $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{normalizedVersion}.html", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/forge/minecraft/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLatestSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge Official", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateNeoForgeLegacySources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("NeoForge Official", "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricRootSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric Official", "https://meta.fabricmc.net/v2/versions", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions", false));
    }

    private static IReadOnlyList<RemoteSource> CreateFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("Fabric Official", $"https://meta.fabricmc.net/v2/versions/loader/{normalizedVersion}", true),
            new RemoteSource("BMCLAPI", $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{normalizedVersion}", false));
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Legacy Fabric Official", "https://meta.legacyfabric.net/v2/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLegacyFabricLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "1.12.2" : minecraftVersion;
        return
        [
            new RemoteSource("Legacy Fabric Official", $"https://meta.legacyfabric.net/v2/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltRootSources(int versionSourceIndex)
    {
        return
        [
            new RemoteSource("Quilt Official", "https://meta.quiltmc.org/v3/versions", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateQuiltLoaderSources(int versionSourceIndex, string minecraftVersion)
    {
        var normalizedVersion = string.IsNullOrWhiteSpace(minecraftVersion) ? "latest" : minecraftVersion;
        return
        [
            new RemoteSource("Quilt Official", $"https://meta.quiltmc.org/v3/versions/loader/{normalizedVersion}", true)
        ];
    }

    private static IReadOnlyList<RemoteSource> CreateLiteLoaderSources(int versionSourceIndex)
    {
        return CreateSourceSequence(
            versionSourceIndex,
            new RemoteSource("LiteLoader Official", "https://dl.liteloader.com/versions/versions.json", true),
            new RemoteSource("BMCLAPI", "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json", false));
    }

    private static IReadOnlyList<RemoteSource> CreateSourceSequence(
        int versionSourceIndex,
        RemoteSource officialSource,
        RemoteSource? mirrorSource)
    {
        if (mirrorSource is null)
        {
            return [officialSource];
        }

        return versionSourceIndex == 0
            ? [mirrorSource, officialSource]
            : [officialSource, mirrorSource];
    }
}
