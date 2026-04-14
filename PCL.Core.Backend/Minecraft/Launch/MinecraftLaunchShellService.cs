using PCL.Core.App;
using PCL.Core.App.I18n;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchShellService
{
    private static readonly HashSet<int> _supportPromptMilestones =
    [
        10, 20, 40, 60, 80, 100, 120, 150, 200, 250, 300, 350, 400, 500, 600, 700, 800, 900, 1000, 1200, 1400, 1600, 1800, 2000
    ];

    public static MinecraftLaunchNotification GetCompletionNotification(MinecraftLaunchCompletionRequest request)
    {
        return request.Outcome switch
        {
            MinecraftLaunchOutcome.Succeeded => new MinecraftLaunchNotification(
                I18nText.WithArgs(
                    "launch.notifications.success",
                    I18nTextArgument.String("instance_name", request.InstanceName)),
                MinecraftLaunchNotificationKind.Finish),
            MinecraftLaunchOutcome.Aborted when !string.IsNullOrEmpty(request.AbortHint) => new MinecraftLaunchNotification(
                I18nText.WithArgs(
                    "launch.notifications.abort_with_hint",
                    I18nTextArgument.String("hint", request.AbortHint)),
                MinecraftLaunchNotificationKind.Finish),
            MinecraftLaunchOutcome.Aborted => new MinecraftLaunchNotification(
                I18nText.Plain(
                    request.IsScriptExport
                        ? "launch.notifications.script_export_canceled"
                        : "launch.notifications.launch_canceled"),
                MinecraftLaunchNotificationKind.Info),
            _ => new MinecraftLaunchNotification(I18nText.Plain("launch.notifications.none"), MinecraftLaunchNotificationKind.None)
        };
    }

    public static MinecraftLaunchFailureDisplay GetFailureDisplay(bool isScriptExport)
    {
        return isScriptExport
            ? new MinecraftLaunchFailureDisplay("导出启动脚本失败", "导出启动脚本失败")
            : new MinecraftLaunchFailureDisplay("启动失败", "Minecraft 启动失败");
    }

    public static MinecraftLaunchScriptExportPlan BuildScriptExportPlan(string exportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportPath);

        return new MinecraftLaunchScriptExportPlan(
            exportPath,
            "导出启动脚本完成，强制结束启动过程",
            "导出启动脚本成功！",
            exportPath);
    }

    public static MinecraftLaunchPrompt? GetSupportPrompt(int launchCount)
    {
        if (!_supportPromptMilestones.Contains(launchCount))
        {
            return null;
        }

        return new MinecraftLaunchPrompt(
            I18nText.WithArgs(
                "launch.prompts.support.message",
                I18nTextArgument.Int("launch_count", launchCount)),
            I18nText.WithArgs(
                "launch.prompts.support.title",
                I18nTextArgument.Int("launch_count", launchCount)),
            [
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.support.actions.sponsor"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, "https://afdian.com/a/LTCat")]),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.support.actions.continue"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)])
            ],
            IsWarning: false);
    }

    public static MinecraftGameShellPlan GetPostLaunchShellPlan(MinecraftLaunchPostLaunchShellRequest request)
    {
        return new MinecraftGameShellPlan(
            GetLaunchMusicAction(request.StopMusicInGame, request.StartMusicInGame),
            new MinecraftLaunchVideoBackgroundShellAction(MinecraftLaunchVideoBackgroundActionKind.Pause),
            GetPostLaunchShellAction(request.Visibility),
            GlobalLaunchCountIncrement: 1,
            InstanceLaunchCountIncrement: 1);
    }

    public static MinecraftGameShellPlan GetWatcherStopShellPlan(MinecraftLaunchWatcherStopShellRequest request)
    {
        return new MinecraftGameShellPlan(
            GetWatcherStopMusicAction(request.StopMusicInGame, request.StartMusicInGame),
            new MinecraftLaunchVideoBackgroundShellAction(MinecraftLaunchVideoBackgroundActionKind.Play),
            GetWatcherStopShellAction(request.Visibility, request.TriggerLauncherShutdown),
            GlobalLaunchCountIncrement: 0,
            InstanceLaunchCountIncrement: 0);
    }

    public static MinecraftLaunchShellAction GetPostLaunchShellAction(LauncherVisibility visibility)
    {
        return visibility switch
        {
            LauncherVisibility.ExitImmediately => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.ExitLauncher,
                "已根据设置，在启动后关闭启动器"),
            LauncherVisibility.HideAndExit or LauncherVisibility.HideAndReopen => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.HideLauncher,
                "已根据设置，在启动后隐藏启动器"),
            LauncherVisibility.MinimizeAndReopen => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.MinimizeLauncher,
                "已根据设置，在启动后最小化启动器"),
            _ => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.None,
                string.Empty)
        };
    }

    private static MinecraftLaunchMusicShellAction GetLaunchMusicAction(bool stopMusicInGame, bool startMusicInGame)
    {
        if (stopMusicInGame)
        {
            return new MinecraftLaunchMusicShellAction(
                MinecraftLaunchMusicActionKind.Pause,
                "[Music] 已根据设置，在启动后暂停音乐播放");
        }

        if (startMusicInGame)
        {
            return new MinecraftLaunchMusicShellAction(
                MinecraftLaunchMusicActionKind.Resume,
                "[Music] 已根据设置，在启动后开始音乐播放");
        }

        return MinecraftLaunchMusicShellAction.None;
    }

    private static MinecraftLaunchMusicShellAction GetWatcherStopMusicAction(bool stopMusicInGame, bool startMusicInGame)
    {
        if (stopMusicInGame)
        {
            return new MinecraftLaunchMusicShellAction(
                MinecraftLaunchMusicActionKind.Resume,
                "[Music] 已根据设置，在结束后开始音乐播放");
        }

        if (startMusicInGame)
        {
            return new MinecraftLaunchMusicShellAction(
                MinecraftLaunchMusicActionKind.Pause,
                "[Music] 已根据设置，在结束后暂停音乐播放");
        }

        return MinecraftLaunchMusicShellAction.None;
    }

    private static MinecraftLaunchShellAction GetWatcherStopShellAction(LauncherVisibility visibility, bool triggerLauncherShutdown)
    {
        return visibility switch
        {
            LauncherVisibility.HideAndExit when triggerLauncherShutdown => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.ExitLauncher,
                string.Empty),
            LauncherVisibility.HideAndExit or LauncherVisibility.HideAndReopen => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.ShowLauncher,
                string.Empty),
            _ => new MinecraftLaunchShellAction(
                MinecraftLaunchShellActionKind.None,
                string.Empty)
        };
    }
}

