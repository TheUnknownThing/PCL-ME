using Avalonia;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
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

    public IReadOnlyList<string> BackgroundSuitOptions => SetupText.Ui.BackgroundSuitOptions;

    public int SelectedBackgroundSuitIndex
    {
        get => _selectedBackgroundSuitIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, BackgroundSuitOptions.Count, out var normalizedValue))
            {
                SetProperty(ref _selectedBackgroundSuitIndex, normalizedValue);
            }
        }
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

    public string BackgroundBlurLabel => BackgroundBlur <= 0.5
        ? _i18n.T("setup.ui.background.labels.blur_off")
        : $"{Math.Round(BackgroundBlur)}";

    public string BackgroundCardHeader => HasBackgroundAssets
        ? _i18n.T(
            "setup.ui.background.card_header_with_count",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["count"] = _backgroundAssetCount
            })
        : _i18n.T("setup.ui.background.card_header");

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
            AddActivity(
                _i18n.T("setup.ui.background.activities.refresh"),
                _i18n.T(
                    "setup.ui.background.activities.loaded",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["file"] = Path.GetFileName(_currentBackgroundAssetPath)
                    }));
            return;
        }

        AddActivity(
            _i18n.T("setup.ui.background.activities.refresh"),
            _i18n.T("setup.ui.background.activities.empty"));
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
