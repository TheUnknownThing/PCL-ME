namespace PCL.Frontend.Spike.Models;

internal sealed record FrontendToolsComposition(
    FrontendToolsGameLinkState GameLink,
    FrontendToolsHelpState Help,
    FrontendToolsTestState Test);

internal sealed record FrontendToolsGameLinkState(
    string Announcement,
    string NatStatus,
    string AccountStatus,
    string LobbyId,
    string SessionPing,
    string SessionId,
    string ConnectionType,
    string ConnectedUserName,
    string ConnectedUserType,
    IReadOnlyList<string> WorldOptions,
    int SelectedWorldIndex,
    IReadOnlyList<FrontendToolsSimpleEntry> PolicyEntries,
    IReadOnlyList<FrontendToolsSimpleEntry> PlayerEntries);

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
    string GroupTitle,
    string Title,
    string Summary,
    string Keywords,
    string RawPath,
    bool IsEvent,
    string? EventType,
    string? EventData,
    string? DetailContent);
