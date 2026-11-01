using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.RegularExpressions;
using fNbt;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstanceCompositionService
{
    private static FrontendInstanceInstallState BuildInstallState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifestSummary.FabricVersion) && !manifestSummary.HasFabricApi)
        {
            hints.Add(Text(i18n, "instance.install.hints.fabric_api_required", "Fabric API is not installed, so most mods will not work."));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion) && !manifestSummary.HasQsl)
        {
            hints.Add(Text(i18n, "instance.install.hints.qsl_required", "QFAPI / QSL is not installed, so most mods will not work."));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion)
            && !string.IsNullOrWhiteSpace(manifestSummary.FabricVersion)
            && !manifestSummary.HasOptiFabric)
        {
            hints.Add(Text(i18n, "instance.install.hints.optifabric_required", "OptiFabric is not installed, so OptiFine will not work."));
        }

        return new FrontendInstanceInstallState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary, i18n),
            Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", selection.VanillaVersion)),
            DetermineInstallIconName(manifestSummary),
            hints,
            [
                new FrontendInstanceInstallOption("Forge", DisplayVersionState(manifestSummary.ForgeVersion, i18n), "Anvil.png"),
                new FrontendInstanceInstallOption("Cleanroom", DisplayVersionState(manifestSummary.CleanroomVersion, i18n), "Cleanroom.png"),
                new FrontendInstanceInstallOption("NeoForge", DisplayVersionState(manifestSummary.NeoForgeVersion, i18n), "NeoForge.png"),
                new FrontendInstanceInstallOption("Fabric", DisplayVersionState(manifestSummary.FabricVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("Legacy Fabric", DisplayVersionState(manifestSummary.LegacyFabricVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("Fabric API", DisplayInstalledState(manifestSummary.HasFabricApi, manifestSummary.FabricApiVersion, i18n), "Fabric.png"),
                new FrontendInstanceInstallOption("QFAPI / QSL", DisplayInstalledState(manifestSummary.HasQsl, manifestSummary.QslVersion, i18n), "Quilt.png"),
                new FrontendInstanceInstallOption("Quilt", DisplayVersionState(manifestSummary.QuiltVersion, i18n), "Quilt.png"),
                new FrontendInstanceInstallOption("LabyMod", DisplayVersionState(manifestSummary.LabyModVersion, i18n), "LabyMod.png"),
                new FrontendInstanceInstallOption("OptiFine", DisplayVersionState(manifestSummary.OptiFineVersion, i18n), "GrassPath.png"),
                new FrontendInstanceInstallOption("OptiFabric", DisplayInstalledState(manifestSummary.HasOptiFabric, manifestSummary.OptiFabricVersion, i18n), "OptiFabric.png"),
                new FrontendInstanceInstallOption("LiteLoader", DisplayInstalledState(manifestSummary.HasLiteLoader, manifestSummary.LiteLoaderVersion, i18n), "Egg.png")
            ]);
    }

    private static FrontendVersionManifestSummary MergeInstallAddonStates(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        bool includeMetadataFallback)
    {
        var modsDirectory = ResolveResourceDirectory(selection, ResourceKind.Mods);
        if (!Directory.Exists(modsDirectory))
        {
            return manifestSummary;
        }

        var hasFabricApi = manifestSummary.HasFabricApi;
        string? fabricApiVersion = null;
        var hasQsl = manifestSummary.HasQsl;
        string? qslVersion = null;
        var hasOptiFabric = manifestSummary.HasOptiFabric;
        string? optiFabricVersion = null;

        var modFiles = Directory.EnumerateFiles(modsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => EnabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                FileName = Path.GetFileNameWithoutExtension(path),
                NormalizedFileName = NormalizeManagedAddonIdentity(Path.GetFileNameWithoutExtension(path))
            })
            .ToArray();

        foreach (var file in modFiles)
        {
            if (!hasFabricApi && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "fabricapi"))
            {
                hasFabricApi = true;
            }

            if (!hasQsl && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "quiltedfabricapi", "qsl"))
            {
                hasQsl = true;
            }

            if (!hasOptiFabric && LooksLikeManagedAddonFromFileName(file.NormalizedFileName, "optifabric"))
            {
                hasOptiFabric = true;
            }
        }

        if (includeMetadataFallback && (!hasFabricApi || !hasQsl || !hasOptiFabric))
        {
            foreach (var file in modFiles)
            {
                var metadata = TryReadLocalModMetadata(file.Path);

                if (!hasFabricApi && IsManagedAddonMod(metadata, file.FileName, "fabricapi"))
                {
                    hasFabricApi = true;
                    fabricApiVersion = NormalizeInlineText(metadata?.Version);
                    continue;
                }

                if (!hasQsl && IsManagedAddonMod(metadata, file.FileName, "quiltedfabricapi", "qsl"))
                {
                    hasQsl = true;
                    qslVersion = NormalizeInlineText(metadata?.Version);
                    continue;
                }

                if (!hasOptiFabric && IsManagedAddonMod(metadata, file.FileName, "optifabric"))
                {
                    hasOptiFabric = true;
                    optiFabricVersion = NormalizeInlineText(metadata?.Version);
                }
            }
        }

        return manifestSummary with
        {
            HasFabricApi = hasFabricApi,
            FabricApiVersion = FirstNonEmpty(manifestSummary.FabricApiVersion, fabricApiVersion),
            HasQsl = hasQsl,
            QslVersion = FirstNonEmpty(manifestSummary.QslVersion, qslVersion),
            HasOptiFabric = hasOptiFabric,
            OptiFabricVersion = FirstNonEmpty(manifestSummary.OptiFabricVersion, optiFabricVersion)
        };
    }

    private static bool IsManagedAddonMod(
        RecognizedModMetadata? metadata,
        string fileNameWithoutExtension,
        params string[] identifiers)
    {
        return MatchesManagedAddonIdentity(metadata?.Identity, identifiers)
               || MatchesManagedAddonIdentity(metadata?.Title, identifiers)
               || identifiers.Any(identifier => NormalizeManagedAddonIdentity(fileNameWithoutExtension)
                   .StartsWith(NormalizeManagedAddonIdentity(identifier), StringComparison.Ordinal));
    }

    private static bool LooksLikeManagedAddonFromFileName(string normalizedFileName, params string[] identifiers)
    {
        return identifiers.Any(identifier => normalizedFileName.StartsWith(
            NormalizeManagedAddonIdentity(identifier),
            StringComparison.Ordinal));
    }

    private static bool MatchesManagedAddonIdentity(string? value, IEnumerable<string> identifiers)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeManagedAddonIdentity(value);
        return identifiers.Any(identifier => string.Equals(
            normalized,
            NormalizeManagedAddonIdentity(identifier),
            StringComparison.Ordinal));
    }

    private static string NormalizeManagedAddonIdentity(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string DetermineInstallIconName(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary switch
        {
            { NeoForgeVersion: not null and not "" } => "NeoForge.png",
            { CleanroomVersion: not null and not "" } => "Cleanroom.png",
            { FabricVersion: not null and not "" } => "Fabric.png",
            { QuiltVersion: not null and not "" } => "Quilt.png",
            { ForgeVersion: not null and not "" } => "Anvil.png",
            { OptiFineVersion: not null and not "" } => "GrassPath.png",
            _ => "Grass.png"
        };
    }

    private static string DetermineInstallIconNameFromExtension(string family, FrontendInstanceSelectionState selection)
    {
        return family switch
        {
            "mods" => "Fabric.png",
            _ => "Grass.png"
        };
    }

}
