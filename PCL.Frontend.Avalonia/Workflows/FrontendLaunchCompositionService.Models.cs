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
    private sealed record LibraryDownloadInfo(
        string TargetPath,
        string? DownloadUrl,
        string? Sha1);

    private sealed record LibraryCoordinate(
        string GroupId,
        string ArtifactId,
        string Version,
        string? Classifier);

    private sealed record NativeArchiveDownloadInfo(
        string TargetPath,
        string? DownloadUrl,
        string? Sha1,
        IReadOnlyList<string> ExtractExcludes);

    private readonly record struct FrontendConfiguredJavaSelection(
        bool FollowGlobal,
        string RawSelection);

    private readonly record struct FrontendJavaSelectionResult(
        FrontendJavaRuntimeSummary? Runtime,
        string? WarningMessage,
        MinecraftLaunchPrompt? CompatibilityPrompt);

    private readonly record struct FrontendRetroWrapperOptions(
        bool UseRetroWrapper,
        string? RetroWrapperPath);

    private readonly record struct FrontendProxyOptions(
        string? Scheme,
        string? Host,
        int? Port)
    {
        public static FrontendProxyOptions None { get; } = new(null, null, null);
    }

    private readonly record struct FrontendJavaWrapperOptions(
        bool IsRequested,
        string? TempDirectory,
        string? WrapperPath)
    {
        public static FrontendJavaWrapperOptions Disabled { get; } = new(false, null, null);
    }

    private sealed record FrontendNativePathPlan(
        string BaseDirectory,
        string ExtractionDirectory,
        string SearchPath,
        string? AliasDirectory);

    private sealed record FrontendVersionManifestSummary(
        bool IsVersionInfoValid,
        DateTime? ReleaseTime,
        Version? VanillaVersion,
        string? VersionType,
        string? AssetsIndexName,
        IReadOnlyList<MinecraftLaunchClasspathLibrary> Libraries,
        bool HasOptiFine,
        bool HasForge,
        string? ForgeVersion,
        string? NeoForgeVersion,
        bool HasCleanroom,
        bool HasFabric,
        string? LegacyFabricVersion,
        string? QuiltVersion,
        bool HasLiteLoader,
        bool HasLabyMod,
        int? JsonRequiredMajorVersion,
        int? MojangRecommendedMajorVersion,
        string? MojangRecommendedComponent)
    {
        public bool HasForgeLike => HasForge || !string.IsNullOrWhiteSpace(NeoForgeVersion);

        public bool HasFabricLike => HasFabric
                                     || !string.IsNullOrWhiteSpace(LegacyFabricVersion)
                                     || !string.IsNullOrWhiteSpace(QuiltVersion);

        public static FrontendVersionManifestSummary Empty { get; } = new(
            false,
            null,
            null,
            null,
            null,
            Array.Empty<MinecraftLaunchClasspathLibrary>(),
            false,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            false,
            false,
            null,
            null,
            null);
    }
}
