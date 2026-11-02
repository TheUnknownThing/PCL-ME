using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using PCL.Core.App;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.I18n;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendLaunchCompositionService
{
    private static FrontendVersionManifestSummary ReadManifestSummary(
        string launcherFolder,
        string selectedInstanceName,
        YamlFileProvider? instanceConfig,
        FrontendLaunchManifestContext manifestContext)
    {
        if (string.IsNullOrWhiteSpace(selectedInstanceName))
        {
            return FrontendVersionManifestSummary.Empty;
        }

        var profile = FrontendVersionManifestInspector.ReadProfile(launcherFolder, selectedInstanceName);
        var storedVersionName = instanceConfig is null
            ? null
            : NullIfWhiteSpace(ReadValue(instanceConfig, "VersionVanillaName", string.Empty));
        var fallbackVersion = TryParseVanillaVersion(storedVersionName)
                              ?? TryParseVanillaVersion(selectedInstanceName);
        var fallbackReleaseTime = instanceConfig is null
            ? null
            : TryParseReleaseTime(ReadValue(instanceConfig, "ReleaseTime", string.Empty));
        var effectiveVersion = profile.ParsedVanillaVersion ?? fallbackVersion;
        var effectiveReleaseTime = profile.ReleaseTime
                                   ?? fallbackReleaseTime
                                   ?? InferJavaRequirementReleaseTime(effectiveVersion);
        return new FrontendVersionManifestSummary(
            IsVersionInfoValid: profile.IsManifestValid || effectiveVersion is not null || effectiveReleaseTime is not null,
            ReleaseTime: effectiveReleaseTime,
            VanillaVersion: effectiveVersion,
            VersionType: profile.VersionType,
            AssetsIndexName: profile.AssetsIndexName,
            Libraries: ReadManifestLibraries(launcherFolder, manifestContext),
            HasOptiFine: profile.HasOptiFine,
            HasForge: profile.HasForge,
            ForgeVersion: profile.ForgeVersion,
            NeoForgeVersion: profile.NeoForgeVersion,
            HasCleanroom: profile.HasCleanroom,
            HasFabric: profile.HasFabric,
            LegacyFabricVersion: profile.LegacyFabricVersion,
            QuiltVersion: profile.QuiltVersion,
            HasLiteLoader: profile.HasLiteLoader,
            HasLabyMod: profile.HasLabyMod,
            JsonRequiredMajorVersion: profile.JsonRequiredMajorVersion,
            MojangRecommendedMajorVersion: profile.MojangRecommendedMajorVersion,
            MojangRecommendedComponent: profile.MojangRecommendedComponent);
    }

    private static string ReadFileOrDefault(string path, string fallback)
    {
        return File.Exists(path) ? File.ReadAllText(path) : fallback;
    }

    private static string? ReadOptionValue(string optionsPath, string key)
    {
        if (!File.Exists(optionsPath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(optionsPath))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            if (string.Equals(line[..separatorIndex], key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separatorIndex + 1)..];
            }
        }

        return null;
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static string? ReadManifestProperty(FrontendLaunchManifestContext manifestContext, string propertyName)
    {
        foreach (var document in manifestContext.ChildFirstDocuments)
        {
            var value = GetString(document.Root, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private sealed class FrontendLaunchManifestContext : IDisposable
    {
        private readonly List<FrontendLaunchManifestDocument> _documents;

        private FrontendLaunchManifestContext(List<FrontendLaunchManifestDocument> documents)
        {
            _documents = documents;
        }

        public IReadOnlyList<FrontendLaunchManifestDocument> ChildFirstDocuments => _documents;

        public IEnumerable<FrontendLaunchManifestDocument> ParentFirstDocuments => _documents.AsEnumerable().Reverse();

        public static FrontendLaunchManifestContext Load(string launcherFolder, string selectedInstanceName)
        {
            var documents = new List<FrontendLaunchManifestDocument>();
            if (string.IsNullOrWhiteSpace(selectedInstanceName))
            {
                return new FrontendLaunchManifestContext(documents);
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var currentVersion = selectedInstanceName;
            try
            {
                while (!string.IsNullOrWhiteSpace(currentVersion) && visited.Add(currentVersion))
                {
                    var manifestPath = FrontendVersionManifestPathResolver.ResolveManifestPath(launcherFolder, currentVersion);
                    if (string.IsNullOrWhiteSpace(manifestPath))
                    {
                        break;
                    }

                    var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    documents.Add(new FrontendLaunchManifestDocument(currentVersion, manifestPath, document));
                    currentVersion = GetString(document.RootElement, "inheritsFrom");
                }
            }
            catch
            {
                foreach (var document in documents)
                {
                    document.Document.Dispose();
                }

                throw;
            }

            return new FrontendLaunchManifestContext(documents);
        }

        public void Dispose()
        {
            foreach (var document in _documents)
            {
                document.Document.Dispose();
            }
        }
    }

    private sealed record FrontendLaunchManifestDocument(
        string VersionName,
        string ManifestPath,
        JsonDocument Document)
    {
        public JsonElement Root => Document.RootElement;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? GetNestedString(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static int? GetNestedInt(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value) ? value : null;
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var longValue))
            {
                return longValue;
            }

            if (value.ValueKind == JsonValueKind.String &&
                long.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool? GetNestedBoolean(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
        {
            if (!element.TryGetProperty(segment, out var next))
            {
                return null;
            }

            element = next;
        }

        return element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : null;
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        var rawValue = GetString(element, propertyName);
        return DateTime.TryParse(rawValue, out var value) ? value : null;
    }

}
