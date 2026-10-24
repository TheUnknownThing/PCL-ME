using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class SetupUiRightPaneView : UserControl
{
    private static readonly (string Name, Func<LauncherViewModel, int> Index)[] SelectionBindings =
    [
        ("LauncherLocaleComboBox", static launcher => launcher.SelectedLauncherLocaleIndex),
        ("ThemeModeComboBox", static launcher => launcher.SelectedDarkModeIndex),
        ("LightPaletteComboBox", static launcher => launcher.SelectedLightColorIndex),
        ("DarkPaletteComboBox", static launcher => launcher.SelectedDarkColorIndex),
        ("GlobalFontComboBox", static launcher => launcher.SelectedGlobalFontIndex),
        ("MotdFontComboBox", static launcher => launcher.SelectedMotdFontIndex),
        ("BackgroundSuitComboBox", static launcher => launcher.SelectedBackgroundSuitIndex),
        ("HomepagePresetComboBox", static launcher => launcher.SelectedHomepagePresetIndex)
    ];
    private readonly Dictionary<PclDeferredContentControl, List<(ComboBox Combo, Func<LauncherViewModel, int> Index)>> _sectionSelections = [];
    private LauncherViewModel? _observedLauncher;
    private bool _isSelectionRestoreQueued;

    public SetupUiRightPaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            ObserveLauncher(DataContext as LauncherViewModel);
            ScheduleSelectionRestore();
        };
        DetachedFromVisualTree += (_, _) => ObserveLauncher(null);
        ScheduleSelectionRestore();
    }

    private void OnSectionContentCreated(object? sender, EventArgs e)
    {
        if (sender is not PclDeferredContentControl { Content: Control content } section)
        {
            return;
        }

        // PclCard has its own namescope. Search the newly built card content,
        // whose logical children exist before its presenters are attached.
        var root = content is PclCard { CardContent: Control cardContent } ? cardContent : content;
        var combos = root.GetLogicalDescendants().OfType<ComboBox>()
            .Where(combo => combo.Name is not null).ToDictionary(combo => combo.Name!);
        var selections = new List<(ComboBox, Func<LauncherViewModel, int>)>();
        foreach (var binding in SelectionBindings)
        {
            if (combos.TryGetValue(binding.Name, out var combo))
            {
                selections.Add((combo, binding.Index));
            }
        }

        _sectionSelections[section] = selections;
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
        if (_isSelectionRestoreQueued)
        {
            return;
        }

        _isSelectionRestoreQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isSelectionRestoreQueued = false;
            RestoreSelections();
        }, DispatcherPriority.Background);
    }

    private void RestoreSelections()
    {
        if (_observedLauncher is null)
        {
            return;
        }

        foreach (var selections in _sectionSelections.Values)
        {
            foreach (var binding in selections)
            {
                ApplySelectedIndex(binding.Combo, binding.Index(_observedLauncher));
            }
        }
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
