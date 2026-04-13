using System;

namespace PCL.Core.App.Essentials;

public static class LauncherUpdateLogService
{
    public static LauncherUpdateLogPrompt BuildPrompt(LauncherUpdateLogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var changelog = string.IsNullOrWhiteSpace(request.ChangelogMarkdown)
            ? "欢迎使用呀~"
            : request.ChangelogMarkdown;

        return new LauncherUpdateLogPrompt(
            changelog,
            $"PCL-ME 已更新至 {request.VersionBranchName} {request.VersionBaseName}",
            "确定",
            "完整更新日志",
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
