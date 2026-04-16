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
                    "Notice",
                    "You have opened the PCL cross-platform edition 99 times. Thank you for your long-term support!" + System.Environment.NewLine +
                    "The hidden Hardcore Fan theme is not unlocked. The cross-platform edition does not include hidden themes!")
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
