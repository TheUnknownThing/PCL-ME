namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupConsentRequest(
    LauncherStartupSpecialBuildKind SpecialBuildKind,
    bool IsSpecialBuildHintDisabled,
    bool HasAcceptedEula);
