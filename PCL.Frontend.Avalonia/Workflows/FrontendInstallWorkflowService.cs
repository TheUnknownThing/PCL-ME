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

internal static class FrontendInstallWorkflowService
{
    private const string MojangVersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonNodeOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static IReadOnlyList<FrontendInstallChoice> GetMinecraftChoices(string? preferredVersion)
    {
        var root = ReadJsonObject(MojangVersionManifestUrl);
        if (root["versions"] is not JsonArray versions)
        {
            return [];
        }

        var choices = versions
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Where(node => string.Equals(node!["type"]?.GetValue<string>(), "release", StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var version = node!["id"]?.GetValue<string>() ?? string.Empty;
                var releaseTime = node["releaseTime"]?.GetValue<string>() ?? string.Empty;
                return new FrontendInstallChoice(
                    Id: $"minecraft:{version}",
                    Title: version,
                    Summary: string.IsNullOrWhiteSpace(releaseTime)
                        ? "正式版"
                        : $"正式版 • {FormatReleaseTime(releaseTime)}",
                    Version: version,
                    Kind: FrontendInstallChoiceKind.Minecraft,
                    ManifestUrl: node["url"]?.GetValue<string>());
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version) && !string.IsNullOrWhiteSpace(choice.ManifestUrl))
            .Take(36)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && choices.All(choice => !string.Equals(choice.Version, preferredVersion, StringComparison.OrdinalIgnoreCase)))
        {
            var extra = versions
                .Select(node => node as JsonObject)
                .FirstOrDefault(node => string.Equals(node?["id"]?.GetValue<string>(), preferredVersion, StringComparison.OrdinalIgnoreCase));
            if (extra is not null)
            {
                choices.Insert(
                    0,
                    new FrontendInstallChoice(
                        Id: $"minecraft:{preferredVersion}",
                        Title: preferredVersion,
                        Summary: $"当前实例 • {FormatReleaseTime(extra["releaseTime"]?.GetValue<string>())}",
                        Version: preferredVersion,
                        Kind: FrontendInstallChoiceKind.Minecraft,
                        ManifestUrl: extra["url"]?.GetValue<string>()));
            }
        }

