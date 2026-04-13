namespace PCL.Core.Minecraft.Launch;

/// <summary>
/// 启动前预检查所需的上下文。
/// </summary>
public sealed record MinecraftLaunchPrecheckRequest(
    string InstanceName,
    string InstancePathIndie,
    string InstancePath,
    bool IsInstanceSelected,
    bool IsInstanceError,
    string? InstanceErrorDescription,
    bool IsUtf8CodePage,
    bool IsNonAsciiPathWarningDisabled,
    bool IsInstancePathAscii,
    string ProfileValidationMessage,
    MinecraftLaunchProfileKind SelectedProfileKind,
    bool HasLabyMod,
    MinecraftLaunchLoginRequirement LoginRequirement,
    string? RequiredAuthServer,
    string? SelectedAuthServer,
    bool HasMicrosoftProfile,
    bool IsRestrictedFeatureAllowed);
