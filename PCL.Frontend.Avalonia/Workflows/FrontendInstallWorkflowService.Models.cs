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


internal enum FrontendInstallChoiceKind
{
    Minecraft,
    Forge,
    NeoForge,
    Cleanroom,
    FabricLoader,
    LegacyFabricLoader,
    QuiltLoader,
    LabyMod,
    OptiFine,
    LiteLoader,
    ModFile
}


internal sealed record FrontendInstallChoice(
    string Id,
    string Title,
    string Summary,
    string Version,
    FrontendInstallChoiceKind Kind,
    string? ManifestUrl = null,
    string? DownloadUrl = null,
    string? FileName = null,
    JsonObject? Metadata = null);


internal sealed record FrontendInstallApplyRequest(
    string LauncherDirectory,
    string TargetInstanceName,
    int DownloadSourceIndex,
    FrontendInstallChoice MinecraftChoice,
    FrontendInstallChoice? PrimaryLoaderChoice,
    FrontendInstallChoice? LiteLoaderChoice,
    FrontendInstallChoice? OptiFineChoice,
    FrontendInstallChoice? FabricApiChoice,
    FrontendInstallChoice? LegacyFabricApiChoice,
    FrontendInstallChoice? QslChoice,
    FrontendInstallChoice? OptiFabricChoice,
    bool UseInstanceIsolation,
    bool RunRepair,
    bool ForceCoreRefresh,
    bool PreserveExistingManagedModFiles = false);


internal sealed record FrontendInstallApplyResult(
    string TargetDirectory,
    string ManifestPath,
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);


internal enum FrontendInstallApplyPhase
{
    PrepareManifest,
    DownloadSupportFiles,
    Finalize
}
