using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendLaunchComposition(
    string Scenario,
    string InstanceName,
    string InstancePath,
    string LauncherFolder,
    IReadOnlyList<FrontendLaunchArtifactRequirement> RequiredArtifacts,
    FrontendLaunchProfileSummary SelectedProfile,
    FrontendJavaRuntimeSummary? SelectedJavaRuntime,
    string? JavaWarningMessage,
    MinecraftLaunchPrompt? JavaCompatibilityPrompt,
    int LaunchCount,
    MinecraftLaunchPrecheckRequest PrecheckRequest,
    MinecraftLaunchPrecheckResult PrecheckResult,
    MinecraftLaunchPrompt? SupportPrompt,
    MinecraftLaunchJavaWorkflowPlan JavaWorkflow,
    FrontendDownloadSourcePreference DownloadSourcePreference,
    MachineType TargetJavaRuntimeArchitecture,
    FrontendJavaRuntimeInstallPlan? JavaRuntimeInstallPlan,
    MinecraftLaunchResolutionPlan ResolutionPlan,
    MinecraftLaunchClasspathPlan ClasspathPlan,
    string NativesDirectory,
    string? NativePathAliasDirectory,
    MinecraftLaunchNativesSyncRequest? NativeSyncRequest,
    MinecraftLaunchReplacementValuePlan ReplacementPlan,
    MinecraftLaunchPrerunWorkflowPlan PrerunPlan,
    MinecraftLaunchArgumentPlan ArgumentPlan,
    MinecraftLaunchSessionStartWorkflowPlan SessionStartPlan,
    MinecraftGameShellPlan PostLaunchShell,
    MinecraftLaunchNotification CompletionNotification);

internal sealed record FrontendLaunchArtifactRequirement(
    string TargetPath,
    string DownloadUrl,
    string? Sha1);

internal sealed record FrontendLaunchProfileSummary(
    MinecraftLaunchProfileKind Kind,
    string UserName,
    string? Uuid,
    string? AccessToken,
    string? ClientToken,
    string? AuthServer,
    string? AuthServerName,
    string? SkinHeadId,
    string? RawJson,
    bool HasMicrosoftProfile)
{
    public string AuthLabelKey => Kind switch
    {
        MinecraftLaunchProfileKind.Microsoft => "launch.profile.kinds.microsoft",
        MinecraftLaunchProfileKind.Auth => "launch.profile.kinds.authlib",
        MinecraftLaunchProfileKind.Legacy => "launch.profile.kinds.offline",
        _ => "launch.profile.none_selected"
    };
}

internal sealed record FrontendJavaRuntimeSummary(
    string ExecutablePath,
    string DisplayName,
    int? MajorVersion,
    bool IsEnabled,
    bool? Is64Bit,
    MachineType? Architecture);

internal sealed record FrontendJavaRuntimeInstallPlan(
    FrontendJavaRuntimeInstallPlanKind Kind,
    string SourceName,
    string DisplayName,
    string VersionName,
    string RequestedComponent,
    string PlatformKey,
    string RuntimeDirectory,
    MachineType RuntimeArchitecture,
    bool IsJre,
    JavaBrandType? Brand,
    MinecraftJavaRuntimeManifestRequestPlan? MojangManifestPlan = null,
    MinecraftJavaRuntimeDownloadTransferPlan? MojangTransferPlan = null,
    FrontendJavaRuntimeArchiveDownloadPlan? ArchivePlan = null);

internal enum FrontendJavaRuntimeInstallPlanKind
{
    MojangManifest = 0,
    ArchivePackage = 1
}

internal sealed record FrontendJavaRuntimeArchiveDownloadPlan(
    string PackageName,
    MinecraftJavaRuntimeRequestUrlPlan RequestUrls,
    long Size,
    string? Sha256,
    string? ImageType);
