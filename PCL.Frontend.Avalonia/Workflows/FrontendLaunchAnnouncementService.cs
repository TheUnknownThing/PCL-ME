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
            "跨平台版提示",
            "你正在使用 PCL 跨平台版！此版本为独立开发和维护，与官方版本维护路线不同，体验有所出入。",
            "现在软件仍未经过充分测试，欢迎参与测试并提出反馈！",
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
