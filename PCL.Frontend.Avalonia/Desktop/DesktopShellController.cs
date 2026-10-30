using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

/// <summary>Coordinates launcher residency without giving the pet ownership of the main window.</summary>
internal sealed class DesktopShellController : IDisposable
{
    private readonly Window _mainWindow;
    private readonly II18nService _i18n;
    private readonly Action _exitLauncher;
    private readonly Action _flushSettings;
    private readonly ICommand _launchCommand;
    private readonly ActionCommand _showCommand;
    private readonly ActionCommand _desktopLaunchCommand;
    private readonly ActionCommand _togglePetCommand;
    private readonly ActionCommand _quitCommand;
    private readonly NativeMenuItem _trayShowItem;
    private readonly NativeMenuItem _trayLaunchItem;
    private readonly NativeMenuItem _trayPetItem;
    private readonly NativeMenuItem _trayQuitItem;
    private readonly MenuItem _petShowItem;
    private readonly MenuItem _petLaunchItem;
    private readonly MenuItem _petHideItem;
    private readonly MenuItem _petQuitItem;
    private readonly NativeMenu _trayMenu;
    private readonly ContextMenu _petMenu;
    private FrontendDesktopSettings _settings = new();
    private WindowState _restoreState = WindowState.Normal;
    private TrayIcon? _trayIcon;
    private DesktopPetWindow? _pet;
    private PixelPoint? _savedPetPosition;
    private bool _trayHostAvailable = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    private bool _disposed;
    private bool _hasShownMainWindow;
    private bool _hasAppliedSettings;

    public DesktopShellController(
        Window mainWindow,
        II18nService i18n,
        ICommand launchCommand,
        Action exitLauncher,
        Action flushSettings,
        PixelPoint? savedPetPosition = null)
    {
        _mainWindow = mainWindow;
        _i18n = i18n;
        _launchCommand = launchCommand;
        _exitLauncher = exitLauncher;
        _flushSettings = flushSettings;
        _savedPetPosition = savedPetPosition;
        _hasShownMainWindow = mainWindow.IsVisible;
        _showCommand = new ActionCommand(ShowLauncher);
        _desktopLaunchCommand = new ActionCommand(LaunchSelectedInstance, () => !_disposed && _launchCommand.CanExecute(null));
        _togglePetCommand = new ActionCommand(() => PetEnabledChanged?.Invoke(!_settings.PetEnabled));
        _quitCommand = new ActionCommand(Quit);
        _trayShowItem = new NativeMenuItem { Command = _showCommand };
        _trayLaunchItem = new NativeMenuItem { Command = _desktopLaunchCommand };
        _trayPetItem = new NativeMenuItem { Command = _togglePetCommand, ToggleType = NativeMenuItemToggleType.CheckBox };
        _trayQuitItem = new NativeMenuItem { Command = _quitCommand };
        _trayMenu = new NativeMenu
        {
            _trayShowItem, _trayLaunchItem, new NativeMenuItemSeparator(),
            _trayPetItem, new NativeMenuItemSeparator(), _trayQuitItem
        };
        _petShowItem = new MenuItem { Command = _showCommand };
        _petLaunchItem = new MenuItem { Command = _desktopLaunchCommand };
        _petHideItem = new MenuItem { Command = _togglePetCommand };
        _petQuitItem = new MenuItem { Command = _quitCommand };
        _petMenu = new ContextMenu
        {
            ItemsSource = new object[] { _petShowItem, _petLaunchItem, new Separator(), _petHideItem, _petQuitItem }
        };
        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.Opened += OnMainWindowOpened;
        _mainWindow.Closed += OnMainWindowClosed;
        _mainWindow.PropertyChanged += OnMainWindowPropertyChanged;
        _launchCommand.CanExecuteChanged += OnLaunchCanExecuteChanged;
        _i18n.Changed += RefreshText;
    }

    public event Action<bool>? PetEnabledChanged;
    public event Action<PixelPoint>? PetPositionChanged;

    internal DesktopPetWindow? Pet => _pet;
    internal TrayIcon? TrayIcon => _trayIcon;
    internal bool HasRecoverySurface =>
        _pet is { IsVisible: true, WindowState: not WindowState.Minimized }
        || _trayIcon?.IsVisible == true && _trayHostAvailable;

    public void ApplySettings(FrontendDesktopSettings settings)
    {
        if (_disposed) return;
        var normalized = settings with { PetScale = FrontendDesktopSettingsService.NormalizeScale(settings.PetScale) };
        if (_hasAppliedSettings && normalized == _settings)
        {
            // Reimporting a previously missing asset may restore the same managed path.
            if (_settings.PetEnabled) _pet?.ApplySettings(normalized);
            return;
        }
        _hasAppliedSettings = true;
        _settings = normalized;
        UpdateTrayIcon();
        if (_settings.PetEnabled)
        {
            if (_pet is null)
            {
                _pet = new DesktopPetWindow();
                _pet.RestoreRequested += ShowLauncher;
                _pet.PositionSaved += SavePetPosition;
                _pet.Closing += OnPetClosing;
                _pet.PropertyChanged += OnPetPropertyChanged;
            }
            _pet.ApplySettings(_settings);
            if (!_pet.IsVisible)
            {
                _pet.RestorePosition(_savedPetPosition);
                // An owned window can disappear with its owner when the launcher is hidden.
                _pet.Show();
            }
        }
        else
        {
            _pet?.Hide();
        }

        RefreshText();
        RestoreWithoutRecoverySurface();
    }

    public void SetTrayHostAvailable(bool available)
    {
        if (_disposed || _trayHostAvailable == available) return;
        _trayHostAvailable = available;
        // Avalonia chooses the Linux tray implementation when constructing the icon.
        RemoveTrayIcon();
        UpdateTrayIcon();
        RefreshText();
        RestoreWithoutRecoverySurface();
    }

