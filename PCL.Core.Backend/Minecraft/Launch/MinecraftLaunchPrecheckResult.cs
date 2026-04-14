using PCL.Core.App.I18n;

namespace PCL.Core.Minecraft.Launch;

/// <summary>
/// 启动前预检查的结果。
/// </summary>
public sealed record MinecraftLaunchPrecheckResult(
    MinecraftLaunchPrecheckFailure? Failure,
    IReadOnlyList<MinecraftLaunchPrompt> Prompts)
{
    public bool IsSuccess => Failure is null;
}

public sealed record MinecraftLaunchPrecheckFailure(
    MinecraftLaunchPrecheckFailureKind Kind,
    string? Path = null,
    string? Detail = null)
{
    public I18nText ToLocalizedText()
    {
        return Kind switch
        {
            MinecraftLaunchPrecheckFailureKind.InstancePathContainsReservedCharacters => I18nText.WithArgs(
                "launch.precheck.failures.instance_path_reserved_chars",
                I18nTextArgument.String("path", Path)),
            MinecraftLaunchPrecheckFailureKind.InstanceIndiePathContainsReservedCharacters => I18nText.WithArgs(
                "launch.precheck.failures.instance_indie_path_reserved_chars",
                I18nTextArgument.String("path", Path)),
            MinecraftLaunchPrecheckFailureKind.InstanceNotSelected => I18nText.Plain("launch.precheck.failures.instance_not_selected"),
            MinecraftLaunchPrecheckFailureKind.InstanceHasError => I18nText.WithArgs(
                "launch.precheck.failures.instance_has_error",
                I18nTextArgument.String("detail", Detail)),
            MinecraftLaunchPrecheckFailureKind.NoProfileSelected => I18nText.Plain("launch.precheck.failures.no_profile_selected"),
            MinecraftLaunchPrecheckFailureKind.MicrosoftProfileRequired => I18nText.Plain("launch.precheck.failures.microsoft_profile_required"),
            MinecraftLaunchPrecheckFailureKind.AuthProfileRequired => I18nText.Plain("launch.precheck.failures.auth_profile_required"),
            MinecraftLaunchPrecheckFailureKind.AuthServerMismatch => I18nText.Plain("launch.precheck.failures.auth_server_mismatch"),
            MinecraftLaunchPrecheckFailureKind.MicrosoftOrAuthProfileRequired => I18nText.Plain("launch.precheck.failures.microsoft_or_auth_profile_required"),
            _ => I18nText.Plain("launch.precheck.failures.unknown")
        };
    }
}

public enum MinecraftLaunchPrecheckFailureKind
{
    InstanceIndiePathContainsReservedCharacters = 0,
    InstancePathContainsReservedCharacters = 1,
    InstanceNotSelected = 2,
    InstanceHasError = 3,
    NoProfileSelected = 4,
    MicrosoftProfileRequired = 5,
    AuthProfileRequired = 6,
    AuthServerMismatch = 7,
    MicrosoftOrAuthProfileRequired = 8
}
