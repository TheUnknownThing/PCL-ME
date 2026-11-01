using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right.Sections;

internal sealed partial class InstanceResourceEntriesSection : UserControl
{
    private const double DefaultListChromeHeight = 83;
    private const double LayoutTolerance = 1;
    private double _listAvailableHeight = double.PositiveInfinity;
    private double _requestedBottomPadding;

    public InstanceResourceEntriesSection()
    {
        InitializeComponent();
        SizeChanged += (_, _) => RefreshListMaxHeight();
    }

    public void SetListBottomPadding(double bottomPadding)
    {
        _requestedBottomPadding = Math.Max(0, bottomPadding);
        RefreshListPadding();
    }

    public void SetListAvailableHeight(double availableHeight)
    {
        _listAvailableHeight = NormalizeMaxHeight(availableHeight);
        MaxHeight = _listAvailableHeight;
        RefreshListMaxHeight();
    }

    private void OnSortButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not LauncherViewModel shell)
        {
            return;
        }

        var menu = new ContextMenu
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = control,
            ItemsSource = new object[]
            {
                CreateMenuItem(shell.SD("instance.content.sort.file_name"), shell.SetInstanceResourceFileNameSort),
                CreateMenuItem(shell.SD("instance.content.sort.resource_name"), shell.SetInstanceResourceNameSort),
                CreateMenuItem(shell.SD("instance.content.sort.added_time"), shell.SetInstanceResourceCreateTimeSort),
                CreateMenuItem(shell.SD("instance.content.sort.file_size"), shell.SetInstanceResourceFileSizeSort)
            }
        };

        menu.Open(control);
    }

    private static MenuItem CreateMenuItem(string title, Action onClick)
    {
        var item = new MenuItem
        {
            Header = title
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    private void RefreshListMaxHeight()
    {
        if (double.IsInfinity(_listAvailableHeight))
        {
            EntriesList.MaxHeight = double.PositiveInfinity;
            return;
        }

        var chromeHeight = ResolveListChromeHeight();
        EntriesList.MaxHeight = Math.Max(0, _listAvailableHeight - chromeHeight);
        RefreshListPadding();
    }

    private double ResolveListChromeHeight()
    {
        if (Bounds.Height > 0 && EntriesList.Bounds.Height > 0 && Bounds.Height > EntriesList.Bounds.Height)
        {
            return Bounds.Height - EntriesList.Bounds.Height;
        }

        return DefaultListChromeHeight;
    }

    private static double NormalizeMaxHeight(double height)
    {
        return double.IsFinite(height) && height > 0
            ? height
            : double.PositiveInfinity;
    }

    private void RefreshListPadding()
    {
        var padding = EntriesList.Padding;
        var bottomPadding = IsListHeightConstrained()
            ? _requestedBottomPadding
            : 0;
        EntriesList.Padding = new Thickness(padding.Left, padding.Top, padding.Right, bottomPadding);
    }

    private bool IsListHeightConstrained()
    {
        return !double.IsInfinity(EntriesList.MaxHeight)
               && EntriesList.Bounds.Height >= EntriesList.MaxHeight - LayoutTolerance;
    }
}
