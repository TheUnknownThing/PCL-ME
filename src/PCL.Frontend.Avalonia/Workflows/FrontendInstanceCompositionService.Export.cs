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
    private static FrontendInstanceExportState BuildExportState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var resourcePackEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.ResourcePacks), ArchiveExtensions, allowDirectories: true, recursive: false);
        var shaderEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Shaders), ArchiveExtensions, allowDirectories: true, recursive: false);
        var schematicEntries = BuildFolderEntries(ResolveResourceDirectory(selection, ResourceKind.Schematics), SchematicExtensions, allowDirectories: false, recursive: true);
        var disabledModEntries = BuildResourceEntries(selection, ResourceKind.DisabledMods, i18n);
        var saveEntries = BuildExportWorldOptions(selection);
        var screenshotEntries = BuildScreenshotEntries(selection, i18n);
        var replayEntries = BuildFolderEntries(Path.Combine(selection.IndieDirectory, "replay_recordings"), [".mcpr"], allowDirectories: false, recursive: false);
        var hasServers = File.Exists(Path.Combine(selection.IndieDirectory, "servers.dat"));
        var hasLauncherContent = Directory.Exists(Path.Combine(selection.InstanceDirectory, "PCL"));

        var groups = new List<FrontendInstanceExportOptionGroup>
        {
            new(
                "game",
                Text(i18n, "instance.export.groups.game", "Game"),
                string.Empty,
                true,
                [
                    CreateExportOption("game_settings", Text(i18n, "instance.export.items.game_settings", "Game settings"), File.Exists(Path.Combine(selection.IndieDirectory, "options.txt")) ? Text(i18n, "instance.export.detected.options", "Detected options.txt") : Text(i18n, "instance.export.detected.config_missing", "Configuration file not found"), File.Exists(Path.Combine(selection.IndieDirectory, "options.txt"))),
                    CreateExportOption("game_personal", Text(i18n, "instance.export.items.game_personal", "Personal game data"), File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt")) ? Text(i18n, "instance.export.detected.optifine_settings", "Detected OptiFine settings") : Text(i18n, "instance.export.detected.personal_missing", "Personal settings not found"), File.Exists(Path.Combine(selection.IndieDirectory, "optionsof.txt"))),
                    CreateExportOption(
                        "optifine_settings",
                        Text(i18n, "instance.export.items.optifine_settings", "OptiFine settings"),
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion) ? Text(i18n, "instance.export.detected.optifine_present", "This instance includes OptiFine") : Text(i18n, "instance.export.detected.optifine_missing", "This instance does not have OptiFine installed"),
                        !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion))
                ]),
            new(
                "mods",
                Text(i18n, "instance.export.groups.mods", "Mods"),
                Text(i18n, "instance.export.descriptions.mods", "Mods"),
                selection.IsModable,
                [
                    CreateExportOption("disabled_mods", Text(i18n, "instance.export.items.disabled_mods", "Disabled mods"), Text(i18n, "instance.export.count.items", "{count} items", ("count", disabledModEntries.Count)), disabledModEntries.Count > 0),
                    CreateExportOption("important_data", Text(i18n, "instance.export.items.important_data", "Important modpack data"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? Text(i18n, "instance.export.detected.config_folder", "Detected config folder") : Text(i18n, "instance.export.detected.config_folder_missing", "Config folder not found"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config"))),
                    CreateExportOption("mod_settings", Text(i18n, "instance.export.items.mod_settings", "Mod settings"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")) ? Text(i18n, "instance.export.detected.config_directory", "Detected configuration directory") : Text(i18n, "instance.export.detected.config_directory_missing", "Configuration directory not found"), Directory.Exists(Path.Combine(selection.IndieDirectory, "config")))
                ]),
            new("resource_packs", Text(i18n, "instance.export.groups.resource_packs", "Resource packs"), Text(i18n, "instance.export.descriptions.resource_packs", "Texture packs / resource packs"), resourcePackEntries.Count > 0, resourcePackEntries.Select(ToExportOption).ToArray()),
            new("shaders", Text(i18n, "instance.export.groups.shaders", "Shader packs"), string.Empty, shaderEntries.Count > 0, shaderEntries.Select(ToExportOption).ToArray()),
            new("screenshots", Text(i18n, "instance.export.groups.screenshots", "Screenshots"), string.Empty, screenshotEntries.Count > 0, []),
            new("schematics", Text(i18n, "instance.export.groups.schematics", "Schematics"), Text(i18n, "instance.export.descriptions.schematics", "schematics folder"), schematicEntries.Count > 0, schematicEntries.Select(ToExportOption).ToArray()),
            new("replays", Text(i18n, "instance.export.groups.replays", "Replays"), Text(i18n, "instance.export.descriptions.replays", "Replay Mod recordings"), replayEntries.Count > 0, replayEntries.Select(ToExportOption).ToArray()),
            new("worlds", Text(i18n, "instance.export.groups.worlds", "World saves"), Text(i18n, "instance.export.descriptions.worlds", "Worlds / maps"), false, saveEntries),
            new("servers", Text(i18n, "instance.export.groups.servers", "Server list"), string.Empty, hasServers, []),
            new(
                "launcher",
                Text(i18n, "instance.export.groups.launcher", "PCL launcher"),
                Text(i18n, "instance.export.descriptions.launcher", "Bundle the cross-platform PCL launcher so players without it can install the modpack."),
                hasLauncherContent,
                [
                    CreateExportOption("launcher_personalization", Text(i18n, "instance.export.items.launcher_personalization", "PCL personalization content"), hasLauncherContent ? Text(i18n, "instance.export.detected.pcl_directory", "Detected instance PCL configuration directory") : Text(i18n, "instance.export.detected.pcl_directory_missing", "Instance PCL configuration directory not found"), hasLauncherContent)
                ])
        };

        return new FrontendInstanceExportState(
            selection.InstanceName,
            ReadVersionFallback(selection.InstanceDirectory),
            IncludeResources: false,
            ModrinthMode: false,
            HasOptiFine: !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            OptionGroups: groups);
    }

    private static FrontendInstanceExportState CreatePlaceholderExportState(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary)
    {
        return new FrontendInstanceExportState(
            selection.InstanceName,
            ReadVersionFallback(selection.InstanceDirectory),
            IncludeResources: false,
            ModrinthMode: false,
            HasOptiFine: !string.IsNullOrWhiteSpace(manifestSummary.OptiFineVersion),
            OptionGroups: []);
    }

    private static IReadOnlyList<FrontendInstanceExportOptionEntry> BuildExportWorldOptions(FrontendInstanceSelectionState selection)
    {
        var savesDirectory = Path.Combine(selection.IndieDirectory, "saves");
        if (!Directory.Exists(savesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(savesDirectory)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => new FrontendInstanceExportOptionEntry(
                directory.FullName,
                directory.Name,
                directory.LastWriteTime.ToString("yyyy/MM/dd HH:mm"),
                true))
            .ToArray();
    }

    private static FrontendInstanceExportOptionEntry ToExportOption(FrontendInstanceDirectoryEntry entry)
    {
        return new FrontendInstanceExportOptionEntry(entry.Path, entry.Title, entry.Summary, true);
    }

    private static FrontendInstanceExportOptionEntry CreateExportOption(string key, string title, string description, bool isChecked)
    {
        return new FrontendInstanceExportOptionEntry(key, title, description, isChecked);
    }

}