    public void UpdateSavedPetPosition(PixelPoint? position)
    {
        if (_disposed || _savedPetPosition == position) return;
        _savedPetPosition = position;
        if (_pet?.IsVisible == true) _pet.RestorePosition(position);
    }

    public void ShowLauncher()
    {
        if (_disposed) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = _restoreState;
        _mainWindow.Activate();
    }

    public void ApplyShellAction(MinecraftLaunchShellActionKind action)
    {
        if (_disposed) return;
        switch (action)
        {
            case MinecraftLaunchShellActionKind.HideLauncher:
                HideLauncher();
                break;
            case MinecraftLaunchShellActionKind.MinimizeLauncher:
                MinimizeLauncher();
                break;
            case MinecraftLaunchShellActionKind.ShowLauncher:
                ShowLauncher();
                break;
        }
    }

    public void LaunchSelectedInstance()
    {
        if (_disposed) return;
        // Login, Java and launch confirmations are presented inside the main window.
        ShowLauncher();
        if (_launchCommand.CanExecute(null)) _launchCommand.Execute(null);
    }

    public void Quit()
    {
        if (_disposed) return;
        Dispose();
        _exitLauncher();
    }

    private void UpdateTrayIcon()
    {
        if (!_settings.KeepRunningInTray && !_settings.PetEnabled)
        {
            if (_trayIcon is not null) _trayIcon.IsVisible = false;
            return;
        }
        if (_trayIcon is not null)
        {
            _trayIcon.IsVisible = true;
            return;
        }
        try
        {
            _trayIcon = new TrayIcon
            {
                Icon = _mainWindow.Icon,
                Menu = _trayMenu,
                ToolTipText = FrontendApplicationIdentity.DisplayName,
                Command = _showCommand,
                IsVisible = true
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Desktop", "Could not create the launcher tray icon.");
            RemoveTrayIcon();
        }
    }

    private void RefreshText()
    {
        if (_disposed) return;
        _trayShowItem.Header = _i18n.T("desktop.actions.show_launcher");
        _trayLaunchItem.Header = _i18n.T("desktop.actions.launch_selected");
        _trayPetItem.Header = _i18n.T("desktop.actions.show_pet");
        _trayPetItem.IsChecked = _settings.PetEnabled;
        _trayQuitItem.Header = _i18n.T("desktop.actions.quit");
        _petShowItem.Header = _trayShowItem.Header;
        _petLaunchItem.Header = _trayLaunchItem.Header;
        _petHideItem.Header = _i18n.T("desktop.actions.hide_pet");
        _petQuitItem.Header = _trayQuitItem.Header;
        if (_pet is not null)
        {
            _pet.SetText(_i18n.T("desktop.pet.title"), _i18n.T("desktop.pet.hint"));
            _pet.ContextMenu = _petMenu;
        }
    }

    private void HideLauncher()
    {
        _flushSettings();
        _mainWindow.Hide();
    }

    private void MinimizeLauncher()
    {
        if (_settings.KeepRunningInTray && HasRecoverySurface) HideLauncher();
        else _mainWindow.WindowState = WindowState.Minimized;
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // OS shutdown and application Quit must never turn into close-to-tray.
        if (e.CloseReason != WindowCloseReason.WindowClosing || !_settings.KeepRunningInTray || !HasRecoverySurface) return;
        e.Cancel = true;
        HideLauncher();
    }

    private void OnMainWindowClosed(object? sender, EventArgs e) => Dispose();

    private void OnMainWindowOpened(object? sender, EventArgs e) => _hasShownMainWindow = true;

    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            if (_settings.KeepRunningInTray && HasRecoverySurface) HideLauncher();
        }
        else
        {
            _restoreState = _mainWindow.WindowState;
        }
    }

    private void OnPetClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_disposed || e.CloseReason != WindowCloseReason.WindowClosing) return;
        e.Cancel = true;
        PetEnabledChanged?.Invoke(false);
    }

    private void SavePetPosition(PixelPoint position)
    {
        _savedPetPosition = position;
        PetPositionChanged?.Invoke(position);
    }

    private void OnPetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.IsVisibleProperty || e.Property == Window.WindowStateProperty)
        {
            RestoreWithoutRecoverySurface();
        }
    }

    private void OnLaunchCanExecuteChanged(object? sender, EventArgs e) => _desktopLaunchCommand.NotifyCanExecuteChanged();

    private void RestoreWithoutRecoverySurface()
    {
        if (_hasShownMainWindow && !_mainWindow.IsVisible && !HasRecoverySurface) ShowLauncher();
    }

    private void RemoveTrayIcon()
    {
        if (_trayIcon is null) return;
        // Avalonia 11's macOS exporter requires the same menu for the icon's lifetime.
        _trayIcon.Dispose();
        _trayIcon = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mainWindow.Closing -= OnMainWindowClosing;
        _mainWindow.Opened -= OnMainWindowOpened;
        _mainWindow.Closed -= OnMainWindowClosed;
        _mainWindow.PropertyChanged -= OnMainWindowPropertyChanged;
        _launchCommand.CanExecuteChanged -= OnLaunchCanExecuteChanged;
        _i18n.Changed -= RefreshText;
        RemoveTrayIcon();
        foreach (var item in _trayMenu.Items.OfType<NativeMenuItem>()) item.Command = null;
        foreach (var item in _petMenu.Items.OfType<MenuItem>()) item.Command = null;
        if (_pet is not null)
        {
            _pet.PropertyChanged -= OnPetPropertyChanged;
            _pet.ContextMenu = null;
        }
        _pet?.Close();
        _pet = null;
    }
}
