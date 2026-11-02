using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendCommunityResourceCatalogService
{

    public static FrontendCommunityResourceCatalogService Instance { get; } = new(CreateHttpClient());

    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, CacheEntry<QuerySession>> _queryResultCache = new(StringComparer.Ordinal);
    private CacheEntry<IReadOnlyList<string>>? _minecraftVersionOptionsCache;

    internal FrontendCommunityResourceCatalogService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private static string BuildQueryCacheKey(
        LauncherFrontendSubpageKey route,
        FrontendCommunityResourceQuery query,
        string preferredVersion,
        int communitySourcePreference)
    {
        return string.Join(
            "|",
            route,
            Uri.EscapeDataString(query.SearchText),
            Uri.EscapeDataString(query.Source),
            Uri.EscapeDataString(query.Tag),
            Uri.EscapeDataString(query.Sort),
            Uri.EscapeDataString(query.Version),
            Uri.EscapeDataString(query.Loader),
            Uri.EscapeDataString(preferredVersion),
            communitySourcePreference.ToString());
    }

    private static string ResolvePreferredMinecraftVersion(FrontendInstanceComposition instanceComposition)
    {
        var candidate = NormalizeMinecraftVersion(instanceComposition.Selection.VanillaVersion);
        return string.IsNullOrWhiteSpace(candidate) ? string.Empty : candidate;
    }

    private static string? NormalizeMinecraftVersion(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var match = Regex.Match(rawValue, @"\d+\.\d+(?:\.\d+)?");
        return match.Success ? match.Value : null;
    }

    private static string ResolvePrimaryVersion(IEnumerable<string> versions, string? preferredVersion)
    {
        var normalizedVersions = versions
            .Select(NormalizeMinecraftVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && normalizedVersions.Contains(preferredVersion, StringComparer.OrdinalIgnoreCase))
        {
            return preferredVersion;
        }

        return normalizedVersions
            .OrderByDescending(version => ParseVersion(version))
            .FirstOrDefault()
               ?? string.Empty;
    }

    private static Version ParseVersion(string? rawValue)
    {
        return Version.TryParse(rawValue, out var version)
            ? version
            : new Version(0, 0);
    }

    private static string BuildEntryInfo(string? description, string? author, DateTimeOffset? _, int __)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description.Trim());
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add(author.Trim());
        }

        return string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static int ToRank(DateTimeOffset? value)
    {
        return value is null ? 0 : (int)Math.Clamp(value.Value.ToUnixTimeSeconds(), 0, int.MaxValue);
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var rawValue = node?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed)
            ? parsed
            : null;
    }

    private static string GetString(JsonObject? obj, string propertyName)
    {
        return obj?[propertyName]?.GetValue<string>()?.Trim() ?? string.Empty;
    }

    private static int GetInt(JsonObject? obj, string propertyName)
    {
        if (obj?[propertyName] is null)
        {
            return 0;
        }

        return obj[propertyName]!.GetValue<int>();
    }

    private async Task<IReadOnlyList<string>> GetMinecraftVersionOptionsAsync(
        string? preferredVersion,
        string? selectedVersion,
        IEnumerable<string> resultVersions,
        CancellationToken cancellationToken)
    {
        if (_minecraftVersionOptionsCache is null
            || DateTimeOffset.UtcNow - _minecraftVersionOptionsCache.CreatedAt > TimeSpan.FromHours(6))
        {
            var options = await FetchMinecraftVersionOptionsAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _minecraftVersionOptionsCache = new CacheEntry<IReadOnlyList<string>>(options, DateTimeOffset.UtcNow);
        }

        var versions = new List<string>();
        AddIfVersionMissing(versions, preferredVersion);
        AddIfVersionMissing(versions, selectedVersion);

        foreach (var version in _minecraftVersionOptionsCache.State)
        {
            AddIfVersionMissing(versions, version);
        }

        foreach (var version in resultVersions)
        {
            AddIfVersionMissing(versions, version);
        }

        return versions
            .OrderByDescending(ParseVersion)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> FetchMinecraftVersionOptionsAsync(CancellationToken cancellationToken)
    {
        var sources = new[]
        {
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"
        };
        foreach (var source in sources)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, source);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var root = JsonNode.Parse(content)?.AsObject();
                var versions = root?["versions"]?.AsArray()
                    .Select(node => node as JsonObject)
                    .Where(node => node is not null && string.Equals(GetString(node, "type"), "release", StringComparison.OrdinalIgnoreCase))
                    .Select(node => NormalizeMinecraftVersion(GetString(node, "id")))
                    .Where(version => !string.IsNullOrWhiteSpace(version))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(ParseVersion)
                    .ToArray();
                if (versions is { Length: > 0 })
                {
                    return versions;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Fall back to the static list below.
            }
        }

        return
        [
            "26.1.1",
            "26.1",
            "1.21.11",
            "1.21.1",
            "1.20.6",
            "1.20.1",
            "1.19.4",
            "1.19.2",
            "1.18.2",
            "1.16.5",
            "1.12.2",
            "1.10.2",
            "1.8.9",
            "1.7.10"
        ];
    }

    private static void AddIfVersionMissing(ICollection<string> versions, string? version)
    {
        var normalized = NormalizeMinecraftVersion(version);
        if (!string.IsNullOrWhiteSpace(normalized) && !versions.Contains(normalized))
        {
            versions.Add(normalized);
        }
    }

}
