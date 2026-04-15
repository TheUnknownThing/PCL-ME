using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendModpackInstallWorkflowService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly string CurseForgeApiKey = FrontendEmbeddedSecrets.GetCurseForgeApiKey();
    private static readonly IReadOnlyDictionary<string, object?> EmptyI18nArgs =
        new Dictionary<string, object?>(0, StringComparer.Ordinal);

    internal static string ModpackText(
        II18nService? i18n,
        string key,
        params (string Name, object? Value)[] args)
    {
        var dictionary = args.Length == 0
            ? EmptyI18nArgs
            : args.ToDictionary(arg => arg.Name, arg => arg.Value, StringComparer.Ordinal);
        var template = i18n?.T(key, dictionary) ?? GetModpackFallbackTemplate(key);
        return FormatTemplate(template, dictionary);
    }

    private static string GetModpackFallbackTemplate(string key)
    {
        return key switch
        {
            "resource_detail.modpack.workflow.status.inspecting_manifest" => "Inspecting modpack manifest...",
            "resource_detail.modpack.workflow.status.preparing_install_plan" => "Preparing instance install plan...",
            "resource_detail.modpack.workflow.status.extracting_overrides" => "Extracting modpack overrides...",
            "resource_detail.modpack.workflow.status.downloading_bundled_files" => "Downloading bundled modpack files...",
            "resource_detail.modpack.workflow.status.processing_file" => "Processing {file_name}...",
            "resource_detail.modpack.workflow.status.bundled_files_ready" => "Bundled modpack files are ready.",
            "resource_detail.modpack.workflow.status.file_ready" => "{file_name} is ready",
            "resource_detail.modpack.workflow.status.installing_game" => "Installing game and loaders...",
            "resource_detail.modpack.workflow.status.repairing_game_files" => "Completing game files...",
            "resource_detail.modpack.workflow.status.finalizing_instance" => "Finalizing instance directory...",
            "resource_detail.modpack.workflow.status.completed" => "Modpack installation completed.",
            "resource_detail.modpack.workflow.errors.unsupported_package_kind" => "Only PCL Standard, Modrinth, and CurseForge modpacks are supported for automatic installation.",
            "resource_detail.modpack.workflow.errors.modrinth_missing_minecraft_version" => "The Modrinth modpack is missing Minecraft version information.",
            "resource_detail.modpack.workflow.errors.curseforge_missing_minecraft_version" => "The CurseForge modpack is missing Minecraft version information.",
            "resource_detail.modpack.workflow.errors.curseforge_recommended_forge_unsupported" => "This CurseForge modpack uses recommended Forge, which is not supported for automatic installation yet.",
            "resource_detail.modpack.workflow.errors.curseforge_missing_files" => "Some CurseForge files referenced by the modpack no longer exist, so automatic installation cannot continue.",
            "resource_detail.modpack.workflow.errors.pcl_missing_addons" => "This PCL Standard modpack does not include the required game version addon metadata.",
            "resource_detail.modpack.workflow.errors.pcl_missing_game_version" => "This PCL Standard modpack does not include Minecraft version information.",
            "resource_detail.modpack.workflow.errors.missing_critical_file" => "The modpack is missing a required file: {path}",
            "resource_detail.modpack.workflow.errors.minecraft_choice_missing" => "No available installation plan was found for Minecraft {version}.",
            "resource_detail.modpack.workflow.errors.loader_choice_missing" => "No installation plan was found for {option_title} {requested_version}.",
            "resource_detail.modpack.workflow.errors.file_download_failed" => "Failed to download modpack file: {file_name}",
            "resource_detail.modpack.workflow.errors.curseforge_metadata_empty" => "CurseForge file metadata response was empty.",
            "resource_detail.modpack.workflow.errors.curseforge_metadata_missing_data" => "CurseForge file metadata did not include a data array.",
            "resource_detail.modpack.workflow.errors.file_path_outside_instance" => "The modpack file path escapes the instance directory: {relative_path}",
            "resource_detail.modpack.workflow.errors.archive_illegal_path" => "The modpack archive contains an invalid path: {entry_path}",
            "resource_detail.modpack.workflow.errors.rar_unsupported" => "RAR archives are not supported. Extract and recompress the modpack as a ZIP archive, then try again.",
            "resource_detail.modpack.workflow.errors.archive_open_failed" => "Failed to open the modpack archive. The file may be corrupted or use an unsupported archive format.",
            "resource_detail.modpack.workflow.errors.json_parse_failed" => "Failed to parse the JSON file inside the modpack: {entry_path}",
            "resource_detail.modpack.task.canceling" => "Canceling modpack installation...",
            "resource_detail.modpack.task.queued" => "Added to the task center",
            "resource_detail.modpack.task.completed" => "Modpack installation completed",
            "resource_detail.modpack.task.canceled" => "Modpack installation was canceled",
            "resource_detail.modpack.task.copying_archive" => "Copying modpack archive...",
            "resource_detail.modpack.task.downloading_archive" => "Downloading modpack archive...",
            "resource_detail.modpack.task.source_missing" => "The modpack source is missing.",
            _ => key
        };
    }

    private static string FormatTemplate(string template, IReadOnlyDictionary<string, object?> args)
    {
        foreach (var pair in args)
        {
            var replacement = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            template = template.Replace("{" + pair.Key + "}", replacement, StringComparison.Ordinal);
        }

        return template;
    }

    public static async Task<FrontendModpackInstallResult> InstallDownloadedArchiveAsync(
        FrontendModpackInstallRequest request,
        Action<FrontendModpackInstallStatus>? onStatusChanged = null,
        TimeSpan? requestTimeout = null,
        FrontendDownloadTransferOptions? downloadOptions = null,
        CancellationToken cancelToken = default,
        II18nService? i18n = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancelToken.ThrowIfCancellationRequested();
        var effectiveTimeout = NormalizeDownloadTimeout(requestTimeout);
        using var httpClient = CreateDownloadHttpClient(effectiveTimeout);
        var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;

        Directory.CreateDirectory(request.TargetDirectory);
        ReportStatus(onStatusChanged, 0.02, ModpackText(i18n, "resource_detail.modpack.workflow.status.inspecting_manifest"));

        var package = InspectPackage(request.ArchivePath, request.CommunitySourcePreference, httpClient, cancelToken, i18n);
        cancelToken.ThrowIfCancellationRequested();

        ReportStatus(onStatusChanged, 0.08, ModpackText(i18n, "resource_detail.modpack.workflow.status.preparing_install_plan"));
        var installRequest = BuildInstallRequest(package, request, i18n);

        var extractRoot = CreateTempDirectory("pcl-modpack-");
        var downloadedFiles = new List<string>();
        var reusedFiles = new List<string>();
        var resolvedFiles = package.Files
            .Select(file => file.Resolve(request.TargetDirectory))
            .ToArray();
        try
        {
            ReportStatus(onStatusChanged, 0.16, ModpackText(i18n, "resource_detail.modpack.workflow.status.extracting_overrides"));
            ExtractArchiveToDirectory(
                request.ArchivePath,
                extractRoot,
                progress => ReportStatus(onStatusChanged, 0.16 + progress * 0.16, ModpackText(i18n, "resource_detail.modpack.workflow.status.extracting_overrides")),
                cancelToken);

            ApplyOverrides(package, extractRoot, request.TargetDirectory);
            cancelToken.ThrowIfCancellationRequested();

            if (resolvedFiles.Length > 0)
            {
                var completedCount = 0;
                foreach (var file in resolvedFiles)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file.TargetPath);
                    var progressBase = 0.34 + completedCount / (double)resolvedFiles.Length * 0.26;
                    ReportStatus(
                        onStatusChanged,
                        progressBase,
                        string.IsNullOrWhiteSpace(fileName)
                            ? ModpackText(i18n, "resource_detail.modpack.workflow.status.downloading_bundled_files")
                            : ModpackText(i18n, "resource_detail.modpack.workflow.status.processing_file", ("file_name", fileName)),
                        RemainingFileCount: resolvedFiles.Length - completedCount);

                    if (await EnsurePackFileAsync(file, request.CommunitySourcePreference, httpClient, speedLimiter, cancelToken, i18n).ConfigureAwait(false))
                    {
                        downloadedFiles.Add(file.TargetPath);
                    }
                    else
                    {
                        reusedFiles.Add(file.TargetPath);
                    }

                    completedCount += 1;
                    ReportStatus(
                        onStatusChanged,
                        0.34 + completedCount / (double)resolvedFiles.Length * 0.26,
                        string.IsNullOrWhiteSpace(fileName)
                            ? ModpackText(i18n, "resource_detail.modpack.workflow.status.bundled_files_ready")
                            : ModpackText(i18n, "resource_detail.modpack.workflow.status.file_ready", ("file_name", fileName)),
                        RemainingFileCount: resolvedFiles.Length - completedCount);
                }
            }

            cancelToken.ThrowIfCancellationRequested();
            ReportStatus(onStatusChanged, 0.62, ModpackText(i18n, "resource_detail.modpack.workflow.status.installing_game"));

            var applyResult = await Task.Run(
                () => FrontendInstallWorkflowService.Apply(
                    installRequest,
                    (phase, message) =>
                    {
                        var mappedProgress = phase switch
                        {
                            FrontendInstallApplyPhase.PrepareManifest => 0.68,
                            FrontendInstallApplyPhase.DownloadSupportFiles => 0.74,
                            FrontendInstallApplyPhase.Finalize => 0.95,
                            _ => 0.68
                        };
                        ReportStatus(onStatusChanged, mappedProgress, message);
                    },
                    snapshot =>
                    {
                        ReportStatus(
                            onStatusChanged,
                            0.74 + snapshot.Progress * 0.21,
                            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                                ? ModpackText(i18n, "resource_detail.modpack.workflow.status.repairing_game_files")
                                : ModpackText(i18n, "resource_detail.modpack.workflow.status.processing_file", ("file_name", snapshot.CurrentFileName)),
                            snapshot.SpeedBytesPerSecond,
                            snapshot.RemainingFileCount,
                            snapshot.CurrentFileName);
                    },
                    downloadOptions,
                    i18n,
                    cancelToken),
                cancelToken).ConfigureAwait(false);

            downloadedFiles.AddRange(applyResult.DownloadedFiles);
            reusedFiles.AddRange(applyResult.ReusedFiles);

            ReportStatus(onStatusChanged, 0.97, ModpackText(i18n, "resource_detail.modpack.workflow.status.finalizing_instance"));
            FinalizeInstalledInstance(package, request);
            TryDeleteFile(request.ArchivePath);

            ReportStatus(onStatusChanged, 1d, ModpackText(i18n, "resource_detail.modpack.workflow.status.completed"));
            return new FrontendModpackInstallResult(
                request.InstanceName,
                request.TargetDirectory,
                downloadedFiles,
                reusedFiles);
        }
        finally
        {
            TryDeleteDirectory(extractRoot);
        }
    }

    public static string SuggestInstanceName(string archivePath)
    {
        var fallbackName = Path.GetFileNameWithoutExtension(archivePath);
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return fallbackName;
        }

        try
        {
            using var archive = OpenArchiveRead(archivePath);
            var (kind, baseFolder) = DetectPackageKind(archive);
            var entryPath = kind switch
            {
                FrontendModpackPackageKind.Modrinth => baseFolder + "modrinth.index.json",
                FrontendModpackPackageKind.CurseForge => baseFolder + "manifest.json",
                FrontendModpackPackageKind.Mcbbs => ResolveMcbbsMetadataEntryPath(archive, baseFolder, i18n: null),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                return fallbackName;
            }

            var root = ReadJsonObjectFromEntry(archive, entryPath);
            var packageName = root["name"]?.GetValue<string>()?.Trim();
            return string.IsNullOrWhiteSpace(packageName) ? fallbackName : packageName;
        }
        catch
        {
            return fallbackName;
        }
    }

    private static FrontendModpackPackage InspectPackage(
        string archivePath,
        int communitySourcePreference,
        HttpClient httpClient,
        CancellationToken cancelToken,
        II18nService? i18n)
    {
        using var archive = OpenArchiveRead(archivePath);
        var (kind, baseFolder) = DetectPackageKind(archive);
        return kind switch
        {
            FrontendModpackPackageKind.Modrinth => BuildModrinthPackage(archive, baseFolder, i18n),
            FrontendModpackPackageKind.CurseForge => BuildCurseForgePackage(archive, baseFolder, communitySourcePreference, httpClient, cancelToken, i18n),
            FrontendModpackPackageKind.Mcbbs => BuildMcbbsPackage(archive, baseFolder, i18n),
            _ => throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.unsupported_package_kind"))
        };
    }

    private static (FrontendModpackPackageKind Kind, string BaseFolder) DetectPackageKind(ZipArchive archive)
    {
        if (archive.GetEntry("mcbbs.packmeta") is not null)
        {
            return (FrontendModpackPackageKind.Mcbbs, string.Empty);
        }

        if (archive.GetEntry("modrinth.index.json") is not null)
        {
            return (FrontendModpackPackageKind.Modrinth, string.Empty);
        }

        if (archive.GetEntry("manifest.json") is not null)
        {
            var rootManifest = ReadJsonObjectFromEntry(archive, "manifest.json");
            return rootManifest["addons"] is null
                ? (FrontendModpackPackageKind.CurseForge, string.Empty)
                : (FrontendModpackPackageKind.Mcbbs, string.Empty);
        }

        foreach (var entry in archive.Entries)
        {
            var parts = entry.FullName.Split('/', StringSplitOptions.None);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var baseFolder = parts[0] + "/";
            if (string.Equals(parts[1], "mcbbs.packmeta", StringComparison.OrdinalIgnoreCase))
            {
                return (FrontendModpackPackageKind.Mcbbs, baseFolder);
            }

            if (string.Equals(parts[1], "modrinth.index.json", StringComparison.OrdinalIgnoreCase))
            {
                return (FrontendModpackPackageKind.Modrinth, baseFolder);
            }

            if (!string.Equals(parts[1], "manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var manifest = ReadJsonObjectFromEntry(archive, entry.FullName);
            return manifest["addons"] is null
                ? (FrontendModpackPackageKind.CurseForge, baseFolder)
                : (FrontendModpackPackageKind.Mcbbs, baseFolder);
        }

        return (FrontendModpackPackageKind.Unknown, string.Empty);
    }

    private static FrontendModpackPackage BuildModrinthPackage(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, baseFolder + "modrinth.index.json", i18n);
        if (root["dependencies"] is not JsonObject dependencies
            || string.IsNullOrWhiteSpace(dependencies["minecraft"]?.GetValue<string>()))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.modrinth_missing_minecraft_version"));
        }

        var files = new List<FrontendModpackFilePlan>();
        foreach (var node in root["files"] as JsonArray ?? [])
        {
            if (node is not JsonObject file)
            {
                continue;
            }

            var env = file["env"] as JsonObject;
            var clientSupport = env?["client"]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(clientSupport, "unsupported", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clientSupport, "optional", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = file["path"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var targetPath = BuildValidatedTargetPath(relativePath);
            var urls = (file["downloads"] as JsonArray ?? [])
                .Select(node => node?.GetValue<string>())
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Cast<string>()
                .ToArray();
            if (urls.Length == 0)
            {
                continue;
            }

            files.Add(new FrontendModpackFilePlan(
                targetPath,
                urls,
                file["fileSize"]?.GetValue<long?>(),
                file["hashes"]?["sha1"]?.GetValue<string>(),
                Path.GetFileName(targetPath)));
        }

        return new FrontendModpackPackage(
            FrontendModpackPackageKind.Modrinth,
            dependencies["minecraft"]?.GetValue<string>() ?? string.Empty,
            dependencies["forge"]?.GetValue<string>(),
            dependencies["neoforge"]?.GetValue<string>() ?? dependencies["neo-forge"]?.GetValue<string>(),
            dependencies["fabric-loader"]?.GetValue<string>(),
            dependencies["quilt-loader"]?.GetValue<string>(),
            null,
            root["versionId"]?.GetValue<string>(),
            null,
            null,
            [
                new FrontendModpackOverrideSource(baseFolder + "overrides"),
                new FrontendModpackOverrideSource(baseFolder + "client-overrides")
            ],
            files);
    }

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
            fabricVersion,
            quiltVersion,
            null,
            root["version"]?.GetValue<string>(),
            null,
            null,
            [new FrontendModpackOverrideSource(effectiveOverridesPath)],
            files);
    }

    private static FrontendModpackPackage BuildMcbbsPackage(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var root = ReadJsonObjectFromEntry(archive, ResolveMcbbsMetadataEntryPath(archive, baseFolder, i18n), i18n);
        if (root["addons"] is not JsonArray addons)
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_addons"));
        }

        var addonVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in addons)
        {
            if (entry is not JsonObject addon)
            {
                continue;
            }

            var id = addon["id"]?.GetValue<string>()?.Trim();
            var version = addon["version"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            {
                continue;
            }

            addonVersions[id] = version;
        }

        if (!addonVersions.TryGetValue("game", out var minecraftVersion) || string.IsNullOrWhiteSpace(minecraftVersion))
        {
            throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.pcl_missing_game_version"));
        }

        var launchInfo = root["launchInfo"] as JsonObject;
        return new FrontendModpackPackage(
            FrontendModpackPackageKind.Mcbbs,
            minecraftVersion,
            addonVersions.TryGetValue("forge", out var forgeVersion) ? forgeVersion : null,
            addonVersions.TryGetValue("neoforge", out var neoForgeVersion) ? neoForgeVersion : null,
            addonVersions.TryGetValue("fabric", out var fabricVersion) ? fabricVersion : null,
            addonVersions.TryGetValue("quilt", out var quiltVersion) ? quiltVersion : null,
            addonVersions.TryGetValue("optifine", out var optiFineVersion) ? optiFineVersion : null,
            root["version"]?.GetValue<string>(),
            ReadJoinedText(launchInfo?["javaArgument"]),
            ReadJoinedText(launchInfo?["launchArgument"]),
            [new FrontendModpackOverrideSource(baseFolder + "overrides")],
            []);
    }

    private static string ResolveMcbbsMetadataEntryPath(ZipArchive archive, string baseFolder, II18nService? i18n)
    {
        var packMetaPath = baseFolder + "mcbbs.packmeta";
        if (archive.GetEntry(packMetaPath) is not null)
        {
            return packMetaPath;
        }

        var manifestPath = baseFolder + "manifest.json";
        if (archive.GetEntry(manifestPath) is not null)
        {
            return manifestPath;
        }

        throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.missing_critical_file", ("path", packMetaPath)));
    }

    private static FrontendInstallApplyRequest BuildInstallRequest(
        FrontendModpackPackage package,
        FrontendModpackInstallRequest request,
        II18nService? i18n)
    {
        var minecraftChoice = ResolveMinecraftChoice(package.MinecraftVersion, i18n);
        var primaryChoice =
            ResolveLoaderChoice("Forge", package.MinecraftVersion, package.ForgeVersion, i18n) ??
            ResolveLoaderChoice("NeoForge", package.MinecraftVersion, package.NeoForgeVersion, i18n) ??
            ResolveLoaderChoice("Fabric", package.MinecraftVersion, package.FabricVersion, i18n) ??
            ResolveLoaderChoice("Quilt", package.MinecraftVersion, package.QuiltVersion, i18n);
        var optiFineChoice = ResolveLoaderChoice("OptiFine", package.MinecraftVersion, package.OptiFineVersion, i18n);

        return new FrontendInstallApplyRequest(
            request.LauncherDirectory,
            request.InstanceName,
            minecraftChoice,
            primaryChoice,
            LiteLoaderChoice: null,
            OptiFineChoice: optiFineChoice,
            FabricApiChoice: null,
            LegacyFabricApiChoice: null,
            QslChoice: null,
            OptiFabricChoice: null,
            UseInstanceIsolation: true,
            RunRepair: true,
            ForceCoreRefresh: true,
            PreserveExistingManagedModFiles: true);
    }

    private static FrontendInstallChoice ResolveMinecraftChoice(string version, II18nService? i18n)
    {
        var choices = FrontendInstallWorkflowService.GetMinecraftCatalogChoices(version, i18n);
        var choice = choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Metadata?["rawVersion"]?.GetValue<string>(), version, StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.minecraft_choice_missing", ("version", version)));
    }

    private static FrontendInstallChoice? ResolveLoaderChoice(string optionTitle, string minecraftVersion, string? requestedVersion, II18nService? i18n)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return null;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion, i18n);
        var choice = choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, requestedVersion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Title, requestedVersion, StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new InvalidOperationException(
            ModpackText(
                i18n,
                "resource_detail.modpack.workflow.errors.loader_choice_missing",
                ("option_title", optionTitle),
                ("requested_version", requestedVersion)));
    }

    private static void ApplyOverrides(
        FrontendModpackPackage package,
        string extractRoot,
        string targetDirectory)
    {
        var normalizedExtractRoot = NormalizeDirectoryRoot(extractRoot);
        foreach (var source in package.OverrideSources)
        {
            if (string.IsNullOrWhiteSpace(source.RelativePath))
            {
                continue;
            }

            var normalizedRelativePath = source.RelativePath is "." or "./"
                ? string.Empty
                : NormalizeRelativePath(source.RelativePath);
            var sourcePath = string.IsNullOrWhiteSpace(normalizedRelativePath)
                ? normalizedExtractRoot
                : Path.GetFullPath(Path.Combine(normalizedExtractRoot, normalizedRelativePath));
            if (!string.IsNullOrWhiteSpace(normalizedRelativePath) && !IsPathWithinDirectory(sourcePath, normalizedExtractRoot))
            {
                continue;
            }

            if (!Directory.Exists(sourcePath))
            {
                continue;
            }

            CopyDirectoryContents(sourcePath, targetDirectory);
        }
    }

    private static async Task<bool> EnsurePackFileAsync(
        FrontendModpackFileDownloadPlan file,
        int communitySourcePreference,
        HttpClient httpClient,
        FrontendDownloadSpeedLimiter? speedLimiter,
        CancellationToken cancelToken,
        II18nService? i18n)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.TargetPath)!);
        if (File.Exists(file.TargetPath) && ValidateExistingFile(file))
        {
            return false;
        }

        foreach (var url in BuildPreferredDownloadUrls(file.DownloadUrls, communitySourcePreference))
        {
            cancelToken.ThrowIfCancellationRequested();
            var tempFile = file.TargetPath + ".pcltmp";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                TryDeleteFile(tempFile);
                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
                await FrontendDownloadTransferService.CopyToPathAsync(sourceStream, tempFile, speedLimiter: speedLimiter, cancelToken: cancelToken).ConfigureAwait(false);

                if (!ValidateDownloadedFile(tempFile, file))
                {
                    TryDeleteFile(tempFile);
                    continue;
                }

                if (File.Exists(file.TargetPath))
                {
                    File.Delete(file.TargetPath);
                }

                File.Move(tempFile, file.TargetPath);
                return true;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(tempFile);
                throw;
            }
            catch
            {
                TryDeleteFile(tempFile);
                // Try the next candidate URL.
            }
        }

        throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.file_download_failed", ("file_name", file.DisplayName)));
    }

    private static bool ValidateExistingFile(FrontendModpackFileDownloadPlan file)
    {
        var info = new FileInfo(file.TargetPath);
        if (file.Size is long expectedSize && info.Exists && info.Length != expectedSize)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.Sha1)
            || string.Equals(ComputeSha1(file.TargetPath), file.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateDownloadedFile(string path, FrontendModpackFileDownloadPlan file)
    {
        var info = new FileInfo(path);
        if (file.Size is long expectedSize && info.Length != expectedSize)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(file.Sha1)
            || string.Equals(ComputeSha1(path), file.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static void FinalizeInstalledInstance(FrontendModpackPackage package, FrontendModpackInstallRequest request)
    {
        var provider = FrontendRuntimePaths.OpenInstanceConfigProvider(request.TargetDirectory);
        provider.Set("VersionArgumentIndieV2", true);
        PersistKnownMinecraftVersion(provider, package.MinecraftVersion);
        provider.Set("VersionModpackVersion", package.PackageVersion ?? string.Empty);
        provider.Set("VersionModpackSource", request.ProjectSource ?? string.Empty);
        provider.Set("VersionModpackId", request.ProjectId ?? string.Empty);
        provider.Set("CustomInfo", request.ProjectDescription ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(package.LaunchJvmArguments))
        {
            provider.Set("VersionAdvanceJvm", package.LaunchJvmArguments);
        }

        if (!string.IsNullOrWhiteSpace(package.LaunchGameArguments))
        {
            provider.Set("VersionAdvanceGame", package.LaunchGameArguments);
        }

        if (!string.IsNullOrWhiteSpace(request.IconPath) && File.Exists(request.IconPath))
        {
            var logoDirectory = Path.Combine(request.TargetDirectory, "PCL");
            Directory.CreateDirectory(logoDirectory);
            File.Copy(request.IconPath, Path.Combine(logoDirectory, "Logo.png"), true);
            provider.Set("Logo", "PCL/Logo.png");
            provider.Set("LogoCustom", true);
        }

        provider.Sync();
    }

    private static void PersistKnownMinecraftVersion(YamlFileProvider provider, string? minecraftVersion)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return;
        }

        var trimmedVersion = minecraftVersion.Trim();
        provider.Set("VersionVanillaName", trimmedVersion);
        provider.Set("VersionVanilla", FrontendVersionManifestInspector.ParseComparableVanillaVersion(trimmedVersion).ToString());
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

    private static string BuildValidatedTargetPath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var expectedRoot = NormalizeDirectoryRoot(Path.Combine(Path.GetTempPath(), "placeholder"));
        if (Path.IsPathRooted(normalized))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.file_path_outside_instance", ("relative_path", relativePath)));
        }

        var combined = Path.GetFullPath(Path.Combine(expectedRoot, normalized));
        if (!IsPathWithinDirectory(combined, expectedRoot))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.file_path_outside_instance", ("relative_path", relativePath)));
        }

        return Path.GetRelativePath(expectedRoot, combined);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static void ExtractArchiveToDirectory(
        string archivePath,
        string destinationDirectory,
        Action<double>? onProgress,
        CancellationToken cancelToken)
    {
        using var archive = OpenArchiveRead(archivePath);
        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = NormalizeDirectoryRoot(destinationDirectory);
        var totalEntries = Math.Max(archive.Entries.Count, 1);

        for (var index = 0; index < archive.Entries.Count; index += 1)
        {
            cancelToken.ThrowIfCancellationRequested();
            var entry = archive.Entries[index];
            var normalizedEntryPath = entry.FullName
                .Replace('\\', '/')
                .TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedEntryPath))
            {
                onProgress?.Invoke((index + 1d) / totalEntries);
                continue;
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedEntryPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathWithinDirectory(destinationPath, destinationRoot))
            {
                throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.archive_illegal_path", ("entry_path", entry.FullName)));
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                onProgress?.Invoke((index + 1d) / totalEntries);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var source = entry.Open();
            using var target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
            onProgress?.Invoke((index + 1d) / totalEntries);
        }
    }

    private static ZipArchive OpenArchiveRead(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex) when (archivePath.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.rar_unsupported"), ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(ModpackText(null, "resource_detail.modpack.workflow.errors.archive_open_failed"), ex);
        }
    }

    private static JsonObject ReadJsonObjectFromEntry(ZipArchive archive, string entryPath, II18nService? i18n = null)
    {
        using var stream = archive.GetEntry(entryPath)?.Open()
                           ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.missing_critical_file", ("path", entryPath)));
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var content = reader.ReadToEnd();
        return JsonNode.Parse(content)?.AsObject()
               ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.json_parse_failed", ("entry_path", entryPath)));
    }

    private static string? ReadJoinedText(JsonNode? node)
    {
        if (node is not JsonArray array)
        {
            return null;
        }

        var values = array
            .Select(entry => entry?.GetValue<string>()?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        return values.Length == 0 ? null : string.Join(" ", values);
    }

    private static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeDirectoryRoot(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var normalizedDirectory = EnsureTrailingSeparator(NormalizeDirectoryRoot(directory));
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedDirectory, GetPathComparison());
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, true);
        }
    }

    private static void ReportStatus(
        Action<FrontendModpackInstallStatus>? callback,
        double progress,
        string message,
        double? speedBytesPerSecond = null,
        int? RemainingFileCount = null,
        string? currentFileName = null)
    {
        callback?.Invoke(new FrontendModpackInstallStatus(
            Math.Clamp(progress, 0d, 1d),
            message,
            speedBytesPerSecond,
            RemainingFileCount,
            currentFileName));
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout)
    {
        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = timeout
        };
    }

    private static TimeSpan NormalizeDownloadTimeout(TimeSpan? requestTimeout)
    {
        var timeout = requestTimeout ?? TimeSpan.FromSeconds(8);
        if (timeout <= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(8);
        }

        return timeout;
    }

    internal static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    internal static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}

