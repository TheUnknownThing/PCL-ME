using PCL.Core.App;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchShellService
{
    public static MinecraftLaunchNotification GetCompletionNotification(MinecraftLaunchCompletionRequest request)
    {
        return request.Outcome switch
        {
            MinecraftLaunchOutcome.Succeeded => new MinecraftLaunchNotification(
                $"{request.InstanceName} 启动成功！",
                MinecraftLaunchNotificationKind.Finish),
            MinecraftLaunchOutcome.Aborted when !string.IsNullOrEmpty(request.AbortHint) => new MinecraftLaunchNotification(
                request.AbortHint,
                MinecraftLaunchNotificationKind.Finish),
            MinecraftLaunchOutcome.Aborted => new MinecraftLaunchNotification(
                request.IsScriptExport ? "已取消导出启动脚本！" : "已取消启动！",
                MinecraftLaunchNotificationKind.Info),
            _ => new MinecraftLaunchNotification(string.Empty, MinecraftLaunchNotificationKind.None)
        };
    }

    public static MinecraftLaunchFailureDisplay GetFailureDisplay(bool isScriptExport)
    {
        return isScriptExport
            ? new MinecraftLaunchFailureDisplay("导出启动脚本失败", "导出启动脚本失败")
            : new MinecraftLaunchFailureDisplay("启动失败", "Minecraft 启动失败");
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
}

public sealed record MinecraftLaunchCompletionRequest(
    string InstanceName,
    MinecraftLaunchOutcome Outcome,
    bool IsScriptExport,
    string? AbortHint);

public sealed record MinecraftLaunchNotification(
    string Message,
    MinecraftLaunchNotificationKind Kind);

public sealed record MinecraftLaunchFailureDisplay(
    string DialogTitle,
    string LogTitle);

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
    MinimizeLauncher = 3
}
