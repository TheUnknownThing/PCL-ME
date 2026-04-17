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
    private sealed record RecognizedModMetadata(
        string Identity,
        string Title,
        string Description,
        string Authors,
        string Version,
        string Website,
        string Loader,
        byte[]? IconBytes);

    private sealed record FrontendJavaEntry(
        string ExecutablePath,
        string DisplayName,
        bool? Is64Bit);

    private sealed record FrontendJavaPreference(
        FrontendJavaPreferenceKind Kind,
        string? Value);

    private sealed record FrontendVersionManifestSummary(
        string VanillaVersion,
        string? VersionType,
        bool HasForge,
        string? ForgeVersion,
        string? NeoForgeVersion,
        string? CleanroomVersion,
        string? FabricVersion,
        string? LegacyFabricVersion,
        string? QuiltVersion,
        string? OptiFineVersion,
        bool HasLiteLoader,
        string? LiteLoaderVersion,
        string? LabyModVersion,
        bool HasLabyMod,
        bool HasFabricApi,
        string? FabricApiVersion,
        bool HasQsl,
        string? QslVersion,
        bool HasOptiFabric,
        string? OptiFabricVersion,
        IReadOnlyList<string> LibraryNames)
    {
        public static FrontendVersionManifestSummary Empty { get; } = new(
            VanillaVersion: "Unknown",
            VersionType: null,
            HasForge: false,
            ForgeVersion: null,
            NeoForgeVersion: null,
            CleanroomVersion: null,
            FabricVersion: null,
            LegacyFabricVersion: null,
            QuiltVersion: null,
            OptiFineVersion: null,
            HasLiteLoader: false,
            LiteLoaderVersion: null,
            LabyModVersion: null,
            HasLabyMod: false,
            HasFabricApi: false,
            FabricApiVersion: null,
            HasQsl: false,
            QslVersion: null,
            HasOptiFabric: false,
            OptiFabricVersion: null,
            LibraryNames: Array.Empty<string>());
    }

    private enum ResourceKind
    {
        Mods = 0,
        DisabledMods = 1,
        ResourcePacks = 2,
        Shaders = 3,
        Schematics = 4
    }

    private enum FrontendJavaPreferenceKind
    {
        Global = 0,
        Auto = 1,
        RelativePath = 2,
        Existing = 3
    }
}
