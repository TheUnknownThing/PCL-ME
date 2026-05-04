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
    private static IReadOnlyList<FrontendLaunchArtifactRequirement> BuildRequiredArtifacts(
        string launcherFolder,
        FrontendLaunchManifestContext manifestContext,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        if (manifestContext.ChildFirstDocuments.Count == 0)
        {
            return [];
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var requirements = new Dictionary<string, FrontendLaunchArtifactRequirement>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in manifestContext.ParentFirstDocuments)
        {
            CollectRequiredArtifacts(
                launcherFolder,
                document.VersionName,
                document.Root,
                runtimeArchitecture,
                requirements);
        }

        return requirements.Values.ToArray();
    }

    private static MinecraftLaunchNativesSyncRequest? BuildNativeSyncRequest(
        string launcherFolder,
        FrontendLaunchManifestContext manifestContext,
        string nativesDirectory,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime)
    {
        if (manifestContext.ChildFirstDocuments.Count == 0)
        {
            return null;
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var archives = new Dictionary<string, MinecraftLaunchNativeArchive>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in manifestContext.ParentFirstDocuments)
        {
            CollectNativeArchives(
                launcherFolder,
                document.Root,
                runtimeArchitecture,
                archives);
        }

        return archives.Count == 0
            ? null
            : new MinecraftLaunchNativesSyncRequest(nativesDirectory, archives.Values.ToArray(), LogSkippedFiles: false);
    }

    private static void CollectRequiredArtifacts(
        string launcherFolder,
        string versionName,
        JsonElement root,
        MachineType runtimeArchitecture,
        IDictionary<string, FrontendLaunchArtifactRequirement> requirements)
    {
        var clientDownloadUrl = GetNestedString(root, "downloads", "client", "url");
        if (!string.IsNullOrWhiteSpace(clientDownloadUrl))
        {
            var clientJarPath = Path.Combine(launcherFolder, "versions", versionName, $"{versionName}.jar");
            requirements[clientJarPath] = new FrontendLaunchArtifactRequirement(
                clientJarPath,
                clientDownloadUrl,
                GetNestedString(root, "downloads", "client", "sha1"));
        }

        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture))
            {
                continue;
            }

            if (TryResolveEffectiveArtifactDownload(library, launcherFolder, runtimeArchitecture, out var artifactDownload) &&
                !string.IsNullOrWhiteSpace(artifactDownload.DownloadUrl))
            {
                requirements[artifactDownload.TargetPath] = new FrontendLaunchArtifactRequirement(
                    artifactDownload.TargetPath,
                    artifactDownload.DownloadUrl!,
                    artifactDownload.Sha1);
            }

            if (TryResolveNativeArchiveDownload(library, launcherFolder, runtimeArchitecture, out var nativeArchive) &&
                !string.IsNullOrWhiteSpace(nativeArchive.DownloadUrl))
            {
                requirements[nativeArchive.TargetPath] = new FrontendLaunchArtifactRequirement(
                    nativeArchive.TargetPath,
                    nativeArchive.DownloadUrl!,
                    nativeArchive.Sha1);
            }
        }
    }

    private static void CollectNativeArchives(
        string launcherFolder,
        JsonElement root,
        MachineType runtimeArchitecture,
        IDictionary<string, MinecraftLaunchNativeArchive> archives)
    {
        if (!root.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var library in libraries.EnumerateArray())
        {
            if (!IsLibraryAllowedOnCurrentPlatform(library, runtimeArchitecture) ||
                !TryResolveNativeArchiveDownload(library, launcherFolder, runtimeArchitecture, out var nativeArchive))
            {
                continue;
            }

            archives[nativeArchive.TargetPath] = new MinecraftLaunchNativeArchive(
                nativeArchive.TargetPath,
                nativeArchive.ExtractExcludes);
        }
    }

    private static bool TryResolveNativeArchiveDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out NativeArchiveDownloadInfo nativeArchive)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveNativeArchiveDownload(
                library,
                launcherFolder,
                runtimeArchitecture,
                out var resolved))
        {
            nativeArchive = null!;
            return false;
        }

        nativeArchive = new NativeArchiveDownloadInfo(
            resolved.TargetPath,
            resolved.DownloadUrl,
            resolved.Sha1,
            resolved.ExtractExcludes);
        return true;
    }

    private static bool TryResolveEffectiveArtifactDownload(
        JsonElement library,
        string launcherFolder,
        MachineType runtimeArchitecture,
        out LibraryDownloadInfo download)
    {
        if (!FrontendLibraryArtifactResolver.TryResolveArtifactDownload(
                library,
                launcherFolder,
                runtimeArchitecture,
                out var resolved))
        {
            download = null!;
            return false;
        }

        download = new LibraryDownloadInfo(resolved.TargetPath, resolved.DownloadUrl, resolved.Sha1);
        return true;
    }

    private static string BuildLibraryUrl(JsonElement library, string relativePath)
    {
        return FrontendLibraryArtifactResolver.BuildLibraryUrl(library, relativePath);
    }

}
