using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.Minecraft.Launch;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

/// <summary>Owns application residency independently of extension recovery surfaces.</summary>
internal sealed class DesktopShellController : IDisposable
{
    private readonly Window _mainWindow;
    private readonly II18nService _i18n;
    private readonly Action _exit;
    private readonly Action _flush;
    private readonly ICommand _launch;
    private readonly ActionCommand _showCommand;
    private readonly ActionCommand _launchCommand;
    private readonly ActionCommand _quitCommand;
    private readonly NativeMenuItem _showItem;
    private readonly NativeMenuItem _launchItem;
    private readonly NativeMenuItem _quitItem;
    private readonly HashSet<Window> _recovery = [];
    private FrontendDesktopSettings _settings = new();
    private bool _trayHostAvailable = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    private bool _disposed;
    private bool _shown;
    private WindowState _restoreState = WindowState.Normal;
    private NativeMenuItem[] _extensionItems = [];

    public DesktopShellController(Window mainWindow, II18nService i18n, ICommand launchCommand, Action exitLauncher, Action flushSettings)
    {
        _mainWindow = mainWindow;
        _i18n = i18n;
        _launch = launchCommand;
        _exit = exitLauncher;
        _flush = flushSettings;
        _shown = mainWindow.IsVisible;
        _showCommand = new(ShowLauncher);
        _launchCommand = new(LaunchSelectedInstance, () => !_disposed && _launch.CanExecute(null));
        _quitCommand = new(Quit);
        _showItem = new() { Command = _showCommand };
        _launchItem = new() { Command = _launchCommand };
        _quitItem = new() { Command = _quitCommand };
        _mainWindow.Closing += OnClosing;
        _mainWindow.Opened += OnOpened;
        _mainWindow.Closed += OnClosed;
        _mainWindow.PropertyChanged += OnWindowChanged;
        _launch.CanExecuteChanged += OnLaunchChanged;
        _i18n.Changed += RefreshText;
        RefreshText();
    }

    internal TrayIcon? TrayIcon { get; private set; }
    internal bool HasRecoverySurface => _recovery.Any(window => window.IsVisible && window.WindowState != WindowState.Minimized)
                                        || TrayIcon?.IsVisible == true && _trayHostAvailable;

    public IDisposable RegisterRecoverySurface(Window window)
    {
        _recovery.Add(window);
        window.PropertyChanged += OnRecoveryChanged;
        UpdateTray();
        return new ExtensionRegistration(() =>
        {
            window.PropertyChanged -= OnRecoveryChanged;
            _recovery.Remove(window);
            UpdateTray();
            RestoreWithoutRecoverySurface();
        });
    }

    public void SetExtensionTrayItems(IEnumerable<NativeMenuItem> items)
    {
        foreach (var item in _extensionItems) item.Command = null;
        _extensionItems = items.ToArray();
        UpdateTrayMenu();
    }

    public void ApplySettings(FrontendDesktopSettings settings)
    {
        if (_disposed) return;
        _settings = settings;
        UpdateTray();
        RestoreWithoutRecoverySurface();
    }

    public void SetTrayHostAvailable(bool available)
    {
        if (_disposed || _trayHostAvailable == available) return;
        _trayHostAvailable = available;
        // Avalonia monitors Linux tray hosts itself. Disposing its exporter here
        // can leave a queued D-Bus menu update targeting a removed handler.
        UpdateTray();
        RestoreWithoutRecoverySurface();
    }

    public void ShowLauncher()
    {
        if (_disposed) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = _restoreState;
        _mainWindow.Activate();
    }

    public void LaunchSelectedInstance()
    {
        if (_disposed) return;
        ShowLauncher();
        if (_launch.CanExecute(null)) _launch.Execute(null);
    }

    public void ApplyShellAction(MinecraftLaunchShellActionKind action)
    {
        if (_disposed) return;
        switch (action)
        {
            case MinecraftLaunchShellActionKind.HideLauncher:
                if (HasRecoverySurface) Hide();
                else _mainWindow.WindowState = WindowState.Minimized;
                break;
            case MinecraftLaunchShellActionKind.MinimizeLauncher:
                if (_settings.KeepRunningInTray && HasRecoverySurface) Hide();
                else _mainWindow.WindowState = WindowState.Minimized;
                break;
            case MinecraftLaunchShellActionKind.ShowLauncher: ShowLauncher(); break;
        }
    }

