using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static FrontendModpackPackage BuildCurseForgePackage(
        ZipArchive archive,
        string baseFolder,
        int communitySourcePreference,
        HttpClient httpClient,
        CancellationToken cancelToken,
        II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, baseFolder + "manifest.json", i18n);
        if (root["minecraft"] is not JsonObject minecraft
            || string.IsNullOrWhiteSpace(minecraft["version"]?.GetValue<string>()))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.curseforge_missing_minecraft_version"));
        }

        string? forgeVersion = null;
        string? neoForgeVersion = null;
        string? fabricVersion = null;
        string? quiltVersion = null;
        foreach (var loaderNode in minecraft["modLoaders"] as JsonArray ?? [])
        {
            var loaderId = loaderNode?["id"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(loaderId))
            {
                continue;
            }

            if (loaderId.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
            {
                if (loaderId.Contains("recommended", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.curseforge_recommended_forge_unsupported"));
                }

                forgeVersion = loaderId["forge-".Length..];
                continue;
            }

            if (loaderId.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
            {
                neoForgeVersion = loaderId["neoforge-".Length..];
                continue;
            }

            if (loaderId.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
            {
                fabricVersion = loaderId["fabric-".Length..];
                continue;
            }

            if (loaderId.StartsWith("quilt-", StringComparison.OrdinalIgnoreCase))
            {
                quiltVersion = loaderId["quilt-".Length..];
            }
        }

        var manifestFiles = (root["files"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node => new
            {
                FileId = node!["fileID"]?.GetValue<int?>(),
                Required = node["required"]?.GetValue<bool?>() ?? true
            })
            .Where(node => node.FileId is > 0 && node.Required)
            .ToArray();

        var fileMetadata = ReadCurseForgeFileMetadata(
            manifestFiles.Select(node => node.FileId!.Value).ToArray(),
            requestMirrorFirst: communitySourcePreference == 0,
            httpClient,
            cancelToken);
        if (fileMetadata.Count < manifestFiles.Length)
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.curseforge_missing_files"));
        }

        var files = new List<FrontendModpackFilePlan>();
        foreach (var manifestFile in manifestFiles)
        {
            if (!fileMetadata.TryGetValue(manifestFile.FileId!.Value, out var file))
            {
                continue;
            }

            var fileName = file["fileName"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var targetFolder = DetermineCurseForgeTargetFolder(file, fileName);
            var targetPath = BuildValidatedTargetPath(Path.Combine(targetFolder, fileName));
            var downloadUrl = file["downloadUrl"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                downloadUrl = BuildCurseForgeMediaUrl(manifestFile.FileId.Value, fileName);
            }

            files.Add(new FrontendModpackFilePlan(
                targetPath,
                [downloadUrl],
                file["fileLength"]?.GetValue<long?>(),
                TryGetCurseForgeSha1(file),
                fileName));
        }

        var overridesPath = root["overrides"]?.GetValue<string>() ?? "overrides";
        var effectiveOverridesPath = overridesPath is "." or "./"
            ? (string.IsNullOrWhiteSpace(baseFolder) ? "." : baseFolder.TrimEnd('/'))
            : baseFolder + overridesPath.TrimStart('/');

        return new FrontendModpackPackage(
            FrontendModpackPackageKind.CurseForge,
            minecraft["version"]?.GetValue<string>() ?? string.Empty,
            forgeVersion,
            neoForgeVersion,
            null,
            fabricVersion,
            null,
            quiltVersion,
            null,
            null,
            null,
            root["version"]?.GetValue<string>(),
            null,
            null,
            [new FrontendModpackOverrideSource(effectiveOverridesPath)],
            files);
    }

    private static Dictionary<int, JsonObject> ReadCurseForgeFileMetadata(
        IReadOnlyList<int> fileIds,
        bool requestMirrorFirst,
        HttpClient httpClient,
        CancellationToken cancelToken)
    {
        if (fileIds.Count == 0)
        {
            return [];
        }

        var body = new JsonObject
        {
            ["fileIds"] = new JsonArray(fileIds.Select(id => JsonValue.Create(id)).ToArray())
        }.ToJsonString();

        var candidates = BuildCurseForgeApiCandidates(requestMirrorFirst);
        var errors = new List<string>();
        foreach (var candidate in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, candidate.Url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (candidate.UseApiKey)
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", CurseForgeApiKey);
                }

                request.Content = new StringContent(body, Utf8NoBom, "application/json");
                using var response = httpClient.Send(request, cancelToken);
                response.EnsureSuccessStatusCode();

                var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var root = JsonNode.Parse(content)?.AsObject()
                           ?? throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.curseforge_metadata_empty"));
                var data = root["data"] as JsonArray ?? throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.curseforge_metadata_missing_data"));
                return data
                    .Select(node => node as JsonObject)
                    .Where(node => node is not null)
                    .ToDictionary(node => node!["id"]?.GetValue<int>() ?? 0, node => node!, EqualityComparer<int>.Default);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException(string.Join("；", errors.Distinct(StringComparer.Ordinal)));
    }

    private static IReadOnlyList<FrontendCurseForgeApiCandidate> BuildCurseForgeApiCandidates(bool requestMirrorFirst)
    {
        const string officialUrl = "https://api.curseforge.com/v1/mods/files";
        const string mirrorUrl = "https://mod.mcimirror.top/curseforge/v1/mods/files";
        var candidates = new List<FrontendCurseForgeApiCandidate>();

        void AddOfficial()
        {
            if (!string.IsNullOrWhiteSpace(CurseForgeApiKey))
            {
                candidates.Add(new FrontendCurseForgeApiCandidate(officialUrl, true));
            }
        }

        if (requestMirrorFirst)
        {
            candidates.Add(new FrontendCurseForgeApiCandidate(mirrorUrl, false));
            AddOfficial();
        }
        else
        {
            AddOfficial();
            candidates.Add(new FrontendCurseForgeApiCandidate(mirrorUrl, false));
        }

        return candidates
            .DistinctBy(candidate => candidate.Url)
            .ToArray();
    }

    private static string DetermineCurseForgeTargetFolder(JsonObject file, string fileName)
    {
        var moduleNames = (file["modules"] as JsonArray ?? [])
            .Select(node => node as JsonObject)
            .Select(node => node?["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        if (moduleNames.Contains("META-INF", StringComparer.OrdinalIgnoreCase)
            || moduleNames.Contains("mcmod.info", StringComparer.OrdinalIgnoreCase)
            || fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            return "mods";
        }

        if (moduleNames.Contains("pack.mcmeta", StringComparer.OrdinalIgnoreCase))
        {
            return "resourcepacks";
        }

        if (moduleNames.Contains("level.dat", StringComparer.OrdinalIgnoreCase))
        {
            return "saves";
        }

        return "shaderpacks";
    }

    private static string? TryGetCurseForgeSha1(JsonObject file)
    {
        foreach (var hashNode in file["hashes"] as JsonArray ?? [])
        {
            if (hashNode is not JsonObject hash)
            {
                continue;
            }

            var algo = hash["algo"]?.GetValue<int?>();
            if (algo is 1)
            {
                return hash["value"]?.GetValue<string>();
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildPreferredDownloadUrls(IReadOnlyList<string> urls, int preference)
    {
        foreach (var originalUrl in urls.Where(url => !string.IsNullOrWhiteSpace(url)))
        {
            var mirrorUrl = originalUrl
                .Replace("https://cdn.modrinth.com", "https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                .Replace("https://edge.forgecdn.net", "https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase)
                .Replace("https://mediafiles.forgecdn.net", "https://mod.mcimirror.top", StringComparison.OrdinalIgnoreCase);

            switch (preference)
            {
                case 0:
                    yield return mirrorUrl;
                    yield return mirrorUrl;
                    yield return originalUrl;
                    break;
                case 1:
                    yield return originalUrl;
                    yield return mirrorUrl;
                    yield return originalUrl;
                    break;
                default:
                    yield return originalUrl;
                    break;
            }
        }
    }

    private static string BuildCurseForgeMediaUrl(int fileId, string fileName)
    {
        return $"https://mediafiles.forgecdn.net/files/{fileId / 1000}/{fileId % 1000:D3}/{Uri.EscapeDataString(fileName)}";
    }
}
