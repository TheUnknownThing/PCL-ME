using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchAnnouncementService
{
    private const string PreferenceKey = "SystemSystemActivity";
    private const string ShownAnnouncementKey = "SystemSystemAnnouncement";

    private static readonly LauncherAnnouncement[] Catalog =
    [
        new(
            "launch-community-edition-intro",
            "Community Edition notice",
            "You are using the PCL Community Edition. This build is maintained independently and may differ from the original release line.",
            "The app is still under active testing. Feedback and bug reports are welcome.",
            LauncherAnnouncementSeverity.Important)
    ];

    public static IReadOnlyList<LauncherAnnouncement> Compose(
        JsonFileProvider sharedConfig,
        YamlFileProvider localConfig)
    {
        ArgumentNullException.ThrowIfNull(sharedConfig);
        ArgumentNullException.ThrowIfNull(localConfig);

        var preference = LauncherAnnouncementService.NormalizePreference(
            ReadValue(localConfig, PreferenceKey, 0));
        var shownState = ReadValue(sharedConfig, ShownAnnouncementKey, string.Empty);
        return LauncherAnnouncementService.Filter(Catalog, preference, shownState);
    }

    public static string MarkAnnouncementAsShown(string? existingState, string announcementId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(announcementId);
        return LauncherAnnouncementService.MergeShownAnnouncementState(existingState, [announcementId]);
    }

    private static T ReadValue<T>(IKeyValueFileProvider provider, string key, T fallback)
    {
        if (!provider.Exists(key))
        {
            return fallback;
        }

        try
        {
            return provider.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }
}
