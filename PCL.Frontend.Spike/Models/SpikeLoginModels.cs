using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Spike.Models;

internal sealed record LaunchLoginSpikeInputs(
    LaunchLoginProviderKind Provider,
    MicrosoftLaunchLoginSpikeInputs? Microsoft,
    AuthlibLaunchLoginSpikeInputs? Authlib);

internal enum LaunchLoginProviderKind
{
    Microsoft = 0,
    Authlib = 1
}

internal sealed record MicrosoftLaunchLoginSpikeInputs(
    MinecraftLaunchMicrosoftSessionReuseRequest SessionReuseRequest,
    string OAuthRefreshToken,
    string OAuthRefreshResponseJson,
    string XboxLiveResponseJson,
    string XstsResponseJson,
    string MinecraftAccessTokenResponseJson,
    string OwnershipResponseJson,
    string ProfileResponseJson,
    bool IsCreatingProfile,
    int? SelectedProfileIndex,
    IReadOnlyList<MinecraftLaunchStoredProfile> Profiles);

internal sealed record AuthlibLaunchLoginSpikeInputs(
    bool ForceReselectProfile,
    string? CachedProfileId,
    string? ServerSelectedProfileId,
    bool IsExistingProfile,
    int? SelectedProfileIndex,
    string ServerBaseUrl,
    string LoginName,
    string Password,
    string AuthenticateResponseJson,
    string RefreshResponseJson,
    string MetadataResponseJson);

internal sealed record LaunchLoginSpikePlan(
    LaunchLoginProviderKind Provider,
    IReadOnlyList<LaunchLoginSpikeStepPlan> Steps,
    MinecraftLaunchProfileMutationPlan? MutationPlan);

internal sealed record LaunchLoginSpikeStepPlan(
    string Title,
    double Progress,
    string? Method,
    string? Url,
    string? ContentType,
    string? RequestBody,
    string? ResponseBody,
    IReadOnlyList<string> Notes);
