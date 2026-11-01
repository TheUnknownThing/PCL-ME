using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private static readonly WindowCloseAction[] CloseActionOrder = [WindowCloseAction.MinimizeToTray, WindowCloseAction.Exit];
    private bool _keepRunningInTray;
    private int _selectedWindowCloseActionIndex;
    public IReadOnlyList<string> CloseActionOptions => SetupText.LauncherMisc.CloseActionOptions;
    public bool KeepRunningInTray { get => _keepRunningInTray; set => SetProperty(ref _keepRunningInTray, value); }
    public int SelectedCloseActionIndex
    {
        get => _selectedWindowCloseActionIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, CloseActionOptions.Count, out var index)) SetProperty(ref _selectedWindowCloseActionIndex, index);
        }
    }
    internal FrontendDesktopSettings DesktopSettings => new(KeepRunningInTray, CloseActionOrder[SelectedCloseActionIndex]);
    private void InitializeDesktopSettings()
    {
        var settings = _setupComposition.LauncherMisc.Desktop;
        _keepRunningInTray = settings.KeepRunningInTray;
        _selectedWindowCloseActionIndex = Math.Max(0, Array.IndexOf(CloseActionOrder, settings.CloseAction));
        RaisePropertyChanged(nameof(KeepRunningInTray));
        RaisePropertyChanged(nameof(SelectedCloseActionIndex));
    }
}
