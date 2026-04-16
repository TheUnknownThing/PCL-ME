using System;
using PCL.Core.App.I18n;

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
            I18nText.WithArgs(
                "startup.prompts.update_log.title",
                I18nTextArgument.String("version_branch", request.VersionBranchName),
                I18nTextArgument.String("version_base", request.VersionBaseName)),
            I18nText.Plain("startup.prompts.update_log.actions.confirm"),
            I18nText.Plain("startup.prompts.update_log.actions.full_changelog"),
            "https://github.com/TheUnknownThing/PCL-ME/releases");
    }
}

public sealed record LauncherUpdateLogRequest(
    string? ChangelogMarkdown,
    string VersionBranchName,
    string VersionBaseName);

public sealed record LauncherUpdateLogPrompt(
    string MarkdownContent,
    I18nText Title,
    I18nText ConfirmLabel,
    I18nText FullChangelogLabel,
    string FullChangelogUrl);
