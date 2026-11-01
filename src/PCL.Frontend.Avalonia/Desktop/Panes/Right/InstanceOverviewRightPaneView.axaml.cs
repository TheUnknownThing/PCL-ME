using Avalonia.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class InstanceOverviewRightPaneView : UserControl
{
    public InstanceOverviewRightPaneView()
    {
        InitializeComponent();
        ContextRequested += (_, e) =>
        {
            if (e.Handled || DataContext is not ViewModels.LauncherViewModel { ExtensionSession: { } session }) return;
            var menu = session.CreateInstanceMenu();
            if (menu.ItemCount == 0) return;
            menu.Open(this);
            e.Handled = true;
        };
    }
}
