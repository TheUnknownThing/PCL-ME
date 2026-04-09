using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right.Sections;

internal sealed partial class InstanceResourceEntriesSection : UserControl
{
    public InstanceResourceEntriesSection()
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
                CreateMenuItem("文件名", shell.SetInstanceResourceFileNameSort),
                CreateMenuItem("资源名称", shell.SetInstanceResourceNameSort),
                CreateMenuItem("加入时间", shell.SetInstanceResourceCreateTimeSort),
                CreateMenuItem("文件大小", shell.SetInstanceResourceFileSizeSort)
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
