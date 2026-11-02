using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    private static FrontendInstallApplyRequest BuildInstallRequest(
        FrontendModpackPackage package,
        FrontendModpackInstallRequest request,
        II18nService? i18n)
    {
        var minecraftChoice = ResolveMinecraftChoice(package.MinecraftVersion, request.DownloadSourceIndex, i18n);
        var primaryChoice =
            ResolveLoaderChoice("Forge", package.MinecraftVersion, package.ForgeVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("NeoForge", package.MinecraftVersion, package.NeoForgeVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Cleanroom", package.MinecraftVersion, package.CleanroomVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Fabric", package.MinecraftVersion, package.FabricVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Legacy Fabric", package.MinecraftVersion, package.LegacyFabricVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("Quilt", package.MinecraftVersion, package.QuiltVersion, request.DownloadSourceIndex, i18n) ??
            ResolveLoaderChoice("LabyMod", package.MinecraftVersion, package.LabyModVersion, request.DownloadSourceIndex, i18n);
        var liteLoaderChoice = ResolveLoaderChoice("LiteLoader", package.MinecraftVersion, package.LiteLoaderVersion, request.DownloadSourceIndex, i18n);
        var optiFineChoice = ResolveLoaderChoice("OptiFine", package.MinecraftVersion, package.OptiFineVersion, request.DownloadSourceIndex, i18n);

        return new FrontendInstallApplyRequest(
            request.LauncherDirectory,
            request.InstanceName,
            request.DownloadSourceIndex,
            minecraftChoice,
            primaryChoice,
            LiteLoaderChoice: liteLoaderChoice,
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

    private static FrontendInstallChoice ResolveMinecraftChoice(string version, int downloadSourceIndex, II18nService? i18n = null)
    {
        var choices = FrontendInstallWorkflowService.GetMinecraftCatalogChoices(version, downloadSourceIndex, i18n);
        var choice = choices.FirstOrDefault(candidate =>
            string.Equals(candidate.Version, version, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Metadata?["rawVersion"]?.GetValue<string>(), version, StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new InvalidOperationException(ModpackText(i18n, "resource_detail.modpack.workflow.errors.minecraft_choice_missing", ("version", version)));
    }

    private static FrontendInstallChoice? ResolveLoaderChoice(
        string optionTitle,
        string minecraftVersion,
        string? requestedVersion,
        int downloadSourceIndex,
        II18nService? i18n = null)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            return null;
        }

        var choices = FrontendInstallWorkflowService.GetSupportedChoices(optionTitle, minecraftVersion, downloadSourceIndex, i18n);
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

        if (package.InstanceConfigValues is not null)
        {
            foreach (var (key, value) in package.InstanceConfigValues)
            {
                SetInstanceConfigValue(provider, key, value);
            }
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

    private static void SetInstanceConfigValue(YamlFileProvider provider, string key, object? value)
    {
        switch (value)
        {
            case bool boolValue:
                provider.Set(key, boolValue);
                break;
            case int intValue:
                provider.Set(key, intValue);
                break;
            case long longValue:
                provider.Set(key, longValue);
                break;
            case double doubleValue:
                provider.Set(key, doubleValue);
                break;
            default:
                provider.Set(key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static bool ApplyPackageManifestPatch(FrontendModpackPackage package, string manifestPath)
    {
        if (package.ManifestPatch is not { } patch || string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return false;
        }

        var root = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject;
        if (root is null)
        {
            return false;
        }

        if (patch.RemoveLegacyMinecraftArguments)
        {
            root.Remove("minecraftArguments");
        }

        AppendManifestLibraries(root, patch.Libraries);
        AppendManifestArguments(root, "game", patch.GameArguments);
        AppendManifestArguments(root, "jvm", patch.JvmArguments);
        foreach (var (key, value) in patch.ExtraProperties)
        {
            if (value is not null)
            {
                root[key] = value.DeepClone();
            }
        }

        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), Utf8NoBom);
        return true;
    }

    private static void AppendManifestLibraries(JsonObject root, JsonArray patchLibraries)
    {
        if (patchLibraries.Count == 0)
        {
            return;
        }

        var libraries = root["libraries"] as JsonArray ?? new JsonArray();
        var seen = libraries
            .Select(node => node?["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in patchLibraries)
        {
            if (node is not JsonObject library)
            {
                continue;
            }

            var name = library["name"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(name) && !seen.Add(name))
            {
                continue;
            }

            libraries.Add(library.DeepClone());
        }

        root["libraries"] = libraries;
    }

    private static void AppendManifestArguments(JsonObject root, string key, JsonArray patchArguments)
    {
        if (patchArguments.Count == 0)
        {
            return;
        }

        var arguments = root["arguments"] as JsonObject ?? new JsonObject();
        var values = arguments[key] as JsonArray ?? new JsonArray();
        foreach (var node in patchArguments)
        {
            if (node is not null)
            {
                values.Add(node.DeepClone());
            }
        }

        arguments[key] = values;
        root["arguments"] = arguments;
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
}
