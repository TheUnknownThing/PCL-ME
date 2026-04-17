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
    private static FrontendInstanceOverviewState BuildOverviewState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        YamlFileProvider instanceConfig,
        FrontendInstanceSetupState setupState,
        II18nService? i18n)
    {
        var isStarred = ReadValue(instanceConfig, "IsStar", false);
        var categoryIndex = MapInstanceCategoryIndex(ReadValue(instanceConfig, "DisplayType", 0));
        var customInfo = ReadValue(
            instanceConfig,
            "VersionArgumentInfo",
            ReadValue(instanceConfig, "CustomInfo", string.Empty));
        var launchCount = ReadValue(instanceConfig, "VersionLaunchCount", 0);
        var modpackVersion = ReadValue(instanceConfig, "VersionModpackVersion", string.Empty);
        var infoEntries = new List<FrontendInstanceInfoEntry>();

        infoEntries.Add(new FrontendInstanceInfoEntry(
            Text(i18n, "instance.overview.info.launch_count", "Launch count"),
            launchCount == 0
                ? Text(i18n, "instance.overview.info.launch_count_never", "Never launched")
                : Text(i18n, "instance.overview.info.launch_count_value", "Launched {count} times", ("count", launchCount))));

        if (!string.IsNullOrWhiteSpace(modpackVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry(Text(i18n, "instance.overview.info.modpack_version", "Modpack version"), modpackVersion));
        }

        infoEntries.Add(new FrontendInstanceInfoEntry("Minecraft", selection.VanillaVersion));

        if (manifestSummary.HasForge && !string.IsNullOrWhiteSpace(manifestSummary.ForgeVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Forge", manifestSummary.ForgeVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.NeoForgeVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("NeoForge", manifestSummary.NeoForgeVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.CleanroomVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Cleanroom", manifestSummary.CleanroomVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.FabricVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Fabric", manifestSummary.FabricVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Quilt", manifestSummary.QuiltVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("OptiFine", manifestSummary.OptiFineVersion));
        }

        if (manifestSummary.HasLiteLoader)
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("LiteLoader", Text(i18n, "instance.common.installed", "Installed")));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.LegacyFabricVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("Legacy Fabric", manifestSummary.LegacyFabricVersion));
        }

        if (!string.IsNullOrWhiteSpace(manifestSummary.LabyModVersion))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry("LabyMod", manifestSummary.LabyModVersion));
        }

        if (!string.IsNullOrWhiteSpace(customInfo))
        {
            infoEntries.Add(new FrontendInstanceInfoEntry(Text(i18n, "instance.overview.info.description", "Description"), customInfo));
        }

        var tags = new List<string>();
        AddIfNotEmpty(tags, DeterminePrimaryLoaderLabel(manifestSummary));
        if (selection.IsModable)
        {
            tags.Add(Text(i18n, "instance.overview.tags.mod_supported", "Supports Mods"));
        }

        if (isStarred)
        {
            tags.Add(Text(i18n, "instance.overview.tags.favorited", "Favorited"));
        }
        else
        {
            AddIfNotEmpty(tags, DetermineCategoryLabel(categoryIndex, i18n));
        }

        var iconPath = ResolveOverviewIconPath(selection, manifestSummary, instanceConfig);
        return new FrontendInstanceOverviewState(
            selection.InstanceName,
            BuildInstanceSubtitle(selection, manifestSummary, i18n),
            iconPath,
            ResolveOverviewIconIndex(instanceConfig, manifestSummary),
            categoryIndex,
            isStarred,
            tags,
            infoEntries);
    }

    private static string? ResolveOverviewIconPath(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        YamlFileProvider instanceConfig)
    {
        var isCustomLogo = ReadValue(instanceConfig, "LogoCustom", false);
        var rawLogoPath = ReadValue(instanceConfig, "Logo", string.Empty);
        if (isCustomLogo)
        {
            var customPath = Path.Combine(selection.InstanceDirectory, "PCL", "Logo.png");
            if (File.Exists(customPath))
            {
                return customPath;
            }
        }

        var mappedLogo = MapStoredLogoPath(rawLogoPath);
        if (mappedLogo is not null)
        {
            return mappedLogo;
        }

        return Path.Combine(
            LauncherRootDirectory,
            "Images",
            "Blocks",
            DetermineInstallIconName(manifestSummary));
    }

    private static int ResolveOverviewIconIndex(YamlFileProvider instanceConfig, FrontendVersionManifestSummary manifestSummary)
    {
        var isCustomLogo = ReadValue(instanceConfig, "LogoCustom", false);
        if (isCustomLogo)
        {
            return 0;
        }

        var rawLogoPath = ReadValue(instanceConfig, "Logo", string.Empty);
        return rawLogoPath switch
        {
            var path when path.Contains("CobbleStone", StringComparison.OrdinalIgnoreCase) => 1,
            var path when path.Contains("CommandBlock", StringComparison.OrdinalIgnoreCase) => 2,
            var path when path.Contains("GoldBlock", StringComparison.OrdinalIgnoreCase) => 3,
            var path when path.Contains("Grass.png", StringComparison.OrdinalIgnoreCase) => 4,
            var path when path.Contains("GrassPath", StringComparison.OrdinalIgnoreCase) => 5,
            var path when path.Contains("Anvil", StringComparison.OrdinalIgnoreCase) => 6,
            var path when path.Contains("RedstoneBlock", StringComparison.OrdinalIgnoreCase) => 7,
            var path when path.Contains("RedstoneLampOn", StringComparison.OrdinalIgnoreCase) => 8,
            var path when path.Contains("RedstoneLampOff", StringComparison.OrdinalIgnoreCase) => 9,
            var path when path.Contains("Egg", StringComparison.OrdinalIgnoreCase) => 10,
            var path when path.Contains("Fabric", StringComparison.OrdinalIgnoreCase) => 11,
            var path when path.Contains("Quilt", StringComparison.OrdinalIgnoreCase) => 12,
            var path when path.Contains("NeoForge", StringComparison.OrdinalIgnoreCase) => 13,
            var path when path.Contains("Cleanroom", StringComparison.OrdinalIgnoreCase) => 14,
            _ => DetermineInstallIconName(manifestSummary) switch
            {
                "Anvil.png" => 6,
                "Fabric.png" => 11,
                "Quilt.png" => 12,
                "NeoForge.png" => 13,
                "Cleanroom.png" => 14,
                "GrassPath.png" => 5,
                "Egg.png" => 10,
                _ => 4
            }
        };
    }

    private static string? MapStoredLogoPath(string rawLogoPath)
    {
        if (string.IsNullOrWhiteSpace(rawLogoPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(rawLogoPath.Replace("pack://application:,,,/images/Blocks/", string.Empty, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.Combine(LauncherRootDirectory, "Images", "Blocks", fileName);
    }

    private static int MapInstanceCategoryIndex(int storedValue)
    {
        return storedValue switch
        {
            <= 0 => 0,
            1 => 1,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            _ => 0
        };
    }

    private static string DetermineCategoryLabel(int categoryIndex, II18nService? i18n)
    {
        return categoryIndex switch
        {
            1 => Text(i18n, "instance.overview.categories.hidden", "Hidden instance"),
            2 => Text(i18n, "instance.overview.categories.modable", "Mod-capable"),
            3 => Text(i18n, "instance.overview.categories.regular", "Regular instance"),
            4 => Text(i18n, "instance.overview.categories.rare", "Rarely used instance"),
            5 => Text(i18n, "instance.overview.categories.april_fools", "April Fools version"),
            _ => Text(i18n, "instance.overview.categories.auto", "Auto")
        };
    }

}
