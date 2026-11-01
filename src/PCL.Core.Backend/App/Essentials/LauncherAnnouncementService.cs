using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.App.Essentials;

public static class LauncherAnnouncementService
{
    public static LauncherAnnouncementPreference NormalizePreference(int rawValue)
    {
        return Math.Clamp(rawValue, 0, (int)LauncherAnnouncementPreference.Disabled) switch
        {
            1 => LauncherAnnouncementPreference.ImportantOnly,
            2 => LauncherAnnouncementPreference.Disabled,
            _ => LauncherAnnouncementPreference.All
        };
    }

    public static bool ShouldDisplay(
        LauncherAnnouncementPreference preference,
        LauncherAnnouncementSeverity severity)
    {
        return preference switch
        {
            LauncherAnnouncementPreference.All => true,
            LauncherAnnouncementPreference.ImportantOnly => severity == LauncherAnnouncementSeverity.Important,
            _ => false
        };
    }

    public static IReadOnlyList<LauncherAnnouncement> Filter(
        IEnumerable<LauncherAnnouncement> announcements,
        LauncherAnnouncementPreference preference,
        string? shownAnnouncementState)
    {
        ArgumentNullException.ThrowIfNull(announcements);

        var shownIds = ParseShownAnnouncementState(shownAnnouncementState);
        return announcements
            .Where(announcement => !shownIds.Contains(announcement.Id))
            .Where(announcement => ShouldDisplay(preference, announcement.Severity))
            .ToArray();
    }

    public static HashSet<string> ParseShownAnnouncementState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return state
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string MergeShownAnnouncementState(
        string? existingState,
        IEnumerable<string> announcementIds)
    {
        ArgumentNullException.ThrowIfNull(announcementIds);

        var shownIds = ParseShownAnnouncementState(existingState);
        foreach (var announcementId in announcementIds)
        {
            if (!string.IsNullOrWhiteSpace(announcementId))
            {
                shownIds.Add(announcementId);
            }
        }

        return string.Join("|", shownIds.OrderBy(id => id, StringComparer.Ordinal));
    }
}

public sealed record LauncherAnnouncement(
    string Id,
    string Title,
    string Message,
    string? Detail,
    LauncherAnnouncementSeverity Severity);

public enum LauncherAnnouncementPreference
{
    All = 0,
    ImportantOnly = 1,
    Disabled = 2
}

public enum LauncherAnnouncementSeverity
{
    General = 0,
    Important = 1
}
