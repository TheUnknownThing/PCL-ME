using Avalonia;
using Avalonia.Controls;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.DesktopPet;

public sealed class DesktopPetExtension : IExtension
{
    private IExtensionContext? _context;
    private IExtensionUi? _ui;
    private DesktopPetWindow? _window;
    private bool _stopping;
    private bool _updatingImage;
    private string _managedImage = "";
    internal DesktopPetWindow? Window => _window;

    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        _context = context;
        _ui = context.GetService<IExtensionUi>();
        _stopping = false;
        _window = new DesktopPetWindow();
        _window.SingleClicked += () => Execute(PetSettingsService.Read(context.Settings).PetClickAction);
        _window.DoubleClicked += () => Execute(PetSettingsService.Read(context.Settings).PetDoubleClickAction);
        _window.PositionSaved += point => { context.Settings.Set("x", point.X); context.Settings.Set("y", point.Y); };
        _window.Closing += OnClosing;
        context.Settings.Changed += OnSettingsChanged;
        context.Events.Subscribe("launcher.locale.changed", _ => RefreshText());
        ApplySettings();
        var x = context.Settings.Get<int?>("x", null);
        var y = context.Settings.Get<int?>("y", null);
        _window.RestorePosition(x is int px && y is int py ? new PixelPoint(px, py) : null);
        _ui.ShowWindow(_window, new() { IsRecoverySurface = true });
        var command = context.Manifest.Commands.Single();
        context.Commands.Register(command, token => context.DisableAsync(token));
        _ui.RegisterAction(new("pcl.desktop-pet.hide", command.Title, command.Id, ExtensionActionLocation.Tray, command.TitleKey));
        _ui.RegisterView(new() { Id = "pcl.desktop-pet.preview", Title = "Pet preview", TitleKey = "pet.title", Location = ExtensionViewLocation.Settings },
            _ => context.Own(new PetPreview(context, _ui)));
        RefreshText();
        return ValueTask.CompletedTask;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_stopping || e.CloseReason != WindowCloseReason.WindowClosing) return;
        e.Cancel = true;
        Execute(PetAction.HidePet);
    }

    private void OnSettingsChanged(string key)
    {
        if (_stopping || key is "x" or "y" || _updatingImage) return;
        _ui!.Dispatch(ApplySettings);
    }

    private void ApplySettings()
    {
        if (_stopping || _context is not { } context || _window is null) return;
        var settings = PetSettingsService.Read(context.Settings);
        if (settings.PetImagePath.Length > 0 && settings.PetImagePath != _managedImage)
        {
            _updatingImage = true;
            try
            {
                _managedImage = PetImageService.Import(context.DataDirectory, settings.PetImagePath, allowAnimation: false);
                context.Settings.Set("image", _managedImage);
                settings = settings with { PetImagePath = _managedImage };
            }
            catch (Exception ex)
            {
                context.Log(ExtensionLogLevel.Warning, "Image import failed.", ex);
                context.Launcher.Notify(context.Text.Get("settings.image_error_message"), isError: true);
                context.Settings.Set("image", _managedImage);
                settings = settings with { PetImagePath = _managedImage };
            }
            finally { _updatingImage = false; }
        }
        _window.ApplySettings(settings);
    }

    private void RefreshText()
    {
        if (_context is not { } context || _window is null) return;
        _window.SetText(context.Text.Get("pet.title"), context.Text.Get("pet.hint"));
        var menu = new ContextMenu();
        Add("actions.show_launcher", PetAction.ShowLauncher);
        Add("actions.launch_selected", PetAction.LaunchSelected);
        menu.Items.Add(new Separator());
        Add("actions.hide_pet", PetAction.HidePet);
        Add("actions.quit", PetAction.Quit);
        _window.ContextMenu = menu;
        void Add(string key, PetAction action)
        {
            var item = new MenuItem { Header = context.Text.Get(key) };
            item.Click += (_, _) => Execute(action);
            menu.Items.Add(item);
        }
    }

    private async void Execute(PetAction action)
    {
        if (_stopping || _context is not { } context) return;
        try
        {
            switch (action)
            {
                case PetAction.PlayAnimation: _window?.Sprite.PlayClickAnimation(); break;
                case PetAction.ShowLauncher: context.Launcher.ShowLauncher(); break;
                case PetAction.HidePet: await context.DisableAsync(); break;
                case PetAction.LaunchSelected: await context.Commands.ExecuteAsync("pcl.launcher.launch-selected", context.Lifetime); break;
                case PetAction.Quit: await context.Commands.ExecuteAsync("pcl.launcher.quit"); break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { context.Log(ExtensionLogLevel.Error, "Pet action failed.", ex); context.Launcher.Notify(ex.Message, true); }
    }

    public ValueTask DisposeAsync()
    {
        _stopping = true;
        if (_context is not null) _context.Settings.Changed -= OnSettingsChanged;
        if (_window is not null)
        {
            _window.Closing -= OnClosing;
            _window.ContextMenu = null;
            _window.Close();
        }
        _window = null;
        _context = null;
        return ValueTask.CompletedTask;
    }

    private sealed class PetPreview : UserControl, IDisposable
    {
        private readonly IExtensionContext _context;
        private readonly IExtensionUi _ui;
        private readonly DesktopPetSprite _sprite = new() { Width = 120, Height = 156 };
        private bool _disposed;

        public PetPreview(IExtensionContext context, IExtensionUi ui)
        {
            _context = context;
            _ui = ui;
            Content = _sprite;
            context.Settings.Changed += OnSettingsChanged;
            Refresh();
        }

        private void OnSettingsChanged(string key) { if (key is not ("x" or "y")) _ui.Dispatch(Refresh); }
        private void Refresh()
        {
            if (_disposed) return;
            var settings = PetSettingsService.Read(_context.Settings);
            if (settings.PetUseCustom) _sprite.SetImagePath(settings.PetImagePath);
            else _sprite.SetPreset(PetPresetCatalog.Find(settings.PetPresetId), 256);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _context.Settings.Changed -= OnSettingsChanged;
            _sprite.Dispose();
        }
    }
}
