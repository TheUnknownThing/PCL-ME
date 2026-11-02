using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Animation;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private OverlaySession? _overlay;
    internal Control? OverlayContent => _overlay?.Content;

    private IExtensionOverlay ShowOverlay(IExtensionContext context, Control content, ExtensionOverlayOptions options)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(content);
        context.Lifetime.ThrowIfCancellationRequested();
        if (!CanInteract) throw new ExtensionOperationException("launcher.busy", T("unavailable"));
        if (double.IsNaN(options.MaxWidth) || options.MaxWidth <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        _overlay?.Dispose();
        _shell.ShowLauncher();
        var host = _window.FindControl<Grid>("ExtensionOverlayHost") ?? throw new InvalidOperationException("The launcher overlay host is unavailable.");
        var overlay = new OverlaySession(this, context, host, content, options);
        _overlay = overlay;
        return overlay;
    }

    private sealed class OverlaySession : IExtensionOverlay
    {
        private readonly FrontendExtensionSession _session;
        private readonly IExtensionContext _context;
        private readonly Grid _host;
        private readonly Grid _layer = new();
        private readonly Border _backdrop;
        private readonly Border _panel;
        private readonly IInputElement? _previousFocus;
        private readonly ExtensionOverlayOptions _options;
        public IExtensionContext Owner => _context;
        public Control Content { get; }
        public bool IsOpen { get; private set; } = true;
        public event Action? Closed;

        public OverlaySession(FrontendExtensionSession session, IExtensionContext context, Grid host, Control content, ExtensionOverlayOptions options)
        {
            _session = session; _context = context; _host = host; Content = content; _options = options;
            _previousFocus = session._window.FocusManager?.GetFocusedElement();
            _backdrop = new Border { Classes = { "pcl-dialog-overlay" } };
            _backdrop.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Pcl.Overlay"));
            _backdrop.PointerPressed += (_, e) => { e.Handled = true; if (options.DismissOnOutsideClick) Dispose(); };
            var surface = new ContentControl
            {
                Content = content, Margin = options.Margin, MaxWidth = options.MaxWidth,
                HorizontalAlignment = options.HorizontalAlignment, VerticalAlignment = options.VerticalAlignment,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
            };
            KeyboardNavigation.SetTabNavigation(surface, KeyboardNavigationMode.Cycle);
            _panel = new Border { Classes = { "pcl-dialog-panel" }, Child = surface };
            _layer.Children.Add(_backdrop); _layer.Children.Add(_panel);
            host.Children.Add(_layer); host.IsVisible = true;
            _layer.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
            PclModalMotion.ResetToClosedState(_backdrop, _panel);
            _ = PclModalMotion.PlayOpenAsync(_backdrop, _panel);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (IsOpen && _options.DismissOnEscape && e.Key == Key.Escape) { e.Handled = true; Dispose(); }
        }

        public void Dispose()
        {
            Dispatcher.UIThread.VerifyAccess();
            if (!IsOpen) return;
            IsOpen = false;
            _layer.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            if (ReferenceEquals(_session._overlay, this)) _session._overlay = null;
            try { if (Content is IDisposable resource) resource.Dispose(); }
            catch (Exception ex) { _context.Log(ExtensionLogLevel.Error, "Overlay cleanup failed.", ex); }
            if (_previousFocus is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } previous && TopLevel.GetTopLevel(previous) is not null) previous.Focus();
            try { Closed?.Invoke(); }
            catch (Exception ex) { _context.Log(ExtensionLogLevel.Error, "Overlay close callback failed.", ex); }
            _ = CloseVisualAsync();
        }

        private async Task CloseVisualAsync()
        {
            await PclModalMotion.PlayCloseAsync(_backdrop, _panel);
            _host.Children.Remove(_layer);
            _host.IsVisible = _host.Children.Count > 0;
        }
    }
}