    public void Quit() { if (_disposed) return; Dispose(); _exit(); }

    private void UpdateTray()
    {
        if (_disposed) return;
        var visible = _settings.KeepRunningInTray || _settings.CloseAction == WindowCloseAction.MinimizeToTray || _recovery.Count > 0;
        if (!visible) { if (TrayIcon is not null) TrayIcon.IsVisible = false; return; }
        try
        {
            if (TrayIcon is null)
            {
                if (!_trayHostAvailable) return;
                TrayIcon = new TrayIcon { Icon = _mainWindow.Icon, ToolTipText = FrontendApplicationIdentity.DisplayName, Command = _showCommand };
                UpdateTrayMenu();
            }
            TrayIcon.IsVisible = true;
        }
        catch (Exception ex) { PCL.Core.Logging.LogWrapper.Warn(ex, "Desktop", "Could not create the tray icon."); }
    }

    private void UpdateTrayMenu()
    {
        if (TrayIcon is null) return;
        TrayIcon.Menu ??= new NativeMenu();
        foreach (var item in TrayIcon.Menu.Items.ToArray()) TrayIcon.Menu.Items.Remove(item);
        TrayIcon.Menu.Items.Add(_showItem);
        TrayIcon.Menu.Items.Add(_launchItem);
        if (_extensionItems.Length > 0) TrayIcon.Menu.Items.Add(new NativeMenuItemSeparator());
        foreach (var item in _extensionItems) TrayIcon.Menu.Items.Add(item);
        TrayIcon.Menu.Items.Add(new NativeMenuItemSeparator());
        TrayIcon.Menu.Items.Add(_quitItem);
    }

    private void RefreshText()
    {
        _showItem.Header = _i18n.T("desktop.actions.show_launcher");
        _launchItem.Header = _i18n.T("desktop.actions.launch_selected");
        _quitItem.Header = _i18n.T("desktop.actions.quit");
    }
    private void Hide() { _flush(); _mainWindow.Hide(); }
    private void RestoreWithoutRecoverySurface() { if (!_disposed && _shown && !_mainWindow.IsVisible && !HasRecoverySurface) ShowLauncher(); }
    private void OnOpened(object? sender, EventArgs e) => _shown = true;
    private void OnClosed(object? sender, EventArgs e) => Dispose();
    private void OnLaunchChanged(object? sender, EventArgs e) => _launchCommand.NotifyCanExecuteChanged();
    private void OnRecoveryChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.IsVisibleProperty || e.Property == Window.WindowStateProperty) RestoreWithoutRecoverySurface();
    }
    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            if (_settings.KeepRunningInTray && HasRecoverySurface) Hide();
        }
        else _restoreState = _mainWindow.WindowState;
    }
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason != WindowCloseReason.WindowClosing || _settings.CloseAction == WindowCloseAction.Exit || !HasRecoverySurface) return;
        e.Cancel = true;
        Hide();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mainWindow.Closing -= OnClosing;
        _mainWindow.Opened -= OnOpened;
        _mainWindow.Closed -= OnClosed;
        _mainWindow.PropertyChanged -= OnWindowChanged;
        _launch.CanExecuteChanged -= OnLaunchChanged;
        _i18n.Changed -= RefreshText;
        foreach (var window in _recovery) window.PropertyChanged -= OnRecoveryChanged;
        _recovery.Clear();
        var trayIcon = TrayIcon;
        TrayIcon = null;
        if (trayIcon is not null)
        {
            trayIcon.Menu = null;
            trayIcon.Command = null;
            trayIcon.IsVisible = false;
            // Detach subscribers before clearing commands, and let already queued
            // D-Bus menu updates finish before removing their handler.
            if (OperatingSystem.IsLinux()) Dispatcher.UIThread.Post(trayIcon.Dispose, DispatcherPriority.Background);
            else trayIcon.Dispose();
        }
        _showItem.Command = _launchItem.Command = _quitItem.Command = null;
        foreach (var item in _extensionItems) item.Command = null;
    }
}
