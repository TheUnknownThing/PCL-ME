using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class SetupUiShellRightPaneView : UserControl
{
    private readonly ComboBox _launcherLocaleComboBox;
    private readonly ComboBox _themeModeComboBox;
    private readonly ComboBox _lightPaletteComboBox;
    private readonly ComboBox _darkPaletteComboBox;
    private readonly ComboBox _globalFontComboBox;
    private readonly ComboBox _motdFontComboBox;
    private readonly ComboBox _backgroundSuitComboBox;
    private readonly ComboBox _homepagePresetComboBox;
    private FrontendShellViewModel? _observedShell;

    public SetupUiShellRightPaneView()
    {
        InitializeComponent();
        _launcherLocaleComboBox = this.FindControl<ComboBox>("LauncherLocaleComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the launcher locale combo box.");
        _themeModeComboBox = this.FindControl<ComboBox>("ThemeModeComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the theme mode combo box.");
        _lightPaletteComboBox = this.FindControl<ComboBox>("LightPaletteComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the light palette combo box.");
        _darkPaletteComboBox = this.FindControl<ComboBox>("DarkPaletteComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the dark palette combo box.");
        _globalFontComboBox = this.FindControl<ComboBox>("GlobalFontComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the global font combo box.");
        _motdFontComboBox = this.FindControl<ComboBox>("MotdFontComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the MOTD font combo box.");
        _backgroundSuitComboBox = this.FindControl<ComboBox>("BackgroundSuitComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the background suit combo box.");
        _homepagePresetComboBox = this.FindControl<ComboBox>("HomepagePresetComboBox")
            ?? throw new InvalidOperationException("The setup UI page did not contain the homepage preset combo box.");
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ObserveShell(null);
        ScheduleSelectionRestore();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleSelectionRestore();
    }

    private void ObserveShell(FrontendShellViewModel? shell)
    {
        if (ReferenceEquals(_observedShell, shell))
        {
            return;
        }

        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged -= OnShellPropertyChanged;
        }

        _observedShell = shell;
        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(FrontendShellViewModel.LauncherLocaleOptions)
            or nameof(FrontendShellViewModel.SelectedLauncherLocaleIndex)
            or nameof(FrontendShellViewModel.DarkModeOptions)
            or nameof(FrontendShellViewModel.SelectedDarkModeIndex)
            or nameof(FrontendShellViewModel.ThemeColorOptions)
            or nameof(FrontendShellViewModel.SelectedLightColorIndex)
            or nameof(FrontendShellViewModel.SelectedDarkColorIndex)
            or nameof(FrontendShellViewModel.FontOptions)
            or nameof(FrontendShellViewModel.SelectedGlobalFontIndex)
            or nameof(FrontendShellViewModel.SelectedMotdFontIndex)
            or nameof(FrontendShellViewModel.BackgroundSuitOptions)
            or nameof(FrontendShellViewModel.SelectedBackgroundSuitIndex)
            or nameof(FrontendShellViewModel.HomepagePresetOptions)
            or nameof(FrontendShellViewModel.SelectedHomepagePresetIndex)))
        {
            return;
        }

        ScheduleSelectionRestore();
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(RestoreSelections, DispatcherPriority.Background);
    }

    private void RestoreSelections()
    {
        if (_observedShell is null)
        {
            return;
        }

        ApplySelectedIndex(_launcherLocaleComboBox, _observedShell.SelectedLauncherLocaleIndex);
        ApplySelectedIndex(_themeModeComboBox, _observedShell.SelectedDarkModeIndex);
        ApplySelectedIndex(_lightPaletteComboBox, _observedShell.SelectedLightColorIndex);
        ApplySelectedIndex(_darkPaletteComboBox, _observedShell.SelectedDarkColorIndex);
        ApplySelectedIndex(_globalFontComboBox, _observedShell.SelectedGlobalFontIndex);
        ApplySelectedIndex(_motdFontComboBox, _observedShell.SelectedMotdFontIndex);
        ApplySelectedIndex(_backgroundSuitComboBox, _observedShell.SelectedBackgroundSuitIndex);
        ApplySelectedIndex(_homepagePresetComboBox, _observedShell.SelectedHomepagePresetIndex);
    }

    private static void ApplySelectedIndex(ComboBox comboBox, int selectedIndex)
    {
        if (selectedIndex < 0 || comboBox.SelectedIndex == selectedIndex)
        {
            return;
        }

        comboBox.SelectedIndex = selectedIndex;
    }
}
