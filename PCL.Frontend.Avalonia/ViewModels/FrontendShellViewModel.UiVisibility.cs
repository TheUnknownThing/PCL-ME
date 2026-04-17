using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool HasSelectedInstance => _hasOptimisticLaunchInstanceName
        ? _optimisticHasSelectedInstance
        : _instanceComposition.Selection.HasSelection;

    public bool ShowLaunchVersionSetupButton => HasSelectedInstance;

    public int LaunchVersionSelectButtonColumnSpan => ShowLaunchVersionSetupButton ? 1 : 3;

    public bool ShowUiFeatureHiddenCard => FrontendUiVisibilityService.ShouldShowFunctionHiddenCard(GetUiVisibilityPreferences());

    public string UiFeatureHiddenCardHeader => _showHiddenItemsOverride
        ? SetupText.Ui.HiddenFeaturesCardHeaderTemporary
        : SetupText.Ui.HiddenFeaturesCardHeader;

    public bool ShowLaunchInstanceManagementButtons => FrontendUiVisibilityService.ShouldShowLaunchInstanceManagement(GetUiVisibilityPreferences());

    private void OpenSelectedInstanceSetup()
    {
        if (!HasSelectedInstance)
        {
            return;
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup),
            "Opened instance settings from the launch pane.");
    }

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
        RaisePropertyChanged(nameof(HasSelectedInstance));
        RaisePropertyChanged(nameof(ShowLaunchVersionSetupButton));
        RaisePropertyChanged(nameof(LaunchVersionSelectButtonColumnSpan));
        RaisePropertyChanged(nameof(ShowUiFeatureHiddenCard));
        RaisePropertyChanged(nameof(UiFeatureHiddenCardHeader));
        RaisePropertyChanged(nameof(ShowLaunchInstanceManagementButtons));
        RaisePropertyChanged(nameof(ShowInstanceResourceCheckButton));
    }
}
