using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class InstanceWorldShellRightPaneView : UserControl
{
    public InstanceWorldShellRightPaneView()
    {
        InitializeComponent();
    }

    private void OnSortButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not FrontendShellViewModel shell)
        {
            return;
        }

        var menu = new ContextMenu
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = control,
            ItemsSource = new object[]
            {
                CreateMenuItem(shell.SD("instance.content.sort.file_name"), shell.SetInstanceWorldFileNameSort),
                CreateMenuItem(shell.SD("instance.content.sort.create_time"), shell.SetInstanceWorldCreateTimeSort),
                CreateMenuItem(shell.SD("instance.content.sort.modify_time"), shell.SetInstanceWorldModifyTimeSort)
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
}
