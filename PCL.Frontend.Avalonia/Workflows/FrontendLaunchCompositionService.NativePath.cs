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
    private static FrontendNativePathPlan BuildNativePathPlan(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        string baseNativesDirectory)
    {
        var extractionDirectory = ResolveNativeExtractionDirectory(
            launcherFolder,
            selectedInstanceName,
            manifestSummary,
            selectedJavaRuntime,
            baseNativesDirectory);
        var aliasDirectory = ShouldUseLegacyAsciiNativePathWorkaround(manifestSummary, baseNativesDirectory)
            ? BuildNativePathAliasDirectory(baseNativesDirectory)
            : null;
        var searchPath = string.IsNullOrWhiteSpace(aliasDirectory)
            ? baseNativesDirectory
            : aliasDirectory + Path.PathSeparator + baseNativesDirectory;

        return new FrontendNativePathPlan(
            baseNativesDirectory,
            extractionDirectory,
            searchPath,
            aliasDirectory);
    }

    private static string ResolveNativeExtractionDirectory(
        string launcherFolder,
        string selectedInstanceName,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        string baseNativesDirectory)
    {
        var modernJvmSections = CollectArgumentSectionJsons(launcherFolder, selectedInstanceName, "jvm");
        if (modernJvmSections.Count == 0)
        {
            return baseNativesDirectory;
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var jvmArguments = MinecraftLaunchJsonArgumentService.ExtractValues(
            new MinecraftLaunchJsonArgumentRequest(
                modernJvmSections,
                Environment.OSVersion.Version.ToString(),
                runtimeArchitecture == MachineType.I386));
        const string prefix = "-Djava.library.path=${natives_directory}/";
        foreach (var argument in jvmArguments)
        {
            if (!argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = argument[prefix.Length..];
            var resolvedPath = TryResolveNativeExtractionSubdirectory(baseNativesDirectory, relativePath);
            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return baseNativesDirectory;
    }

    private static string? TryResolveNativeExtractionSubdirectory(string baseNativesDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return null;
        }

        try
        {
            var basePath = Path.GetFullPath(baseNativesDirectory);
            var resolvedPath = Path.GetFullPath(Path.Combine(basePath, normalizedRelativePath));
            var basePrefix = basePath.EndsWith(Path.DirectorySeparatorChar)
                ? basePath
                : basePath + Path.DirectorySeparatorChar;
            return resolvedPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase)
                ? resolvedPath
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool ShouldUseLegacyAsciiNativePathWorkaround(
        FrontendVersionManifestSummary manifestSummary,
        string nativesDirectory)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (IsAscii(nativesDirectory))
        {
            return false;
        }

        return manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion < new Version(1, 19);
    }

    private static string BuildNativePathAliasDirectory(string nativesDirectory)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(nativesDirectory)))
            .ToLowerInvariant();
        return Path.Combine("/tmp", $"pclme-natives-{hash[..12]}");
    }

}
