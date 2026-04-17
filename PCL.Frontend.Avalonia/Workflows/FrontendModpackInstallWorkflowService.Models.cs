using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendModpackInstallRequest(
    string? SourceUrl,
    string? SourceArchivePath,
    string ArchivePath,
    string LauncherDirectory,
    int DownloadSourceIndex,
    string InstanceName,
    string TargetDirectory,
    string? ProjectId,
    string? ProjectSource,
    string? IconPath,
    string? ProjectDescription,
    int CommunitySourcePreference);

internal sealed record FrontendModpackInstallResult(
    string InstanceName,
    string TargetDirectory,
    IReadOnlyList<string> DownloadedFiles,
    IReadOnlyList<string> ReusedFiles);

internal sealed record FrontendModpackInstallStatus(
    double Progress,
    string Message,
    double? SpeedBytesPerSecond = null,
    int? RemainingFileCount = null,
    string? CurrentFileName = null);

internal sealed record FrontendModpackPackage(
    FrontendModpackPackageKind Kind,
    string MinecraftVersion,
    string? ForgeVersion,
    string? NeoForgeVersion,
    string? FabricVersion,
    string? QuiltVersion,
    string? OptiFineVersion,
    string? PackageVersion,
    string? LaunchJvmArguments,
    string? LaunchGameArguments,
    IReadOnlyList<FrontendModpackOverrideSource> OverrideSources,
    IReadOnlyList<FrontendModpackFilePlan> Files);

internal sealed record FrontendModpackOverrideSource(string RelativePath);

internal sealed record FrontendModpackFilePlan(
    string RelativeTargetPath,
    IReadOnlyList<string> DownloadUrls,
    long? Size,
    string? Sha1,
    string DisplayName)
{
    public FrontendModpackFileDownloadPlan Resolve(string targetRoot)
    {
        return new FrontendModpackFileDownloadPlan(
            Path.Combine(targetRoot, RelativeTargetPath),
            DownloadUrls,
            Size,
            Sha1,
            DisplayName);
    }
}

internal sealed record FrontendModpackFileDownloadPlan(
    string TargetPath,
    IReadOnlyList<string> DownloadUrls,
    long? Size,
    string? Sha1,
    string DisplayName);

internal sealed record FrontendCurseForgeApiCandidate(string Url, bool UseApiKey);

internal enum FrontendModpackPackageKind
{
    Unknown,
    CurseForge,
    Modrinth,
    Mcbbs
}
