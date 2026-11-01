using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Extensions.Contracts;
using PCL.UI.Commands;
using PCL.UI.Controls;
using PCL.UI.Icons;

namespace PCL.Extensions.GlobalSearch;

internal sealed partial class SearchPalette : UserControl, IDisposable
{
    private readonly IExtensionContext _context;
    private readonly Dictionary<ToggleButton, (ExtensionSearchKind? Kind, string Key)> _filters = [];
    private CancellationTokenSource? _queryCancellation;
    private ExtensionSearchKind? _kind;
    private int _generation;
    private bool _disposed;
    private bool _searching;
    private bool _executing;
    public event Action? CloseRequested;

    public SearchPalette(IExtensionContext context)
    {
        _context = context;
        InitializeComponent();
        CloseSearch.IconData = PclIcons.Close.Data;
        EmptyIcon.Data = Geometry.Parse(PclIcons.Search.Data);
        CloseSearch.Click += (_, _) => CloseRequested?.Invoke();
        RetrySearch.Command = new ActionCommand(QueueQuery);
        foreach (var (kind, key) in new (ExtensionSearchKind?, string)[]
        {
            (null, "all"), (ExtensionSearchKind.Instance, "instances"), (ExtensionSearchKind.Page, "pages"),
            (ExtensionSearchKind.Setting, "settings"), (ExtensionSearchKind.Command, "commands"), (ExtensionSearchKind.Help, "help")
        })
        {
            var filter = new ToggleButton { Name = "Filter_" + key, Classes = { "filter" }, IsChecked = kind is null };
            _filters.Add(filter, (kind, key)); Filters.Children.Add(filter);
            filter.Click += (_, _) =>
            {
                _kind = kind;
                foreach (var pair in _filters) pair.Key.IsChecked = pair.Value.Kind == _kind;
                QueueQuery();
                SearchQuery.FocusSearchTextBox();
            };
        }
        SearchResults.ItemTemplate = new FuncDataTemplate<ExtensionSearchResult>((result, scope) =>
        {
            // Avalonia calls templates with null when recycling item containers.
            if (result is null) return null;
            var row = new PclListItem
            {
                Title = result.Title, Info = Describe(result), TitleSuffix = FormatShortcut(result.Shortcut),
                IconData = result.IconData ?? Icon(result.Kind), Height = 58,
                Command = new ActionCommand(() => { SearchResults.SelectedItem = result; _ = ExecuteSelectedAsync(); })
            };
            row.Bind(PclListItem.IsSelectedProperty, new Binding(nameof(ListBoxItem.IsSelected)) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) } });
            AutomationProperties.SetName(row, result.Title + ", " + Describe(result));
            return row;
        });
        SearchQuery.PropertyChanged += (_, e) => { if (e.Property == PclSearchBox.TextProperty) QueueQuery(); };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            SearchQuery.FocusSearchTextBox(true);
            QueueQuery();
        }, DispatcherPriority.Input);
        RefreshText();
    }

    internal void RefreshText()
    {
        Heading.Text = T("title");
        SearchQuery.Watermark = T("watermark");
        Shortcut.Text = FormatShortcut(_context.Manifest.Commands.Single().Shortcut);
        NavigateHint.Text = T("navigate"); OpenHint.Text = T("open"); CloseHint.Text = T("close");
        RetrySearch.Text = T("retry");
        AutomationProperties.SetName(SearchQuery, T("title"));
        AutomationProperties.SetName(CloseSearch, T("close"));
        foreach (var pair in _filters) pair.Key.Content = T(pair.Value.Key);
        if (TopLevel.GetTopLevel(this) is not null) QueueQuery();
    }

    private string T(string key) => _context.Text.Get(key);
    private string Describe(ExtensionSearchResult result)
    {
        var category = T(result.Kind switch
        {
            ExtensionSearchKind.Page => "pages", ExtensionSearchKind.Setting => "settings", ExtensionSearchKind.Instance => "instances",
            ExtensionSearchKind.Command => "commands", ExtensionSearchKind.Help => "help", _ => "extensions"
        });
        var description = string.Join(' ', result.Description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(description) ? category : category + " · " + description;
    }

    private static string Icon(ExtensionSearchKind kind) => kind switch
    {
        ExtensionSearchKind.Page => PclIcons.Page.Data,
        ExtensionSearchKind.Setting => PclIcons.SettingsOutline.Data,
        ExtensionSearchKind.Instance => PclIcons.GetSidebarIcon("Minecraft").Data,
        ExtensionSearchKind.Help => PclIcons.InfoCircle.Data,
        ExtensionSearchKind.Command => PclIcons.Command.Data,
        _ => PclIcons.Extensions.Data
    };

    private static string FormatShortcut(string? shortcut) => (shortcut ?? "")
        .Replace("Primary", OperatingSystem.IsMacOS() ? "⌘" : "Ctrl", StringComparison.Ordinal)
        .Replace("Shift", OperatingSystem.IsMacOS() ? "⇧" : "Shift", StringComparison.Ordinal)
        .Replace("Meta", "⌘", StringComparison.Ordinal)
        .Replace("+", OperatingSystem.IsMacOS() ? " " : " + ", StringComparison.Ordinal);

    private async void QueueQuery()
    {
        if (_disposed || TopLevel.GetTopLevel(this) is null) return;
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose();
        _queryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_context.Lifetime);
        var token = _queryCancellation.Token;
        var generation = ++_generation;
        _searching = true;
        SearchResults.SelectedIndex = -1;
        SearchResults.IsEnabled = false;
        SearchResults.Opacity = .45;
        RetrySearch.IsVisible = false;
        SearchStatus.Text = T("searching");
        EmptyState.IsVisible = false;
        try
        {
            await Task.Delay(100, token);
            var results = await _context.Search.QueryAsync(new(SearchQuery.Text, _context.Settings.Get("limit", 30), _kind), token);
            if (_disposed || generation != _generation) return;
            _searching = false;
            SearchResults.ItemsSource = results;
            SearchResults.IsEnabled = true;
            SearchResults.Opacity = 1;
            SearchResults.SelectedIndex = results.Count > 0 ? 0 : -1;
            if (results.Count > 0) SearchResults.ScrollIntoView(results[0]);
            EmptyState.IsVisible = results.Count == 0;
            EmptyTitle.Text = T("empty"); EmptyHint.Text = T("empty_hint");
            SearchStatus.Text = results.Count == 0 ? T("no_results") : string.IsNullOrWhiteSpace(SearchQuery.Text) ? T("suggested") : T("results").Replace("{count}", results.Count.ToString(), StringComparison.Ordinal);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _context.Log(ExtensionLogLevel.Error, "Search failed.", ex);
            if (_disposed || generation != _generation) return;
            _searching = false;
            SearchResults.ItemsSource = null; SearchResults.Opacity = 1;
            EmptyState.IsVisible = true; EmptyTitle.Text = T("failed"); EmptyHint.Text = T("failed_hint");
            RetrySearch.IsVisible = true; SearchStatus.Text = T("no_results");
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Route == RoutingStrategies.Bubble && e.Key == Key.Enter && (SearchQuery.IsKeyboardFocusWithin || SearchResults.IsKeyboardFocusWithin)) { e.Handled = true; await ExecuteSelectedAsync(); }
        else if (e.Route == RoutingStrategies.Tunnel && (e.Key is Key.Down or Key.Up) && !_searching && SearchResults.ItemCount > 0)
        {
            SearchResults.SelectedIndex = Math.Clamp(SearchResults.SelectedIndex + (e.Key == Key.Down ? 1 : -1), 0, SearchResults.ItemCount - 1);
            if (SearchResults.SelectedItem is { } selected) SearchResults.ScrollIntoView(selected);
            e.Handled = true;
        }
    }

    private async Task ExecuteSelectedAsync()
    {
        if (_disposed || _searching || _executing || SearchResults.SelectedItem is not ExtensionSearchResult result) return;
        _executing = true; SearchResults.IsEnabled = false;
        try
        {
            await _context.Search.ExecuteAsync(result, _context.Lifetime);
            if (!_disposed) CloseRequested?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _context.Log(ExtensionLogLevel.Error, "Search action failed.", ex);
            if (!_disposed) { SearchStatus.Text = T("action_failed"); _context.Launcher.Notify(ex.Message, true); }
        }
        finally { _executing = false; if (!_disposed) SearchResults.IsEnabled = !_searching; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _generation++;
        _queryCancellation?.Cancel(); _queryCancellation?.Dispose();
    }
}
