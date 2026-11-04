using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.UI.Icons;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionDetailsView : UserControl, IDisposable
{
    private readonly FrontendExtensionSession _session;
    private readonly ExtensionInfo _info;
    private readonly bool _logs;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private IExtensionOverlay? _popup;
    private bool _disposed;
    internal event Action? Closed;

    public ExtensionDetailsView(FrontendExtensionSession session, ExtensionInfo info, bool logs)
    {
        _session = session; _info = info; _logs = logs;
        InitializeComponent();
        CloseIcon.IconData = PclIcons.Close.Data;
        CloseIcon.Command = CloseButton.Command = new ActionCommand(Close);
        _timer.Tick += (_, _) => Refresh();
        Refresh();
    }

    internal void Open()
    {
        _popup = _session.ShowOverlay(this, new ExtensionOverlayOptions
        {
            MaxWidth = 620, Margin = new(24), VerticalAlignment = VerticalAlignment.Center
        });
        _popup.Closed += () => Closed?.Invoke();
        if (_logs) _timer.Start();
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            if (_logs && LogText.IsVisible) LogText.Focus();
            else CloseButton.FindControl<Button>("ButtonHost")!.Focus();
        }, DispatcherPriority.Input);
    }

    internal void Refresh()
    {
        if (_disposed) return;
        HeadingText.Text = _session.T(_logs ? "logs" : "info");
        NameText.Text = _info.Text.Get("extension.name", _info.Text.Get("title", _info.Manifest.Name));
        global::Avalonia.Automation.AutomationProperties.SetName(this, NameText.Text + " · " + HeadingText.Text);
        CloseButton.Text = _session.T("close");
        ToolTip.SetTip(CloseIcon, CloseButton.Text);
        global::Avalonia.Automation.AutomationProperties.SetName(CloseIcon, CloseButton.Text);
        InfoPanel.IsVisible = !_logs;
        DescriptionText.Text = _info.Text.Get("extension.description", _info.Manifest.Description);
        var metadata = new List<string>
        {
            Field("id", _info.Manifest.Id), Field("version", _info.Manifest.Version),
            Field("author", _info.Manifest.Author),
            Field("type", _session.T(_info.IsBuiltIn ? "built_in" : "external")),
            Field("status", _session.T("states." + _info.Status.ToString().ToLowerInvariant()))
        };
        if (!string.IsNullOrWhiteSpace(_info.Manifest.Website)) metadata.Add(Field("website", _info.Manifest.Website));
        MetadataText.Text = string.Join('\n', metadata);
        NoticeText.Text = _session.T(_info.RemovalPending ? "pending_removal" : "restart_required");
        NoticeText.IsVisible = _info.RestartRequired;
        ErrorText.Text = _info.Error;
        ErrorText.IsVisible = !string.IsNullOrWhiteSpace(_info.Error);
        if (_logs)
        {
            var diagnostics = _session.Host.Diagnostics.Where(item => item.ExtensionId == _info.Manifest.Id).ToArray();
            var text = string.Join('\n', diagnostics.Select(item => $"{item.Time.LocalDateTime:yyyy-MM-dd HH:mm:ss} [{item.Level}] {item.Message}"));
            if (LogText.Text != text) LogText.Text = text;
            LogText.IsVisible = diagnostics.Length > 0;
            EmptyText.IsVisible = diagnostics.Length == 0;
            EmptyText.Text = _session.T("logs_empty");
            global::Avalonia.Automation.AutomationProperties.SetName(LogText, HeadingText.Text);
        }
        else LogText.IsVisible = false;
        string Field(string key, string? value) => _session.T("metadata." + key) + ": " + value;
    }

    internal void Close() => _popup?.Dispose();

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
