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
    private const string MojangVersionManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    private static readonly HttpClient HttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(100));

    private static readonly JsonSerializerOptions JsonNodeOptions = new()
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);


    public static IReadOnlyList<FrontendInstallChoice> GetMinecraftChoices(
        string? preferredVersion,
        int downloadSourceIndex = (int)FrontendDownloadSourcePreference.OfficialPreferred,
        II18nService? i18n = null)
    {
        var downloadProvider = FrontendDownloadProvider.FromPreference(downloadSourceIndex);
        var root = ReadJsonObject(MojangVersionManifestUrl, downloadProvider);
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
                        ? Text(i18n, "download.install.choices.summaries.release", "Release")
                        : Text(
                            i18n,
                            "download.install.choices.summaries.release_with_time",
                            "Release • {published_at}",
                            ("published_at", FormatReleaseTime(i18n, releaseTime))),
                    Version: version,
                    Kind: FrontendInstallChoiceKind.Minecraft,
                    ManifestUrl: node["url"]?.GetValue<string>());
            })
            .Where(choice => !string.IsNullOrWhiteSpace(choice.Version) && !string.IsNullOrWhiteSpace(choice.ManifestUrl))
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
                        Summary: Text(
                            i18n,
                            "download.install.choices.summaries.current_instance_with_time",
                            "{published_at}",
                            ("published_at", FormatReleaseTime(i18n, extra["releaseTime"]?.GetValue<string>()))),
                        Version: preferredVersion,
                        Kind: FrontendInstallChoiceKind.Minecraft,
                        ManifestUrl: extra["url"]?.GetValue<string>()));
            }
        }

        return choices;
    }


    public static IReadOnlyList<FrontendInstallChoice> GetMinecraftCatalogChoices(
        string? preferredVersion,
        int downloadSourceIndex = (int)FrontendDownloadSourcePreference.OfficialPreferred,
        II18nService? i18n = null)
    {
        var downloadProvider = FrontendDownloadProvider.FromPreference(downloadSourceIndex);
        var root = ReadJsonObject(MojangVersionManifestUrl, downloadProvider);
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
                var lore = ResolveMinecraftCatalogLore(i18n, rawId, group, releaseTime);
                var formattedTitle = FormatMinecraftCatalogVersion(normalizedId);
                var summary = string.IsNullOrWhiteSpace(lore)
                    ? BuildMinecraftCatalogTimestampSummary(i18n, releaseTime, normalizedId, formattedTitle)
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
                        Summary: Text(
                            i18n,
                            "download.install.choices.summaries.current_instance_with_time",
                            "{published_at}",
                            ("published_at", FormatReleaseTime(i18n, releaseTime))),
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
        string minecraftVersion,
        int downloadSourceIndex = (int)FrontendDownloadSourcePreference.OfficialPreferred,
        II18nService? i18n = null)
    {
        var downloadProvider = FrontendDownloadProvider.FromPreference(downloadSourceIndex);
        try
        {
            return optionTitle switch
            {
                "Forge" => GetForgeChoices(minecraftVersion, i18n),
                "NeoForge" => GetNeoForgeChoices(minecraftVersion, downloadProvider, i18n),
                "Cleanroom" => GetCleanroomChoices(minecraftVersion, i18n),
                "Fabric" => GetFabricLoaderChoices(minecraftVersion, downloadProvider, i18n),
                "Legacy Fabric" => GetLegacyFabricLoaderChoices(minecraftVersion, downloadProvider, i18n),
                "Quilt" => GetQuiltLoaderChoices(minecraftVersion, downloadProvider, i18n),
                "LabyMod" => GetLabyModChoices(minecraftVersion, downloadProvider, i18n),
                "OptiFine" => GetOptiFineChoices(minecraftVersion, i18n),
                "LiteLoader" => GetLiteLoaderChoices(minecraftVersion, downloadProvider, i18n),
                "Fabric API" => GetModrinthFileChoices("fabric-api", minecraftVersion, ["fabric"], downloadProvider: downloadProvider, i18n: i18n),
                "Legacy Fabric API" => GetModrinthFileChoices("9CJED7xi", minecraftVersion, null, downloadProvider: downloadProvider, i18n: i18n),
                "QFAPI / QSL" => GetModrinthFileChoices("qvIfYCYJ", minecraftVersion, ["quilt"], allowVersionFallback: true, downloadProvider: downloadProvider, i18n: i18n),
                "OptiFabric" => GetOptiFabricChoices(minecraftVersion, downloadProvider, i18n),
                _ => []
            };
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (InvalidOperationException ex) when (ex.InnerException is HttpRequestException)
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
        Action<FrontendInstanceRepairProgressSnapshot>? onRepairProgress = null,
        FrontendDownloadTransferOptions? downloadOptions = null,
        II18nService? i18n = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancelToken.ThrowIfCancellationRequested();
        var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        var downloadProvider = FrontendDownloadProvider.FromPreference(request.DownloadSourceIndex);

        var launcherDirectory = request.LauncherDirectory;
        var targetDirectory = Path.Combine(launcherDirectory, "versions", request.TargetInstanceName);
        Directory.CreateDirectory(targetDirectory);
        void ReportPrepare(string message) => onPhaseChanged?.Invoke(FrontendInstallApplyPhase.PrepareManifest, message);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.PrepareManifest, Text(i18n, "download.install.workflow.tasks.preparing_manifest", "Writing the install manifest and preparing the environment..."));
        var manifestNode = BuildTargetManifest(request, downloadProvider, ReportPrepare, speedLimiter, i18n, cancelToken);
        cancelToken.ThrowIfCancellationRequested();
        ReportPrepare(Text(i18n, "download.install.workflow.tasks.cleaning_missing_local_libraries", "Cleaning missing local dependency references..."));
        RemoveMissingLocalOnlyLibraries(manifestNode, launcherDirectory);
        var manifestPath = Path.Combine(targetDirectory, $"{request.TargetInstanceName}.json");
        ReportPrepare(
            Text(
                i18n,
                "download.install.workflow.tasks.writing_manifest_file",
                "Writing install manifest file {file_name}...",
                ("file_name", Path.GetFileName(manifestPath))));
        File.WriteAllText(manifestPath, manifestNode.ToJsonString(JsonNodeOptions), Utf8NoBom);

        ReportPrepare(Text(i18n, "download.install.workflow.tasks.writing_instance_config", "Writing instance configuration..."));
        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(targetDirectory);
        instanceConfig.Set("VersionVanillaName", request.MinecraftChoice.Version);
        instanceConfig.Set("VersionArgumentIndieV2", request.UseInstanceIsolation);
        instanceConfig.Sync();

        var modsDirectory = request.UseInstanceIsolation
            ? Path.Combine(targetDirectory, "mods")
            : Path.Combine(launcherDirectory, "mods");
        ReportPrepare(Text(i18n, "download.install.workflow.tasks.creating_instance_directories", "Creating instance directory structure..."));
        Directory.CreateDirectory(modsDirectory);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.DownloadSupportFiles, Text(i18n, "download.install.workflow.tasks.preparing_managed_dependencies", "Preparing managed dependency files..."));
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

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.DownloadSupportFiles, Text(i18n, "download.install.workflow.tasks.repairing_game_files", "Completing game core files and support libraries..."));
        var repairResult = request.RunRepair
            ? FrontendInstanceRepairService.Repair(new FrontendInstanceRepairRequest(
                launcherDirectory,
                targetDirectory,
                request.TargetInstanceName,
                request.ForceCoreRefresh),
                onRepairProgress,
                downloadProvider,
                downloadOptions,
                cancelToken)
            : new FrontendInstanceRepairResult([], []);

        onPhaseChanged?.Invoke(FrontendInstallApplyPhase.Finalize, Text(i18n, "download.install.workflow.tasks.finalizing_installation", "Organizing the instance directory and finishing installation..."));
        EnsureResourceFolders(targetDirectory, request.UseInstanceIsolation ? targetDirectory : launcherDirectory);

        return new FrontendInstallApplyResult(
            targetDirectory,
            manifestPath,
            repairResult.DownloadedFiles,
            repairResult.ReusedFiles);
    }

}
