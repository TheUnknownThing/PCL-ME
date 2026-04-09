using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record LaunchLoginAvaloniaInputs(
    LaunchLoginProviderKind Provider,
    MicrosoftLaunchLoginAvaloniaInputs? Microsoft,
    AuthlibLaunchLoginAvaloniaInputs? Authlib);

internal enum LaunchLoginProviderKind
{
    Microsoft = 0,
    Authlib = 1
}

internal sealed record MicrosoftLaunchLoginAvaloniaInputs(
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

internal sealed record AuthlibLaunchLoginAvaloniaInputs(
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

internal sealed record LaunchLoginAvaloniaPlan(
    LaunchLoginProviderKind Provider,
    IReadOnlyList<LaunchLoginAvaloniaStepPlan> Steps,
    MinecraftLaunchProfileMutationPlan? MutationPlan);

internal sealed record LaunchLoginAvaloniaStepPlan(
    string Title,
    double Progress,
    string? Method,
    string? Url,
    string? ContentType,
    string? RequestBody,
    string? ResponseBody,
    IReadOnlyList<string> Notes);