internal sealed class FrontendManagedModpackInstallTask(
    string title,
    FrontendModpackInstallRequest request,
    TimeSpan requestTimeout,
    FrontendDownloadTransferOptions? downloadOptions = null,
    Action<string>? onStarted = null,
    Action<FrontendModpackInstallResult>? onCompleted = null,
    Action<string>? onFailed = null,
    II18nService? i18n = null) : PCL.Core.App.Tasks.ITask, PCL.Core.App.Tasks.ITaskProgressive, PCL.Core.App.Tasks.ITaskProgressStatus, PCL.Core.App.Tasks.ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private double _progress;
    private PCL.Core.App.Tasks.TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", 1, null);

    public string Title { get; } = title;

    public PCL.Core.App.Tasks.TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event PCL.Core.App.Tasks.TaskStateEvent StateChanged = delegate { };

    public event PCL.Core.App.Tasks.TaskProgressEvent ProgressChanged = delegate { };

    public event PCL.Core.App.Tasks.TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.canceling"));
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var token = linkedCts.Token;
        PublishState(PCL.Core.App.Tasks.TaskState.Waiting, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.queued"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ArchivePath)!);
            onStarted?.Invoke(request.ArchivePath);
            await PrepareArchiveAsync(token, requestTimeout).ConfigureAwait(false);

            var result = await FrontendModpackInstallWorkflowService.InstallDownloadedArchiveAsync(
                request,
                status => UpdateFromInstallStatus(status),
                requestTimeout,
                downloadOptions,
                token,
                i18n).ConfigureAwait(false);

            PublishProgress(1d);
            PublishProgressStatus(new PCL.Core.App.Tasks.TaskProgressStatusSnapshot("100%", "0 B/s", 0, null));
            PublishState(PCL.Core.App.Tasks.TaskState.Success, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.completed"));
            onCompleted?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            CleanupFailedInstall();
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    "0 B/s",
                    _progressStatus.RemainingFileCount,
                    null));
            PublishState(PCL.Core.App.Tasks.TaskState.Canceled, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.canceled"));
        }
        catch (Exception ex)
        {
            CleanupFailedInstall();
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    "0 B/s",
                    _progressStatus.RemainingFileCount,
                    null));
            PublishState(PCL.Core.App.Tasks.TaskState.Failed, ex.Message);
            onFailed?.Invoke(ex.Message);
        }
    }

    private async Task PrepareArchiveAsync(CancellationToken token, TimeSpan timeout)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceArchivePath))
        {
            PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.copying_archive"));
            await CopyArchiveAsync(request.SourceArchivePath!, token).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.downloading_archive"));
            await DownloadArchiveAsync(request.SourceUrl!, token, timeout).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.source_missing"));
    }

    private async Task DownloadArchiveAsync(string sourceUrl, CancellationToken token, TimeSpan timeout)
    {
        using var client = CreateDownloadHttpClient(timeout);
        var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var sourceStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
        var lastReportedBytes = 0L;
        var lastReportedAt = Environment.TickCount64;
        await FrontendDownloadTransferService.CopyToPathAsync(
            sourceStream,
            request.ArchivePath,
            totalRead =>
            {
                var progress = contentLength > 0
                    ? Math.Clamp(totalRead / (double)contentLength, 0d, 1d)
                    : 0d;
                PublishProgress(progress * 0.35);

                var now = Environment.TickCount64;
                if (now - lastReportedAt < 250)
                {
                    return;
                }

                var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                PublishProgressStatus(
                    new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                        $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                        speed > 0d ? $"{FormatBytes(speed)}/s" : "0 B/s",
                        1,
                        null));
                lastReportedBytes = totalRead;
                lastReportedAt = now;
            },
            speedLimiter,
            token).ConfigureAwait(false);

        PublishProgressStatus(
            new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                "0 B/s",
                1,
                null));
    }

    private async Task CopyArchiveAsync(string sourcePath, CancellationToken token)
    {
        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var targetStream = new FileStream(request.ArchivePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var totalLength = sourceStream.Length;
        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var read = await sourceStream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await targetStream.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            totalRead += read;

            var progress = totalLength > 0
                ? Math.Clamp(totalRead / (double)totalLength, 0d, 1d)
                : 1d;
            PublishProgress(progress * 0.35);
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                    "0 B/s",
                    1,
                    null));
        }
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout)
    {
        var safeTimeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(8) : timeout;
        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        })
        {
            Timeout = safeTimeout
        };
    }

    private void UpdateFromInstallStatus(FrontendModpackInstallStatus status)
    {
        PublishProgress(0.35 + status.Progress * 0.65);
        PublishProgressStatus(
            new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                status.SpeedBytesPerSecond is > 0d
                    ? $"{FormatBytes(status.SpeedBytesPerSecond.Value)}/s"
                    : "0 B/s",
                status.RemainingFileCount,
                null));
        PublishState(PCL.Core.App.Tasks.TaskState.Running, status.Message);
    }

    private void CleanupFailedInstall()
    {
        FrontendModpackInstallWorkflowService.TryDeleteFile(request.ArchivePath);
        FrontendModpackInstallWorkflowService.TryDeleteDirectory(request.TargetDirectory);
    }

    private void PublishState(PCL.Core.App.Tasks.TaskState state, string message)
    {
        StateChanged(state, message);
    }

    private void PublishProgress(double value)
    {
        _progress = Math.Clamp(value, 0d, 1d);
        ProgressChanged(_progress);
    }

    private void PublishProgressStatus(PCL.Core.App.Tasks.TaskProgressStatusSnapshot snapshot)
    {
        _progressStatus = snapshot;
        ProgressStatusChanged(snapshot);
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(bytes, 0d);
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex += 1;
        }

        return unitIndex == 0 ? $"{Math.Round(value):0} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }

}

