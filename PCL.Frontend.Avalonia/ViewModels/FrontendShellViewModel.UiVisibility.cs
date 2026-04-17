using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool ShowUiFeatureHiddenCard => FrontendUiVisibilityService.ShouldShowFunctionHiddenCard(GetUiVisibilityPreferences());

    public string UiFeatureHiddenCardHeader => _showHiddenItemsOverride
        ? SetupText.Ui.HiddenFeaturesCardHeaderTemporary
        : SetupText.Ui.HiddenFeaturesCardHeader;

    public bool ShowLaunchInstanceManagementButtons => FrontendUiVisibilityService.ShouldShowLaunchInstanceManagement(GetUiVisibilityPreferences());

    private FrontendUiVisibilityPreferences GetUiVisibilityPreferences()
    {
        return FrontendUiVisibilityService.BuildPreferences(_setupComposition.Ui, _showHiddenItemsOverride, IgnoreQuiltLoader);
    }

    public void ToggleHiddenItemsOverride()
    {
        _showHiddenItemsOverride = !_showHiddenItemsOverride;
        RefreshShell(_showHiddenItemsOverride ? "Feature hiding has been temporarily disabled." : "Feature hiding has been re-enabled.");
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
