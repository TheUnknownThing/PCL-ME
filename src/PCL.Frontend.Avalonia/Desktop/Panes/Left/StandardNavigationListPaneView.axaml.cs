using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.UI.Animation;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Left;

internal sealed partial class StandardNavigationListPaneView : UserControl
{
    private LauncherViewModel? _launcher;
    private int _refreshVersion;
    private bool _animateOutQueued;

    public StandardNavigationListPaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public ObservableCollection<SidebarRenderRow> RenderRows { get; } = [];

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_launcher is not null)
        {
            _launcher.SidebarSections.CollectionChanged -= OnSidebarSectionsChanged;
            _launcher.ExtensionNavigationEntries.CollectionChanged -= OnExtensionNavigationChanged;
        }

        _launcher = DataContext as LauncherViewModel;
        if (_launcher is not null)
        {
            _launcher.SidebarSections.CollectionChanged += OnSidebarSectionsChanged;
            _launcher.ExtensionNavigationEntries.CollectionChanged += OnExtensionNavigationChanged;
        }

        ScheduleRefresh(animateOutCurrent: false);
    }

    private void OnSidebarSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRefresh(animateOutCurrent: RenderRows.Count > 0);
    }

    private void OnExtensionNavigationChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRefresh(animateOutCurrent: false);

    private void ScheduleRefresh(bool animateOutCurrent)
    {
        _animateOutQueued |= animateOutCurrent;
        var version = ++_refreshVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _refreshVersion)
                {
                    return;
                }

                var animateOut = _animateOutQueued;
                _animateOutQueued = false;
                await RefreshRowsAsync(animateOut, version);
            },
            DispatcherPriority.Background);
    }

    private async Task RefreshRowsAsync(bool animateOutCurrent, int version)
    {
        if (_launcher is null)
        {
            RenderRows.Clear();
            return;
        }

        var nativeRows = BuildRenderRows(_launcher.SidebarSections);
        var nextRows = nativeRows.Concat(_launcher.ExtensionNavigationEntries.Select((entry, index) =>
            new SidebarItemRenderRow(entry.Item, (nativeRows.Length + index) * SidebarListItemViewModel.EnterStaggerMilliseconds))).ToArray();
        if (animateOutCurrent && RenderRows.Count > 0)
        {
            var animatedRows = SidebarRowsHost
                .GetVisualDescendants()
                .OfType<Control>()
                .Where(Motion.GetAnimateOnVisible)
                .ToArray();
            if (animatedRows.Length > 0)
            {
                await Task.WhenAll(animatedRows.Select(Motion.PlayExitAsync));
            }

            if (version != _refreshVersion)
            {
                return;
            }
        }

        if (animateOutCurrent) RenderRows.Clear();
        // Remove retired rows first. Moving surviving rows past an obsolete one
        // makes ItemsControl recreate their containers and replay their entrance.
        for (var index = RenderRows.Count - 1; index >= 0; index--)
            if (!nextRows.Any(next => MatchesRow(RenderRows[index], next))) RenderRows.RemoveAt(index);
        for (var index = 0; index < nextRows.Length; index++)
        {
            var next = nextRows[index];
            var existing = RenderRows.FirstOrDefault(row => MatchesRow(row, next));
            if (existing is null) RenderRows.Insert(index, next);
            else
            {
                existing.EnterDelay = next.EnterDelay;
                var previousIndex = RenderRows.IndexOf(existing);
                if (previousIndex != index) RenderRows.Move(previousIndex, index);
            }
        }
        while (RenderRows.Count > nextRows.Length) RenderRows.RemoveAt(RenderRows.Count - 1);

        Dispatcher.UIThread.Post(UpdateAutoPaneWidth, DispatcherPriority.Loaded);
    }

    private static bool MatchesRow(SidebarRenderRow current, SidebarRenderRow next) => (current, next) switch
    {
        (SidebarItemRenderRow item, SidebarItemRenderRow candidate) => ReferenceEquals(item.Item, candidate.Item),
        (SidebarHeaderRenderRow header, SidebarHeaderRenderRow candidate) => header.Title == candidate.Title && header.EnterDelay == candidate.EnterDelay,
        _ => false
    };

    private static SidebarRenderRow[] BuildRenderRows(IEnumerable<SidebarSectionViewModel> sections)
    {
        List<SidebarRenderRow> rows = [];
        foreach (var section in sections)
        {
            if (section.HasTitle)
            {
                rows.Add(new SidebarHeaderRenderRow(section.Title, section.EnterDelay));
            }

            rows.AddRange(section.Items.Select(item => new SidebarItemRenderRow(item, item.EnterDelay)));
        }

        return rows.ToArray();
    }

    private void UpdateAutoPaneWidth()
    {
        if (_launcher is null)
        {
            return;
        }

        var itemControls = SidebarRowsHost
            .GetVisualDescendants()
            .OfType<PclListItem>()
            .ToArray();
        var headerControls = SidebarRowsHost
            .GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.DataContext is SidebarHeaderRenderRow)
            .ToArray();

        var maxWidth = 0d;
        foreach (var item in itemControls)
        {
            item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = item.DesiredSize.Width + item.Margin.Left + item.Margin.Right;
            maxWidth = Math.Max(maxWidth, width);
        }

        foreach (var header in headerControls)
        {
            header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = header.DesiredSize.Width + header.Margin.Left + header.Margin.Right;
            maxWidth = Math.Max(maxWidth, width);
        }

        if (maxWidth <= 0)
        {
            return;
        }

        // Reserve a little room for scrollbar/animation jitter and keep transitions smooth.
        _launcher.SetStandardSidebarAutoWidth(Math.Ceiling(maxWidth + 8));
    }
}

internal abstract class SidebarRenderRow(int enterDelay) : ViewModelBase
{
    private int _enterDelay = enterDelay;
    public int EnterDelay
    {
        get => _enterDelay;
        set => SetProperty(ref _enterDelay, value);
    }
}

internal sealed class SidebarHeaderRenderRow(string title, int enterDelay) : SidebarRenderRow(enterDelay)
{
    public string Title { get; } = title;
}

internal sealed class SidebarItemRenderRow(SidebarListItemViewModel item, int enterDelay) : SidebarRenderRow(enterDelay)
{
    public SidebarListItemViewModel Item { get; } = item;
}