internal sealed record FrontendModpackInstallRequest(
    string? SourceUrl,
    string? SourceArchivePath,
    string ArchivePath,
    string LauncherDirectory,
    string InstanceName,
    string TargetDirectory,
    string? ProjectId,
    string? ProjectSource,
    string? IconPath,
    string? ProjectDescription,
    int CommunitySourcePreference);

internal sealed record FrontendModpackInstallResult(
    string InstanceName,
    string TargetDirectory,
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);

internal sealed record FrontendModpackInstallStatus(
    double Progress,
    string Message,
    double? SpeedBytesPerSecond = null,
    int? RemainingFileCount = null,
    string? CurrentFileName = null);

internal sealed record FrontendModpackPackage(
    FrontendModpackPackageKind Kind,
    string MinecraftVersion,
    string? ForgeVersion,
    string? NeoForgeVersion,
    string? FabricVersion,
    string? QuiltVersion,
    string? OptiFineVersion,
    string? PackageVersion,
    string? LaunchJvmArguments,
    string? LaunchGameArguments,
    IReadOnlyList<FrontendModpackOverrideSource> OverrideSources,
    IReadOnlyList<FrontendModpackFilePlan> Files);

internal sealed record FrontendModpackOverrideSource(string RelativePath);

internal sealed record FrontendModpackFilePlan(
    string RelativeTargetPath,
    IReadOnlyList<string> DownloadUrls,
    long? Size,
    string? Sha1,
    string DisplayName)
{
    public FrontendModpackFileDownloadPlan Resolve(string targetRoot)
    {
        return new FrontendModpackFileDownloadPlan(
            Path.Combine(targetRoot, RelativeTargetPath),
            DownloadUrls,
            Size,
            Sha1,
            DisplayName);
    }
}

internal sealed record FrontendModpackFileDownloadPlan(
    string TargetPath,
    IReadOnlyList<string> DownloadUrls,
    long? Size,
    string? Sha1,
    string DisplayName);

internal sealed record FrontendCurseForgeApiCandidate(string Url, bool UseApiKey);

internal enum FrontendModpackPackageKind
{
    Unknown,
    CurseForge,
    Modrinth,
    Mcbbs
}
