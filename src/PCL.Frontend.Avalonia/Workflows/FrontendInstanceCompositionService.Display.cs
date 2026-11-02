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
    private static string BuildInstanceSubtitle(
        FrontendInstanceSelectionState selection,
        FrontendVersionManifestSummary manifestSummary,
        II18nService? i18n)
    {
        var parts = new List<string>();
        AddIfNotEmpty(parts, DeterminePrimaryLoaderLabel(manifestSummary));
        AddIfNotEmpty(parts, Text(i18n, "instance.install.minecraft.version", "Minecraft {version}", ("version", selection.VanillaVersion)));
        parts.Add(selection.IsIndie
            ? Text(i18n, "instance.common.independent", "Independent instance")
            : Text(i18n, "instance.common.shared", "Shared instance"));
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string DeterminePrimaryLoaderLabel(FrontendVersionManifestSummary manifestSummary)
    {
        return
            FirstNonEmpty(
                PrefixVersion("NeoForge", manifestSummary.NeoForgeVersion),
                PrefixVersion("Cleanroom", manifestSummary.CleanroomVersion),
                PrefixVersion("Fabric", manifestSummary.FabricVersion),
                PrefixVersion("Legacy Fabric", manifestSummary.LegacyFabricVersion),
                PrefixVersion("Quilt", manifestSummary.QuiltVersion),
                PrefixVersion("Forge", manifestSummary.ForgeVersion),
                PrefixVersion("OptiFine", manifestSummary.OptiFineVersion),
                manifestSummary.HasLiteLoader ? "LiteLoader" : null,
                PrefixVersion("LabyMod", manifestSummary.LabyModVersion))
            ?? "Minecraft";
    }

    private static FrontendInstallSelectionState DisplayVersionState(string? version, II18nService? i18n)
    {
        return string.IsNullOrWhiteSpace(version)
            ? FrontendInstallSelectionState.NotInstalled(Text(i18n, "instance.common.not_installed", "Not installed"))
            : FrontendInstallSelectionState.Versioned(version);
    }

    private static FrontendInstallSelectionState DisplayInstalledState(bool installed, string? version, II18nService? i18n)
    {
        if (!installed)
        {
            return FrontendInstallSelectionState.NotInstalled(Text(i18n, "instance.common.not_installed", "Not installed"));
        }

        return string.IsNullOrWhiteSpace(version)
            ? FrontendInstallSelectionState.Installed(Text(i18n, "instance.common.installed", "Installed"))
            : FrontendInstallSelectionState.Versioned(version);
    }

    private static string PrefixVersion(string title, string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? string.Empty : $"{title} {version}";
    }

    private static string GetRelativeParent(string rootDirectory, string path, II18nService? i18n)
    {
        var parent = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return Text(i18n, "instance.content.resource.meta.root_directory", "Root directory");
        }

        var relative = Path.GetRelativePath(rootDirectory, parent);
        return string.Equals(relative, ".", StringComparison.Ordinal) ? Text(i18n, "instance.content.resource.meta.root_directory", "Root directory") : relative;
    }

    private static void AddIfNotEmpty(ICollection<string> target, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add(value);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

}
