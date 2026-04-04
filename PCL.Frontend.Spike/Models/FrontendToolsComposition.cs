namespace PCL.Frontend.Spike.Models;

internal sealed record FrontendToolsComposition(
    FrontendToolsTestState Test);

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
