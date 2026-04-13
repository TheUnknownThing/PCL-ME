using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendCommunityIconCache
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly ConcurrentDictionary<string, string?> IconCache = new(StringComparer.Ordinal);

    public static string? TryGetCachedIconPath(string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return null;
        }

        if (IconCache.TryGetValue(iconUrl, out var cachedPath)
            && !string.IsNullOrWhiteSpace(cachedPath)
            && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        var targetPath = BuildIconPath(iconUrl, mediaType: null);
        if (File.Exists(targetPath))
        {
            IconCache[iconUrl] = targetPath;
            return targetPath;
        }

        return null;
    }

    public static async Task<string?> EnsureCachedIconAsync(string? iconUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return null;
        }

        var cachedPath = TryGetCachedIconPath(iconUrl);
        if (!string.IsNullOrWhiteSpace(cachedPath))
        {
            return cachedPath;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, iconUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var targetPath = BuildIconPath(iconUrl, response.Content.Headers.ContentType?.MediaType);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            if (!File.Exists(targetPath))
            {
                var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                await File.WriteAllBytesAsync(targetPath, payload, cancellationToken);
            }

            IconCache[iconUrl] = targetPath;
            return targetPath;
        }
        catch
        {
            IconCache[iconUrl] = null;
            return null;
        }
    }

    private static string BuildIconPath(string iconUrl, string? mediaType)
    {
        var extension = ResolveIconExtension(iconUrl, mediaType);
        var iconDirectory = Path.Combine(Path.GetTempPath(), "PCL-CE", "frontend-resource-icons");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(iconUrl))).ToLowerInvariant();
        return Path.Combine(iconDirectory, $"{hash}{extension}");
    }

    private static string ResolveIconExtension(string iconUrl, string? mediaType)
    {
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            return mediaType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".png"
            };
        }

        var extension = Path.GetExtension(iconUrl);
        return string.IsNullOrWhiteSpace(extension) || extension == "." ? ".png" : extension;
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                     | System.Net.DecompressionMethods.Deflate
                                     | System.Net.DecompressionMethods.Brotli
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }
}
