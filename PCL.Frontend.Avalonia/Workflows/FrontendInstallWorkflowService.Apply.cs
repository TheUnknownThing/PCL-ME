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

    private static JsonObject BuildTargetManifest(
        FrontendInstallApplyRequest request,
        FrontendDownloadProvider downloadProvider,
        Action<string>? onStatusChanged = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        II18nService? i18n = null,
        CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        ReportPrepareStatus(
            onStatusChanged,
            Text(
                i18n,
                "download.install.workflow.tasks.fetch_vanilla_manifest",
                "Fetching the vanilla manifest for Minecraft {version}...",
                ("version", request.MinecraftChoice.Version)));
        var baseManifest = ReadJsonObject(request.MinecraftChoice.ManifestUrl ?? MojangVersionManifestUrl, downloadProvider);
        JsonObject targetManifest;

        switch (request.PrimaryLoaderChoice?.Kind)
        {
            case FrontendInstallChoiceKind.FabricLoader:
            case FrontendInstallChoiceKind.LegacyFabricLoader:
            case FrontendInstallChoiceKind.QuiltLoader:
                ReportPrepareStatus(
                    onStatusChanged,
                    Text(
                        i18n,
                        "download.install.workflow.tasks.fetch_loader_manifest",
                        "Fetching the manifest for {loader_title}...",
                        ("loader_title", request.PrimaryLoaderChoice.Title)));
                targetManifest = MergeBaseAndLoaderManifest(
                    baseManifest,
                    ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                   ?? throw new InvalidOperationException("Missing installer manifest URL.")),
                    request.TargetInstanceName);
                break;
            case FrontendInstallChoiceKind.LabyMod:
                ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.fetch_labymod_manifest", "Fetching the LabyMod manifest..."));
                targetManifest = ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                               ?? throw new InvalidOperationException("Missing LabyMod manifest URL."));
                targetManifest["id"] = request.TargetInstanceName;
                break;
            case FrontendInstallChoiceKind.Forge:
            case FrontendInstallChoiceKind.NeoForge:
            case FrontendInstallChoiceKind.Cleanroom:
                ReportPrepareStatus(
                    onStatusChanged,
                    Text(
                        i18n,
                        "download.install.workflow.tasks.prepare_loader_installer",
                        "Preparing the installer for {loader_title}...",
                        ("loader_title", request.PrimaryLoaderChoice.Title)));
                targetManifest = MergeBaseAndLoaderManifest(
                    baseManifest,
                    BuildForgelikeManifest(request, request.PrimaryLoaderChoice, downloadProvider, onStatusChanged, speedLimiter, i18n, cancelToken),
                    request.TargetInstanceName);
                break;
            default:
                ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.organizing_vanilla_manifest", "Organizing the vanilla install manifest..."));
                targetManifest = CloneObject(baseManifest);
                targetManifest["id"] = request.TargetInstanceName;
                break;
        }

        if (request.LiteLoaderChoice is not null)
        {
            ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.merging_liteloader_manifest", "Merging LiteLoader install information..."));
            targetManifest = MergeBaseAndLoaderManifest(
                targetManifest,
                BuildLiteLoaderManifest(request.LiteLoaderChoice),
                request.TargetInstanceName);
        }

        if (ShouldInstallOptiFineStandalone(request))
        {
            ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.processing_optifine_manifest", "Processing OptiFine install information..."));
            targetManifest = MergeBaseAndLoaderManifest(
                targetManifest,
                BuildStandaloneOptiFineManifest(request, downloadProvider, onStatusChanged, speedLimiter, i18n, cancelToken),
                request.TargetInstanceName);
        }

        ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.generating_target_manifest", "Generating the target install manifest..."));
        targetManifest["id"] = request.TargetInstanceName;
        return targetManifest;
    }


    private static bool ShouldInstallOptiFineStandalone(FrontendInstallApplyRequest request)
    {
        return request.OptiFineChoice is not null
               && !ShouldInstallOptiFineAsMod(request);
    }


    private static bool ShouldInstallOptiFineAsMod(FrontendInstallApplyRequest request)
    {
        return request.OptiFineChoice is not null
               && (request.PrimaryLoaderChoice is not null || request.LiteLoaderChoice is not null);
    }


    private static void ApplyManagedModSelection(
        string modsDirectory,
        FrontendInstallChoice? selectedChoice,
        bool preserveExistingFilesWhenChoiceMissing,
        params string[] filePrefixes)
    {
        if (Directory.Exists(modsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (!filePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (selectedChoice is null && preserveExistingFilesWhenChoiceMissing)
                {
                    continue;
                }

                if (selectedChoice is not null
                    && string.Equals(fileName, selectedChoice.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(file);
            }
        }

        if (selectedChoice is null || string.IsNullOrWhiteSpace(selectedChoice.DownloadUrl) || string.IsNullOrWhiteSpace(selectedChoice.FileName))
        {
            return;
        }

        var outputPath = Path.Combine(modsDirectory, selectedChoice.FileName);
        if (File.Exists(outputPath))
        {
            return;
        }

        Directory.CreateDirectory(modsDirectory);
        DownloadFileToPath(selectedChoice.DownloadUrl, outputPath);
    }


    private static void RemoveMissingLocalOnlyLibraries(JsonObject manifest, string launcherDirectory)
    {
        if (manifest["libraries"] is not JsonArray libraries)
        {
            return;
        }

        var filtered = new JsonArray();
        foreach (var node in libraries)
        {
            if (node is not JsonObject library)
            {
                filtered.Add(node?.DeepClone());
                continue;
            }

            var artifact = library["downloads"]?["artifact"] as JsonObject;
            var url = artifact?["url"]?.GetValue<string>();
            if (artifact is not null && string.IsNullOrWhiteSpace(url))
            {
                var path = artifact["path"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(path))
                {
                    var name = library["name"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        path = FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(name);
                    }
                }

                if (!string.IsNullOrWhiteSpace(path))
                {
                    var localPath = Path.Combine(launcherDirectory, "libraries", path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(localPath))
                    {
                        continue;
                    }
                }
            }

            filtered.Add(library.DeepClone());
        }

        manifest["libraries"] = filtered;
    }


    private static void EnsureResourceFolders(string instanceDirectory, string runtimeDirectory)
    {
        Directory.CreateDirectory(Path.Combine(runtimeDirectory, "resourcepacks"));
        Directory.CreateDirectory(Path.Combine(runtimeDirectory, "mods"));
        Directory.CreateDirectory(Path.Combine(instanceDirectory, "PCL"));
    }


    private static JsonArray MergeLibraries(JsonArray? baseLibraries, JsonArray? loaderLibraries)
    {
        var merged = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Append(JsonArray? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var node in source)
            {
                if (node is not JsonObject library)
                {
                    continue;
                }

                var name = library["name"]?.GetValue<string>() ?? library.ToJsonString();
                if (!seen.Add(name))
                {
                    continue;
                }

                merged.Add(library.DeepClone());
            }
        }

        Append(baseLibraries);
        Append(loaderLibraries);
        return merged;
    }


    private static JsonObject MergeArguments(JsonObject? baseArguments, JsonObject? loaderArguments)
    {
        var merged = baseArguments is null ? new JsonObject() : CloneObject(baseArguments);
        foreach (var key in new[] { "game", "jvm" })
        {
            var values = new JsonArray();

            void Append(JsonObject? source)
            {
                if (source?[key] is not JsonArray array)
                {
                    return;
                }

                foreach (var item in array)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    values.Add(item.DeepClone());
                }
            }

            Append(baseArguments);
            Append(loaderArguments);
            if (values.Count > 0)
            {
                merged[key] = values;
            }
        }

        return merged;
    }


    private static JsonObject MergeBaseAndLoaderManifest(
        JsonObject baseManifest,
        JsonObject loaderManifest,
        string targetInstanceName)
    {
        var merged = CloneObject(baseManifest);

        foreach (var pair in loaderManifest)
        {
            if (pair.Value is null)
            {
                continue;
            }

            if (pair.Key is "id" or "inheritsFrom" or "releaseTime" or "time" or "jar")
            {
                continue;
            }

            if (pair.Key == "libraries")
            {
                merged["libraries"] = MergeLibraries(
                    merged["libraries"] as JsonArray,
                    pair.Value as JsonArray);
                continue;
            }

            if (pair.Key == "arguments")
            {
                merged["arguments"] = MergeArguments(
                    merged["arguments"] as JsonObject,
                    pair.Value as JsonObject);
                continue;
            }

            merged[pair.Key] = pair.Value.DeepClone();
        }

        merged.Remove("inheritsFrom");
        merged["id"] = targetInstanceName;
        return merged;
    }

}
