using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed partial class App
{
    private DesktopShellController? _desktopShell;
    private DesktopTrayMonitor? _desktopTrayMonitor;
    private LauncherViewModel? _desktopViewModel;
    private ActionCommand? _desktopLaunchCommand;

    private void InitializeDesktopShell(
        Window mainWindow,
        LauncherViewModel viewModel,
        LauncherActionService actions,
        II18nService i18n)
    {
        _desktopViewModel = viewModel;
        _desktopLaunchCommand = new ActionCommand(
            () => viewModel.LaunchCommand.Execute(null),
            () => viewModel.IsTopLevelNavigationInteractive && viewModel.LaunchCommand.CanExecute(null));
        _desktopShell = new DesktopShellController(
            mainWindow, i18n, _desktopLaunchCommand, actions.ExitLauncher, actions.FlushPendingSettings);
        actions.LauncherShellActionHandler = _desktopShell.ApplyShellAction;
        viewModel.PropertyChanged += OnDesktopViewModelPropertyChanged;
        ApplyDesktopSettings();
    }

    private void OnDesktopViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.KeepRunningInTray)
            or nameof(LauncherViewModel.SelectedCloseActionIndex))
        {
            ApplyDesktopSettings();
        }
        // Prompt visibility and launch state already raise property notifications.
        _desktopLaunchCommand?.NotifyCanExecuteChanged();
    }

    private void ApplyDesktopSettings()
    {
        if (_desktopShell is null || _desktopViewModel is null) return;
        var settings = _desktopViewModel.DesktopSettings;
        _desktopShell.ApplySettings(settings);
        if (OperatingSystem.IsLinux() && (settings.KeepRunningInTray || settings.CloseAction == Models.WindowCloseAction.MinimizeToTray))
        {
            if (_desktopTrayMonitor is null)
            {
                _desktopShell.SetTrayHostAvailable(false);
                _desktopTrayMonitor = new DesktopTrayMonitor(_desktopShell.SetTrayHostAvailable);
                _ = _desktopTrayMonitor.StartAsync();
            }
        }
        else
        {
            _desktopTrayMonitor?.Dispose();
            _desktopTrayMonitor = null;
            if (OperatingSystem.IsLinux()) _desktopShell.SetTrayHostAvailable(false);
        }
    }

    private void DisposeDesktopShell()
    {
        if (_desktopViewModel is not null) _desktopViewModel.PropertyChanged -= OnDesktopViewModelPropertyChanged;
        if (_launcherActionService is not null) _launcherActionService.LauncherShellActionHandler = null;
        _desktopTrayMonitor?.Dispose();
        _desktopShell?.Dispose();
        _desktopTrayMonitor = null;
        _desktopShell = null;
        _desktopViewModel = null;
    }
}
