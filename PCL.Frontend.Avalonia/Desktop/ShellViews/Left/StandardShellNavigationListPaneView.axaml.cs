using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Left;

internal sealed partial class StandardShellNavigationListPaneView : UserControl
{
    private FrontendShellViewModel? _shell;
    private int _refreshVersion;

    public StandardShellNavigationListPaneView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public ObservableCollection<SidebarRenderRow> RenderRows { get; } = [];

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_shell is not null)
        {
            _shell.SidebarSections.CollectionChanged -= OnSidebarSectionsChanged;
        }

        _shell = DataContext as FrontendShellViewModel;
        if (_shell is not null)
        {
            _shell.SidebarSections.CollectionChanged += OnSidebarSectionsChanged;
        }

        ScheduleRefresh(animateOutCurrent: false);
    }

    private void OnSidebarSectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScheduleRefresh(animateOutCurrent: RenderRows.Count > 0);
    }

    private void ScheduleRefresh(bool animateOutCurrent)
    {
        var version = ++_refreshVersion;
        Dispatcher.UIThread.Post(
            async () =>
            {
                if (version != _refreshVersion)
                {
                    return;
                }

                await RefreshRowsAsync(animateOutCurrent, version);
            },
            DispatcherPriority.Background);
    }

    private async Task RefreshRowsAsync(bool animateOutCurrent, int version)
    {
        if (_shell is null)
        {
            RenderRows.Clear();
            return;
        }

        var nextRows = BuildRenderRows(_shell.SidebarSections);
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

        RenderRows.Clear();
        foreach (var row in nextRows)
        {
            RenderRows.Add(row);
        }
    }

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
}

internal abstract class SidebarRenderRow(int enterDelay)
{
    public int EnterDelay { get; } = enterDelay;
}

internal sealed class SidebarHeaderRenderRow(string title, int enterDelay) : SidebarRenderRow(enterDelay)
{
    public string Title { get; } = title;
}

internal sealed class SidebarItemRenderRow(SidebarListItemViewModel item, int enterDelay) : SidebarRenderRow(enterDelay)
{
    public SidebarListItemViewModel Item { get; } = item;
}
