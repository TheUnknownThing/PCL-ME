using System;

namespace PCL.Core.App.Essentials;

public static class LauncherUpdateLogService
{
    public static LauncherUpdateLogPrompt BuildPrompt(LauncherUpdateLogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var changelog = string.IsNullOrWhiteSpace(request.ChangelogMarkdown)
            ? "Welcome."
            : request.ChangelogMarkdown;

        return new LauncherUpdateLogPrompt(
            changelog,
            $"PCL-ME has been updated to {request.VersionBranchName} {request.VersionBaseName}",
            "OK",
            "Full changelog",
            "https://github.com/TheUnknownThing/PCL-ME/releases");
    }
}

public sealed record LauncherUpdateLogRequest(
    string? ChangelogMarkdown,
    string VersionBranchName,
    string VersionBaseName);

public sealed record LauncherUpdateLogPrompt(
    string MarkdownContent,
    string Title,
    string ConfirmLabel,
    string FullChangelogLabel,
    string FullChangelogUrl);
