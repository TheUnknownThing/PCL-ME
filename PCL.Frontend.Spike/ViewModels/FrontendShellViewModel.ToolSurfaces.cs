namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public IReadOnlyList<string> GameLinkWorldOptions => _gameLinkWorldOptions;

    public string GameLinkAnnouncement
    {
        get => _gameLinkAnnouncement;
        set => SetProperty(ref _gameLinkAnnouncement, value);
    }

    public string GameLinkNatStatus
    {
        get => _gameLinkNatStatus;
        set => SetProperty(ref _gameLinkNatStatus, value);
    }

    public string GameLinkAccountStatus
    {
        get => _gameLinkAccountStatus;
        set => SetProperty(ref _gameLinkAccountStatus, value);
    }

    public string GameLinkLobbyId
    {
        get => _gameLinkLobbyId;
        set => SetProperty(ref _gameLinkLobbyId, value);
    }

    public string GameLinkSessionPing
    {
        get => _gameLinkSessionPing;
        set => SetProperty(ref _gameLinkSessionPing, value);
    }

    public string GameLinkSessionId
    {
        get => _gameLinkSessionId;
        set => SetProperty(ref _gameLinkSessionId, value);
    }

    public string GameLinkConnectionType
    {
        get => _gameLinkConnectionType;
        set => SetProperty(ref _gameLinkConnectionType, value);
    }

    public string GameLinkConnectedUserName
    {
        get => _gameLinkConnectedUserName;
        set => SetProperty(ref _gameLinkConnectedUserName, value);
    }

    public string GameLinkConnectedUserType
    {
        get => _gameLinkConnectedUserType;
        set => SetProperty(ref _gameLinkConnectedUserType, value);
    }

    public string GameLinkPlayerListTitle => GameLinkPlayerEntries.Count > 0
        ? $"大厅成员列表（{GameLinkPlayerEntries.Count} 人）"
        : "大厅成员列表（正在获取信息）";

    public int SelectedGameLinkWorldIndex
    {
        get => _selectedGameLinkWorldIndex;
        set => SetProperty(ref _selectedGameLinkWorldIndex, Math.Clamp(value, 0, GameLinkWorldOptions.Count - 1));
    }

    public string ToolDownloadUrl
    {
        get => _toolDownloadUrl;
        set => SetProperty(ref _toolDownloadUrl, value);
    }

    public string ToolDownloadUserAgent
    {
        get => _toolDownloadUserAgent;
        set => SetProperty(ref _toolDownloadUserAgent, value);
    }

    public string ToolDownloadFolder
    {
        get => _toolDownloadFolder;
        set => SetProperty(ref _toolDownloadFolder, value);
    }

    public string ToolDownloadName
    {
        get => _toolDownloadName;
        set => SetProperty(ref _toolDownloadName, value);
    }

    public string OfficialSkinPlayerName
    {
        get => _officialSkinPlayerName;
        set => SetProperty(ref _officialSkinPlayerName, value);
    }

    public string AchievementBlockId
    {
        get => _achievementBlockId;
        set => SetProperty(ref _achievementBlockId, value);
    }

    public string AchievementTitle
    {
        get => _achievementTitle;
        set => SetProperty(ref _achievementTitle, value);
    }

    public string AchievementFirstLine
    {
        get => _achievementFirstLine;
        set => SetProperty(ref _achievementFirstLine, value);
    }

    public string AchievementSecondLine
    {
        get => _achievementSecondLine;
        set => SetProperty(ref _achievementSecondLine, value);
    }

    public bool ShowAchievementPreview
    {
        get => _showAchievementPreview;
        private set => SetProperty(ref _showAchievementPreview, value);
    }

    public IReadOnlyList<string> HeadSizeOptions { get; } =
    [
        "64x64",
        "96x96",
        "128x128"
    ];

    public int SelectedHeadSizeIndex
    {
        get => _selectedHeadSizeIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, HeadSizeOptions.Count - 1);
            if (SetProperty(ref _selectedHeadSizeIndex, nextValue))
            {
                RaisePropertyChanged(nameof(HeadPreviewSize));
            }
        }
    }

    public string SelectedHeadSkinPath
    {
        get => _selectedHeadSkinPath;
        set
        {
            if (SetProperty(ref _selectedHeadSkinPath, value))
            {
                RaisePropertyChanged(nameof(HasSelectedHeadSkin));
            }
        }
    }

    public bool HasSelectedHeadSkin => !string.Equals(SelectedHeadSkinPath, "尚未选择皮肤", StringComparison.Ordinal);

    public double HeadPreviewSize => SelectedHeadSizeIndex switch
    {
        0 => 80,
        1 => 96,
        _ => 112
    };
}
