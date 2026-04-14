using Avalonia;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly string[] BackgroundImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp"
    ];

    private string? _currentBackgroundAssetPath;
    private Bitmap? _currentBackgroundBitmap;
    private PixelSize _currentBackgroundSourcePixelSize;
    private int _backgroundAssetCount;

    public IReadOnlyList<string> BackgroundSuitOptions { get; } =
    [
        "智能",
        "居中",
        "适应",
        "拉伸",
        "平铺",
        "居于左上",
        "居于右上",
        "居于左下",
        "居于右下"
    ];

    public int SelectedBackgroundSuitIndex
    {
        get => _selectedBackgroundSuitIndex;
        set => SetProperty(ref _selectedBackgroundSuitIndex, Math.Clamp(value, 0, BackgroundSuitOptions.Count - 1));
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            var normalized = Math.Clamp(value, 0, 1000);
            if (SetProperty(ref _backgroundOpacity, normalized))
            {
                RaisePropertyChanged(nameof(BackgroundOpacityLabel));
            }
        }
    }

    public string BackgroundOpacityLabel => $"{Math.Round(BackgroundOpacity / 10)}%";

    public double BackgroundBlur
    {
        get => _backgroundBlur;
        set
        {
            var normalized = Math.Clamp(value, 0, 40);
            if (SetProperty(ref _backgroundBlur, normalized))
            {
                RaisePropertyChanged(nameof(BackgroundBlurLabel));
            }
        }
    }

    public string BackgroundBlurLabel => BackgroundBlur <= 0.5 ? "关闭" : $"{Math.Round(BackgroundBlur)}";

    public string BackgroundCardHeader => HasBackgroundAssets ? $"背景图片（{_backgroundAssetCount} 张）" : "背景图片";

    public bool HasBackgroundAssets => _backgroundAssetCount > 0;

    public bool ShowBackgroundAdvancedSettings => HasBackgroundAssets;

    public bool ShowBackgroundClearAction => HasBackgroundAssets;

    public Bitmap? CurrentBackgroundBitmap
    {
        get => _currentBackgroundBitmap;
        private set
        {
            if (ReferenceEquals(_currentBackgroundBitmap, value))
            {
                return;
            }

            var previousBitmap = _currentBackgroundBitmap;
            _currentBackgroundBitmap = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(HasCurrentBackgroundBitmap));
            previousBitmap?.Dispose();
        }
    }

    public bool HasCurrentBackgroundBitmap => CurrentBackgroundBitmap is not null;

    public int CurrentBackgroundSourcePixelWidth => _currentBackgroundSourcePixelSize.Width;

    public int CurrentBackgroundSourcePixelHeight => _currentBackgroundSourcePixelSize.Height;

    private void RefreshBackgroundContentState(bool selectNewAsset, bool addActivity)
    {
        var imageAssets = EnumerateMediaFiles(GetBackgroundFolderPath(), BackgroundImageExtensions).ToArray();

        _backgroundAssetCount = imageAssets.Length;

        if (selectNewAsset || string.IsNullOrWhiteSpace(_currentBackgroundAssetPath) || !File.Exists(_currentBackgroundAssetPath))
        {
            _currentBackgroundAssetPath = imageAssets.Length == 0
                ? null
                : imageAssets[Random.Shared.Next(imageAssets.Length)];
        }
        else if (_currentBackgroundAssetPath is not null && !imageAssets.Contains(_currentBackgroundAssetPath, StringComparer.OrdinalIgnoreCase))
        {
            _currentBackgroundAssetPath = imageAssets.Length == 0 ? null : imageAssets[0];
        }

        RefreshBackgroundBitmap();
        RaiseBackgroundContentProperties();

        if (!addActivity)
        {
            return;
        }

        if (_currentBackgroundAssetPath is not null)
        {
            AddActivity("刷新背景内容", $"已加载背景：{Path.GetFileName(_currentBackgroundAssetPath)}");
            return;
        }

        AddActivity("刷新背景内容", "未检测到可用背景内容。");
    }

    private void RefreshBackgroundBitmap()
    {
        if (string.IsNullOrWhiteSpace(_currentBackgroundAssetPath) || !File.Exists(_currentBackgroundAssetPath))
        {
            _currentBackgroundSourcePixelSize = default;
            CurrentBackgroundBitmap = null;
            RaisePropertyChanged(nameof(CurrentBackgroundSourcePixelWidth));
            RaisePropertyChanged(nameof(CurrentBackgroundSourcePixelHeight));
            return;
        }

        try
        {
            using var sourceBitmap = new Bitmap(_currentBackgroundAssetPath);
            _currentBackgroundSourcePixelSize = sourceBitmap.PixelSize;
            CurrentBackgroundBitmap = LoadBackgroundBitmap(_currentBackgroundAssetPath);
        }
        catch
        {
            _currentBackgroundSourcePixelSize = default;
            CurrentBackgroundBitmap = null;
        }

        RaisePropertyChanged(nameof(CurrentBackgroundSourcePixelWidth));
        RaisePropertyChanged(nameof(CurrentBackgroundSourcePixelHeight));
    }

    private void RaiseBackgroundContentProperties()
    {
        RaisePropertyChanged(nameof(HasBackgroundAssets));
        RaisePropertyChanged(nameof(ShowBackgroundAdvancedSettings));
        RaisePropertyChanged(nameof(BackgroundCardHeader));
        RaisePropertyChanged(nameof(ShowBackgroundClearAction));
    }

    private static Bitmap LoadBackgroundBitmap(string path)
    {
        return new Bitmap(path);
    }
}
