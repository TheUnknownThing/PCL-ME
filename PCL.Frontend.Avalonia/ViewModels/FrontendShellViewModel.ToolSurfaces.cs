using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Bitmap? _achievementPreviewImage;
    private RenderTargetBitmap? _headPreviewImage;

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

    public Bitmap? AchievementPreviewImage
    {
        get => _achievementPreviewImage;
        private set
        {
            if (ReferenceEquals(_achievementPreviewImage, value))
            {
                return;
            }

            var previous = _achievementPreviewImage;
            _achievementPreviewImage = value;
            RaisePropertyChanged(nameof(AchievementPreviewImage));
            previous?.Dispose();
        }
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
            if (!TryNormalizeSelectionIndex(value, HeadSizeOptions.Count, out var nextValue))
            {
                return;
            }

            if (SetProperty(ref _selectedHeadSizeIndex, nextValue))
            {
                RaisePropertyChanged(nameof(HeadPreviewSize));
                RefreshHeadPreviewFromSelection(addActivity: false);
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
                RefreshHeadPreviewFromSelection(addActivity: false);
            }
        }
    }

    public RenderTargetBitmap? HeadPreviewImage
    {
        get => _headPreviewImage;
        private set
        {
            if (ReferenceEquals(_headPreviewImage, value))
            {
                return;
            }

            var previous = _headPreviewImage;
            _headPreviewImage = value;
            RaisePropertyChanged(nameof(HeadPreviewImage));
            RaisePropertyChanged(nameof(HasHeadPreviewImage));
            previous?.Dispose();
        }
    }

    public bool HasSelectedHeadSkin => !string.IsNullOrWhiteSpace(SelectedHeadSkinPath);

    public bool HasHeadPreviewImage => HeadPreviewImage is not null;

    public double HeadPreviewSize => SelectedHeadSizeIndex switch
    {
        0 => 80,
        1 => 96,
        _ => 112
    };
}
