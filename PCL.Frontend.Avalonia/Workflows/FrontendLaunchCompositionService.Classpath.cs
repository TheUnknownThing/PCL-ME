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
    private static MinecraftLaunchClasspathRequest BuildClasspathRequest(
        string launcherFolder,
        string selectedInstanceName,
        FrontendLaunchManifestContext manifestContext,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        YamlFileProvider? instanceConfig,
        FrontendRetroWrapperOptions retroWrapperOptions)
    {
        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var instanceJarPath = string.IsNullOrWhiteSpace(selectedInstanceName)
            ? null
            : Path.Combine(FrontendVersionManifestPathResolver.GetInstanceDirectory(launcherFolder, selectedInstanceName), $"{selectedInstanceName}.jar");
        var customHeadEntries = BuildClasspathHeadEntries(instanceConfig, instanceJarPath);

        return new MinecraftLaunchClasspathRequest(
            Libraries: ReadClasspathLibraries(launcherFolder, manifestContext, runtimeArchitecture),
            CustomHeadEntries: customHeadEntries,
            RetroWrapperPath: retroWrapperOptions.RetroWrapperPath,
            ClasspathSeparator: GetClasspathSeparator());
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> ReadClasspathLibraries(
        string launcherFolder,
        FrontendLaunchManifestContext manifestContext,
        MachineType runtimeArchitecture)
    {
        if (manifestContext.ChildFirstDocuments.Count == 0)
        {
            return [];
        }

        var libraries = new List<MinecraftLaunchClasspathLibrary>();
        foreach (var document in manifestContext.ParentFirstDocuments)
        {
            CollectClasspathLibraries(launcherFolder, document.Root, runtimeArchitecture, libraries);
        }

        return DeduplicateClasspathLibraries(libraries);
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> DeduplicateClasspathLibraries(
        IReadOnlyList<MinecraftLaunchClasspathLibrary> libraries)
    {
        if (libraries.Count <= 1)
        {
            return libraries;
        }

        var deduplicated = new List<MinecraftLaunchClasspathLibrary>(libraries.Count);
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            var key = GetClasspathLibraryIdentityKey(library);
            if (indexByKey.TryGetValue(key, out var existingIndex))
            {
                deduplicated[existingIndex] = library;
                continue;
            }

            indexByKey[key] = deduplicated.Count;
            deduplicated.Add(library);
        }

        return deduplicated;
    }

    private static string GetClasspathLibraryIdentityKey(MinecraftLaunchClasspathLibrary library)
    {
        if (TryParseLibraryCoordinate(library.Name, out var coordinate))
        {
            return $"{coordinate.GroupId}:{coordinate.ArtifactId}:{coordinate.Classifier ?? string.Empty}:{library.IsNatives}";
        }

        if (!string.IsNullOrWhiteSpace(library.Name))
        {
            var parts = library.Name.Split(':', StringSplitOptions.None);
            if (parts.Length >= 3)
            {
                var classifier = parts.Length >= 4 ? parts[3] : string.Empty;
                return $"{parts[0]}:{parts[1]}:{classifier}:{library.IsNatives}";
            }

            return $"{library.Name}:{library.IsNatives}";
        }

        return $"{library.Path}:{library.IsNatives}";
    }

    private static bool TryParseLibraryCoordinate(string? name, out LibraryCoordinate coordinate)
    {
        coordinate = new LibraryCoordinate(string.Empty, string.Empty, string.Empty, null);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var parts = name.Split(':', StringSplitOptions.None);
        if (parts.Length is < 3 or > 4)
        {
            return false;
        }

        coordinate = new LibraryCoordinate(
            parts[0],
            parts[1],
            parts[2],
            parts.Length >= 4 ? parts[3] : null);
        return true;
    }

    private static void CollectClasspathLibraries(
        string launcherFolder,
        JsonElement root,
        MachineType runtimeArchitecture,
        IList<MinecraftLaunchClasspathLibrary> libraries)
    {
        if (!root.TryGetProperty("libraries", out var manifestLibraries) ||
            manifestLibraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in manifestLibraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture))
            {
                continue;
            }

            if (!TryResolveEffectiveArtifactDownload(library, launcherFolder, runtimeArchitecture, out var artifactDownload))
            {
                continue;
            }

            libraries.Add(new MinecraftLaunchClasspathLibrary(
                GetString(library, "name"),
                artifactDownload.TargetPath,
                IsNatives: library.TryGetProperty("natives", out _)));
        }
    }

    private static IReadOnlyList<MinecraftLaunchClasspathLibrary> ReadManifestLibraries(
        string launcherFolder,
        FrontendLaunchManifestContext manifestContext)
    {
        if (manifestContext.ChildFirstDocuments.Count == 0)
        {
            return [];
        }

        return manifestContext.ParentFirstDocuments
            .SelectMany(document => ParseLibraries(document.Root, launcherFolder))
            .ToArray();
    }

    private static MinecraftLaunchClasspathLibrary[] ParseLibraries(JsonElement root, string launcherFolder)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<MinecraftLaunchClasspathLibrary>();
        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library))
            {
                continue;
            }

            var name = GetString(library, "name");
            var artifactPath = GetNestedString(library, "downloads", "artifact", "path");
            if (string.IsNullOrWhiteSpace(artifactPath) && !string.IsNullOrWhiteSpace(name))
            {
                artifactPath = DeriveLibraryPathFromName(name);
            }

            if (string.IsNullOrWhiteSpace(artifactPath))
            {
                continue;
            }

            result.Add(new MinecraftLaunchClasspathLibrary(
                name,
                Path.Combine(launcherFolder, "libraries", artifactPath.Replace('/', Path.DirectorySeparatorChar)),
                IsNatives: library.TryGetProperty("natives", out _)));
        }

        return result.ToArray();
    }

    private static bool IsLibraryAllowedOnCurrentPlatform(JsonElement library, MachineType runtimeArchitecture)
    {
        return FrontendLibraryArtifactResolver.IsLibraryAllowed(library, runtimeArchitecture);
    }

    private static bool IsLibraryAllowedOnCurrentPlatform(JsonElement library)
    {
        return IsLibraryAllowedOnCurrentPlatform(library, GetPreferredMachineType());
    }

    private static bool RuleMatchesCurrentPlatform(JsonElement rule, MachineType runtimeArchitecture)
    {
        if (!rule.TryGetProperty("os", out var os) || os.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var osName = GetString(os, "name");
        if (!string.IsNullOrWhiteSpace(osName) && !IsCurrentOs(osName))
        {
            return false;
        }

        var osArch = GetString(os, "arch");
        if (!string.IsNullOrWhiteSpace(osArch) && !IsCurrentArchitecture(osArch, runtimeArchitecture))
        {
            return false;
        }

        return true;
    }

    private static bool IsCurrentOs(string osName)
    {
        return osName.ToLowerInvariant() switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "osx" or "macos" => OperatingSystem.IsMacOS(),
            "linux" => OperatingSystem.IsLinux(),
            _ => true
        };
    }

    private static bool IsCurrentArchitecture(string osArch, MachineType runtimeArchitecture)
    {
        return osArch.ToLowerInvariant() switch
        {
            "x86" => runtimeArchitecture == MachineType.I386,
            "x86_64" or "amd64" => runtimeArchitecture == MachineType.AMD64,
            "arm64" or "aarch64" => runtimeArchitecture == MachineType.ARM64,
            "arm" => runtimeArchitecture is MachineType.ARM or MachineType.ARMNT,
            _ => true
        };
    }

    private static string GetClasspathSeparator()
    {
        return OperatingSystem.IsWindows() ? ";" : ":";
    }

    private static bool ContainsLibrary(IEnumerable<MinecraftLaunchClasspathLibrary> libraries, string searchText)
    {
        return libraries.Any(library => library.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? ExtractLibraryVersion(IEnumerable<MinecraftLaunchClasspathLibrary> libraries, string prefix)
    {
        var match = libraries.FirstOrDefault(library => library.Name?.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase) == true);
        return match?.Name?.Split(':').LastOrDefault();
    }

    private static string DeriveLibraryPathFromName(string libraryName, string? classifier = null)
    {
        return FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(libraryName, classifier);
    }

    private static void ParseLibraryCoordinateExtension(ref string? value, ref string extension)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var extensionIndex = value.IndexOf('@');
        if (extensionIndex < 0)
        {
            return;
        }

        if (extensionIndex < value.Length - 1)
        {
            extension = value[(extensionIndex + 1)..];
        }

        value = value[..extensionIndex];
    }

}
