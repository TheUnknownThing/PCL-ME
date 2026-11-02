using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityProjectService
{

    private static JsonArray ReadJsonArray(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        return ReadJsonNode(sourceName, officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey, postBody)?.AsArray()
               ?? throw new InvalidOperationException($"{sourceName} returned an invalid JSON array.");
    }

    private static JsonObject ReadJsonObject(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        return ReadJsonNode(sourceName, officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey, postBody)?.AsObject()
               ?? throw new InvalidOperationException($"{sourceName} returned an invalid JSON object.");
    }

    private static JsonNode? ReadJsonNode(
        string sourceName,
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey,
        string? postBody)
    {
        var candidates = BuildCandidateUrls(officialUrl, mirrorUrl, communitySourcePreference, officialRequiresApiKey);
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(postBody is null ? HttpMethod.Get : HttpMethod.Post, candidate.Url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (candidate.UseCurseForgeApiKey)
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", CurseForgeApiKey);
                }

                if (postBody is not null)
                {
                    request.Content = new StringContent(postBody, Encoding.UTF8, "application/json");
                }

                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonNode.Parse(content);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(string.Join("; ", errors.Distinct(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<RequestCandidate> BuildCandidateUrls(
        string officialUrl,
        string mirrorUrl,
        int communitySourcePreference,
        bool officialRequiresApiKey)
    {
        var canUseOfficial = !officialRequiresApiKey || !string.IsNullOrWhiteSpace(CurseForgeApiKey);
        var candidates = new List<RequestCandidate>();
        switch (communitySourcePreference)
        {
            case 0:
                candidates.Add(new RequestCandidate(mirrorUrl, false));
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                break;
            default:
                if (canUseOfficial)
                {
                    candidates.Add(new RequestCandidate(officialUrl, officialRequiresApiKey));
                }

                candidates.Add(new RequestCandidate(mirrorUrl, false));
                break;
        }

        return candidates
            .DistinctBy(candidate => candidate.Url)
            .ToArray();
    }

    private static HttpClient CreateHttpClient()
    {
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            TimeSpan.FromSeconds(20),
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
    }

}
