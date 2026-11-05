using Avalonia.Controls;
using PCL.Extensions.Contracts;
using PCL.Extensions.Runtime;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class ExtensionManagerView : UserControl, IDisposable
{
    private readonly FrontendExtensionSession _session;
    private readonly Dictionary<string, ExtensionCardView> _cards = new(StringComparer.Ordinal);
    private bool _disposed;
    private bool _installing;
    private ExtensionUrlInstallView? _urlInstaller;
    private ExtensionTrustWarningView? _trustWarning;

    public ExtensionManagerView(FrontendExtensionSession session)
    {
        _session = session;
        InitializeComponent();
        InstallButton.Command = new ActionCommand(() => InstallPackage());
        InstallUrlButton.Command = new ActionCommand(() =>
        {
            var installer = new ExtensionUrlInstallView();
            _urlInstaller = installer;
            installer.Installed += Reveal;
            installer.Closed += () => { if (ReferenceEquals(_urlInstaller, installer)) _urlInstaller = null; };
            installer.Open(session);
        });
        ExtensionSearch.PropertyChanged += (_, e) => { if (e.Property == PclSearchBox.TextProperty) FilterCards(); };
        SizeChanged += (_, _) => Classes.Set("compact", Bounds.Width < 700);
        ActionsButton.Command = new ActionCommand(() => ActionsButton.ContextFlyout?.ShowAt(ActionsButton));
        session.AttachManager(this);
        Refresh();
        if (!session.HasAcknowledgedTrustWarning) ShowTrustWarning();
    }

    private void ShowTrustWarning()
    {
        var warning = _trustWarning = new ExtensionTrustWarningView();
        warning.Acknowledged += () => { if (ReferenceEquals(_trustWarning, warning)) _trustWarning = null; };
        warning.Open(_session);
    }

    internal void Refresh()
    {
        if (_disposed) return;
        ActionsButton.ContextFlyout?.Hide();
        ActionsButton.ContextFlyout = _session.HasExtensionMenuEntries ? _session.CreateExtensionMenu() : null;
        TitleText.Text = _session.T("title");
        InstallButton.Text = _session.T("install_file");
        InstallUrlButton.Text = _session.T("install_url");
        _urlInstaller?.RefreshText();
        _trustWarning?.RefreshText();
        ExtensionSearch.Watermark = _session.T("search_hint");
        global::Avalonia.Automation.AutomationProperties.SetName(ExtensionSearch, _session.T("search_hint"));
        EmptyText.Text = _session.T("search_empty");
        ActionsButton.Text = _session.T("actions");
        ActionsButton.IsVisible = _session.HasExtensionMenuEntries;
        SafeModeText.Text = _session.T("safe_mode");
        SafeModeText.IsVisible = _session.Host.SafeMode;
        var extensions = _session.Host.Extensions;
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
        FilterCards();
        var errors = _session.Host.Diagnostics.Where(item => item.Level == ExtensionLogLevel.Error).TakeLast(20).ToArray();
        DiagnosticsCard.Header = _session.T("diagnostics");
        DiagnosticsCard.IsVisible = errors.Length > 0;
        DiagnosticsText.Text = string.Join('\n', errors.Select(item => $"{item.Time:t} [{item.ExtensionId}] {item.Message}"));
    }

    private void FilterCards()
    {
        var query = ExtensionSearch.Text?.Trim() ?? "";
        var matching = 0;
        foreach (var card in _cards.Values)
        {
            var info = card.Info;
            var text = string.Join(' ', info.Manifest.Id, info.Manifest.Name, info.Manifest.Author, info.Manifest.Description,
                info.Text.Get("extension.name", info.Text.Get("title", info.Manifest.Name)),
                info.Text.Get("extension.description", info.Manifest.Description));
            card.IsVisible = text.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (card.IsVisible) matching++;
        }
        EmptyText.IsVisible = matching == 0;
        var extensions = _session.Host.Extensions;
        CountText.Text = query.Length == 0
            ? _session.T("count", new Dictionary<string, object?> { ["total"] = extensions.Count, ["enabled"] = extensions.Count(info => info.IsEnabled) })
            : _session.T("filtered_count", new Dictionary<string, object?> { ["matching"] = matching, ["total"] = extensions.Count });
    }

    private void Reveal(ExtensionInfo info)
    {
        if (_disposed) return;
        ExtensionSearch.Text = "";
        Refresh();
        _cards[info.Manifest.Id].Expand();
        _cards[info.Manifest.Id].BringIntoView();
    }

    private async void InstallPackage()
    {
        if (_disposed || _installing) return;
        _installing = true;
        InstallButton.IsEnabled = false;
        FeedbackText.IsVisible = false;
        try
        {
            var path = await _session.PickFileAsync(_session.T("install_file"), ["*.pclext", "*.zip"]);
            if (path is null || _disposed) return;
            var info = await _session.Host.InstallAsync(path);
            if (_disposed) return;
            Reveal(info);
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
        ActionsButton.ContextFlyout?.Hide();
        ActionsButton.ContextFlyout = null;
        _session.DetachManager(this);
        _urlInstaller?.Close();
        _trustWarning?.Close();
        _trustWarning = null;
        foreach (var card in _cards.Values) card.Dispose();
        _cards.Clear();
    }
}
