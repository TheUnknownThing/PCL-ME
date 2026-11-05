using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly DispatcherTimer _copyFeedbackTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private IExtensionOverlay? _popup;
    private bool _disposed;
    internal event Action? Closed;

    public ExtensionDetailsView(FrontendExtensionSession session, ExtensionInfo info, bool logs)
    {
        _session = session; _info = info; _logs = logs;
        InitializeComponent();
        ExtensionIcon.Data = Geometry.Parse(PclIcons.Extensions.Data);
        ProjectLinkIcon.Data = Geometry.Parse(PclIcons.Link.Data);
        CopyIdIcon.Data = Geometry.Parse(PclIcons.Copy.Data);
        CloseIcon.IconData = PclIcons.Close.Data;
        CloseIcon.Command = new ActionCommand(Close);
        CopyIdButton.Command = new ActionCommand(CopyId);
        _timer.Tick += (_, _) => Refresh();
        _copyFeedbackTimer.Tick += (_, _) =>
        {
            _copyFeedbackTimer.Stop();
            CopyFeedbackText.IsVisible = false;
        };
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
            else CloseIcon.FindControl<Button>("ButtonHost")!.Focus();
        }, DispatcherPriority.Input);
    }

    internal void Refresh()
    {
        if (_disposed) return;
        HeadingText.Text = _session.T(_logs ? "logs" : "info");
        HeadingText.IsVisible = _logs;
        NameText.Text = _info.Text.Get("extension.name", _info.Text.Get("title", _info.Manifest.Name));
        SetLink(ExtensionLink, _info.Manifest.Website, _session.T("open_website") + " · " + NameText.Text);
        ToolTip.SetTip(ExtensionLink, _session.T("open_website"));
        global::Avalonia.Automation.AutomationProperties.SetName(this, NameText.Text + " · " + HeadingText.Text);
        ToolTip.SetTip(CloseIcon, _session.T("close"));
        global::Avalonia.Automation.AutomationProperties.SetName(CloseIcon, _session.T("close"));
        InfoPanel.IsVisible = !_logs;
        IdLabelText.Text = _session.T("metadata.id");
        IdValueText.Text = _info.Manifest.Id;
        ToolTip.SetTip(CopyIdButton, _session.T("copy_id"));
        global::Avalonia.Automation.AutomationProperties.SetName(CopyIdButton, _session.T("copy_id") + " · " + _info.Manifest.Id);
        CopyFeedbackText.Text = _session.T("id_copied");
        VersionLabelText.Text = _session.T("metadata.version");
        VersionValueText.Text = _info.Manifest.Version;
        AuthorLabelText.Text = _session.T("metadata.author");
        AuthorValueText.Text = _info.Manifest.Author;
        AuthorLinkText.Text = AuthorValueText.Text;
        SetLink(AuthorLink, _info.Manifest.AuthorWebsite, AuthorValueText.Text);
        AuthorValueText.IsVisible = !AuthorLink.IsVisible;
        AuthorMetadata.IsVisible = !string.IsNullOrWhiteSpace(_info.Manifest.Author);
        BuiltInBadge.IsVisible = !_logs && _info.IsBuiltIn;
        BuiltInText.Text = _session.T("built_in");
        DescriptionText.Text = _info.Text.Get("extension.description", _info.Manifest.Description);
        DescriptionText.IsVisible = !string.IsNullOrWhiteSpace(DescriptionText.Text);
        NoticeText.Text = _session.T(_info.RemovalPending ? "pending_removal" : "restart_required");
        NoticePanel.IsVisible = _info.RestartRequired;
        ErrorText.Text = _info.Error;
        ErrorPanel.IsVisible = !string.IsNullOrWhiteSpace(_info.Error);
        if (_logs)
        {
            var diagnostics = _session.Host.Diagnostics.Where(item => item.ExtensionId == _info.Manifest.Id).ToArray();
            var text = string.Join('\n', diagnostics.Select(item => $"{item.Time.LocalDateTime:yyyy-MM-dd HH:mm:ss} [{item.Level}] {item.Message}"));
            if (LogText.Text != text) LogText.Text = text;
            LogText.IsVisible = diagnostics.Length > 0;
            EmptyPanel.IsVisible = diagnostics.Length == 0;
            EmptyText.Text = _session.T("logs_empty");
            global::Avalonia.Automation.AutomationProperties.SetName(LogText, HeadingText.Text);
        }
        else LogText.IsVisible = false;
    }

    private static void SetLink(HyperlinkButton link, string? website, string? label)
    {
        link.NavigateUri = Uri.TryCreate(website, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;
        link.IsVisible = link.NavigateUri is not null;
        ToolTip.SetTip(link, link.NavigateUri?.AbsoluteUri);
        global::Avalonia.Automation.AutomationProperties.SetName(link, label);
    }

    private async void CopyId()
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard
                ?? throw new InvalidOperationException(_session.T("unavailable"));
            await clipboard.SetTextAsync(_info.Manifest.Id);
            if (_disposed) return;
            CopyFeedbackText.IsVisible = true;
            _copyFeedbackTimer.Stop();
            _copyFeedbackTimer.Start();
        }
        catch (Exception ex)
        {
            if (!_disposed) _session.Notify(_session.T("operation_failed") + " " + ex.Message, isError: true);
        }
    }

    internal void Close() => _popup?.Dispose();

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _copyFeedbackTimer.Stop();
    }
}
