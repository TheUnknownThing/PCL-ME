using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private Bitmap? _minecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    private string _minecraftServerQueryAddress = string.Empty;
    private string _minecraftServerQueryTitle = string.Empty;
    private IBrush _minecraftServerQueryTitleBrush = Brushes.White;
    private string _minecraftServerQueryPlayerCount = "-/-";
    private string _minecraftServerQueryLatency = string.Empty;
    private IBrush _minecraftServerQueryLatencyBrush = Brushes.White;
    private string? _minecraftServerQueryPlayerTooltip;
    private IReadOnlyList<MinecraftServerQueryMotdLineViewModel> _minecraftServerQueryMotdLines = [];
    private bool _hasMinecraftServerQueryResult;

    public ActionCommand QueryMinecraftServerCommand => _queryMinecraftServerCommand;

    public string MinecraftServerQueryAddress
    {
        get => _minecraftServerQueryAddress;
        set => SetProperty(ref _minecraftServerQueryAddress, value);
    }

    public string MinecraftServerQueryTitle
    {
        get => _minecraftServerQueryTitle;
        private set => SetProperty(ref _minecraftServerQueryTitle, value);
    }

    public IBrush MinecraftServerQueryTitleBrush
    {
        get => _minecraftServerQueryTitleBrush;
        private set => SetProperty(ref _minecraftServerQueryTitleBrush, value);
    }

    public string MinecraftServerQueryPlayerCount
    {
        get => _minecraftServerQueryPlayerCount;
        private set => SetProperty(ref _minecraftServerQueryPlayerCount, value);
    }

    public string MinecraftServerQueryLatency
    {
        get => _minecraftServerQueryLatency;
        private set
        {
            if (SetProperty(ref _minecraftServerQueryLatency, value))
            {
                RaisePropertyChanged(nameof(HasMinecraftServerQueryLatency));
            }
        }
    }

    public IBrush MinecraftServerQueryLatencyBrush
    {
        get => _minecraftServerQueryLatencyBrush;
        private set => SetProperty(ref _minecraftServerQueryLatencyBrush, value);
    }

    public string? MinecraftServerQueryPlayerTooltip
    {
        get => _minecraftServerQueryPlayerTooltip;
        private set => SetProperty(ref _minecraftServerQueryPlayerTooltip, value);
    }

    public IReadOnlyList<MinecraftServerQueryMotdLineViewModel> MinecraftServerQueryMotdLines
    {
        get => _minecraftServerQueryMotdLines;
        private set
        {
            if (SetProperty(ref _minecraftServerQueryMotdLines, value))
            {
                RaisePropertyChanged(nameof(HasMinecraftServerQueryMotd));
            }
        }
    }

    public bool HasMinecraftServerQueryResult
    {
        get => _hasMinecraftServerQueryResult;
        private set => SetProperty(ref _hasMinecraftServerQueryResult, value);
    }

    public bool HasMinecraftServerQueryMotd => MinecraftServerQueryMotdLines.Count > 0;

    public bool HasMinecraftServerQueryLatency => !string.IsNullOrWhiteSpace(MinecraftServerQueryLatency);

    public Bitmap? MinecraftServerQueryLogo
    {
        get => _minecraftServerQueryLogo;
        private set => SetProperty(ref _minecraftServerQueryLogo, value);
    }

    public Bitmap? MinecraftServerQueryBackgroundImage => LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png");

    private void InitializeMinecraftServerQuerySurface()
    {
        ResetMinecraftServerQuerySurface();
    }

    private void ResetMinecraftServerQuerySurface()
    {
        MinecraftServerQueryAddress = string.Empty;
        MinecraftServerQueryTitle = string.Empty;
        MinecraftServerQueryTitleBrush = Brushes.White;
        MinecraftServerQueryPlayerCount = "-/-";
        MinecraftServerQueryLatency = string.Empty;
        MinecraftServerQueryLatencyBrush = Brushes.White;
        MinecraftServerQueryPlayerTooltip = null;
        MinecraftServerQueryMotdLines = [];
        MinecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
        HasMinecraftServerQueryResult = false;
    }

    private Task QueryMinecraftServerAsync()
    {
        HasMinecraftServerQueryResult = true;
        MinecraftServerQueryTitle = "MinecraftServerQuery 迁移已接线，下一步将补上真实查询逻辑。";
        MinecraftServerQueryTitleBrush = Brushes.White;
        MinecraftServerQueryPlayerCount = "-/-";
        MinecraftServerQueryLatency = string.Empty;
        MinecraftServerQueryPlayerTooltip = null;
        MinecraftServerQueryMotdLines = [];
        MinecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
        AddActivity("查询 Minecraft 服务器", "原版控件布局已接回 spike 页面，下一步补上真实探测流程。");
        return Task.CompletedTask;
    }
}

internal sealed class MinecraftServerQueryMotdLineViewModel(
    IReadOnlyList<MinecraftServerQueryMotdSegmentViewModel> segments)
{
    public IReadOnlyList<MinecraftServerQueryMotdSegmentViewModel> Segments { get; } = segments;
}

internal sealed class MinecraftServerQueryMotdSegmentViewModel(
    string text,
    IBrush foregroundBrush,
    FontWeight fontWeight,
    FontStyle fontStyle)
{
    public string Text { get; } = text;

    public IBrush ForegroundBrush { get; } = foregroundBrush;

    public FontWeight FontWeight { get; } = fontWeight;

    public FontStyle FontStyle { get; } = fontStyle;
}
