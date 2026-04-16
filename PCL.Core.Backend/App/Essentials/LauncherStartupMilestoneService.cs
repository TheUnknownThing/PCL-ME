using PCL.Core.App.I18n;

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
                    I18nText.Plain("startup.prompts.milestone.title"),
                    I18nText.Plain("startup.prompts.milestone.message"))
                : null);
    }
}

public sealed record LauncherStartupMilestoneResult(
    int UpdatedCount,
    bool ShouldAttemptUnlockHiddenTheme,
    LauncherStartupMilestoneNotice? HiddenThemeNotice);

public sealed record LauncherStartupMilestoneNotice(
    I18nText Title,
    I18nText Message);