        return choices;
    }

    public static IReadOnlyList<FrontendInstallChoice> GetMinecraftCatalogChoices(string? preferredVersion)
    {
        var root = ReadJsonObject(MojangVersionManifestUrl);
        if (root["versions"] is not JsonArray versions)
        {
            return [];
        }

        var choices = versions
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node =>
            {
                var rawId = node!["id"]?.GetValue<string>() ?? string.Empty;
                var manifestUrl = node["url"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(manifestUrl))
                {
                    return null;
                }

                var releaseTime = node["releaseTime"]?.GetValue<string>();
                var normalizedId = NormalizeMinecraftCatalogVersionId(rawId);
                var group = ResolveMinecraftCatalogGroup(rawId, node["type"]?.GetValue<string>(), releaseTime);
                var iconName = ResolveMinecraftCatalogIconName(group);
                var lore = ResolveMinecraftCatalogLore(rawId, group, releaseTime);
                var formattedTitle = FormatMinecraftCatalogVersion(normalizedId);
                var summary = string.IsNullOrWhiteSpace(lore)
                    ? BuildMinecraftCatalogTimestampSummary(releaseTime, normalizedId, formattedTitle)
                    : BuildMinecraftCatalogLoreSummary(lore, normalizedId, formattedTitle);

                return new FrontendInstallChoice(
                    Id: $"minecraft:{normalizedId}",
                    Title: formattedTitle,
                    Summary: summary,
                    Version: normalizedId,
                    Kind: FrontendInstallChoiceKind.Minecraft,
                    ManifestUrl: manifestUrl,
                    Metadata: new JsonObject
                    {
                        ["catalogGroup"] = group,
                        ["iconName"] = iconName,
                        ["releaseTime"] = ParseCatalogReleaseTime(releaseTime)?.ToString("O"),
                        ["rawVersion"] = rawId
                    });
            })
            .Where(choice => choice is not null)
            .Cast<FrontendInstallChoice>()
            .OrderByDescending(choice => choice.Metadata?["releaseTime"]?.GetValue<string>(), StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredVersion)
            && choices.All(choice => !string.Equals(choice.Version, preferredVersion, StringComparison.OrdinalIgnoreCase)))
        {
            var extra = versions
                .Select(node => node as JsonObject)
                .FirstOrDefault(node => string.Equals(node?["id"]?.GetValue<string>(), preferredVersion, StringComparison.OrdinalIgnoreCase));
            if (extra is not null)
            {
                var releaseTime = extra["releaseTime"]?.GetValue<string>();
                var group = ResolveMinecraftCatalogGroup(preferredVersion, extra["type"]?.GetValue<string>(), releaseTime);
                choices.Insert(
                    0,
                    new FrontendInstallChoice(
                        Id: $"minecraft:{preferredVersion}",
                        Title: FormatMinecraftCatalogVersion(preferredVersion),
                        Summary: $"当前实例 • {FormatReleaseTime(releaseTime)}",
                        Version: preferredVersion,
                        Kind: FrontendInstallChoiceKind.Minecraft,
                        ManifestUrl: extra["url"]?.GetValue<string>(),
                        Metadata: new JsonObject
                        {
                            ["catalogGroup"] = group,
                            ["iconName"] = ResolveMinecraftCatalogIconName(group),
                            ["releaseTime"] = ParseCatalogReleaseTime(releaseTime)?.ToString("O"),
                            ["rawVersion"] = preferredVersion
                        }));
            }
        }

        return choices;
    }

    public static IReadOnlyList<FrontendInstallChoice> GetSupportedChoices(
        string optionTitle,
        string minecraftVersion)
    {
        try
        {
            return optionTitle switch
            {
                "Forge" => GetForgeChoices(minecraftVersion),
                "NeoForge" => GetNeoForgeChoices(minecraftVersion),
                "Cleanroom" => GetCleanroomChoices(minecraftVersion),
                "Fabric" => GetFabricLoaderChoices(minecraftVersion),
                "Legacy Fabric" => GetLegacyFabricLoaderChoices(minecraftVersion),
                "Quilt" => GetQuiltLoaderChoices(minecraftVersion),
                "LabyMod" => GetLabyModChoices(minecraftVersion),
                "OptiFine" => GetOptiFineChoices(minecraftVersion),
                "LiteLoader" => GetLiteLoaderChoices(minecraftVersion),
                "Fabric API" => GetModrinthFileChoices("fabric-api", minecraftVersion, ["fabric"]),
                "Legacy Fabric API" => GetModrinthFileChoices("9CJED7xi", minecraftVersion, null),
                "QFAPI / QSL" => GetModrinthFileChoices("qvIfYCYJ", minecraftVersion, ["quilt"], allowVersionFallback: true),
                "OptiFabric" => GetOptiFabricChoices(minecraftVersion),
                _ => []
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    public static bool IsFrontendManagedOption(string optionTitle)
    {
        return optionTitle is "Forge"
            or "NeoForge"
            or "Cleanroom"
            or "Fabric"
            or "Legacy Fabric"
            or "Quilt"
            or "LabyMod"
            or "OptiFine"
            or "LiteLoader"
            or "Fabric API"
            or "Legacy Fabric API"
            or "QFAPI / QSL"
            or "OptiFabric";
    }

    public static FrontendInstallApplyResult Apply(
        FrontendInstallApplyRequest request,
        Action<FrontendInstallApplyPhase, string>? onPhaseChanged = null,
        Action<FrontendInstanceRepairTelemetrySnapshot>? onRepairTelemetry = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancelToken.ThrowIfCancellationRequested();

        var launcherDirectory = request.LauncherDirectory;
        var targetDirectory = Path.Combine(launcherDirectory, "versions", request.TargetInstanceName);
        Directory.CreateDirectory(targetDirectory);
        void ReportPrepare(string message) => onPhaseChanged?.Invoke(FrontendInstallApplyPhase.PrepareManifest, message);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.PrepareManifest, "正在写入安装清单并准备安装环境…");
        var manifestNode = BuildTargetManifest(request, ReportPrepare, cancelToken);
        cancelToken.ThrowIfCancellationRequested();
        ReportPrepare("正在清理本地缺失依赖引用…");
        RemoveMissingLocalOnlyLibraries(manifestNode, launcherDirectory);
        var manifestPath = Path.Combine(targetDirectory, $"{request.TargetInstanceName}.json");
        ReportPrepare($"正在写入安装清单文件 {Path.GetFileName(manifestPath)}…");
        File.WriteAllText(manifestPath, manifestNode.ToJsonString(JsonNodeOptions), Utf8NoBom);

        ReportPrepare("正在写入实例配置…");
        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(targetDirectory);
        instanceConfig.Set("VersionVanillaName", request.MinecraftChoice.Version);
        instanceConfig.Set("VersionArgumentIndieV2", request.UseInstanceIsolation);
        instanceConfig.Sync();

        var modsDirectory = request.UseInstanceIsolation
            ? Path.Combine(targetDirectory, "mods")
            : Path.Combine(launcherDirectory, "mods");
        ReportPrepare("正在创建实例目录结构…");
        Directory.CreateDirectory(modsDirectory);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.DownloadSupportFiles, "正在准备受管依赖文件…");
        ApplyManagedModSelection(
            modsDirectory,
            request.FabricApiChoice,
            request.PreserveExistingManagedModFiles,
            "fabric-api");
        ApplyManagedModSelection(
            modsDirectory,
            request.LegacyFabricApiChoice,
            request.PreserveExistingManagedModFiles,
            "legacy-fabric-api");
        ApplyManagedModSelection(
            modsDirectory,
            request.QslChoice,
            request.PreserveExistingManagedModFiles,
            "quilted-fabric-api",
            "qsl");
        ApplyManagedModSelection(
            modsDirectory,
            request.OptiFabricChoice,
            request.PreserveExistingManagedModFiles,
            "optifabric");
        ApplyManagedModSelection(
            modsDirectory,
            ShouldInstallOptiFineAsMod(request) ? request.OptiFineChoice : null,
            request.PreserveExistingManagedModFiles,
            "OptiFine_");

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.DownloadSupportFiles, "正在补全游戏主文件与支持库…");
        var repairResult = request.RunRepair
            ? FrontendInstanceRepairService.Repair(new FrontendInstanceRepairRequest(
                launcherDirectory,
                targetDirectory,
                request.TargetInstanceName,
                request.ForceCoreRefresh),
                onRepairTelemetry,
                cancelToken)
            : new FrontendInstanceRepairResult([], []);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.Finalize, "正在整理实例目录并完成安装…");
        EnsureResourceFolders(targetDirectory, request.UseInstanceIsolation ? targetDirectory : launcherDirectory);

        return new FrontendInstallApplyResult(
            targetDirectory,
            manifestPath,
            repairResult.DownloadedFiles,
            repairResult.ReusedFiles);
    }

    private static JsonObject BuildTargetManifest(
        FrontendInstallApplyRequest request,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        ReportPrepareStatus(onStatusChanged, $"正在获取 Minecraft {request.MinecraftChoice.Version} 原版清单…");
        var baseManifest = ReadJsonObject(request.MinecraftChoice.ManifestUrl ?? MojangVersionManifestUrl);
        JsonObject targetManifest;

        switch (request.PrimaryLoaderChoice?.Kind)
        {
            case FrontendInstallChoiceKind.FabricLoader:
            case FrontendInstallChoiceKind.LegacyFabricLoader:
            case FrontendInstallChoiceKind.QuiltLoader:
                ReportPrepareStatus(onStatusChanged, $"正在获取 {request.PrimaryLoaderChoice.Title} 安装清单…");
                targetManifest = MergeBaseAndLoaderManifest(
                    baseManifest,
                    ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                   ?? throw new InvalidOperationException("缺少安装器清单地址。")),
                    request.TargetInstanceName);
                break;
            case FrontendInstallChoiceKind.LabyMod:
                ReportPrepareStatus(onStatusChanged, "正在获取 LabyMod 安装清单…");
                targetManifest = ReadJsonObject(request.PrimaryLoaderChoice.ManifestUrl
                                               ?? throw new InvalidOperationException("缺少 LabyMod 清单地址。"));
                targetManifest["id"] = request.TargetInstanceName;
                break;
            case FrontendInstallChoiceKind.Forge:
            case FrontendInstallChoiceKind.NeoForge:
            case FrontendInstallChoiceKind.Cleanroom:
                ReportPrepareStatus(onStatusChanged, $"正在准备 {request.PrimaryLoaderChoice.Title} 安装器…");
                targetManifest = MergeBaseAndLoaderManifest(
                    baseManifest,
                    BuildForgelikeManifest(request, request.PrimaryLoaderChoice, onStatusChanged, cancelToken),
                    request.TargetInstanceName);
                break;
            default:
                ReportPrepareStatus(onStatusChanged, "正在整理原版安装清单…");
                targetManifest = CloneObject(baseManifest);
                targetManifest["id"] = request.TargetInstanceName;
                break;
        }

        if (request.LiteLoaderChoice is not null)
        {
            ReportPrepareStatus(onStatusChanged, "正在合并 LiteLoader 安装信息…");
            targetManifest = MergeBaseAndLoaderManifest(
                targetManifest,
                BuildLiteLoaderManifest(request.LiteLoaderChoice),
                request.TargetInstanceName);
        }

        if (ShouldInstallOptiFineStandalone(request))
        {
            ReportPrepareStatus(onStatusChanged, "正在处理 OptiFine 安装信息…");
            targetManifest = MergeBaseAndLoaderManifest(
                targetManifest,
                BuildStandaloneOptiFineManifest(request, onStatusChanged, cancelToken),
                request.TargetInstanceName);
        }

        ReportPrepareStatus(onStatusChanged, "正在生成目标安装清单…");
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

    private static JsonObject BuildForgelikeManifest(
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(loaderChoice);
        cancelToken.ThrowIfCancellationRequested();

        var installerUrl = loaderChoice.DownloadUrl
                           ?? throw new InvalidOperationException("缺少安装器下载地址。");
        var installerPath = CreateTempFile("pcl-forgelike-installer-", ".jar");
        try
        {
            ReportPrepareStatus(onStatusChanged, $"正在下载 {loaderChoice.Title} 安装器…");
            File.WriteAllBytes(installerPath, HttpClient.GetByteArrayAsync(installerUrl, cancelToken).GetAwaiter().GetResult());
            using var archive = ZipFile.OpenRead(installerPath);
            var installProfile = ReadJsonObjectFromEntry(archive, "install_profile.json");

            if (loaderChoice.Kind == FrontendInstallChoiceKind.Forge
                && IsLegacyForgeInstallProfile(installProfile))
            {
                ReportPrepareStatus(onStatusChanged, $"正在解析 {loaderChoice.Title} 安装器内容…");
                return BuildLegacyForgeManifest(archive, request, loaderChoice, installProfile, onStatusChanged);
            }

            return BuildModernForgelikeManifest(
                archive,
                installProfile,
                installerPath,
                request,
                loaderChoice,
                onStatusChanged,
                cancelToken);
        }
        finally
        {
            TryDeleteFile(installerPath);
        }
    }

    private static JsonObject BuildLegacyForgeManifest(
        ZipArchive archive,
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        JsonObject installProfile,
        Action<string>? onStatusChanged = null)
    {
        ValidateLegacyForgeInstallProfile(installProfile, request.MinecraftChoice.Version);
        if (installProfile["install"] is null)
        {
            var jsonPath = installProfile["json"]?.GetValue<string>()?.TrimStart('/');
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                throw new InvalidOperationException("旧版 Forge 安装器缺少版本清单路径。");
            }

            return ReadJsonObjectFromEntry(archive, jsonPath);
        }

        var installPath = installProfile["install"]?["path"]?.GetValue<string>();
        var entryPath = installProfile["install"]?["filePath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(entryPath))
        {
            throw new InvalidOperationException("旧版 Forge 安装器缺少支持库写入信息。");
        }

        var libraryOutputPath = Path.Combine(
            request.LauncherDirectory,
            "libraries",
            FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(installPath).Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(libraryOutputPath)!);
        ReportPrepareStatus(onStatusChanged, $"正在写入 {loaderChoice.Title} 支持库…");
        using (var source = archive.GetEntry(entryPath)?.Open()
                             ?? throw new InvalidOperationException($"旧版 Forge 安装器缺少条目：{entryPath}"))
        using (var output = File.Create(libraryOutputPath))
        {
            source.CopyTo(output);
        }

        if (installProfile["versionInfo"] is not JsonObject versionInfo)
        {
            throw new InvalidOperationException("旧版 Forge 安装器缺少 versionInfo。");
        }

        var manifest = CloneObject(versionInfo);
        if (manifest["inheritsFrom"] is null)
        {
            manifest["inheritsFrom"] = request.MinecraftChoice.Version;
        }

        return manifest;
    }

    private static JsonObject BuildModernForgelikeManifest(
        ZipArchive archive,
        JsonObject installProfile,
        string installerPath,
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        var minecraftVersion = GetRequiredString(
            installProfile,
            "minecraft",
            $"{loaderChoice.Title} 安装器缺少 Minecraft 版本信息。");
        if (!string.Equals(minecraftVersion, request.MinecraftChoice.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{loaderChoice.Title} 安装器目标版本为 {minecraftVersion}，与当前选择的 {request.MinecraftChoice.Version} 不一致。");
        }

        var versionJsonPath = GetRequiredString(
            installProfile,
            "json",
            $"{loaderChoice.Title} 安装器缺少版本清单路径。").TrimStart('/');

        var tempRoot = CreateTempDirectory("pcl-forgelike-");
        try
        {
            ReportPrepareStatus(onStatusChanged, $"正在准备 Minecraft {request.MinecraftChoice.Version} 原版文件…");
            EnsureVanillaVersionFiles(tempRoot, request.MinecraftChoice, onStatusChanged, cancelToken);

            ReportPrepareStatus(onStatusChanged, $"正在解包 {loaderChoice.Title} 安装器中的支持库…");
            CopyEmbeddedForgelikeLibraries(archive, installProfile, tempRoot);

            ReportPrepareStatus(onStatusChanged, $"正在补全 {loaderChoice.Title} 安装依赖…");
            EnsureForgelikeLibrariesAvailable(installProfile, tempRoot, cancelToken);

            ReportPrepareStatus(onStatusChanged, $"正在执行 {loaderChoice.Title} 安装步骤…");
            ExecuteForgelikeProcessors(
                archive,
                installProfile,
                tempRoot,
                installerPath,
                request.MinecraftChoice,
                cancelToken);

            ReportPrepareStatus(onStatusChanged, "正在复制安装器生成的支持库…");
            CopyDirectoryContents(Path.Combine(tempRoot, "libraries"), Path.Combine(request.LauncherDirectory, "libraries"));

            ReportPrepareStatus(onStatusChanged, "正在读取安装器版本清单…");
            return ReadJsonObjectFromEntry(archive, versionJsonPath);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static bool IsLegacyForgeInstallProfile(JsonObject installProfile)
    {
        return installProfile["install"] is JsonObject
               && installProfile["versionInfo"] is JsonObject;
    }

    private static void ValidateLegacyForgeInstallProfile(JsonObject installProfile, string expectedMinecraftVersion)
    {
        var profileMinecraftVersion = installProfile["install"]?["minecraft"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(profileMinecraftVersion)
            && !string.Equals(profileMinecraftVersion, expectedMinecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Forge 安装器目标版本为 {profileMinecraftVersion}，与当前选择的 {expectedMinecraftVersion} 不一致。");
        }
    }

    private static void CopyEmbeddedForgelikeLibraries(
        ZipArchive archive,
        JsonObject installProfile,
        string launcherDirectory)
    {
        if (installProfile["libraries"] is JsonArray libraries)
        {
            foreach (var node in libraries)
            {
                if (node is not JsonObject library)
                {
                    continue;
                }

                var relativePath = ResolveLibraryRelativePath(library);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                TryCopyInstallerEntryToFile(
                    archive,
                    "maven/" + relativePath.Replace('\\', '/'),
                    Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        var mainArtifact = GetArtifactDescriptor(installProfile["path"]);
        if (!string.IsNullOrWhiteSpace(mainArtifact))
        {
            var relativePath = FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(mainArtifact);
            TryCopyInstallerEntryToFile(
                archive,
                "maven/" + relativePath.Replace('\\', '/'),
                Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    private static void EnsureForgelikeLibrariesAvailable(
        JsonObject installProfile,
        string launcherDirectory,
        CancellationToken cancelToken = default)
    {
        if (installProfile["libraries"] is not JsonArray libraries)
        {
            return;
        }

        foreach (var node in libraries)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (node is not JsonObject library)
            {
                continue;
            }

            EnsureForgelikeLibraryAvailable(library, launcherDirectory, cancelToken);
        }
    }

    private static void EnsureForgelikeLibraryAvailable(
        JsonObject library,
        string launcherDirectory,
        CancellationToken cancelToken = default)
    {
        var relativePath = ResolveLibraryRelativePath(library);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var localPath = Path.Combine(launcherDirectory, "libraries", relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
        {
            var expectedSha1 = GetLibraryArtifactSha1(library);
            if (string.IsNullOrWhiteSpace(expectedSha1)
                || string.Equals(ComputeFileSha1(localPath), expectedSha1, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TryDeleteFile(localPath);
        }

        var downloadUrl = ResolveLibraryArtifactUrl(library, relativePath);
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return;
        }

        DownloadFileToPath(downloadUrl, localPath, GetLibraryArtifactSha1(library), cancelToken);
    }

    private static void ExecuteForgelikeProcessors(
        ZipArchive archive,
        JsonObject installProfile,
        string launcherDirectory,
        string installerPath,
        FrontendInstallChoice minecraftChoice,
        CancellationToken cancelToken = default)
    {
        if (installProfile["processors"] is not JsonArray processors)
        {
            return;
        }

        var librariesDirectory = Path.Combine(launcherDirectory, "libraries");
        var tempDataDirectory = Path.Combine(launcherDirectory, "PCL", "InstallerData");
        Directory.CreateDirectory(tempDataDirectory);

        var variables = BuildForgelikeProcessorVariables(
            archive,
            installProfile["data"] as JsonObject,
            tempDataDirectory,
            launcherDirectory,
            installerPath,
            minecraftChoice.Version,
            librariesDirectory);

        foreach (var node in processors)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (node is not JsonObject processor || !IsClientProcessor(processor))
            {
                continue;
            }

            ExecuteForgelikeProcessor(
                processor,
                variables,
                librariesDirectory,
                launcherDirectory,
                minecraftChoice,
                cancelToken);
        }
    }

    private static Dictionary<string, string> BuildForgelikeProcessorVariables(
        ZipArchive archive,
        JsonObject? data,
        string tempDataDirectory,
        string launcherDirectory,
        string installerPath,
        string minecraftVersion,
        string librariesDirectory)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SIDE"] = "client",
            ["MINECRAFT_JAR"] = Path.GetFullPath(Path.Combine(launcherDirectory, "versions", minecraftVersion, minecraftVersion + ".jar")),
            ["MINECRAFT_VERSION"] = Path.GetFullPath(Path.Combine(launcherDirectory, "versions", minecraftVersion, minecraftVersion + ".jar")),
            ["ROOT"] = Path.GetFullPath(launcherDirectory),
            ["INSTALLER"] = Path.GetFullPath(installerPath),
            ["LIBRARY_DIR"] = Path.GetFullPath(librariesDirectory)
        };

        if (data is null)
        {
            return variables;
        }

        foreach (var pair in data)
        {
            var rawValue = pair.Value is JsonObject datum
                ? datum["client"]?.GetValue<string>()
                : pair.Value?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            variables[pair.Key] = ParseForgelikeLiteral(
                rawValue,
                new Dictionary<string, string>(StringComparer.Ordinal),
                librariesDirectory,
                path => ExtractInstallerEntryToTempFile(archive, path, tempDataDirectory));
        }

        return variables;
    }

    private static bool IsClientProcessor(JsonObject processor)
    {
        if (processor["sides"] is not JsonArray sides || sides.Count == 0)
        {
            return true;
        }

        return sides
            .Select(node => node?.GetValue<string>())
            .Any(side => string.Equals(side, "client", StringComparison.OrdinalIgnoreCase));
    }

    private static void ExecuteForgelikeProcessor(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        string launcherDirectory,
        FrontendInstallChoice minecraftChoice,
        CancellationToken cancelToken = default)
    {
        var outputs = ResolveProcessorOutputs(processor, variables, librariesDirectory);
        if (AreProcessorOutputsSatisfied(outputs))
        {
            return;
        }

        if (TryHandleDownloadMojmapsProcessor(processor, variables, librariesDirectory, minecraftChoice, cancelToken))
        {
            EnsureProcessorOutputs(outputs);
            return;
        }

        var processorJar = GetRequiredArtifactPath(
            processor["jar"],
            librariesDirectory,
            "安装器处理器缺少可执行 JAR。");
        if (!File.Exists(processorJar))
        {
            throw new InvalidOperationException($"安装器处理器缺少依赖文件：{processorJar}");
        }

        var mainClass = ReadJarMainClass(processorJar);
        if (string.IsNullOrWhiteSpace(mainClass))
        {
            throw new InvalidOperationException($"处理器 JAR 缺少 Main-Class：{processorJar}");
        }

        var classpath = new List<string>();
        if (processor["classpath"] is JsonArray classpathNodes)
        {
            foreach (var node in classpathNodes)
            {
                var entryPath = GetRequiredArtifactPath(
                    node,
                    librariesDirectory,
                    "安装器处理器缺少类路径依赖。");
                if (!File.Exists(entryPath))
                {
                    throw new InvalidOperationException($"安装器处理器缺少类路径依赖：{entryPath}");
                }

                classpath.Add(entryPath);
            }
        }

        classpath.Add(processorJar);

        var arguments = new List<string>
        {
            "-cp",
            string.Join(Path.PathSeparator, classpath),
            mainClass
        };

        if (processor["args"] is JsonArray argNodes)
        {
            foreach (var node in argNodes)
            {
                var rawArgument = node?.GetValue<string>()
                                  ?? throw new InvalidOperationException("安装器处理器参数缺少文本值。");
                arguments.Add(ParseForgelikeLiteral(rawArgument, variables, librariesDirectory));
            }
        }

        RunProcess(
            ResolveJavaExecutable(),
            arguments,
            launcherDirectory,
            "Forge-like 安装器处理器执行失败。",
            cancelToken);

        EnsureProcessorOutputs(outputs);
    }

    private static IReadOnlyDictionary<string, string> ResolveProcessorOutputs(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        if (processor["outputs"] is not JsonObject outputNodes)
        {
            return outputs;
        }

        foreach (var pair in outputNodes)
        {
            var rawPath = pair.Key;
            var rawHash = pair.Value?.GetValue<string>()
                          ?? throw new InvalidOperationException("安装器处理器输出缺少校验值。");
            var resolvedPath = ParseForgelikeLiteral(rawPath, variables, librariesDirectory);
            var resolvedHash = ParseForgelikeLiteral(rawHash, variables, librariesDirectory);
            outputs[resolvedPath] = resolvedHash;
        }

        return outputs;
    }

    private static bool AreProcessorOutputsSatisfied(IReadOnlyDictionary<string, string> outputs)
    {
        if (outputs.Count == 0)
        {
            return false;
        }

        foreach (var pair in outputs)
        {
            if (!File.Exists(pair.Key))
            {
                return false;
            }

            if (!string.Equals(ComputeFileSha1(pair.Key), pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(pair.Key);
                return false;
            }
        }

        return true;
    }

    private static void EnsureProcessorOutputs(IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var pair in outputs)
        {
            if (!File.Exists(pair.Key))
            {
                throw new InvalidOperationException($"安装器处理器没有生成预期文件：{pair.Key}");
            }

            var actualHash = ComputeFileSha1(pair.Key);
            if (!string.Equals(actualHash, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(pair.Key);
                throw new InvalidOperationException(
                    $"安装器处理器输出校验失败：{pair.Key}，期望 {pair.Value}，实际 {actualHash}。");
            }
        }
    }

    private static bool TryHandleDownloadMojmapsProcessor(
        JsonObject processor,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        FrontendInstallChoice minecraftChoice,
        CancellationToken cancelToken = default)
    {
        if (processor["args"] is not JsonArray argNodes)
        {
            return false;
        }

        var options = ParseProcessorOptions(
            argNodes.Select(node => node?.GetValue<string>() ?? string.Empty),
            variables,
            librariesDirectory);
        if (!string.Equals(options.GetValueOrDefault("task"), "DOWNLOAD_MOJMAPS", StringComparison.Ordinal)
            || !string.Equals(options.GetValueOrDefault("side"), "client", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var version = options.GetValueOrDefault("version");
        var output = options.GetValueOrDefault("output");
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var mappings = ResolveClientMappingsDownload(version, minecraftChoice);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var payload = HttpClient.GetByteArrayAsync(mappings.Url, cancelToken).GetAwaiter().GetResult();
        File.WriteAllBytes(output, payload);

        if (!string.IsNullOrWhiteSpace(mappings.Sha1))
        {
            var actualHash = ComputeFileSha1(output);
            if (!string.Equals(actualHash, mappings.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(output);
                throw new InvalidOperationException(
                    $"Mojang mappings 下载校验失败：期望 {mappings.Sha1}，实际 {actualHash}。");
            }
        }

        return true;
    }

    private static Dictionary<string, string> ParseProcessorOptions(
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? optionName = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (optionName is not null)
                {
                    options[optionName] = string.Empty;
                }

                optionName = arg[2..];
                continue;
            }

            if (optionName is null)
            {
                continue;
            }

            options[optionName] = ParseForgelikeLiteral(arg, variables, librariesDirectory);
            optionName = null;
        }

        if (optionName is not null)
        {
            options[optionName] = string.Empty;
        }

        return options;
    }

    private static (string Url, string? Sha1) ResolveClientMappingsDownload(
        string version,
        FrontendInstallChoice minecraftChoice)
    {
        JsonObject versionManifest;
        if (string.Equals(version, minecraftChoice.Version, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(minecraftChoice.ManifestUrl))
        {
            versionManifest = ReadJsonObject(minecraftChoice.ManifestUrl);
        }
        else
        {
            var manifest = ReadJsonObject(MojangVersionManifestUrl);
            var versionUrl = manifest["versions"] is JsonArray versions
                ? versions
                    .Select(node => node as JsonObject)
                    .FirstOrDefault(node => string.Equals(node?["id"]?.GetValue<string>(), version, StringComparison.OrdinalIgnoreCase))
                    ?["url"]?.GetValue<string>()
                : null;
            if (string.IsNullOrWhiteSpace(versionUrl))
            {
                throw new InvalidOperationException($"无法找到 Minecraft {version} 的 Mojang 版本清单。");
            }

            versionManifest = ReadJsonObject(versionUrl);
        }

        var clientMappings = versionManifest["downloads"]?["client_mappings"] as JsonObject;
        var url = clientMappings?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException($"Minecraft {version} 缺少 client_mappings 下载地址。");
        }

        return (url, clientMappings?["sha1"]?.GetValue<string>());
    }

    private static string ParseForgelikeLiteral(
        string literal,
        IReadOnlyDictionary<string, string> variables,
        string librariesDirectory,
        Func<string, string>? plainValueResolver = null)
    {
        if (literal.Length >= 2 && literal[0] == '{' && literal[^1] == '}')
        {
            var key = literal[1..^1];
            return variables.TryGetValue(key, out var value)
                ? value
                : throw new InvalidOperationException($"安装器变量缺失：{key}");
        }

        if (literal.Length >= 2 && literal[0] == '\'' && literal[^1] == '\'')
        {
            return literal[1..^1];
        }

        if (literal.Length >= 2 && literal[0] == '[' && literal[^1] == ']')
        {
            return GetArtifactAbsolutePath(librariesDirectory, literal[1..^1]);
        }

        var replaced = ReplaceForgelikeTokens(literal, variables);
        return plainValueResolver is null ? replaced : plainValueResolver(replaced);
    }

    private static string ReplaceForgelikeTokens(string value, IReadOnlyDictionary<string, string> variables)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '\\')
            {
                if (index == value.Length - 1)
                {
                    throw new InvalidOperationException($"非法安装器参数：{value}");
                }

                builder.Append(value[++index]);
                continue;
            }

            if (current is '{' or '\'')
            {
                var close = current == '{' ? '}' : '\'';
                var key = new StringBuilder();
                while (++index < value.Length)
                {
                    var tokenChar = value[index];
                    if (tokenChar == '\\')
                    {
                        if (index == value.Length - 1)
                        {
                            throw new InvalidOperationException($"非法安装器参数：{value}");
                        }

                        key.Append(value[++index]);
                        continue;
                    }

                    if (tokenChar == close)
                    {
                        break;
                    }

                    key.Append(tokenChar);
                }

                if (index >= value.Length)
                {
                    throw new InvalidOperationException($"非法安装器参数：{value}");
                }

                if (current == '\'')
                {
                    builder.Append(key);
                    continue;
                }

                if (!variables.TryGetValue(key.ToString(), out var replacement))
                {
                    throw new InvalidOperationException($"安装器变量缺失：{key}");
                }

                builder.Append(replacement);
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string GetRequiredArtifactPath(
        JsonNode? node,
        string librariesDirectory,
        string errorMessage)
    {
        var descriptor = GetArtifactDescriptor(node);
        return string.IsNullOrWhiteSpace(descriptor)
            ? throw new InvalidOperationException(errorMessage)
            : GetArtifactAbsolutePath(librariesDirectory, descriptor);
    }

    private static string GetArtifactAbsolutePath(string librariesDirectory, string descriptor)
    {
        return Path.Combine(
            librariesDirectory,
            FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(descriptor).Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetRequiredString(JsonObject source, string key, string errorMessage)
    {
        var value = source[key]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException(errorMessage) : value;
    }

    private static string? ResolveLibraryRelativePath(JsonObject library)
    {
        var explicitPath = library["downloads"]?["artifact"]?["path"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath.Replace('\\', '/');
        }

        var descriptor = GetArtifactDescriptor(library["name"]);
        return string.IsNullOrWhiteSpace(descriptor)
            ? null
            : FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(descriptor).Replace('\\', '/');
    }

    private static string? ResolveLibraryArtifactUrl(JsonObject library, string relativePath)
    {
        var explicitArtifactUrl = library["downloads"]?["artifact"]?["url"]?.GetValue<string>();
        if (library["downloads"]?["artifact"] is JsonObject && explicitArtifactUrl is not null)
        {
            return string.IsNullOrWhiteSpace(explicitArtifactUrl) ? null : explicitArtifactUrl;
        }

        return FrontendLibraryArtifactResolver.BuildLibraryUrl(
            library["url"]?.GetValue<string>(),
            relativePath);
    }

    private static string? GetLibraryArtifactSha1(JsonObject library)
    {
        return library["downloads"]?["artifact"]?["sha1"]?.GetValue<string>()
               ?? library["sha1"]?.GetValue<string>();
    }

    private static string? GetArtifactDescriptor(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue)
        {
            return node.GetValue<string>();
        }

        if (node is JsonObject obj)
        {
            return obj["name"]?.GetValue<string>();
        }

        return null;
    }

    private static void CopyInstallerEntryToFile(ZipArchive archive, string entryPath, string targetPath)
    {
        using var source = OpenInstallerEntry(archive, entryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var target = File.Create(targetPath);
        source.CopyTo(target);
    }

    private static bool TryCopyInstallerEntryToFile(ZipArchive archive, string entryPath, string targetPath)
    {
        var entry = archive.GetEntry(entryPath.TrimStart('/').Replace('\\', '/'));
        if (entry is null)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var source = entry.Open();
        using var target = File.Create(targetPath);
        source.CopyTo(target);
        return true;
    }

    private static string ExtractInstallerEntryToTempFile(ZipArchive archive, string entryPath, string tempDirectory)
    {
        Directory.CreateDirectory(tempDirectory);
        var extension = Path.GetExtension(entryPath);
        var outputPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + extension);
        CopyInstallerEntryToFile(archive, entryPath, outputPath);
        return Path.GetFullPath(outputPath);
    }

    private static Stream OpenInstallerEntry(ZipArchive archive, string entryPath)
    {
        return archive.GetEntry(entryPath.TrimStart('/').Replace('\\', '/'))?.Open()
               ?? throw new InvalidOperationException($"安装器中缺少条目：{entryPath}");
    }

    private static string ReadJarMainClass(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        using var stream = OpenInstallerEntry(archive, "META-INF/MANIFEST.MF");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? currentHeader = null;
        var currentValue = new StringBuilder();

        void FlushCurrentHeader()
        {
            if (string.Equals(currentHeader, "Main-Class", StringComparison.OrdinalIgnoreCase))
            {
                throw new OperationCanceledException(currentValue.ToString());
            }

            currentHeader = null;
            currentValue.Clear();
        }

        try
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0)
                {
                    FlushCurrentHeader();
                    continue;
                }

                if (line[0] == ' ' && currentHeader is not null)
                {
                    currentValue.Append(line[1..]);
                    continue;
                }

                FlushCurrentHeader();
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                {
                    continue;
                }

                currentHeader = line[..separatorIndex];
                currentValue.Append(line[(separatorIndex + 1)..].TrimStart());
            }

            FlushCurrentHeader();
        }
        catch (OperationCanceledException ex)
        {
            return ex.Message;
        }

        return string.Empty;
    }

    private static string ComputeFileSha1(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }

    private static void DownloadFileToPath(
        string url,
        string targetPath,
        string? expectedSha1 = null,
        CancellationToken cancelToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = HttpClient.GetByteArrayAsync(url, cancelToken).GetAwaiter().GetResult();
            File.WriteAllBytes(tempPath, bytes);

            if (!string.IsNullOrWhiteSpace(expectedSha1))
            {
                var actualSha1 = ComputeFileSha1(tempPath);
                if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"下载的安装器依赖校验失败：{targetPath}，期望 {expectedSha1}，实际 {actualSha1}。");
                }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static JsonObject BuildLiteLoaderManifest(FrontendInstallChoice choice)
    {
        if (choice.Metadata?["token"] is not JsonObject token)
        {
            throw new InvalidOperationException("LiteLoader 安装项缺少版本元数据。");
        }

        var releaseTime = choice.Metadata["releaseTime"]?.GetValue<string>() ?? string.Empty;
        var libraries = token["libraries"] is JsonArray tokenLibraries
            ? (JsonArray)tokenLibraries.DeepClone()
            : new JsonArray();
        libraries.Add(new JsonObject
        {
            ["name"] = $"com.mumfrey:liteloader:{token["version"]?.GetValue<string>() ?? choice.Version}",
            ["url"] = "https://dl.liteloader.com/versions/"
        });

        return new JsonObject
        {
            ["time"] = releaseTime,
            ["releaseTime"] = releaseTime,
            ["type"] = "release",
            ["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray(
                    "--tweakClass",
                    token["tweakClass"]?.GetValue<string>() ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker")
            },
            ["libraries"] = libraries,
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minimumLauncherVersion"] = 18
        };
    }

    private static JsonObject BuildStandaloneOptiFineManifest(
        FrontendInstallApplyRequest request,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        var choice = request.OptiFineChoice
                     ?? throw new InvalidOperationException("缺少 OptiFine 选择项。");
        if (IsModernOptiFineVersion(choice))
        {
            return BuildModernOptiFineManifest(request, choice, onStatusChanged, cancelToken);
        }

        return BuildLegacyOptiFineManifest(choice);
    }

    private static JsonObject BuildModernOptiFineManifest(
        FrontendInstallApplyRequest request,
        FrontendInstallChoice choice,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        var installerUrl = choice.DownloadUrl
                           ?? throw new InvalidOperationException("缺少 OptiFine 下载地址。");
        var installerPath = CreateTempFile("pcl-optifine-", ".jar");
        var tempRoot = CreateTempDirectory("pcl-optifine-home-");

        try
        {
            ReportPrepareStatus(onStatusChanged, "正在下载 OptiFine 安装器…");
            File.WriteAllBytes(installerPath, HttpClient.GetByteArrayAsync(installerUrl, cancelToken).GetAwaiter().GetResult());
            ReportPrepareStatus(onStatusChanged, $"正在准备 Minecraft {request.MinecraftChoice.Version} 原版文件…");
            EnsureVanillaVersionFiles(tempRoot, request.MinecraftChoice, onStatusChanged, cancelToken);
            ReportPrepareStatus(onStatusChanged, "正在执行 OptiFine 安装器…");
            RunOptiFineInstaller(installerPath, tempRoot, cancelToken);
            ReportPrepareStatus(onStatusChanged, "正在复制 OptiFine 生成的支持库…");
            CopyDirectoryContents(Path.Combine(tempRoot, "libraries"), Path.Combine(request.LauncherDirectory, "libraries"));

            var generatedVersion = choice.Metadata?["nameVersion"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(generatedVersion))
            {
                throw new InvalidOperationException("OptiFine 安装项缺少版本目录信息。");
            }

            var manifestPath = Path.Combine(tempRoot, "versions", generatedVersion, $"{generatedVersion}.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException("OptiFine 安装器没有产出可读取的版本清单。");
            }

            ReportPrepareStatus(onStatusChanged, "正在读取 OptiFine 生成的版本清单…");
            return ReadJsonObjectFromFile(manifestPath);
        }
        finally
        {
            TryDeleteFile(installerPath);
            TryDeleteDirectory(tempRoot);
        }
    }

    private static JsonObject BuildLegacyOptiFineManifest(FrontendInstallChoice choice)
    {
        var libraryVersion = choice.Metadata?["libraryVersion"]?.GetValue<string>();
        var downloadUrl = choice.DownloadUrl;
        if (string.IsNullOrWhiteSpace(libraryVersion) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("旧版 OptiFine 安装项缺少库信息。");
        }

        return new JsonObject
        {
            ["type"] = "release",
            ["libraries"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = $"optifine:OptiFine:{libraryVersion}",
                    ["downloads"] = new JsonObject
                    {
                        ["artifact"] = new JsonObject
                        {
                            ["path"] = $"optifine/OptiFine/{libraryVersion}/OptiFine-{libraryVersion}.jar",
                            ["url"] = downloadUrl
                        }
                    }
                },
                new JsonObject
                {
                    ["name"] = "net.minecraft:launchwrapper:1.12"
                }
            },
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minimumLauncherVersion"] = "21",
            ["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray("--tweakClass", "optifine.OptiFineTweaker")
            }
        };
    }

    private static void EnsureVanillaVersionFiles(
        string launcherDirectory,
        FrontendInstallChoice minecraftChoice,
        Action<string>? onStatusChanged = null,
        CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        var manifestUrl = minecraftChoice.ManifestUrl
                          ?? throw new InvalidOperationException("缺少 Minecraft 版本清单地址。");
        ReportPrepareStatus(onStatusChanged, $"正在读取 Minecraft {minecraftChoice.Version} 版本详情…");
        var baseManifest = ReadJsonObject(manifestUrl);
        var versionDirectory = Path.Combine(launcherDirectory, "versions", minecraftChoice.Version);
        Directory.CreateDirectory(versionDirectory);

        var manifestPath = Path.Combine(versionDirectory, $"{minecraftChoice.Version}.json");
        ReportPrepareStatus(onStatusChanged, $"正在写入 Minecraft {minecraftChoice.Version} 原版清单…");
        File.WriteAllText(manifestPath, baseManifest.ToJsonString(JsonNodeOptions), Utf8NoBom);

        var jarPath = Path.Combine(versionDirectory, $"{minecraftChoice.Version}.jar");
        if (File.Exists(jarPath))
        {
            return;
        }

        var clientUrl = baseManifest["downloads"]?["client"]?["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(clientUrl))
        {
            throw new InvalidOperationException("缺少原版客户端下载地址。");
        }

        ReportPrepareStatus(onStatusChanged, $"正在下载 Minecraft {minecraftChoice.Version} 原版客户端…");
        File.WriteAllBytes(jarPath, HttpClient.GetByteArrayAsync(clientUrl, cancelToken).GetAwaiter().GetResult());
        var launcherProfilesPath = Path.Combine(launcherDirectory, "launcher_profiles.json");
        if (!File.Exists(launcherProfilesPath))
        {
            ReportPrepareStatus(onStatusChanged, "正在初始化 launcher_profiles.json…");
            File.WriteAllText(launcherProfilesPath, "{}", Utf8NoBom);
        }
    }

    private static void RunOptiFineInstaller(string installerPath, string launcherDirectory, CancellationToken cancelToken = default)
    {
        var javaPath = ResolveJavaExecutable();
        var arguments = $"-Duser.home={QuoteArgument(launcherDirectory)} -cp {QuoteArgument(installerPath)} optifine.Installer";
        if ((GetJavaMajorVersion(javaPath) ?? 0) >= 9)
        {
            arguments = "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + arguments;
        }

        RunProcess(javaPath, arguments, launcherDirectory, "OptiFine 安装器执行失败。", cancelToken);
    }

    private static void RunProcess(string fileName, string arguments, string workingDirectory, string failureMessage, CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException(failureMessage);
        using var cancellationRegistration = cancelToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill failures: the process may have already exited.
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        cancelToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"{failureMessage} {detail}".Trim());
        }
    }

    private static void RunProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string failureMessage,
        CancellationToken cancelToken = default)
    {
        cancelToken.ThrowIfCancellationRequested();
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException(failureMessage);
        using var cancellationRegistration = cancelToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill failures: the process may have already exited.
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        cancelToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"{failureMessage} {detail}".Trim());
        }
    }

    private static string ResolveJavaExecutable()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var candidate = Path.Combine(javaHome, "bin", OperatingSystem.IsWindows() ? "java.exe" : "java");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            foreach (var candidate in new[]
                     {
                         "/opt/homebrew/opt/openjdk/bin/java",
                         "/usr/local/opt/openjdk/bin/java"
                     })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private static int? GetJavaMajorVersion(string javaPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var marker = output.IndexOf('"');
            if (marker < 0)
            {
                return null;
            }

            var versionString = output[(marker + 1)..];
            var endMarker = versionString.IndexOf('"');
            if (endMarker >= 0)
            {
                versionString = versionString[..endMarker];
            }

            if (versionString.StartsWith("1.", StringComparison.Ordinal))
            {
                return int.TryParse(versionString.Split('.')[1], out var legacyMajor) ? legacyMajor : null;
            }

            return int.TryParse(versionString.Split('.')[0], out var major) ? major : null;
        }
        catch
        {
            return null;
        }
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
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

    private static IReadOnlyList<FrontendInstallChoice> GetForgeChoices(string minecraftVersion)
    {
        var root = ReadJsonArray($"https://bmclapi2.bangbang93.com/forge/minecraft/{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Select(node =>
            {
                var installerFile = node!["files"] is JsonArray files
                    ? files.Select(file => file as JsonObject)
                        .FirstOrDefault(file =>
                            string.Equals(file?["category"]?.GetValue<string>(), "installer", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(file?["format"]?.GetValue<string>(), "jar", StringComparison.OrdinalIgnoreCase))
                    : null;
                if (installerFile is null)
                {
                    return null;
                }

                var version = node["version"]?.GetValue<string>() ?? string.Empty;
                var branch = node["branch"]?.GetValue<string>();
                var fileVersion = version + (string.IsNullOrWhiteSpace(branch) ? string.Empty : "-" + branch);
                var fileName = $"{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}/forge-{minecraftVersion.Replace("-", "_", StringComparison.Ordinal)}-{fileVersion}-installer.jar";
                return new FrontendInstallChoice(
                    Id: $"forge:{minecraftVersion}:{version}",
                    Title: version,
                    Summary: $"安装器 • {FormatReleaseTime(node["modified"]?.GetValue<string>())}",
                    Version: version,
                    Kind: FrontendInstallChoiceKind.Forge,
                    DownloadUrl: $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                    FileName: Path.GetFileName(fileName),
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["fileVersion"] = fileVersion,
                        ["hash"] = installerFile["hash"]?.GetValue<string>(),
                        ["releaseTime"] = ParseCatalogReleaseTime(node["modified"]?.GetValue<string>())?.ToString("O")
                    });
            })
            .Where(choice => choice is not null)
            .Cast<FrontendInstallChoice>(),
            36);
    }

    private static IReadOnlyList<FrontendInstallChoice> GetNeoForgeChoices(string minecraftVersion)
    {
        var main = ReadJsonObject("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge");
        var legacy = ReadJsonObject("https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge");
        var choices = new List<FrontendInstallChoice>();

        AddNeoForgeChoices(choices, main, FrontendInstallChoiceKind.NeoForge, minecraftVersion);
        AddNeoForgeChoices(choices, legacy, FrontendInstallChoiceKind.NeoForge, minecraftVersion);

        return SortInstallChoicesByVersionDescending(
            choices
            .GroupBy(choice => choice.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()),
            36);
    }

    private static void AddNeoForgeChoices(
        ICollection<FrontendInstallChoice> choices,
        JsonObject root,
        FrontendInstallChoiceKind kind,
        string minecraftVersion)
    {
        if (root["files"] is not JsonArray files)
        {
            return;
        }

        foreach (var file in files.Select(node => node as JsonObject))
        {
            var apiName = file?["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(apiName))
            {
                continue;
            }

            var (inherit, versionName, packageName) = ParseNeoForgeApiName(apiName);
            if (!string.Equals(inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            choices.Add(new FrontendInstallChoice(
                Id: $"neoforge:{apiName}",
                Title: versionName,
                Summary: apiName.Contains("beta", StringComparison.OrdinalIgnoreCase) || apiName.Contains("alpha", StringComparison.OrdinalIgnoreCase)
                    ? "测试版"
                    : "稳定版",
                Version: versionName,
                Kind: kind,
                DownloadUrl: $"https://bmclapi2.bangbang93.com/maven/net/neoforged/{packageName}/{apiName}/{packageName}-{apiName}-installer.jar",
                FileName: $"{packageName}-{apiName}-installer.jar",
                Metadata: new JsonObject
                {
                    ["apiName"] = apiName,
                    ["minecraftVersion"] = inherit
                }));
        }
    }

    private static (string MinecraftVersion, string VersionName, string PackageName) ParseNeoForgeApiName(string apiName)
    {
        if (apiName.Contains("1.20.1-", StringComparison.Ordinal))
        {
            return ("1.20.1", apiName.Replace("1.20.1-", string.Empty, StringComparison.Ordinal), "forge");
        }

        if (apiName.StartsWith("0.", StringComparison.Ordinal))
        {
            var parts = apiName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return (parts.Length > 1 ? parts[1] : apiName, apiName, "neoforge");
        }

        var versionCore = apiName.Split('-', 2)[0];
        var segments = versionCore.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return (apiName, apiName, "neoforge");
        }

        var major = int.TryParse(segments[0], out var parsedMajor) ? parsedMajor : 0;
        var minor = segments.Length > 1 && int.TryParse(segments[1], out var parsedMinor) ? parsedMinor : 0;
        var inherit = major >= 24
            ? versionCore.TrimEnd('0').TrimEnd('.')
            : "1." + major + (minor > 0 ? "." + minor : string.Empty);
        if (apiName.Contains('+', StringComparison.Ordinal))
        {
            inherit += "-" + apiName.Split('+', 2)[1];
        }

        return (inherit, apiName, "neoforge");
    }

    private static IReadOnlyList<FrontendInstallChoice> GetCleanroomChoices(string minecraftVersion)
    {
        if (!string.Equals(minecraftVersion, "1.12.2", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var root = ReadJsonArray("https://api.github.com/repos/CleanroomMC/Cleanroom/releases");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => !string.IsNullOrWhiteSpace(node?["tag_name"]?.GetValue<string>()))
            .Select(node =>
            {
                var tag = node!["tag_name"]!.GetValue<string>();
                return new FrontendInstallChoice(
                    Id: $"cleanroom:{tag}",
                    Title: tag,
                    Summary: tag.Contains("alpha", StringComparison.OrdinalIgnoreCase) ? "测试版" : "稳定版",
                    Version: tag,
                    Kind: FrontendInstallChoiceKind.Cleanroom,
                    DownloadUrl: $"https://github.com/CleanroomMC/Cleanroom/releases/download/{tag}/cleanroom-{tag}-installer.jar",
                    FileName: $"cleanroom-{tag}-installer.jar",
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["releaseTime"] = ParseCatalogReleaseTime(node["published_at"]?.GetValue<string>())?.ToString("O")
                    });
            })
            .Cast<FrontendInstallChoice>(),
            18);
    }

    private static IReadOnlyList<FrontendInstallChoice> GetFabricLoaderChoices(string minecraftVersion)
    {
        return ReadLoaderChoices(
            $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.FabricLoader,
            "Fabric");
    }

    private static IReadOnlyList<FrontendInstallChoice> GetLegacyFabricLoaderChoices(string minecraftVersion)
    {
        return ReadLoaderChoices(
            $"https://meta.legacyfabric.net/v2/versions/loader/{minecraftVersion}",
            FrontendInstallChoiceKind.LegacyFabricLoader,
            "Legacy Fabric");
    }

    private static IReadOnlyList<FrontendInstallChoice> GetQuiltLoaderChoices(string minecraftVersion)
    {
        var root = ReadJsonArray($"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => node?["loader"] is JsonObject)
            .Select(node =>
            {
                var loader = (JsonObject)node!["loader"]!;
                var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                var profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{version}/profile/json";
                return new FrontendInstallChoice(
                    Id: $"quilt:{version}",
                    Title: version,
                    Summary: loader["maven"]?.GetValue<string>() ?? "Quilt 安装器",
                    Version: version,
                    Kind: FrontendInstallChoiceKind.QuiltLoader,
                    ManifestUrl: profileUrl);
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version)),
            18);
    }

    private static IReadOnlyList<FrontendInstallChoice> GetLabyModChoices(string minecraftVersion)
    {
        var production = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json");
        var snapshot = ReadJsonObject("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json");
        var choices = new List<FrontendInstallChoice>();

        AddLabyChoice(choices, production, minecraftVersion, "production", "稳定版");
        AddLabyChoice(choices, snapshot, minecraftVersion, "snapshot", "快照版");
        return choices;
    }

    private static void AddLabyChoice(
        ICollection<FrontendInstallChoice> choices,
        JsonObject manifest,
        string minecraftVersion,
        string channel,
        string channelLabel)
    {
        if (manifest["minecraftVersions"] is not JsonArray versions)
        {
            return;
        }

        var versionEntry = versions
            .Select(node => node as JsonObject)
            .FirstOrDefault(node => string.Equals(node?["version"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase));
        if (versionEntry is null)
        {
            return;
        }

        var commitReference = manifest["commitReference"]?.GetValue<string>() ?? string.Empty;
        var versionLabel = manifest["labyModVersion"]?.GetValue<string>() ?? commitReference;
        choices.Add(new FrontendInstallChoice(
            Id: $"labymod:{channel}:{commitReference}",
            Title: $"{versionLabel} {channelLabel}",
            Summary: $"LabyMod 4 • {minecraftVersion}",
            Version: versionLabel,
            Kind: FrontendInstallChoiceKind.LabyMod,
            ManifestUrl: versionEntry["customManifestUrl"]?.GetValue<string>()));
    }

    private static IReadOnlyList<FrontendInstallChoice> GetOptiFineChoices(string minecraftVersion)
    {
        var root = ReadJsonArray("https://bmclapi2.bangbang93.com/optifine/versionList");
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => string.Equals(node?["mcversion"]?.GetValue<string>(), minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .Select(node =>
            {
                var type = node!["type"]?.GetValue<string>() ?? "HD_U";
                var patch = node["patch"]?.GetValue<string>() ?? string.Empty;
                var fileName = node["filename"]?.GetValue<string>() ?? string.Empty;
                var displayName = (minecraftVersion + type.Replace("HD_U", string.Empty, StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal) + " " + patch)
                    .Replace(".0 ", " ", StringComparison.Ordinal);
                var nameVersion = minecraftVersion + "-OptiFine_" + (type + " " + patch)
                    .Replace(".0 ", " ", StringComparison.Ordinal)
                    .Replace(" ", "_", StringComparison.Ordinal)
                    .Replace(minecraftVersion + "_", string.Empty, StringComparison.Ordinal);
                var libraryVersion = fileName
                    .Replace("OptiFine_", string.Empty, StringComparison.Ordinal)
                    .Replace(".jar", string.Empty, StringComparison.Ordinal)
                    .Replace("preview_", string.Empty, StringComparison.Ordinal);
                var bmclVersion = minecraftVersion is "1.8" or "1.9" ? minecraftVersion + ".0" : minecraftVersion;
                var shortName = displayName.Replace(minecraftVersion + " ", string.Empty, StringComparison.Ordinal);
                var downloadUrl = patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    ? "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U_" + shortName.Replace(" ", "/", StringComparison.Ordinal)
                    : "https://bmclapi2.bangbang93.com/optifine/" + bmclVersion + "/HD_U/" + shortName;
                var requiredForge = node["forge"]?.GetValue<string>()
                    ?.Replace("Forge ", string.Empty, StringComparison.Ordinal)
                    .Replace("#", string.Empty, StringComparison.Ordinal);
                if (requiredForge?.Contains("N/A", StringComparison.OrdinalIgnoreCase) == true)
                {
                    requiredForge = null;
                }

                return new FrontendInstallChoice(
                    Id: $"optifine:{nameVersion}",
                    Title: shortName,
                    Summary: patch.Contains("pre", StringComparison.OrdinalIgnoreCase) ? "预览版" : "正式版",
                    Version: shortName,
                    Kind: FrontendInstallChoiceKind.OptiFine,
                    DownloadUrl: downloadUrl,
                    FileName: fileName,
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["nameVersion"] = nameVersion,
                        ["libraryVersion"] = libraryVersion,
                        ["requiredForgeVersion"] = requiredForge,
                        ["isPreview"] = patch.Contains("pre", StringComparison.OrdinalIgnoreCase)
                    });
            })
            .Cast<FrontendInstallChoice>(),
            24);
    }

    private static IReadOnlyList<FrontendInstallChoice> GetLiteLoaderChoices(string minecraftVersion)
    {
        var root = ReadJsonObject("https://dl.liteloader.com/versions/versions.json");
        if (root["versions"] is not JsonObject versions
            || !versions.TryGetPropertyValue(minecraftVersion, out var versionNode)
            || versionNode is not JsonObject versionObject)
        {
            return [];
        }

        var source = versionObject["artefacts"] ?? versionObject["snapshots"];
        var token = source?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
        if (token is null)
        {
            return [];
        }

        var releaseTime = token["timestamp"]?.GetValue<string>() ?? string.Empty;
        var formattedReleaseTime = long.TryParse(releaseTime, out var rawTimestamp)
            ? DateTimeOffset.FromUnixTimeSeconds(rawTimestamp).LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            : "未记录发布时间";

        return
        [
            new FrontendInstallChoice(
                Id: $"liteloader:{minecraftVersion}:{token["version"]?.GetValue<string>()}",
                Title: token["version"]?.GetValue<string>() ?? minecraftVersion,
                Summary: $"{(string.Equals(token["stream"]?.GetValue<string>(), "SNAPSHOT", StringComparison.OrdinalIgnoreCase) ? "测试版" : "稳定版")} • {formattedReleaseTime}",
                Version: token["version"]?.GetValue<string>() ?? minecraftVersion,
                Kind: FrontendInstallChoiceKind.LiteLoader,
                Metadata: new JsonObject
                {
                    ["minecraftVersion"] = minecraftVersion,
                    ["releaseTime"] = DateTimeOffset.FromUnixTimeSeconds(rawTimestamp).ToString("O"),
                    ["token"] = token.DeepClone()
                })
        ];
    }

    private static IReadOnlyList<FrontendInstallChoice> GetOptiFabricChoices(string minecraftVersion)
    {
        if (minecraftVersion.StartsWith("1.14", StringComparison.OrdinalIgnoreCase)
            || minecraftVersion.StartsWith("1.15", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var root = ReadJsonObject("https://api.cfwidget.com/minecraft/mc-mods/optifabric");
        if (root["files"] is not JsonArray files)
        {
            return [];
        }

        return files
            .Select(node => node as JsonObject)
            .Where(node => node is not null)
            .Where(node => CfWidgetFileMatchesVersion(node!, minecraftVersion))
            .OrderByDescending(node => node!["uploaded_at"]?.GetValue<string>() ?? string.Empty)
            .Take(18)
            .Select(node =>
            {
                var fileId = node!["id"]?.GetValue<int>() ?? 0;
                var fileName = node["name"]?.GetValue<string>() ?? string.Empty;
                var displayName = node["display"]?.GetValue<string>() ?? fileName;
                var type = node["type"]?.GetValue<string>() ?? string.Empty;
                var uploadedAt = node["uploaded_at"]?.GetValue<string>() ?? string.Empty;

                return new FrontendInstallChoice(
                    Id: $"optifabric:{fileId}",
                    Title: displayName.Replace("OptiFabric-v", string.Empty, StringComparison.OrdinalIgnoreCase).Trim(),
                    Summary: $"{NormalizeVersionType(type)} • {FormatReleaseTime(uploadedAt)}",
                    Version: displayName,
                    Kind: FrontendInstallChoiceKind.ModFile,
                    DownloadUrl: BuildCurseForgeMediaUrl(fileId, fileName),
                    FileName: fileName,
                    Metadata: new JsonObject
                    {
                        ["minecraftVersion"] = minecraftVersion,
                        ["fileId"] = fileId
                    });
            })
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstallChoice> GetModrinthFileChoices(
        string projectId,
        string minecraftVersion,
        IReadOnlyList<string>? loaders,
        bool allowVersionFallback = false)
    {
        foreach (var candidateVersion in GetVersionCandidates(minecraftVersion, allowVersionFallback))
        {
            var url = BuildModrinthVersionUrl(projectId, candidateVersion, loaders);
            JsonArray root;
            try
            {
                root = ReadJsonArray(url);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            var choices = root
                .Select(node => node as JsonObject)
                .Where(node => node is not null)
                .Select(ToModrinthChoice)
                .Where(choice => choice is not null)
                .Cast<FrontendInstallChoice>();
            var orderedChoices = SortInstallChoicesDescending(choices, 18);
            if (orderedChoices.Count > 0)
            {
                return orderedChoices;
            }
        }

        return [];
    }

    private static FrontendInstallChoice? ToModrinthChoice(JsonObject? version)
    {
        if (version?["files"] is not JsonArray files)
        {
            return null;
        }

        var primaryFile = files
            .Select(node => node as JsonObject)
            .FirstOrDefault(file => file?["primary"]?.GetValue<bool>() == true)
            ?? files.Select(node => node as JsonObject).FirstOrDefault(file => file is not null);
        if (primaryFile is null)
        {
            return null;
        }

        var title = version["version_number"]?.GetValue<string>()
                    ?? version["name"]?.GetValue<string>()
                    ?? primaryFile["filename"]?.GetValue<string>()
                    ?? string.Empty;
        var published = version["date_published"]?.GetValue<string>() ?? string.Empty;
        var versionType = version["version_type"]?.GetValue<string>() ?? "release";

        return new FrontendInstallChoice(
            Id: $"mod:{version["id"]?.GetValue<string>()}",
            Title: title,
            Summary: $"{NormalizeVersionType(versionType)} • {FormatReleaseTime(published)}",
            Version: title,
            Kind: FrontendInstallChoiceKind.ModFile,
            DownloadUrl: primaryFile["url"]?.GetValue<string>(),
            FileName: primaryFile["filename"]?.GetValue<string>(),
            Metadata: new JsonObject
            {
                ["releaseTime"] = ParseCatalogReleaseTime(published)?.ToString("O")
            });
    }

    private static IReadOnlyList<FrontendInstallChoice> ReadLoaderChoices(
        string url,
        FrontendInstallChoiceKind kind,
        string prefix)
    {
        var root = ReadJsonArray(url);
        return SortInstallChoicesByVersionDescending(
            root
            .Select(node => node as JsonObject)
            .Where(node => node?["loader"] is JsonObject)
            .Select(node =>
            {
                var loader = (JsonObject)node!["loader"]!;
                var version = loader["version"]?.GetValue<string>() ?? string.Empty;
                return new FrontendInstallChoice(
                    Id: $"{kind}:{version}",
                    Title: version,
                    Summary: loader["stable"]?.GetValue<bool>() == true ? "稳定版" : $"{prefix} 测试版",
                    Version: version,
                    Kind: kind,
                    ManifestUrl: $"{url.TrimEnd('/')}/{version}/profile/json");
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version)),
            18);
    }

    private static IEnumerable<string> GetVersionCandidates(string minecraftVersion, bool allowFallback)
    {
        yield return minecraftVersion;

        if (!allowFallback)
        {
            yield break;
        }

        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            yield return $"{parts[0]}.{parts[1]}";
        }
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
        File.WriteAllBytes(outputPath, HttpClient.GetByteArrayAsync(selectedChoice.DownloadUrl).GetAwaiter().GetResult());
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

    private static JsonObject ReadJsonObject(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.UserAgent.ParseAdd("PCL-CE-Frontend");
        }

        using var response = HttpClient.Send(request);
        response.EnsureSuccessStatusCode();
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return JsonNode.Parse(content)?.AsObject()
               ?? throw new InvalidOperationException($"无法读取 JSON 对象：{url}");
    }

    private static JsonArray ReadJsonArray(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.UserAgent.ParseAdd("PCL-CE-Frontend");
        }

        using var response = HttpClient.Send(request);
        response.EnsureSuccessStatusCode();
        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return JsonNode.Parse(content)?.AsArray()
               ?? throw new InvalidOperationException($"无法读取 JSON 数组：{url}");
    }

    private static JsonObject ReadJsonObjectFromEntry(ZipArchive archive, string entryPath)
    {
        using var stream = archive.GetEntry(entryPath)?.Open()
                           ?? throw new InvalidOperationException($"安装器中缺少条目：{entryPath}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject()
               ?? throw new InvalidOperationException($"无法读取安装器 JSON：{entryPath}");
    }

    private static JsonObject ReadJsonObjectFromFile(string filePath)
    {
        return JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
               ?? throw new InvalidOperationException($"无法读取 JSON 文件：{filePath}");
    }

    private static JsonObject CloneObject(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString())?.AsObject()
               ?? throw new InvalidOperationException("复制安装清单失败。");
    }

    private static void ReportPrepareStatus(Action<string>? onStatusChanged, string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            onStatusChanged?.Invoke(message);
        }
    }

    private static string BuildModrinthVersionUrl(string projectId, string minecraftVersion, IReadOnlyList<string>? loaders)
    {
        var builder = new UriBuilder($"https://api.modrinth.com/v2/project/{projectId}/version");
        var query = new List<string> { $"game_versions=%5B%22{Uri.EscapeDataString(minecraftVersion)}%22%5D" };
        if (loaders is { Count: > 0 })
        {
            query.Add($"loaders=%5B%22{string.Join("%22,%22", loaders.Select(Uri.EscapeDataString))}%22%5D");
        }

        builder.Query = string.Join("&", query);
        return builder.ToString();
    }

    private static string BuildCurseForgeMediaUrl(int fileId, string fileName)
    {
        return $"https://mediafiles.forgecdn.net/files/{fileId / 1000}/{fileId % 1000:D3}/{Uri.EscapeDataString(fileName)}";
    }

    private static bool CfWidgetFileMatchesVersion(JsonObject file, string minecraftVersion)
    {
        if (file["versions"] is not JsonArray versions)
        {
            return false;
        }

        var tokens = versions
            .Select(node => node?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tokens.Contains("Fabric") && tokens.Contains(minecraftVersion);
    }

    private static DateTimeOffset? ParseCatalogReleaseTime(string? rawValue)
    {
        return DateTimeOffset.TryParse(rawValue, out var value) ? value : null;
    }

    private static string NormalizeMinecraftCatalogVersionId(string rawId)
    {
        return rawId switch
        {
            "2point0_blue" => "2.0_blue",
            "2point0_red" => "2.0_red",
            "2point0_purple" => "2.0_purple",
            "20w14infinite" => "20w14∞",
            _ => rawId
        };
    }

    private static string ResolveMinecraftCatalogGroup(string versionId, string? type, string? releaseTime)
    {
        var normalizedId = versionId.ToLowerInvariant();
        switch (type?.ToLowerInvariant())
        {
            case "release":
                return "正式版";
            case "snapshot":
            case "pending":
                if (IsAprilFoolsVersion(normalizedId, releaseTime))
                {
                    return "愚人节版";
                }

                return LooksLikeMisclassifiedRelease(normalizedId) ? "正式版" : "预览版";
            case "special":
                return "愚人节版";
            default:
                return IsAprilFoolsVersion(normalizedId, releaseTime) ? "愚人节版" : "远古版";
        }
    }

    private static bool LooksLikeMisclassifiedRelease(string normalizedId)
    {
        return normalizedId.StartsWith("1.", StringComparison.Ordinal)
               && !normalizedId.Contains("combat", StringComparison.Ordinal)
               && !normalizedId.Contains("rc", StringComparison.Ordinal)
               && !normalizedId.Contains("experimental", StringComparison.Ordinal)
               && !normalizedId.Equals("1.2", StringComparison.Ordinal)
               && !normalizedId.Contains("pre", StringComparison.Ordinal);
    }

    private static bool IsAprilFoolsVersion(string normalizedId, string? releaseTime)
    {
        if (ResolveMinecraftCatalogAprilFoolsLore(normalizedId).Length > 0)
        {
            return true;
        }

        var parsed = ParseCatalogReleaseTime(releaseTime);
        return parsed is not null
               && parsed.Value.ToUniversalTime().AddHours(2) is var adjusted
               && adjusted.Month == 4
               && adjusted.Day == 1;
    }

    private static string ResolveMinecraftCatalogIconName(string group)
    {
        return group switch
        {
            "预览版" => "CommandBlock.png",
            "愚人节版" => "GoldBlock.png",
            "远古版" => "CobbleStone.png",
            _ => "Grass.png"
        };
    }

    private static string ResolveMinecraftCatalogLore(string versionId, string group, string? releaseTime)
    {
        var lore = ResolveMinecraftCatalogAprilFoolsLore(versionId.ToLowerInvariant());
        if (lore.Length > 0)
        {
            return lore;
        }

        if (!string.Equals(group, "愚人节版", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var parsed = ParseCatalogReleaseTime(releaseTime);
        return parsed is null ? string.Empty : $"发布于 {parsed.Value.LocalDateTime:yyyy/MM/dd HH:mm} 的愚人节特辑";
    }

    private static string ResolveMinecraftCatalogAprilFoolsLore(string normalizedId)
    {
        return normalizedId switch
        {
            var value when value.StartsWith("2.0", StringComparison.Ordinal) || value.StartsWith("2point0", StringComparison.Ordinal) =>
                value.EndsWith("red", StringComparison.Ordinal)
                    ? "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！（红色版本）"
                    : value.EndsWith("blue", StringComparison.Ordinal)
                        ? "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！（蓝色版本）"
                        : value.EndsWith("purple", StringComparison.Ordinal)
                            ? "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！（紫色版本）"
                            : "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！",
            "15w14a" => "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。",
            "1.rv-pre1" => "2016 | 是时候将现代科技带入 Minecraft 了！",
            "3d shareware v1.34" => "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！",
            var value when value.StartsWith("20w14inf", StringComparison.Ordinal) || value == "20w14∞" => "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！",
            "22w13oneblockatatime" => "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！",
            "23w13a_or_b" => "2023 | 研究表明：玩家喜欢作出选择——越多越好！",
            "24w14potato" => "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！",
            "25w14craftmine" => "2025 | 你可以合成任何东西——包括合成你的世界！",
            "26w14a" => "2026 | 为什么需要物品栏？让方块们跟着你走吧！",
            _ => string.Empty
        };
    }

    private static string FormatMinecraftCatalogVersion(string versionId)
    {
        return versionId.Replace('_', ' ');
    }

    private static string BuildMinecraftCatalogTimestampSummary(string? releaseTime, string normalizedId, string formattedTitle)
    {
        var published = FormatReleaseTime(releaseTime);
        return formattedTitle == normalizedId
            ? published
            : $"{published} | {normalizedId}";
    }

    private static string BuildMinecraftCatalogLoreSummary(string lore, string normalizedId, string formattedTitle)
    {
        return formattedTitle == normalizedId
            ? lore
            : $"{lore} | {normalizedId}";
    }

    private static string FormatReleaseTime(string? rawValue)
    {
        return DateTimeOffset.TryParse(rawValue, out var value)
            ? value.LocalDateTime.ToString("yyyy/MM/dd HH:mm")
            : "未记录发布时间";
    }

    private static string NormalizeVersionType(string rawValue)
    {
        return rawValue switch
        {
            "release" => "正式版",
            "beta" => "测试版",
            "alpha" => "预览版",
            _ => rawValue
        };
    }

    private static IReadOnlyList<FrontendInstallChoice> SortInstallChoicesDescending(
        IEnumerable<FrontendInstallChoice> choices,
        int? maxCount = null)
    {
        var ordered = choices.ToList();
        ordered.Sort(CompareInstallChoicesDescending);
        if (maxCount is int limit && ordered.Count > limit)
        {
            ordered.RemoveRange(limit, ordered.Count - limit);
        }

        return ordered;
    }

    private static IReadOnlyList<FrontendInstallChoice> SortInstallChoicesByVersionDescending(
        IEnumerable<FrontendInstallChoice> choices,
        int? maxCount = null)
    {
        var ordered = choices.ToList();
        ordered.Sort((left, right) =>
        {
            var versionCompare = CompareLooseVersions(right.Version, left.Version);
            if (versionCompare != 0)
            {
                return versionCompare;
            }

            return CompareInstallChoicesDescending(left, right);
        });

        if (maxCount is int limit && ordered.Count > limit)
        {
            ordered.RemoveRange(limit, ordered.Count - limit);
        }

        return ordered;
    }

    private static int CompareInstallChoicesDescending(FrontendInstallChoice? left, FrontendInstallChoice? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var leftReleaseTime = GetInstallChoiceReleaseTime(left);
        var rightReleaseTime = GetInstallChoiceReleaseTime(right);
        if (leftReleaseTime is not null && rightReleaseTime is not null)
        {
            var releaseCompare = rightReleaseTime.Value.CompareTo(leftReleaseTime.Value);
            if (releaseCompare != 0)
            {
                return releaseCompare;
            }
        }

        var versionCompare = CompareLooseVersions(right.Version, left.Version);
        if (versionCompare != 0)
        {
            return versionCompare;
        }

        return string.Compare(right.Title, left.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? GetInstallChoiceReleaseTime(FrontendInstallChoice choice)
    {
        var rawValue = choice.Metadata?["releaseTime"]?.GetValue<string>();
        return DateTimeOffset.TryParse(rawValue, out var parsed) ? parsed : null;
    }

    private static int CompareLooseVersions(string? left, string? right)
    {
        var (leftCore, leftSuffix) = SplitVersionCoreAndSuffix(left);
        var (rightCore, rightSuffix) = SplitVersionCoreAndSuffix(right);

        var coreCompare = CompareVersionNumberSequences(
            ExtractVersionNumbers(leftCore),
            ExtractVersionNumbers(rightCore));
        if (coreCompare != 0)
        {
            return coreCompare;
        }

        var stabilityCompare = GetVersionStabilityRank(leftSuffix).CompareTo(GetVersionStabilityRank(rightSuffix));
        if (stabilityCompare != 0)
        {
            return stabilityCompare;
        }

        var suffixCompare = CompareVersionNumberSequences(
            ExtractVersionNumbers(leftSuffix),
            ExtractVersionNumbers(rightSuffix));
        if (suffixCompare != 0)
        {
            return suffixCompare;
        }

        return string.Compare(
            NormalizeVersionText(leftSuffix),
            NormalizeVersionText(rightSuffix),
            StringComparison.OrdinalIgnoreCase);
    }

    private static (string Core, string Suffix) SplitVersionCoreAndSuffix(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (string.Empty, string.Empty);
        }

        var match = Regex.Match(
            rawValue,
            @"alpha|beta|preview|pre|rc|snapshot|nightly|dev|experimental|test",
            RegexOptions.IgnoreCase);
        return !match.Success
            ? (rawValue, string.Empty)
            : (rawValue[..match.Index], rawValue[match.Index..]);
    }

    private static IReadOnlyList<long> ExtractVersionNumbers(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return Regex.Matches(rawValue, @"\d+")
            .Select(match => long.TryParse(match.Value, out var value) ? value : 0L)
            .ToArray();
    }

    private static int CompareVersionNumberSequences(
        IReadOnlyList<long> left,
        IReadOnlyList<long> right)
    {
        var maxLength = Math.Max(left.Count, right.Count);
        for (var index = 0; index < maxLength; index++)
        {
            var leftValue = index < left.Count ? left[index] : 0L;
            var rightValue = index < right.Count ? right[index] : 0L;
            var compare = leftValue.CompareTo(rightValue);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }

    private static int GetVersionStabilityRank(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return 5;
        }

        var normalized = suffix.ToLowerInvariant();
        if (normalized.Contains("rc", StringComparison.Ordinal))
        {
            return 4;
        }

        if (normalized.Contains("preview", StringComparison.Ordinal)
            || normalized.Contains("pre", StringComparison.Ordinal))
        {
            return 3;
        }

        if (normalized.Contains("beta", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.Contains("alpha", StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }

    private static string NormalizeVersionText(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : rawValue.Trim().Replace('_', '-');
    }

    private static bool IsModernOptiFineVersion(FrontendInstallChoice choice)
    {
        var minecraftVersion = choice.Metadata?["minecraftVersion"]?.GetValue<string>() ?? string.Empty;
        if (minecraftVersion.Contains('w', StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
               && int.TryParse(parts[1], out var minor)
               && minor >= 14;
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

internal enum FrontendInstallChoiceKind
{
    Minecraft,
    Forge,
    NeoForge,
    Cleanroom,
    FabricLoader,
    LegacyFabricLoader,
    QuiltLoader,
    LabyMod,
    OptiFine,
    LiteLoader,
    ModFile
}

internal sealed record FrontendInstallChoice(
    string Id,
    string Title,
    string Summary,
    string Version,
    FrontendInstallChoiceKind Kind,
    string? ManifestUrl = null,
    string? DownloadUrl = null,
    string? FileName = null,
    JsonObject? Metadata = null);

internal sealed record FrontendInstallApplyRequest(
    string LauncherDirectory,
    string TargetInstanceName,
    FrontendInstallChoice MinecraftChoice,
    FrontendInstallChoice? PrimaryLoaderChoice,
    FrontendInstallChoice? LiteLoaderChoice,
    FrontendInstallChoice? OptiFineChoice,
    FrontendInstallChoice? FabricApiChoice,
    FrontendInstallChoice? LegacyFabricApiChoice,
    FrontendInstallChoice? QslChoice,
    FrontendInstallChoice? OptiFabricChoice,
    bool UseInstanceIsolation,
    bool RunRepair,
    bool ForceCoreRefresh,
    bool PreserveExistingManagedModFiles = false);

internal sealed record FrontendInstallApplyResult(
    string TargetDirectory,
    string ManifestPath,
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);

internal enum FrontendInstallApplyPhase
{
    PrepareManifest,
    DownloadSupportFiles,
    Finalize
}
