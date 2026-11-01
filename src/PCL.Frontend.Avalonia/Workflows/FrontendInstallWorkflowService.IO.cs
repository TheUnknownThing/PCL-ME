using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static JsonObject ReadJsonObject(string url, FrontendDownloadProvider? downloadProvider = null)
    {
        var content = DownloadStringWithCandidates(url, downloadProvider);
        return JsonNode.Parse(content)?.AsObject()
               ?? throw new InvalidOperationException($"Unable to read JSON object: {url}");
    }


    private static JsonArray ReadJsonArray(string url, FrontendDownloadProvider? downloadProvider = null)
    {
        var content = DownloadStringWithCandidates(url, downloadProvider);
        return JsonNode.Parse(content)?.AsArray()
               ?? throw new InvalidOperationException($"Unable to read JSON array: {url}");
    }


    private static string DownloadStringWithCandidates(string url, FrontendDownloadProvider? downloadProvider = null)
    {
        Exception? lastError = null;
        var candidateUrls = downloadProvider?.GetPreferredUrls(url) ?? [url];
        foreach (var candidateUrl in candidateUrls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, candidateUrl);
                if (candidateUrl.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.UserAgent.ParseAdd("PCL-ME-Frontend");
                }

                using var response = HttpClient.Send(request);
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to read remote content: {url}", lastError);
    }


    private static JsonObject ReadJsonObjectFromEntry(ZipArchive archive, string entryPath)
    {
        using var stream = archive.GetEntry(entryPath)?.Open()
                           ?? throw new InvalidOperationException($"Installer is missing entry: {entryPath}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject()
               ?? throw new InvalidOperationException($"Unable to read installer JSON: {entryPath}");
    }


    private static JsonObject ReadJsonObjectFromFile(string filePath)
    {
        return JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
               ?? throw new InvalidOperationException($"Unable to read JSON file: {filePath}");
    }


    private static JsonObject CloneObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())?.AsObject()
               ?? throw new InvalidOperationException("Failed to copy the install manifest.");
    }


    private static void ReportPrepareStatus(Action<string>? onStatusChanged, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            onStatusChanged?.Invoke(message);
        }
    }


    private static string Text(
        II18nService? i18n,
        string key,
        string fallback,
        params (string Key, object? Value)[] args)
    {
        if (i18n is null)
        {
            return ApplyFallbackArgs(fallback, args);
        }

        if (args.Length == 0)
        {
            return i18n.T(key);
        }

        return i18n.T(
            key,
            args.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal));
    }


    private static string ApplyFallbackArgs(string fallback, IReadOnlyList<(string Key, object? Value)> args)
    {
        var result = fallback;
        foreach (var (key, value) in args)
        {
            result = result.Replace("{" + key + "}", value?.ToString() ?? string.Empty, StringComparison.Ordinal);
        }

        return result;
    }


    private static string CreateTempFile(string prefix, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }


    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }


    private static void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(targetDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }


    private static void TryDeleteDirectory(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }


    private static void TryDeleteFile(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
