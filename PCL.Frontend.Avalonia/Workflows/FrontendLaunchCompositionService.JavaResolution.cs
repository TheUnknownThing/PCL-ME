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
    private static MinecraftLaunchResolutionRequest BuildResolutionRequest(
        YamlFileProvider localConfig,
        MinecraftLaunchResolutionRequest fallback,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return fallback with
        {
            WindowMode = ReadValue(localConfig, "LaunchArgumentWindowType", fallback.WindowMode),
            CustomWidth = ReadValue(localConfig, "LaunchArgumentWindowWidth", fallback.CustomWidth),
            CustomHeight = ReadValue(localConfig, "LaunchArgumentWindowHeight", fallback.CustomHeight),
            GameVersionDrop = ResolveGameVersionDrop(manifestSummary.VanillaVersion),
            JavaMajorVersion = selectedJavaRuntime?.MajorVersion ?? javaWorkflow.RecommendedMajorVersion,
            HasOptiFine = manifestSummary.HasOptiFine,
            HasForge = manifestSummary.HasForgeLike
        };
    }

    private static MinecraftLaunchJavaWorkflowRequest BuildJavaWorkflowRequest(
        MinecraftLaunchJavaWorkflowRequest fallback,
        FrontendVersionManifestSummary manifestSummary)
    {
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: manifestSummary.IsVersionInfoValid || fallback.IsVersionInfoValid,
            ReleaseTime: manifestSummary.ReleaseTime ?? fallback.ReleaseTime,
            VanillaVersion: manifestSummary.VanillaVersion ?? fallback.VanillaVersion,
            HasOptiFine: manifestSummary.HasOptiFine,
            HasForge: manifestSummary.HasForgeLike,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabricLike,
            HasLiteLoader: manifestSummary.HasLiteLoader,
            HasLabyMod: manifestSummary.HasLabyMod,
            JsonRequiredMajorVersion: manifestSummary.JsonRequiredMajorVersion ?? fallback.JsonRequiredMajorVersion,
            MojangRecommendedMajorVersion: manifestSummary.MojangRecommendedMajorVersion ?? fallback.MojangRecommendedMajorVersion,
            MojangRecommendedComponent: manifestSummary.MojangRecommendedComponent ?? fallback.MojangRecommendedComponent);
    }

    private static MinecraftLaunchJavaWorkflowRequest CreateRuntimeJavaWorkflowFallback(FrontendVersionManifestSummary manifestSummary)
    {
        var recommendedMajorVersion = manifestSummary.MojangRecommendedMajorVersion
                                     ?? manifestSummary.JsonRequiredMajorVersion
                                     ?? 8;
        var inferredReleaseTime = InferJavaRequirementReleaseTime(manifestSummary.VanillaVersion);
        return new MinecraftLaunchJavaWorkflowRequest(
            IsVersionInfoValid: manifestSummary.IsVersionInfoValid,
            ReleaseTime: manifestSummary.ReleaseTime ?? inferredReleaseTime ?? DateTime.Now,
            VanillaVersion: manifestSummary.VanillaVersion ?? new Version(1, 20, 1),
            HasOptiFine: manifestSummary.HasOptiFine,
            HasForge: manifestSummary.HasForgeLike,
            ForgeVersion: manifestSummary.ForgeVersion,
            HasCleanroom: manifestSummary.HasCleanroom,
            HasFabric: manifestSummary.HasFabricLike,
            HasLiteLoader: manifestSummary.HasLiteLoader,
            HasLabyMod: manifestSummary.HasLabyMod,
            JsonRequiredMajorVersion: manifestSummary.JsonRequiredMajorVersion ?? recommendedMajorVersion,
            MojangRecommendedMajorVersion: recommendedMajorVersion,
            MojangRecommendedComponent: manifestSummary.MojangRecommendedComponent ?? "jre-legacy");
    }

    private static MinecraftLaunchResolutionRequest CreateRuntimeResolutionFallback()
    {
        return new MinecraftLaunchResolutionRequest(
            WindowMode: (int)GameWindowSizeMode.Default,
            LauncherWindowWidth: null,
            LauncherWindowHeight: null,
            LauncherTitleBarHeight: 0,
            CustomWidth: 854,
            CustomHeight: 480,
            GameVersionDrop: 0,
            JavaMajorVersion: 8,
            JavaRevision: 0,
            HasOptiFine: false,
            HasForge: false,
            DpiScale: 1);
    }

    private static FrontendLaunchProfileSummary ReadSelectedProfile(FrontendRuntimePaths runtimePaths)
    {
        try
        {
            var profileDocument = FrontendProfileStorageService.Load(runtimePaths).Document;
            var document = profileDocument;
            var selectedProfile = document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
                ? document.Profiles[document.LastUsedProfile]
                : document.Profiles.FirstOrDefault();
            if (selectedProfile is null)
            {
                return BuildFallbackProfile(runtimePaths);
            }

            var kind = selectedProfile.Kind switch
            {
                MinecraftLaunchStoredProfileKind.Microsoft => MinecraftLaunchProfileKind.Microsoft,
                MinecraftLaunchStoredProfileKind.Authlib => MinecraftLaunchProfileKind.Auth,
                _ => MinecraftLaunchProfileKind.Legacy
            };

            return new FrontendLaunchProfileSummary(
                kind,
                string.IsNullOrWhiteSpace(selectedProfile.Username) ? "No profile selected" : selectedProfile.Username,
                selectedProfile.Uuid,
                selectedProfile.AccessToken,
                selectedProfile.ClientToken,
                selectedProfile.Server,
                selectedProfile.ServerName,
                selectedProfile.SkinHeadId,
                selectedProfile.RawJson,
                document.Profiles.Any(profile => profile.Kind == MinecraftLaunchStoredProfileKind.Microsoft));
        }
        catch
        {
            return BuildFallbackProfile(runtimePaths);
        }
    }

    private static FrontendLaunchProfileSummary BuildFallbackProfile(FrontendRuntimePaths runtimePaths)
    {
        var legacyName = LauncherFrontendRuntimeStateService.TryReadProtectedString(
            runtimePaths.SharedConfigDirectory,
            runtimePaths.SharedConfigPath,
            "LoginLegacyName");
        return new FrontendLaunchProfileSummary(
            string.IsNullOrWhiteSpace(legacyName) ? MinecraftLaunchProfileKind.None : MinecraftLaunchProfileKind.Legacy,
            string.IsNullOrWhiteSpace(legacyName) ? "No profile selected" : legacyName,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            HasMicrosoftProfile: false);
    }

    private static FrontendJavaSelectionResult ResolveJavaRuntime(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig,
        YamlFileProvider? instanceConfig,
        string launcherFolder,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        bool ignoreJavaCompatibilityWarningOnce)
    {
        var selectedJavaPath = ResolveConfiguredJavaSelection(sharedConfig, instanceConfig, launcherFolder);
        var ignoreJavaCompatibilityWarning = ignoreJavaCompatibilityWarningOnce ||
                                            instanceConfig is not null &&
                                            ReadValue(instanceConfig, "VersionAdvanceJava", false);
        var javaEntries = FrontendJavaInventoryService.ParseAvailableRuntimes(ReadValue(localConfig, "LaunchArgumentJavaUser", "[]"));
        MinecraftLaunchPrompt? compatibilityPrompt = null;

        if (!string.IsNullOrWhiteSpace(selectedJavaPath))
        {
            var selectedRuntime = ResolveConfiguredRuntime(javaEntries, selectedJavaPath, javaWorkflow);
            if (selectedRuntime is not null)
            {
                var isCompatible = IsCompatibleWithWorkflow(selectedRuntime, manifestSummary, javaWorkflow);
                if (ShouldUseConfiguredRuntime(selectedRuntime, manifestSummary, javaWorkflow, ignoreJavaCompatibilityWarning))
                {
                    return new FrontendJavaSelectionResult(
                        ToRuntimeSummary(selectedRuntime),
                        ignoreJavaCompatibilityWarning && !isCompatible
                            ? BuildIgnoredJavaCompatibilityWarning(selectedRuntime, javaWorkflow)
                            : null,
                        CompatibilityPrompt: null);
                }

                compatibilityPrompt = BuildJavaCompatibilityPrompt(selectedRuntime, javaWorkflow);
            }
        }

        var autoEntry = SelectCompatibleRuntime(javaEntries, manifestSummary, javaWorkflow);
        if (autoEntry is not null)
        {
            return new FrontendJavaSelectionResult(ToRuntimeSummary(autoEntry), null, compatibilityPrompt);
        }

        var bundledJava = OperatingSystem.IsWindows()
            ? Path.Combine(launcherFolder, "runtime", "java", "bin", "java.exe")
            : Path.Combine(launcherFolder, "runtime", "java", "bin", "java");
        if (TryResolveCompatibleRuntime(
                bundledJava,
                manifestSummary,
                javaWorkflow,
                fallbackDisplayName: $"Java {javaWorkflow.RecommendedMajorVersion}") is { } bundledRuntime)
        {
            return new FrontendJavaSelectionResult(bundledRuntime, null, compatibilityPrompt);
        }

        return new FrontendJavaSelectionResult(ProbeHostJavaRuntime(manifestSummary, javaWorkflow), null, compatibilityPrompt);
    }

    private static int ResolveGameVersionDrop(Version? vanillaVersion)
    {
        if (vanillaVersion is null)
        {
            return 0;
        }

        return vanillaVersion.Major == 1
            ? vanillaVersion.Minor * 10
            : vanillaVersion.Major * 10 + vanillaVersion.Minor;
    }

    private static FrontendJavaRuntimeInstallPlan? BuildJavaRuntimeInstallPlan(
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        FrontendVersionManifestSummary manifestSummary,
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        string launcherFolder,
        FrontendDownloadProvider downloadProvider)
    {
        if (string.IsNullOrWhiteSpace(javaWorkflow.MissingJavaPrompt.DownloadTarget))
        {
            return null;
        }

        var runtimeArchitecture = ResolveTargetJavaArchitecture(selectedJavaRuntime, manifestSummary);
        var platformKind = GetCurrentDesktopPlatformKind();
        if (ShouldPreferAdoptiumForJavaRuntime(platformKind, runtimeArchitecture))
        {
            return TryBuildAdoptiumJavaRuntimeInstallPlan(
                       javaWorkflow,
                       launcherFolder,
                       platformKind,
                       runtimeArchitecture,
                       downloadProvider)
                   ?? TryBuildMojangJavaRuntimeInstallPlan(
                       javaWorkflow,
                       launcherFolder,
                       runtimeArchitecture,
                       downloadProvider);
        }

        return TryBuildMojangJavaRuntimeInstallPlan(
                   javaWorkflow,
                   launcherFolder,
                   runtimeArchitecture,
                   downloadProvider)
               ?? TryBuildAdoptiumJavaRuntimeInstallPlan(
                   javaWorkflow,
                   launcherFolder,
                   platformKind,
                   runtimeArchitecture,
                   downloadProvider);
    }

    private static FrontendJavaRuntimeInstallPlan? TryBuildMojangJavaRuntimeInstallPlan(
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        string launcherFolder,
        MachineType runtimeArchitecture,
        FrontendDownloadProvider downloadProvider)
    {
        var downloadTarget = javaWorkflow.MissingJavaPrompt.DownloadTarget;
        if (string.IsNullOrWhiteSpace(downloadTarget))
        {
            return null;
        }

        try
        {
            var indexRequestUrls = MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultIndexRequestUrlPlan();
            var liveIndexJson = TryDownloadUtf8String(downloadProvider.GetPreferredUrls(
                indexRequestUrls.OfficialUrls,
                indexRequestUrls.MirrorUrls));
            if (string.IsNullOrWhiteSpace(liveIndexJson))
            {
                return null;
            }

            var manifestPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildManifestRequestPlan(
                new MinecraftJavaRuntimeManifestRequestPlanRequest(
                    liveIndexJson,
                    ResolveJavaRuntimePlatformKey(runtimeArchitecture),
                    downloadTarget,
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultManifestUrlRewrites()));
            var runtimeBaseDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
                launcherFolder,
                manifestPlan.Selection.ComponentKey);
            var manifestJson = TryDownloadUtf8String(downloadProvider.GetPreferredUrls(
                manifestPlan.RequestUrls.OfficialUrls,
                manifestPlan.RequestUrls.MirrorUrls));
            if (string.IsNullOrWhiteSpace(manifestJson))
            {
                return null;
            }

            var workflowPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildDownloadWorkflowPlan(
                new MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
                    manifestJson,
                    runtimeBaseDirectory,
                    MinecraftJavaRuntimeDownloadSessionService.GetDefaultIgnoredSha1Hashes(),
                    MinecraftJavaRuntimeDownloadWorkflowService.GetDefaultFileUrlRewrites()));
            var existingRelativePaths = workflowPlan.Files
                .Where(file => File.Exists(file.TargetPath))
                .Select(file => file.RelativePath)
                .ToArray();
            var transferPlan = MinecraftJavaRuntimeDownloadWorkflowService.BuildTransferPlan(
                new MinecraftJavaRuntimeDownloadTransferPlanRequest(
                    workflowPlan,
                    existingRelativePaths));

            return new FrontendJavaRuntimeInstallPlan(
                FrontendJavaRuntimeInstallPlanKind.MojangManifest,
                SourceName: "Mojang",
                DisplayName: $"Java {manifestPlan.Selection.VersionName}",
                VersionName: manifestPlan.Selection.VersionName,
                RequestedComponent: manifestPlan.Selection.RequestedComponent,
                PlatformKey: manifestPlan.Selection.PlatformKey,
                RuntimeDirectory: runtimeBaseDirectory,
                RuntimeArchitecture: runtimeArchitecture,
                IsJre: true,
                Brand: JavaBrandType.OpenJDK,
                MojangManifestPlan: manifestPlan,
                MojangTransferPlan: transferPlan);
        }
        catch
        {
            return null;
        }
    }

    private static FrontendJavaRuntimeInstallPlan? TryBuildAdoptiumJavaRuntimeInstallPlan(
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        string launcherFolder,
        FrontendDesktopPlatformKind platformKind,
        MachineType runtimeArchitecture,
        FrontendDownloadProvider downloadProvider)
    {
        var osToken = ResolveAdoptiumOperatingSystemToken(platformKind);
        var architectureToken = ResolveAdoptiumArchitectureToken(runtimeArchitecture);
        if (string.IsNullOrWhiteSpace(osToken) || string.IsNullOrWhiteSpace(architectureToken))
        {
            return null;
        }

        foreach (var imageType in new[] { "jre", "jdk" })
        {
            try
            {
                var metadataJson = TryDownloadUtf8String(
                    [
                        BuildAdoptiumMetadataUrl(
                            javaWorkflow.RecommendedMajorVersion,
                            osToken,
                            architectureToken,
                            imageType)
                    ]);
                if (string.IsNullOrWhiteSpace(metadataJson))
                {
                    continue;
                }

                var plan = BuildAdoptiumJavaRuntimeInstallPlanFromMetadata(
                    metadataJson,
                    launcherFolder,
                    javaWorkflow.MissingJavaPrompt.DownloadTarget!,
                    javaWorkflow.RecommendedMajorVersion,
                    platformKind,
                    runtimeArchitecture,
                    imageType);
                if (plan is not null)
                {
                    return plan;
                }
            }
            catch
            {
                // Try the next image type.
            }
        }

        return null;
    }

    private static string? TryDownloadUtf8String(IReadOnlyList<string> urls)
    {
        foreach (var url in urls)
        {
            try
            {
                return JavaRuntimeHttpClient.GetStringAsync(url).GetAwaiter().GetResult();
            }
            catch
            {
                // Try the next mirror.
            }
        }

        return null;
    }

    internal static string ResolveJavaRuntimePlatformKeyForPlatform(
        FrontendDesktopPlatformKind platformKind,
        MachineType runtimeArchitecture)
    {
        if (platformKind == FrontendDesktopPlatformKind.Windows)
        {
            return runtimeArchitecture switch
            {
                MachineType.ARM64 => "windows-arm64",
                MachineType.I386 => "windows-x86",
                _ => "windows-x64"
            };
        }

        if (platformKind == FrontendDesktopPlatformKind.MacOS)
        {
            return runtimeArchitecture == MachineType.ARM64
                ? "mac-os-arm64"
                : "mac-os";
        }

        return runtimeArchitecture switch
        {
            // Mojang's Java runtime index currently exposes "linux" and "linux-i386",
            // but not a dedicated "linux-arm64" key.
            MachineType.ARM64 => "linux",
            MachineType.I386 => "linux-i386",
            _ => "linux"
        };
    }

    private static string ResolveJavaRuntimePlatformKey(MachineType runtimeArchitecture)
    {
        return ResolveJavaRuntimePlatformKeyForPlatform(GetCurrentDesktopPlatformKind(), runtimeArchitecture);
    }

    private static FrontendDesktopPlatformKind GetCurrentDesktopPlatformKind()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? FrontendDesktopPlatformKind.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? FrontendDesktopPlatformKind.MacOS
                : FrontendDesktopPlatformKind.Linux;
    }

    private static string NormalizeSelectedJavaPath(string rawValue)
    {
        var trimmed = rawValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return trimmed;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var path = GetString(document.RootElement, "Path");
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.Combine(path, OperatingSystem.IsWindows() ? "java.exe" : "java");
        }
        catch
        {
            return trimmed;
        }
    }

    internal static FrontendJavaRuntimeInstallPlan? BuildAdoptiumJavaRuntimeInstallPlanFromMetadata(
        string metadataJson,
        string launcherFolder,
        string requestedComponent,
        int majorVersion,
        FrontendDesktopPlatformKind platformKind,
        MachineType runtimeArchitecture,
        string imageType)
    {
        using var document = JsonDocument.Parse(metadataJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var release = document.RootElement.EnumerateArray().FirstOrDefault();
        if (release.ValueKind != JsonValueKind.Object ||
            !release.TryGetProperty("binary", out var binary) ||
            binary.ValueKind != JsonValueKind.Object ||
            !binary.TryGetProperty("package", out var package) ||
            package.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var packageName = GetString(package, "name");
        var downloadLink = GetString(package, "link");
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(downloadLink))
        {
            return null;
        }

        var versionName = GetNestedString(release, "version", "semver")
                          ?? GetNestedString(release, "version", "openjdk_version")
                          ?? GetString(release, "release_name")
                          ?? $"Java {majorVersion}";
        var osToken = ResolveAdoptiumOperatingSystemToken(platformKind);
        var architectureToken = ResolveAdoptiumArchitectureToken(runtimeArchitecture) ?? runtimeArchitecture.ToString().ToLowerInvariant();
        var componentKey = $"adoptium-{imageType}-{majorVersion}-{osToken}-{architectureToken}";
        var runtimeDirectory = MinecraftJavaRuntimeDownloadSessionService.GetRuntimeBaseDirectory(
            launcherFolder,
            componentKey);
        return new FrontendJavaRuntimeInstallPlan(
            FrontendJavaRuntimeInstallPlanKind.ArchivePackage,
            SourceName: "Adoptium",
            DisplayName: $"Java {versionName}",
            VersionName: versionName,
            RequestedComponent: requestedComponent,
            PlatformKey: $"{osToken}-{architectureToken}",
            RuntimeDirectory: runtimeDirectory,
            RuntimeArchitecture: runtimeArchitecture,
            IsJre: string.Equals(imageType, "jre", StringComparison.OrdinalIgnoreCase),
            Brand: JavaBrandType.EclipseTemurin,
            ArchivePlan: new FrontendJavaRuntimeArchiveDownloadPlan(
                packageName,
                new MinecraftJavaRuntimeRequestUrlPlan([downloadLink], []),
                GetLong(package, "size") ?? 0L,
                GetString(package, "checksum"),
                imageType));
    }

    internal static string BuildAdoptiumMetadataUrl(
        int majorVersion,
        string osToken,
        string architectureToken,
        string imageType)
    {
        return $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot?architecture={Uri.EscapeDataString(architectureToken)}&heap_size=normal&image_type={Uri.EscapeDataString(imageType)}&jvm_impl=hotspot&os={Uri.EscapeDataString(osToken)}&page_size=1&project=jdk&vendor=eclipse";
    }

    internal static string ResolveAdoptiumOperatingSystemToken(FrontendDesktopPlatformKind platformKind)
    {
        return platformKind switch
        {
            FrontendDesktopPlatformKind.Windows => "windows",
            FrontendDesktopPlatformKind.MacOS => "mac",
            _ => "linux"
        };
    }

    internal static string? ResolveAdoptiumArchitectureToken(MachineType runtimeArchitecture)
    {
        return runtimeArchitecture switch
        {
            MachineType.AMD64 => "x64",
            MachineType.I386 => "x32",
            MachineType.ARM64 => "aarch64",
            _ => null
        };
    }

    internal static bool ShouldPreferAdoptiumForJavaRuntime(
        FrontendDesktopPlatformKind platformKind,
        MachineType runtimeArchitecture)
    {
        return platformKind == FrontendDesktopPlatformKind.Linux &&
               runtimeArchitecture == MachineType.ARM64;
    }

    private static string ResolveConfiguredJavaSelection(
        JsonFileProvider sharedConfig,
        YamlFileProvider? instanceConfig,
        string launcherFolder)
    {
        if (instanceConfig is null || !instanceConfig.Exists("VersionArgumentJavaSelect"))
        {
            return NormalizeSelectedJavaPath(ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty));
        }

        var instanceSelection = ReadInstanceJavaSelection(
            ReadValue(instanceConfig, "VersionArgumentJavaSelect", string.Empty),
            launcherFolder);
        return instanceSelection.FollowGlobal
            ? NormalizeSelectedJavaPath(ReadValue(sharedConfig, "LaunchArgumentJavaSelect", string.Empty))
            : NormalizeSelectedJavaPath(instanceSelection.RawSelection);
    }

    private static FrontendConfiguredJavaSelection ReadInstanceJavaSelection(string rawValue, string launcherFolder)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || string.Equals(rawValue, "使用全局设置", StringComparison.Ordinal))
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty);
        }

        if (!rawValue.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: false, RawSelection: rawValue);
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            var kind = GetString(document.RootElement, "kind")?.ToLowerInvariant();
            return kind switch
            {
                "auto" => new FrontendConfiguredJavaSelection(FollowGlobal: false, RawSelection: string.Empty),
                "exist" => new FrontendConfiguredJavaSelection(
                    FollowGlobal: false,
                    RawSelection: GetString(document.RootElement, "JavaExePath") ?? string.Empty),
                "relative" => new FrontendConfiguredJavaSelection(
                    FollowGlobal: false,
                    RawSelection: Path.Combine(launcherFolder, GetString(document.RootElement, "RelativePath") ?? string.Empty)),
                _ => new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty)
            };
        }
        catch
        {
            return new FrontendConfiguredJavaSelection(FollowGlobal: true, RawSelection: string.Empty);
        }
    }

    private static FrontendJavaRuntimeSummary? ProbeHostJavaRuntime(
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        lock (HostJavaProbeLock)
        {
            if (IsHostJavaProbeCached)
            {
                return SelectCompatibleRuntime(CachedHostJavaRuntimes ?? [], manifestSummary, javaWorkflow) is { } cachedRuntime
                    ? ToRuntimeSummary(cachedRuntime)
                    : null;
            }

            var runtimes = new List<FrontendStoredJavaRuntime>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in EnumerateHostJavaRuntimeCandidates())
            {
                var runtime = TryProbeJavaRuntime(candidate);
                if (runtime is not null)
                {
                    if (seenPaths.Add(runtime.ExecutablePath))
                    {
                        runtimes.Add(runtime);
                    }
                }
            }

            IsHostJavaProbeCached = true;
            CachedHostJavaRuntimes = runtimes;
            return SelectCompatibleRuntime(runtimes, manifestSummary, javaWorkflow) is { } resolvedRuntime
                ? ToRuntimeSummary(resolvedRuntime)
                : null;
        }
    }

    private static IReadOnlyList<string> EnumerateHostJavaRuntimeCandidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["java.exe"];
        }

        var candidates = new List<string>();
        void AddCandidate(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            if (candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            candidates.Add(candidate);
        }

        AddCandidate("/opt/homebrew/opt/openjdk/bin/java");
        AddCandidate("/usr/local/opt/openjdk/bin/java");

        foreach (var directory in EnumerateJavaHomeDirectories("/Library/Java/JavaVirtualMachines"))
        {
            AddCandidate(Path.Combine(directory, "Contents", "Home", "bin", "java"));
        }

        foreach (var directory in EnumerateJavaHomeDirectories(Path.Combine(
                     Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     "Library",
                     "Java",
                     "JavaVirtualMachines")))
        {
            AddCandidate(Path.Combine(directory, "Contents", "Home", "bin", "java"));
        }

        foreach (var candidate in ResolveWhichJavaCandidates())
        {
            AddCandidate(candidate);
        }

        AddCandidate("java");
        AddCandidate("/usr/bin/java");
        return candidates;
    }

    private static IEnumerable<string> EnumerateJavaHomeDirectories(string rootPath)
    {
        try
        {
            return Directory.Exists(rootPath)
                ? Directory.EnumerateDirectories(rootPath)
                : [];
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> ResolveWhichJavaCandidates()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-lc \"which -a java 2>/dev/null\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return [];
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore best-effort probe cleanup.
                }

                return [];
            }

            return process.StandardOutput
                .ReadToEnd()
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static FrontendStoredJavaRuntime? TryProbeJavaRuntime(string executablePath)
    {
        if (FrontendJavaInventoryService.TryResolveRuntime(executablePath) is { ParsedVersion: not null } resolvedRuntime)
        {
            var majorVersion = resolvedRuntime.MajorVersion ?? GetMajorVersion(resolvedRuntime.ParsedVersion);
            return resolvedRuntime with
            {
                DisplayName = majorVersion is { } major ? $"Java {major}" : resolvedRuntime.DisplayName
            };
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "-XshowSettings:properties -version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore best-effort probe cleanup.
                }

                return null;
            }

            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            var parsedVersion = TryParseJavaVersion(output);
            var majorVersion = GetMajorVersion(parsedVersion);
            if (process.ExitCode != 0 ||
                parsedVersion is null ||
                majorVersion is null ||
                output.Contains("Unable to locate a Java Runtime", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("No Java runtime present", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var resolvedExecutablePath = ResolveProbedJavaExecutablePath(executablePath, process);
            if (FrontendJavaInventoryService.TryResolveRuntime(
                    resolvedExecutablePath,
                    fallbackDisplayName: $"Java {majorVersion.Value}") is { ParsedVersion: not null } probedRuntime)
            {
                return probedRuntime with
                {
                    DisplayName = $"Java {majorVersion.Value}"
                };
            }

            return new FrontendStoredJavaRuntime(
                resolvedExecutablePath,
                $"Java {majorVersion.Value}",
                parsedVersion,
                majorVersion.Value,
                IsEnabled: true,
                Is64Bit: TryParseJavaBitness(output),
                IsJre: null,
                Brand: null,
                Architecture: TryParseJavaArchitecture(output));
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveProbedJavaExecutablePath(string executablePath, Process process)
    {
        if (!string.IsNullOrWhiteSpace(executablePath) && Path.IsPathRooted(executablePath))
        {
            return executablePath;
        }

        try
        {
            var mainModulePath = process.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModulePath))
            {
                return mainModulePath;
            }
        }
        catch
        {
            // Some runtimes may block reading MainModule metadata.
        }

        return executablePath;
    }

    private static string ResolveJavaFolderPath(string? executablePath, string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return fallbackDirectory;
        }

        var folder = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(folder)
            ? fallbackDirectory
            : folder;
    }

    private static FrontendJavaRuntimeSummary? TryResolveCompatibleRuntime(
        string executablePath,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        string? fallbackDisplayName = null)
    {
        var runtime = FrontendJavaInventoryService.TryResolveRuntime(executablePath, fallbackDisplayName: fallbackDisplayName);
        return runtime is not null && IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow)
            ? ToRuntimeSummary(runtime)
            : null;
    }

    private static FrontendStoredJavaRuntime? ResolveConfiguredRuntime(
        IReadOnlyList<FrontendStoredJavaRuntime> javaEntries,
        string selectedJavaPath,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        var selectedEntry = javaEntries.FirstOrDefault(entry =>
            string.Equals(entry.ExecutablePath, selectedJavaPath, StringComparison.OrdinalIgnoreCase));
        if (selectedEntry is not null)
        {
            return selectedEntry.IsEnabled ? selectedEntry : null;
        }

        if (!File.Exists(selectedJavaPath))
        {
            return null;
        }

        return FrontendJavaInventoryService.TryResolveRuntime(
                   selectedJavaPath,
                   isEnabled: true,
                   fallbackDisplayName: Path.GetFileName(Path.GetDirectoryName(selectedJavaPath)) ?? $"Java {javaWorkflow.RecommendedMajorVersion}")
               ?? new FrontendStoredJavaRuntime(
                   selectedJavaPath,
                   Path.GetFileName(Path.GetDirectoryName(selectedJavaPath)) ?? $"Java {javaWorkflow.RecommendedMajorVersion}",
                   ParsedVersion: null,
                   MajorVersion: null,
                   IsEnabled: true,
                   Is64Bit: null,
                   IsJre: null,
                   Brand: null,
                   Architecture: null);
    }

    private static bool ShouldUseConfiguredRuntime(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow,
        bool ignoreJavaCompatibilityWarning)
    {
        return runtime.IsEnabled &&
               (ignoreJavaCompatibilityWarning || IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow));
    }

    private static FrontendStoredJavaRuntime? SelectCompatibleRuntime(
        IEnumerable<FrontendStoredJavaRuntime> runtimes,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return runtimes
            .Where(runtime => runtime.IsEnabled && IsCompatibleWithWorkflow(runtime, manifestSummary, javaWorkflow))
            .OrderBy(runtime => runtime.MajorVersion ?? int.MaxValue)
            .ThenBy(runtime => GetArchitecturePreference(runtime, manifestSummary))
            .ThenBy(runtime => runtime.IsJre ?? true)
            .ThenBy(runtime => runtime.Brand ?? JavaBrandType.Unknown)
            .ThenByDescending(runtime => runtime.ParsedVersion)
            .FirstOrDefault();
    }

    private static bool IsCompatibleWithWorkflow(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary,
        MinecraftLaunchJavaWorkflowPlan javaWorkflow)
    {
        return runtime.ParsedVersion is not null &&
               JavaManager.IsVersionSuitable(runtime.ParsedVersion, javaWorkflow.MinimumVersion, javaWorkflow.MaximumVersion) &&
               IsArchitectureCompatible(runtime, manifestSummary) &&
               !ViolatesLegacyLinuxJavaConstraint(runtime, manifestSummary) &&
               !ViolatesLegacyLaunchWrapperConstraint(runtime, manifestSummary);
    }

    private static int GetArchitecturePreference(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (runtime.Architecture is null or MachineType.Unknown)
        {
            return 2;
        }

        if (ShouldForceX86Java(manifestSummary))
        {
            return runtime.Architecture == MachineType.I386 ? 0 : 1;
        }

        return runtime.Architecture == GetPreferredMachineType()
            ? 0
            : 1;
    }

    private static bool IsArchitectureCompatible(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (runtime.Architecture is null or MachineType.Unknown)
        {
            return true;
        }

        if (ShouldForceX86Java(manifestSummary))
        {
            return runtime.Architecture == MachineType.I386;
        }

        return runtime.Architecture == GetPreferredMachineType();
    }

    private static DateTime? InferJavaRequirementReleaseTime(Version? vanillaVersion)
    {
        if (vanillaVersion is null)
        {
            return null;
        }

        if (vanillaVersion >= new Version(1, 20, 5))
        {
            return new DateTime(2024, 4, 2);
        }

        if (vanillaVersion >= new Version(1, 18))
        {
            return new DateTime(2021, 11, 16);
        }

        if (vanillaVersion >= new Version(1, 17))
        {
            return new DateTime(2021, 5, 11);
        }

        return new DateTime(2011, 11, 18);
    }

    private static bool ShouldForceX86Java(FrontendVersionManifestSummary manifestSummary)
    {
        if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
        {
            return false;
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            return false;
        }

        return manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion < new Version(1, 6);
    }

    private static MachineType GetPreferredMachineType()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => MachineType.I386,
            Architecture.X64 => MachineType.AMD64,
            Architecture.Arm64 => MachineType.ARM64,
            _ => MachineType.Unknown
        };
    }

    private static MachineType ResolveTargetJavaArchitecture(
        FrontendJavaRuntimeSummary? selectedJavaRuntime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (selectedJavaRuntime?.Architecture is { } runtimeArchitecture &&
            runtimeArchitecture != MachineType.Unknown)
        {
            return runtimeArchitecture;
        }

        return ShouldForceX86Java(manifestSummary)
            ? MachineType.I386
            : GetPreferredMachineType();
    }

    private static bool ViolatesLegacyLinuxJavaConstraint(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        return OperatingSystem.IsLinux() &&
               RuntimeInformation.OSArchitecture == Architecture.X64 &&
               !manifestSummary.HasCleanroom &&
               manifestSummary.VanillaVersion is not null &&
               manifestSummary.VanillaVersion <= new Version(1, 12, 999) &&
               runtime.Architecture == MachineType.AMD64 &&
               (runtime.MajorVersion ?? int.MaxValue) > 8;
    }

    private static bool ViolatesLegacyLaunchWrapperConstraint(
        FrontendStoredJavaRuntime runtime,
        FrontendVersionManifestSummary manifestSummary)
    {
        if (manifestSummary.VanillaVersion is null || manifestSummary.VanillaVersion > new Version(1, 12, 999))
        {
            return false;
        }

        var launchWrapperVersion = ExtractLibraryVersion(manifestSummary.Libraries, "net.minecraft:launchwrapper");
        if (!Version.TryParse(launchWrapperVersion, out var parsedLaunchWrapperVersion))
        {
            return false;
        }

        return parsedLaunchWrapperVersion < new Version(1, 13) &&
               (runtime.MajorVersion ?? int.MaxValue) > 8;
    }

    private static FrontendJavaRuntimeSummary ToRuntimeSummary(FrontendStoredJavaRuntime runtime)
    {
        return new FrontendJavaRuntimeSummary(
            runtime.ExecutablePath,
            runtime.DisplayName,
            runtime.MajorVersion,
            runtime.IsEnabled,
            runtime.Is64Bit,
            runtime.Architecture);
    }

    private static int? GetMajorVersion(Version? version)
    {
        if (version is null)
        {
            return null;
        }

        return version.Major == 1 ? version.Minor : version.Major;
    }

    private static Version? TryParseJavaVersion(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var versionStart = output.IndexOf('"');
        if (versionStart < 0)
        {
            return null;
        }

        var versionEnd = output.IndexOf('"', versionStart + 1);
        if (versionEnd <= versionStart)
        {
            return null;
        }

        var rawVersion = output[(versionStart + 1)..versionEnd];
        var matches = System.Text.RegularExpressions.Regex.Matches(rawVersion, @"\d+");
        if (matches.Count == 0)
        {
            return null;
        }

        var parts = new int[Math.Min(4, matches.Count)];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = int.Parse(matches[i].Value);
        }

        return parts.Length switch
        {
            1 => new Version(parts[0], 0),
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            _ => new Version(parts[0], parts[1], parts[2], parts[3])
        };
    }

    private static bool? TryParseJavaBitness(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        if (TryParseJavaArchitecture(output) is { } architecture)
        {
            return architecture is MachineType.AMD64 or MachineType.ARM64 or MachineType.IA64;
        }

        if (output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (output.Contains("32-Bit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static MachineType? TryParseJavaArchitecture(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = rawLine[..separatorIndex].Trim();
            if (!string.Equals(key, "os.arch", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return MapJavaArchitecture(rawLine[(separatorIndex + 1)..].Trim().Trim('"'));
        }

        return null;
    }

    private static MachineType? MapJavaArchitecture(string? architectureText)
    {
        return architectureText?.ToLowerInvariant() switch
        {
            "x86_64" or "amd64" => MachineType.AMD64,
            "aarch64" or "arm64" => MachineType.ARM64,
            "x86" or "i386" or "i486" or "i586" or "i686" => MachineType.I386,
            "arm" => MachineType.ARM,
            _ => null
        };
    }

}
