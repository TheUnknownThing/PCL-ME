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
    private static IReadOnlyList<FrontendInstanceDirectoryEntry> BuildWorldEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var savesDirectory = Path.Combine(selection.IndieDirectory, "saves");
        if (!Directory.Exists(savesDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(savesDirectory)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(directory => directory.LastWriteTimeUtc)
            .Select(directory => new FrontendInstanceDirectoryEntry(
                directory.Name,
                Text(i18n, "instance.content.world.summary", "Created: {created_at} • Modified: {modified_at}", ("created_at", directory.CreationTime.ToString("yyyy/MM/dd")), ("modified_at", directory.LastWriteTime.ToString("yyyy/MM/dd"))),
                directory.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceScreenshotEntry> BuildScreenshotEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var screenshotDirectory = Path.Combine(selection.IndieDirectory, "screenshots");
        if (!Directory.Exists(screenshotDirectory))
        {
            return [];
        }

        return ScreenshotPatterns
            .SelectMany(pattern => Directory.EnumerateFiles(screenshotDirectory, pattern, SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(file => new FrontendInstanceScreenshotEntry(
                file.Name,
                Text(i18n, "instance.content.screenshot.summary", "{created_at} • {file_size}", ("created_at", file.CreationTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))),
                file.FullName))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceServerEntry> BuildServerEntries(FrontendInstanceSelectionState selection, II18nService? i18n)
    {
        var serversPath = Path.Combine(selection.IndieDirectory, "servers.dat");
        if (!File.Exists(serversPath))
        {
            return [];
        }

        try
        {
            var file = new NbtFile();
            using var stream = File.OpenRead(serversPath);
            file.LoadFromStream(stream, NbtCompression.AutoDetect);
            var list = file.RootTag.Get<NbtList>("servers");
            if (list is null)
            {
                return [];
            }

            return list
                .OfType<NbtCompound>()
                .Select(server => new FrontendInstanceServerEntry(
                    Title: server.Get<NbtString>("name")?.Value ?? Text(i18n, "instance.content.server.dialogs.edit.name_default", "Minecraft Server"),
                    Address: server.Get<NbtString>("ip")?.Value ?? string.Empty,
                    Status: Text(i18n, "instance.content.server.status.saved", "Saved server")))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildResourceEntries(
        FrontendInstanceSelectionState selection,
        ResourceKind kind,
        II18nService? i18n)
    {
        return kind switch
        {
            ResourceKind.Mods => BuildModResourceEntries(
                ResolveResourceDirectory(selection, kind),
                fileFilter: path => EnabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                defaultIconName: DetermineInstallIconNameFromExtension("mods", selection),
                isEnabled: true,
                i18n: i18n),
            ResourceKind.DisabledMods => BuildModResourceEntries(
                ResolveResourceDirectory(selection, ResourceKind.Mods),
                fileFilter: path => DisabledModExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                defaultIconName: "RedstoneBlock.png",
                isEnabled: false,
                i18n: i18n),
            ResourceKind.ResourcePacks => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), Text(i18n, "instance.content.resource.kind.resource_pack", "Resource pack"), "Grass.png", i18n),
            ResourceKind.Shaders => BuildFolderAndArchiveEntries(ResolveResourceDirectory(selection, kind), Text(i18n, "instance.content.resource.kind.shader", "Shader pack"), "RedstoneLampOn.png", i18n),
            ResourceKind.Schematics => BuildFileResourceEntries(
                ResolveResourceDirectory(selection, kind),
                recursive: true,
                fileFilter: path => SchematicExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase),
                metaPrefix: Text(i18n, "instance.content.resource.kind.schematic_file", "Schematic file"),
                defaultIconName: "CommandBlock.png",
                i18n: i18n),
            _ => []
        };
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildModResourceEntries(
        string directory,
        Func<string, bool> fileFilter,
        string defaultIconName,
        bool isEnabled,
        II18nService? i18n)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(fileFilter)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => BuildModResourceEntry(directory, file, defaultIconName, isEnabled, i18n))
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFileResourceEntries(
        string directory,
        bool recursive,
        Func<string, bool> fileFilter,
        string metaPrefix,
        string defaultIconName,
        bool isEnabled = true,
        II18nService? i18n = null)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(directory, "*", option)
            .Where(fileFilter)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new FrontendInstanceResourceEntry(
                Title: Path.GetFileNameWithoutExtension(file.Name),
                Summary: Text(i18n, "instance.content.resource.summary.file", "{parent_directory} • {modified_at}", ("parent_directory", GetRelativeParent(directory, file.FullName, i18n)), ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm"))),
                Meta: $"{metaPrefix} • {file.Extension.TrimStart('.').ToUpperInvariant()}",
                Path: file.FullName,
                IconName: defaultIconName,
                IsEnabled: isEnabled))
            .ToArray();
    }

    private static FrontendInstanceResourceEntry BuildModResourceEntry(
        string directory,
        FileInfo file,
        string defaultIconName,
        bool isEnabled,
        II18nService? i18n)
    {
        var metadata = TryReadLocalModMetadata(file.FullName);
        var title = !string.IsNullOrWhiteSpace(metadata?.Title)
            ? metadata.Title!
            : GetFallbackModTitle(file.Name);
        var summary = BuildModSummary(file, metadata, i18n);
        var meta = BuildModMeta(file, metadata, i18n);
        var iconName = DetermineModIconName(metadata?.Loader, defaultIconName);

        return new FrontendInstanceResourceEntry(
            Title: title,
            Summary: summary,
            Meta: meta,
            Path: file.FullName,
            IconName: iconName,
            Identity: metadata?.Identity ?? string.Empty,
            IsEnabled: isEnabled,
            Description: NormalizeInlineText(metadata?.Description),
            Website: metadata?.Website ?? string.Empty,
            Authors: metadata?.Authors ?? string.Empty,
            Version: metadata?.Version ?? string.Empty,
            Loader: metadata?.Loader ?? string.Empty,
            IconBytes: metadata?.IconBytes);
    }

    private static IReadOnlyList<FrontendInstanceResourceEntry> BuildFolderAndArchiveEntries(
        string directory,
        string metaPrefix,
        string iconName,
        II18nService? i18n)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => ArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .Select(file => new FrontendInstanceResourceEntry(
                Title: Path.GetFileNameWithoutExtension(file.Name),
                Summary: Text(i18n, "instance.content.resource.summary.archive", "{modified_at} • {file_size}", ("modified_at", file.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("file_size", FormatFileSize(file.Length))),
                Meta: $"{metaPrefix} • {Text(i18n, "instance.content.resource.meta.archive", "Archive")}",
                Path: file.FullName,
                IconName: iconName));
        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .Select(folder => new FrontendInstanceResourceEntry(
                Title: folder.Name,
                Summary: Text(i18n, "instance.content.resource.summary.folder", "{modified_at} • {folder_kind}", ("modified_at", folder.LastWriteTime.ToString("yyyy/MM/dd HH:mm")), ("folder_kind", Text(i18n, "instance.content.resource.meta.folder", "Folder"))),
                Meta: $"{metaPrefix} • {Text(i18n, "instance.content.resource.meta.folder", "Folder")}",
                Path: folder.FullName,
                IconName: iconName));
        return files.Concat(folders)
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<FrontendInstanceDirectoryEntry> BuildFolderEntries(
        string directory,
        IReadOnlyCollection<string> extensions,
        bool allowDirectories,
        bool recursive)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(directory, "*", option)
            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new FrontendInstanceDirectoryEntry(
                file.Name,
                $"{file.LastWriteTime:yyyy/MM/dd HH:mm} • {FormatFileSize(file.Length)}",
                file.FullName));
        if (!allowDirectories)
        {
            return files.ToArray();
        }

        var folders = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new DirectoryInfo(path))
            .Where(folder => folder.EnumerateFileSystemInfos().Any())
            .OrderByDescending(folder => folder.LastWriteTimeUtc)
            .Select(folder => new FrontendInstanceDirectoryEntry(
                folder.Name,
                folder.LastWriteTime.ToString("yyyy/MM/dd HH:mm"),
                folder.FullName));
        return files.Concat(folders).ToArray();
    }

    private static string ReadVersionFallback(string instanceDirectory)
    {
        var iniPath = Path.Combine(instanceDirectory, "PCL", "config.v1.yml");
        return File.Exists(iniPath) ? "1.0.0" : "1.0.0";
    }

    private static bool IsModable(FrontendVersionManifestSummary manifestSummary)
    {
        return manifestSummary.HasForge
               || !string.IsNullOrWhiteSpace(manifestSummary.NeoForgeVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.CleanroomVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.FabricVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.LegacyFabricVersion)
               || !string.IsNullOrWhiteSpace(manifestSummary.QuiltVersion)
               || manifestSummary.HasLiteLoader
               || !string.IsNullOrWhiteSpace(manifestSummary.LabyModVersion);
    }

    private static string ResolveResourceDirectory(FrontendInstanceSelectionState selection, ResourceKind kind)
    {
        var folderName = kind switch
        {
            ResourceKind.Mods or ResourceKind.DisabledMods => "mods",
            ResourceKind.ResourcePacks => "resourcepacks",
            ResourceKind.Shaders => "shaderpacks",
            ResourceKind.Schematics => "schematics",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(folderName))
        {
            return selection.IndieDirectory;
        }

        if (!selection.HasLabyMod || string.IsNullOrWhiteSpace(selection.VanillaVersion))
        {
            return Path.Combine(selection.IndieDirectory, folderName);
        }

        return Path.Combine(selection.IndieDirectory, "labymod-neo", "fabric", selection.VanillaVersion, folderName);
    }

    private static string FormatFileSize(long length)
    {
        if (length >= 1024L * 1024L * 1024L)
        {
            return $"{length / 1024d / 1024d / 1024d:0.0} GB";
        }

        if (length >= 1024L * 1024L)
        {
            return $"{length / 1024d / 1024d:0.0} MB";
        }

        if (length >= 1024L)
        {
            return $"{length / 1024d:0.0} KB";
        }

        return $"{length} B";
    }

}
