namespace PCL.Core.App.Essentials;

public static class LauncherStartupMilestoneService
{
    public static LauncherStartupMilestoneResult AdvanceStartupCount(int currentCount)
    {
        var updatedCount = currentCount + 1;
        return new LauncherStartupMilestoneResult(
            updatedCount,
            ShouldAttemptUnlockHiddenTheme: updatedCount >= 99,
            HiddenThemeNotice: updatedCount >= 99
                ? new LauncherStartupMilestoneNotice(
                    "提示",
                    "你已经打开了 99 次 PCL 社区版啦，感谢你长期以来的支持！" + System.Environment.NewLine +
                    "隐藏主题 铁杆粉 未解锁！社区版不包含隐藏主题！")
                : null);
    }
}

public sealed record LauncherStartupMilestoneResult(
    int UpdatedCount,
    bool ShouldAttemptUnlockHiddenTheme,
    LauncherStartupMilestoneNotice? HiddenThemeNotice);

public sealed record LauncherStartupMilestoneNotice(
    string Title,
    string Message);
