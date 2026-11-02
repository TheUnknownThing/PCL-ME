using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendInstallWorkflowService
{

    private static JsonObject BuildForgelikeManifest(
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        FrontendDownloadProvider downloadProvider,
        Action<string>? onStatusChanged = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        II18nService? i18n = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(loaderChoice);
        cancelToken.ThrowIfCancellationRequested();

        var installerUrl = loaderChoice.DownloadUrl
                           ?? throw new InvalidOperationException("Missing installer download URL.");
        var installerPath = CreateTempFile("pcl-forgelike-installer-", ".jar");
        try
        {
            ReportPrepareStatus(
                onStatusChanged,
                Text(
                    i18n,
                    "download.install.workflow.tasks.download_loader_installer",
                    "Downloading the installer for {loader_title}...",
                    ("loader_title", loaderChoice.Title)));
            DownloadFileToPath(installerUrl, installerPath, downloadProvider: downloadProvider, speedLimiter: speedLimiter, cancelToken: cancelToken);
            using var archive = ZipFile.OpenRead(installerPath);
            var installProfile = ReadJsonObjectFromEntry(archive, "install_profile.json");

            if (loaderChoice.Kind == FrontendInstallChoiceKind.Forge
                && IsLegacyForgeInstallProfile(installProfile))
            {
                ReportPrepareStatus(
                    onStatusChanged,
                    Text(
                        i18n,
                        "download.install.workflow.tasks.inspect_loader_installer",
                        "Inspecting the installer contents for {loader_title}...",
                        ("loader_title", loaderChoice.Title)));
                return BuildLegacyForgeManifest(archive, request, loaderChoice, installProfile, onStatusChanged, i18n);
            }

            return BuildModernForgelikeManifest(
                archive,
                installProfile,
                installerPath,
                request,
                loaderChoice,
                downloadProvider,
                onStatusChanged,
                speedLimiter,
                i18n,
                cancelToken);
        }
        finally
        {
            TryDeleteFile(installerPath);
        }
    }


    private static JsonObject BuildLegacyForgeManifest(
        ZipArchive archive,
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        JsonObject installProfile,
        Action<string>? onStatusChanged = null,
        II18nService? i18n = null)
    {
        ValidateLegacyForgeInstallProfile(installProfile, request.MinecraftChoice.Version);
        if (installProfile["install"] is null)
        {
            var jsonPath = installProfile["json"]?.GetValue<string>()?.TrimStart('/');
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                throw new InvalidOperationException("Legacy Forge installer is missing the version manifest path.");
            }

            return ReadJsonObjectFromEntry(archive, jsonPath);
        }

        var installPath = installProfile["install"]?["path"]?.GetValue<string>();
        var entryPath = installProfile["install"]?["filePath"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(installPath) || string.IsNullOrWhiteSpace(entryPath))
        {
            throw new InvalidOperationException("Legacy Forge installer is missing support library write information.");
        }

        var libraryOutputPath = Path.Combine(
            request.LauncherDirectory,
            "libraries",
            FrontendLibraryArtifactResolver.DeriveLibraryPathFromName(installPath).Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(libraryOutputPath)!);
        ReportPrepareStatus(
            onStatusChanged,
            Text(
                i18n,
                "download.install.workflow.tasks.write_loader_libraries",
                "Writing support libraries for {loader_title}...",
                ("loader_title", loaderChoice.Title)));
        using (var source = archive.GetEntry(entryPath)?.Open()
                             ?? throw new InvalidOperationException($"Legacy Forge installer is missing entry: {entryPath}"))
        using (var output = File.Create(libraryOutputPath))
        {
            source.CopyTo(output);
        }

        if (installProfile["versionInfo"] is not JsonObject versionInfo)
        {
            throw new InvalidOperationException("Legacy Forge installer is missing versionInfo.");
        }

        var manifest = CloneObject(versionInfo);
        if (manifest["inheritsFrom"] is null)
        {
            manifest["inheritsFrom"] = request.MinecraftChoice.Version;
        }

        return manifest;
    }


    private static JsonObject BuildModernForgelikeManifest(
        ZipArchive archive,
        JsonObject installProfile,
        string installerPath,
        FrontendInstallApplyRequest request,
        FrontendInstallChoice loaderChoice,
        FrontendDownloadProvider downloadProvider,
        Action<string>? onStatusChanged = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        II18nService? i18n = null,
        CancellationToken cancelToken = default)
    {
        var minecraftVersion = GetRequiredString(
            installProfile,
            "minecraft",
            $"{loaderChoice.Title} installer is missing Minecraft version information.");
        if (!string.Equals(minecraftVersion, request.MinecraftChoice.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{loaderChoice.Title} installer targets Minecraft {minecraftVersion}, which does not match the selected version {request.MinecraftChoice.Version}.");
        }

        var versionJsonPath = GetRequiredString(
            installProfile,
            "json",
            $"{loaderChoice.Title} installer is missing the version manifest path.").TrimStart('/');

        var tempRoot = CreateTempDirectory("pcl-forgelike-");
        try
        {
            ReportPrepareStatus(
                onStatusChanged,
                Text(
                    i18n,
                    "download.install.workflow.tasks.prepare_vanilla_files",
                    "Preparing vanilla files for Minecraft {version}...",
                    ("version", request.MinecraftChoice.Version)));
            EnsureVanillaVersionFiles(tempRoot, request.MinecraftChoice, downloadProvider, onStatusChanged, speedLimiter, i18n, cancelToken);

            ReportPrepareStatus(
                onStatusChanged,
                Text(
                    i18n,
                    "download.install.workflow.tasks.extract_loader_libraries",
                    "Extracting bundled libraries from the {loader_title} installer...",
                    ("loader_title", loaderChoice.Title)));
            CopyEmbeddedForgelikeLibraries(archive, installProfile, tempRoot);

            ReportPrepareStatus(
                onStatusChanged,
                Text(
                    i18n,
                    "download.install.workflow.tasks.repair_loader_dependencies",
                    "Repairing install dependencies for {loader_title}...",
                    ("loader_title", loaderChoice.Title)));
            EnsureForgelikeLibrariesAvailable(installProfile, tempRoot, downloadProvider, speedLimiter, cancelToken);

            ReportPrepareStatus(
                onStatusChanged,
                Text(
                    i18n,
                    "download.install.workflow.tasks.run_loader_installer",
                    "Running the installation steps for {loader_title}...",
                    ("loader_title", loaderChoice.Title)));
            ExecuteForgelikeProcessors(
                archive,
                installProfile,
                tempRoot,
                installerPath,
                request.MinecraftChoice,
                downloadProvider,
                speedLimiter,
                cancelToken);

            ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.copy_generated_libraries", "Copying generated support libraries..."));
            CopyDirectoryContents(Path.Combine(tempRoot, "libraries"), Path.Combine(request.LauncherDirectory, "libraries"));

            ReportPrepareStatus(onStatusChanged, Text(i18n, "download.install.workflow.tasks.read_generated_manifest", "Reading the generated installer manifest..."));
            return ReadJsonObjectFromEntry(archive, versionJsonPath);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }


    private static bool IsLegacyForgeInstallProfile(JsonObject installProfile)
    {
        return installProfile["install"] is JsonObject
               && installProfile["versionInfo"] is JsonObject;
    }


    private static void ValidateLegacyForgeInstallProfile(JsonObject installProfile, string expectedMinecraftVersion)
    {
        var profileMinecraftVersion = installProfile["install"]?["minecraft"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(profileMinecraftVersion)
            && !string.Equals(profileMinecraftVersion, expectedMinecraftVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Forge installer targets Minecraft {profileMinecraftVersion}, which does not match the selected version {expectedMinecraftVersion}.");
        }
    }

}
