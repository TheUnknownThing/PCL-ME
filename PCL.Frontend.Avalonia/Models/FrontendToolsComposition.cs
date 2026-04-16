namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendToolsComposition(
    FrontendToolsHelpState Help,
    FrontendToolsTestState Test);

internal sealed record FrontendToolsSimpleEntry(
    string Title,
    string Summary);

internal sealed record FrontendToolsTestState(
    IReadOnlyList<FrontendToolboxActionDefinition> ToolboxActions,
    string DownloadUrl,
    string DownloadUserAgent,
    string DownloadFolder,
    string DownloadName,
    string OfficialSkinPlayerName,
    string AchievementBlockId,
    string AchievementTitle,
    string AchievementFirstLine,
    string AchievementSecondLine,
    bool ShowAchievementPreview,
    int SelectedHeadSizeIndex,
    string SelectedHeadSkinPath);

internal sealed record FrontendToolboxActionDefinition(
    string ActionKey,
    string Title,
    string ToolTip,
    double MinWidth,
    bool IsDanger);

internal sealed record FrontendToolsHelpState(
    IReadOnlyList<FrontendToolsHelpEntry> Entries);

internal sealed record FrontendToolsHelpEntry(
    IReadOnlyList<string> GroupTitles,
    string Title,
    string Summary,
    string Keywords,
    string? Logo,
    string RawPath,
    string SourcePath,
    bool ShowInSearch,
    bool ShowInPublic,
    bool ShowInSnapshot,
    bool IsEvent,
    string? EventType,
    string? EventData,
    string? DetailContent);
