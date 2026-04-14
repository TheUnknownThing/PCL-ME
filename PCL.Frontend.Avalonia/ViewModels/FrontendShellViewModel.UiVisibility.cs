using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool ShowUiFeatureHiddenCard => FrontendUiVisibilityService.ShouldShowFunctionHiddenCard(GetUiVisibilityPreferences());

    public string UiFeatureHiddenCardHeader => _showHiddenItemsOverride
        ? "功能隐藏（已暂时关闭，按 F12 以重新启用）"
        : "功能隐藏";

    public bool ShowLaunchInstanceManagementButtons => FrontendUiVisibilityService.ShouldShowLaunchInstanceManagement(GetUiVisibilityPreferences());

    private FrontendUiVisibilityPreferences GetUiVisibilityPreferences()
    {
        return FrontendUiVisibilityService.BuildPreferences(_setupComposition.Ui, _showHiddenItemsOverride);
    }

    public void ToggleHiddenItemsOverride()
    {
        _showHiddenItemsOverride = !_showHiddenItemsOverride;
        RefreshShell(_showHiddenItemsOverride ? "已暂时关闭功能隐藏设置。" : "已重新启用功能隐藏设置。");
        RaiseUiVisibilityProperties();
    }

    private void RaiseUiVisibilityProperties()
    {
        RaisePropertyChanged(nameof(ShowUiFeatureHiddenCard));
        RaisePropertyChanged(nameof(UiFeatureHiddenCardHeader));
        RaisePropertyChanged(nameof(ShowLaunchInstanceManagementButtons));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
    }
}
