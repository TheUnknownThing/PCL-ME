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
    private static FrontendRetroWrapperOptions ResolveRetroWrapperOptions(
        string launcherFolder,
        FrontendVersionManifestSummary manifestSummary,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig)
    {
        var disableGlobalRetroWrapper = ReadValue(sharedConfig, "LaunchAdvanceDisableRW", false);
        var disableInstanceRetroWrapper = instanceConfig is not null &&
                                          ReadValue(instanceConfig, "VersionAdvanceDisableRW", false);
        var gameVersionDrop = ResolveGameVersionDrop(manifestSummary.VanillaVersion);
        var shouldUseRetroWrapper = MinecraftLaunchRetroWrapperService.ShouldUse(new MinecraftLaunchRetroWrapperRequest(
            manifestSummary.ReleaseTime ?? DateTime.Now,
            gameVersionDrop,
            disableGlobalRetroWrapper,
            disableInstanceRetroWrapper));
        var retroWrapperPath = Path.Combine(launcherFolder, "libraries", "retrowrapper", "RetroWrapper.jar");
        if (!shouldUseRetroWrapper || !File.Exists(retroWrapperPath))
        {
            return new FrontendRetroWrapperOptions(false, null);
        }

        return new FrontendRetroWrapperOptions(true, retroWrapperPath);
    }

    private static IReadOnlyList<string> BuildClasspathHeadEntries(
        YamlFileProvider? instanceConfig,
        string? instanceJarPath)
    {
        var customHeadEntries = new List<string>();
        if (!string.IsNullOrWhiteSpace(instanceJarPath) && File.Exists(instanceJarPath))
        {
            customHeadEntries.Add(instanceJarPath);
        }

        if (instanceConfig is null)
        {
            return customHeadEntries;
        }

        var rawClasspathHead = ReadValue(instanceConfig, "VersionAdvanceClasspathHead", string.Empty);
        var userEntries = ParseClasspathHeadEntries(rawClasspathHead);
        for (var index = userEntries.Count - 1; index >= 0; index--)
        {
            customHeadEntries.Add(userEntries[index]);
        }

        return customHeadEntries;
    }

    private static IReadOnlyList<string> ParseClasspathHeadEntries(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([Path.PathSeparator, '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static FrontendProxyOptions ResolveProxyOptions(
        FrontendRuntimePaths runtimePaths,
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null || !ReadValue(instanceConfig, "VersionAdvanceUseProxyV2", false))
        {
            return FrontendProxyOptions.None;
        }

        var configuration = FrontendHttpProxyService.ResolveConfiguration(runtimePaths, sharedConfig);
        return configuration.Mode switch
        {
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.CustomProxy => CreateProxyOptions(
                configuration.CustomProxyAddress,
                configuration.CustomProxyCredentials),
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.SystemProxy => ResolveSystemProxyOptions(),
            _ => FrontendProxyOptions.None
        };
    }

    private static FrontendProxyOptions ResolveSystemProxyOptions()
    {
        try
        {
            var probeTarget = new Uri("http://example.com");
            var proxyUri = WebRequest.DefaultWebProxy?.GetProxy(probeTarget);
            if (proxyUri is null ||
                !proxyUri.IsAbsoluteUri ||
                string.Equals(proxyUri.Host, probeTarget.Host, StringComparison.OrdinalIgnoreCase))
            {
                return FrontendProxyOptions.None;
            }

            return CreateProxyOptions(proxyUri);
        }
        catch
        {
            return FrontendProxyOptions.None;
        }
    }

    private static FrontendProxyOptions CreateProxyOptions(
        Uri? uri,
        NetworkCredential? credentials = null)
    {
        if (uri is null || !uri.IsAbsoluteUri || string.IsNullOrWhiteSpace(uri.Host))
        {
            return FrontendProxyOptions.None;
        }

        var scheme = uri.Scheme.StartsWith("socks", StringComparison.OrdinalIgnoreCase)
            ? "socks"
            : string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                ? "https"
                : "http";
        var port = uri.IsDefaultPort
            ? scheme switch
            {
                "https" => 443,
                "socks" => 1080,
                _ => 80
            }
            : uri.Port;
        if (port <= 0)
        {
            return FrontendProxyOptions.None;
        }

        return new FrontendProxyOptions(
            scheme,
            uri.Host,
            port,
            string.IsNullOrWhiteSpace(credentials?.UserName) ? null : credentials.UserName,
            string.IsNullOrEmpty(credentials?.Password) ? null : credentials.Password);
    }

    private static FrontendJavaWrapperOptions ResolveJavaWrapperOptions(
        string launcherFolder,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var disableGlobalJavaWrapper = ReadValue(localConfig, "LaunchAdvanceDisableJLW", true);
        var disableInstanceJavaWrapper = instanceConfig is not null &&
                                         ReadValue(instanceConfig, "VersionAdvanceDisableJLW", false);
        var isRequested = !disableGlobalJavaWrapper && !disableInstanceJavaWrapper;
        var wrapperPath = ResolveJavaWrapperPath(launcherFolder);
        if (!isRequested || string.IsNullOrWhiteSpace(wrapperPath))
        {
            return FrontendJavaWrapperOptions.Disabled;
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "PCL", "JavaWrapper");
        try
        {
            Directory.CreateDirectory(tempDirectory);
        }
        catch
        {
            return FrontendJavaWrapperOptions.Disabled;
        }

        return new FrontendJavaWrapperOptions(true, tempDirectory, wrapperPath);
    }

    private static string? ResolveDebugLog4jConfigurationPath(
        string launcherFolder,
        string indieDirectory,
        YamlFileProvider? instanceConfig)
    {
        if (instanceConfig is null || !ReadValue(instanceConfig, "VersionUseDebugLog4j2Config", false))
        {
            return null;
        }

        string[] candidates =
        [
            Path.Combine(indieDirectory, "PCL", "log4j2-debug.xml"),
            Path.Combine(indieDirectory, "PCL", "log4j2.xml"),
            Path.Combine(indieDirectory, "log4j2-debug.xml"),
            Path.Combine(indieDirectory, "log4j2.xml"),
            Path.Combine(launcherFolder, "PCL", "log4j2-debug.xml"),
            Path.Combine(launcherFolder, "PCL", "log4j2.xml")
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveRendererAgentArgument(
        string launcherFolder,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var effectiveRendererIndex = ResolveEffectiveRendererIndex(localConfig, instanceConfig);
        var rendererMode = ResolveRendererModeName(effectiveRendererIndex);
        if (string.IsNullOrWhiteSpace(rendererMode))
        {
            return null;
        }

        var rendererAgentPath = ResolveRendererAgentPath(launcherFolder);
        return string.IsNullOrWhiteSpace(rendererAgentPath)
            ? null
            : $"-javaagent:\"{rendererAgentPath}\"={rendererMode}";
    }

    private static int ResolveEffectiveRendererIndex(
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig)
    {
        var globalRendererIndex = Math.Clamp(ReadValue(localConfig, "LaunchAdvanceRenderer", 0), 0, 3);
        if (instanceConfig is null)
        {
            return globalRendererIndex;
        }

        var instanceRendererIndex = Math.Clamp(ReadValue(instanceConfig, "VersionAdvanceRenderer", 0), 0, 4);
        return instanceRendererIndex == 0
            ? globalRendererIndex
            : instanceRendererIndex - 1;
    }

    private static string? ResolveRendererModeName(int effectiveRendererIndex)
    {
        return effectiveRendererIndex switch
        {
            1 => "llvmpipe",
            2 => "d3d12",
            3 => "zink",
            _ => null
        };
    }

    private static string? ResolveRendererAgentPath(string launcherFolder)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string[] directCandidates =
        [
            Path.Combine(launcherFolder, "PCL", "mesa-loader-windows.jar"),
            Path.Combine(launcherFolder, "PCL", "mesa-loader.jar"),
            Path.Combine(launcherFolder, "libraries", "mesa-loader-windows.jar"),
            Path.Combine(launcherFolder, "libraries", "mesa-loader.jar")
        ];

        var directMatch = directCandidates.FirstOrDefault(path => File.Exists(path) && IsJavaAgentJar(path));
        if (!string.IsNullOrWhiteSpace(directMatch))
        {
            return directMatch;
        }

        string[] searchRoots =
        [
            Path.Combine(launcherFolder, "PCL"),
            Path.Combine(launcherFolder, "libraries")
        ];
        foreach (var root in searchRoots)
        {
            var discovered = TryDiscoverRendererAgentPath(root);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                return discovered;
            }
        }

        return null;
    }

    private static string? TryDiscoverRendererAgentPath(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return null;
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*.jar", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(filePath);
                if (fileName.Contains("mesa", StringComparison.OrdinalIgnoreCase) &&
                    fileName.Contains("loader", StringComparison.OrdinalIgnoreCase) &&
                    IsJavaAgentJar(filePath))
                {
                    return filePath;
                }
            }
        }
        catch
        {
            // Ignore best-effort discovery failures and fall back to default behavior.
        }

        return null;
    }

    private static bool IsJavaAgentJar(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var manifestEntry = archive.GetEntry("META-INF/MANIFEST.MF");
            if (manifestEntry is null)
            {
                return false;
            }

            using var manifestStream = manifestEntry.Open();
            using var reader = new StreamReader(manifestStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is not null && line.StartsWith("Premain-Class:", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Invalid archives should not break launch composition.
        }

        return false;
    }

    private static string? ResolveJavaWrapperPath(string launcherFolder)
    {
        string[] candidates =
        [
            Path.Combine(launcherFolder, "PCL", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "PCL", "JavaWrapper.jar"),
            Path.Combine(launcherFolder, "libraries", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "libraries", "java-wrapper", "java-wrapper.jar"),
            Path.Combine(launcherFolder, "runtime", "java-wrapper.jar")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? ResolveAuthlibInjectorArgument(
        string launcherFolder,
        FrontendLaunchProfileSummary selectedProfile)
    {
        if (selectedProfile.Kind != MinecraftLaunchProfileKind.Auth ||
            string.IsNullOrWhiteSpace(selectedProfile.AuthServer))
        {
            return null;
        }

        var apiRoot = selectedProfile.AuthServer.Trim().TrimEnd('/');
        if (apiRoot.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
        {
            apiRoot = apiRoot[..^"/authserver".Length];
        }

        var injectorPath = ResolveAuthlibInjectorPath(launcherFolder)
                           ?? TryDownloadAuthlibInjector(launcherFolder);
        if (string.IsNullOrWhiteSpace(injectorPath))
        {
            LogWrapper.Warn("Launch", "Authlib profile selected but authlib-injector jar is unavailable; multiplayer authentication may fail.");
            return null;
        }

        return $"-javaagent:\"{injectorPath}\"={apiRoot}";
    }

    private static string? ResolveAuthlibInjectorPath(string launcherFolder)
    {
        string[] directCandidates =
        [
            Path.Combine(launcherFolder, "PCL", "authlib-injector.jar"),
            Path.Combine(launcherFolder, "PCL", "Authlib-Injector.jar"),
            Path.Combine(launcherFolder, "libraries", "authlib-injector.jar"),
            Path.Combine(launcherFolder, "libraries", "authlib-injector", "authlib-injector.jar")
        ];

        var directMatch = directCandidates.FirstOrDefault(path => File.Exists(path) && IsJavaAgentJar(path));
        if (!string.IsNullOrWhiteSpace(directMatch))
        {
            return directMatch;
        }

        string[] searchRoots =
        [
            Path.Combine(launcherFolder, "PCL"),
            Path.Combine(launcherFolder, "libraries")
        ];
        foreach (var root in searchRoots)
        {
            var discovered = TryDiscoverAuthlibInjectorPath(root);
            if (!string.IsNullOrWhiteSpace(discovered))
            {
                return discovered;
            }
        }

        return null;
    }

    private static string? TryDiscoverAuthlibInjectorPath(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
        {
            return null;
        }

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(rootDirectory, "*.jar", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(filePath);
                if (!fileName.Contains("authlib", StringComparison.OrdinalIgnoreCase) ||
                    !fileName.Contains("injector", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsJavaAgentJar(filePath))
                {
                    return filePath;
                }
            }
        }
        catch
        {
            // Best effort discovery only.
        }

        return null;
    }

    private static string? TryDownloadAuthlibInjector(string launcherFolder)
    {
        var targetPath = Path.Combine(launcherFolder, "PCL", "authlib-injector.jar");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using var metadataDocument = JsonDocument.Parse(
                JavaRuntimeHttpClient.GetStringAsync("https://authlib-injector.yushi.moe/artifact/latest.json")
                    .GetAwaiter()
                    .GetResult());
            var root = metadataDocument.RootElement;
            if (!root.TryGetProperty("download_url", out var downloadUrlElement))
            {
                return null;
            }

            var downloadUrl = downloadUrlElement.GetString();
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            var bytes = JavaRuntimeHttpClient.GetByteArrayAsync(downloadUrl)
                .GetAwaiter()
                .GetResult();

            if (root.TryGetProperty("checksums", out var checksums) &&
                checksums.TryGetProperty("sha256", out var sha256Element))
            {
                var expectedSha256 = sha256Element.GetString();
                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        LogWrapper.Warn("Launch", $"Downloaded authlib-injector checksum mismatch: expected={expectedSha256}, actual={actualSha256}");
                        return null;
                    }
                }
            }

            File.WriteAllBytes(targetPath, bytes);
            return IsJavaAgentJar(targetPath) ? targetPath : null;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "Failed to download authlib-injector jar.");
            return null;
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

}
