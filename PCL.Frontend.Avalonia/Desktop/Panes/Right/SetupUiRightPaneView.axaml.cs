using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class SetupUiRightPaneView : UserControl
{
    private readonly ComboBox _launcherLocaleComboBox;
    private readonly ComboBox _themeModeComboBox;
    private readonly ComboBox _lightPaletteComboBox;
    private readonly ComboBox _darkPaletteComboBox;
    private readonly ComboBox _globalFontComboBox;
    private readonly ComboBox _motdFontComboBox;
    private readonly ComboBox _backgroundSuitComboBox;
    private readonly ComboBox _homepagePresetComboBox;
    private LauncherViewModel? _observedLauncher;

    public SetupUiRightPaneView()
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
        DetachedFromVisualTree += (_, _) => ObserveLauncher(null);
        ScheduleSelectionRestore();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveLauncher(DataContext as LauncherViewModel);
        ScheduleSelectionRestore();
    }

    private void ObserveLauncher(LauncherViewModel? shell)
    {
        if (ReferenceEquals(_observedLauncher, shell))
        {
            return;
        }

        if (_observedLauncher is not null)
        {
            _observedLauncher.PropertyChanged -= OnLauncherPropertyChanged;
        }

        _observedLauncher = shell;
        if (_observedLauncher is not null)
        {
            _observedLauncher.PropertyChanged += OnLauncherPropertyChanged;
        }
    }

    private void OnLauncherPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(LauncherViewModel.LauncherLocaleOptions)
            or nameof(LauncherViewModel.SelectedLauncherLocaleIndex)
            or nameof(LauncherViewModel.DarkModeOptions)
            or nameof(LauncherViewModel.SelectedDarkModeIndex)
            or nameof(LauncherViewModel.ThemeColorOptions)
            or nameof(LauncherViewModel.SelectedLightColorIndex)
            or nameof(LauncherViewModel.SelectedDarkColorIndex)
            or nameof(LauncherViewModel.FontOptions)
            or nameof(LauncherViewModel.SelectedGlobalFontIndex)
            or nameof(LauncherViewModel.SelectedMotdFontIndex)
            or nameof(LauncherViewModel.BackgroundSuitOptions)
            or nameof(LauncherViewModel.SelectedBackgroundSuitIndex)
            or nameof(LauncherViewModel.HomepagePresetOptions)
            or nameof(LauncherViewModel.SelectedHomepagePresetIndex)))
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
        if (_observedLauncher is null)
        {
            return;
        }

        ApplySelectedIndex(_launcherLocaleComboBox, _observedLauncher.SelectedLauncherLocaleIndex);
        ApplySelectedIndex(_themeModeComboBox, _observedLauncher.SelectedDarkModeIndex);
        ApplySelectedIndex(_lightPaletteComboBox, _observedLauncher.SelectedLightColorIndex);
        ApplySelectedIndex(_darkPaletteComboBox, _observedLauncher.SelectedDarkColorIndex);
        ApplySelectedIndex(_globalFontComboBox, _observedLauncher.SelectedGlobalFontIndex);
        ApplySelectedIndex(_motdFontComboBox, _observedLauncher.SelectedMotdFontIndex);
        ApplySelectedIndex(_backgroundSuitComboBox, _observedLauncher.SelectedBackgroundSuitIndex);
        ApplySelectedIndex(_homepagePresetComboBox, _observedLauncher.SelectedHomepagePresetIndex);
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
