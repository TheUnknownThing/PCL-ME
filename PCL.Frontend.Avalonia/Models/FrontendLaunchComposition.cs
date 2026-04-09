using PCL.Core.Minecraft.Launch;
using PCL.Core.Utils;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendLaunchComposition(
    string Scenario,
    string InstanceName,
    string InstancePath,
    IReadOnlyList<FrontendLaunchArtifactRequirement> RequiredArtifacts,
    FrontendLaunchProfileSummary SelectedProfile,
    FrontendJavaRuntimeSummary? SelectedJavaRuntime,
    string? JavaWarningMessage,
    int LaunchCount,
    MinecraftLaunchPrecheckRequest PrecheckRequest,
    MinecraftLaunchPrecheckResult PrecheckResult,
    MinecraftLaunchPrompt? SupportPrompt,
    MinecraftLaunchJavaWorkflowPlan JavaWorkflow,
    MinecraftJavaRuntimeManifestRequestPlan? JavaRuntimeManifestPlan,
    MinecraftJavaRuntimeDownloadTransferPlan? JavaRuntimeTransferPlan,
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
    public string AuthLabel => Kind switch
    {
        MinecraftLaunchProfileKind.Microsoft => "正版验证",
        MinecraftLaunchProfileKind.Auth => "外置验证",
        MinecraftLaunchProfileKind.Legacy => "离线验证",
        _ => "未选择档案"
    };

    public string IdentityLabel => Kind == MinecraftLaunchProfileKind.Auth &&
                                   !string.IsNullOrWhiteSpace(AuthServer)
        ? $"{UserName} / {AuthLabel}"
        : UserName;
}

internal sealed record FrontendJavaRuntimeSummary(
    string ExecutablePath,
    string DisplayName,
    int? MajorVersion,
    bool IsEnabled,
    bool? Is64Bit,
    MachineType? Architecture);
