using PCL.Core.App.I18n;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

/// <summary>
/// 启动前需要由前端展示的提示。
/// </summary>
public sealed record MinecraftLaunchPrompt(
    I18nText Message,
    I18nText Title,
    IReadOnlyList<MinecraftLaunchPromptButton> Buttons,
    bool IsWarning = false);

/// <summary>
/// 提示中的单个按钮。
/// </summary>
public sealed record MinecraftLaunchPromptButton(
    I18nText Label,
    IReadOnlyList<MinecraftLaunchPromptAction> Actions,
    bool ClosesPrompt = true);

/// <summary>
/// 提示按钮对应的动作。
/// </summary>
public sealed record MinecraftLaunchPromptAction(
    MinecraftLaunchPromptActionKind Kind,
    string? Value = null);

public enum MinecraftLaunchPromptActionKind
{
    Continue,
    Abort,
    OpenUrl,
    AppendLaunchArgument,
    PersistNonAsciiPathWarningDisabled,
    PersistInstanceJavaCompatibilityIgnored,
    IgnoreJavaCompatibilityOnce
}
