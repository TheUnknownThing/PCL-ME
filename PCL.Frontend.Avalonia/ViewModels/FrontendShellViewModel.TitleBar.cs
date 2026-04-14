using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool ShowWindowBranding => !IsContextModeRoute && SelectedLogoTypeIndex != 0;

    public bool ShowCenteredTopLevelNavigation => ShowTopLevelNavigation && !ShowLeftAlignedTopLevelNavigation;

    public bool ShowLeftAlignedTopLevelNavigation => ShowTopLevelNavigation && SelectedLogoTypeIndex == 0 && LogoAlignLeft;

    public bool ShowDefaultTitleBarBranding => ShowWindowBranding && SelectedLogoTypeIndex == 1;

    public bool ShowTextTitleBarBranding => ShowWindowBranding && SelectedLogoTypeIndex == 2;

    public bool ShowImageTitleBarBranding => ShowWindowBranding && SelectedLogoTypeIndex == 3 && TitleBarCustomLogoImage is not null;

    public string TitleBarCustomText => string.IsNullOrWhiteSpace(LogoTextValue)
        ? "Plain Craft Launcher"
        : LogoTextValue.Trim();

    public Bitmap? TitleBarCustomLogoImage => _titleBarLogoImage;

    private void RefreshTitleBarLogoImage()
    {
        var nextImage = TryLoadTitleBarLogoImage();
        if (ReferenceEquals(_titleBarLogoImage, nextImage))
        {
            return;
        }

        _titleBarLogoImage?.Dispose();
        _titleBarLogoImage = nextImage;
        RaiseTitleBarAppearanceProperties();
    }

    private Bitmap? TryLoadTitleBarLogoImage()
    {
        var logoPath = GetLogoImagePath();
        if (!File.Exists(logoPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(logoPath);
        }
        catch
        {
            return null;
        }
    }

    private void RaiseTitleBarAppearanceProperties()
    {
        RaisePropertyChanged(nameof(ShowWindowBranding));
        RaisePropertyChanged(nameof(ShowCenteredTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowLeftAlignedTopLevelNavigation));
        RaisePropertyChanged(nameof(ShowDefaultTitleBarBranding));
        RaisePropertyChanged(nameof(ShowTextTitleBarBranding));
        RaisePropertyChanged(nameof(ShowImageTitleBarBranding));
        RaisePropertyChanged(nameof(TitleBarCustomText));
        RaisePropertyChanged(nameof(TitleBarCustomLogoImage));
    }
}
