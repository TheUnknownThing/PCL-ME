using PCL.Core.App.I18n;
using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public sealed record LauncherStartupPrompt(
    I18nText Message,
    I18nText Title,
    IReadOnlyList<LauncherStartupPromptButton> Buttons,
    bool IsWarning = false);

public sealed record LauncherStartupPromptButton(
    I18nText Label,
    IReadOnlyList<LauncherStartupPromptAction> Actions,
    bool ClosesPrompt = true);

public sealed record LauncherStartupPromptAction(
    LauncherStartupPromptActionKind Kind,
    string? Value = null);

public enum LauncherStartupPromptActionKind
{
    Continue,
    Accept,
    Reject,
    OpenUrl,
    ExitLauncher
}

public enum LauncherStartupSpecialBuildKind
{
    None = 0,
    Debug = 1,
    Ci = 2
}