public sealed record MinecraftLaunchCompletionRequest(
    string InstanceName,
    MinecraftLaunchOutcome Outcome,
    bool IsScriptExport,
    string? AbortHint);

public sealed record MinecraftLaunchPostLaunchShellRequest(
    LauncherVisibility Visibility,
    bool StopMusicInGame,
    bool StartMusicInGame);

public sealed record MinecraftLaunchWatcherStopShellRequest(
    LauncherVisibility Visibility,
    bool StopMusicInGame,
    bool StartMusicInGame,
    bool TriggerLauncherShutdown);

public sealed record MinecraftLaunchNotification(
    I18nText Message,
    MinecraftLaunchNotificationKind Kind);

public sealed record MinecraftLaunchFailureDisplay(
    string DialogTitle,
    string LogTitle);

public sealed record MinecraftLaunchScriptExportPlan(
    string TargetPath,
    string CompletionLogMessage,
    string AbortHint,
    string RevealInShellPath);

public sealed record MinecraftGameShellPlan(
    MinecraftLaunchMusicShellAction MusicAction,
    MinecraftLaunchVideoBackgroundShellAction VideoBackgroundAction,
    MinecraftLaunchShellAction LauncherAction,
    int GlobalLaunchCountIncrement,
    int InstanceLaunchCountIncrement);

public sealed record MinecraftLaunchMusicShellAction(
    MinecraftLaunchMusicActionKind Kind,
    string LogMessage)
{
    public static MinecraftLaunchMusicShellAction None { get; } = new(MinecraftLaunchMusicActionKind.None, string.Empty);
}

public sealed record MinecraftLaunchVideoBackgroundShellAction(
    MinecraftLaunchVideoBackgroundActionKind Kind);

public sealed record MinecraftLaunchShellAction(
    MinecraftLaunchShellActionKind Kind,
    string LogMessage);

public enum MinecraftLaunchOutcome
{
    Succeeded = 0,
    Aborted = 1,
    Failed = 2
}

public enum MinecraftLaunchNotificationKind
{
    None = 0,
    Info = 1,
    Finish = 2
}

public enum MinecraftLaunchShellActionKind
{
    None = 0,
    ExitLauncher = 1,
    HideLauncher = 2,
    MinimizeLauncher = 3,
    ShowLauncher = 4
}

public enum MinecraftLaunchMusicActionKind
{
    None = 0,
    Pause = 1,
    Resume = 2
}

public enum MinecraftLaunchVideoBackgroundActionKind
{
    None = 0,
    Pause = 1,
    Play = 2
}
