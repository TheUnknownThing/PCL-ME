using Avalonia.Controls;
using PCL.Extensions.Contracts;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionManagerView : UserControl, IDisposable
{
    private readonly FrontendExtensionSession _session;
    private readonly Dictionary<string, ExtensionCardView> _cards = new(StringComparer.Ordinal);
    private bool _disposed;
    private bool _installing;

    public ExtensionManagerView(FrontendExtensionSession session)
    {
        _session = session;
        InitializeComponent();
        InstallButton.Command = new ActionCommand(() => InstallPackage());
        session.AttachManager(this);
        Refresh();
    }

    internal void Refresh()
    {
        if (_disposed) return;
        OverviewCard.Header = _session.T("title");
        OverviewText.Text = _session.T("overview");
        InstallButton.Text = _session.T("install");
        TrustText.Text = _session.T("native_code");
        SafeModeText.Text = _session.T("safe_mode");
        SafeModeText.IsVisible = _session.Host.SafeMode;
        var extensions = _session.Host.Extensions;
        CountText.Text = _session.T("count", new Dictionary<string, object?> { ["total"] = extensions.Count, ["enabled"] = extensions.Count(info => info.IsEnabled) });
        foreach (var id in _cards.Keys.Where(id => extensions.All(info => info.Manifest.Id != id)).ToArray())
        {
            var card = _cards[id];
            ExtensionCards.Children.Remove(card);
            card.Dispose();
            _cards.Remove(id);
        }
        foreach (var info in extensions.OrderByDescending(info => info.IsBuiltIn).ThenBy(info => info.Manifest.Id, StringComparer.Ordinal))
        {
            if (_cards.TryGetValue(info.Manifest.Id, out var previous) && !ReferenceEquals(previous.Info, info))
            {
                ExtensionCards.Children.Remove(previous);
                previous.Dispose();
                _cards.Remove(info.Manifest.Id);
            }
            if (!_cards.TryGetValue(info.Manifest.Id, out var card))
            {
                card = new ExtensionCardView(_session, info);
                _cards.Add(info.Manifest.Id, card);
                ExtensionCards.Children.Add(card);
            }
            card.Refresh();
        }
        var errors = _session.Host.Diagnostics.Where(item => item.Level == ExtensionLogLevel.Error).TakeLast(20).ToArray();
        DiagnosticsCard.Header = _session.T("diagnostics");
        DiagnosticsCard.IsVisible = errors.Length > 0;
        DiagnosticsText.Text = string.Join('\n', errors.Select(item => $"{item.Time:t} [{item.ExtensionId}] {item.Message}"));
    }

    private async void InstallPackage()
    {
        if (_disposed || _installing) return;
        _installing = true;
        InstallButton.IsEnabled = false;
        FeedbackText.IsVisible = false;
        try
        {
            var path = await _session.PickFileAsync(_session.T("install"), ["*.pclext", "*.zip"]);
            if (path is null || _disposed) return;
            var info = await _session.Host.InstallAsync(path);
            if (_disposed) return;
            Refresh();
            _cards[info.Manifest.Id].Expand();
            _cards[info.Manifest.Id].BringIntoView();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_disposed) return;
            FeedbackText.Text = _session.T("operation_failed") + " " + ex.Message;
            FeedbackText.IsVisible = true;
        }
        finally
        {
            _installing = false;
            if (!_disposed) InstallButton.IsEnabled = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.DetachManager(this);
        foreach (var card in _cards.Values) card.Dispose();
        _cards.Clear();
    }
}
