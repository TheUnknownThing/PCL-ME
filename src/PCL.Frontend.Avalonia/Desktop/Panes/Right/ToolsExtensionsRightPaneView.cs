using Avalonia.Controls;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed class ToolsExtensionsRightPaneView : UserControl
{
    private ExtensionManagerView? _manager;

    public ToolsExtensionsRightPaneView()
    {
        AttachedToVisualTree += (_, _) => ShowManager();
        DataContextChanged += (_, _) => { if (VisualRoot is not null) ShowManager(); };
        DetachedFromVisualTree += (_, _) => ClearManager();
    }

    private void ShowManager()
    {
        if (_manager is not null) return;
        if (DataContext is ToolsExtensionsRightPaneViewModel { Launcher.ExtensionSession: { } session })
            Content = _manager = new ExtensionManagerView(session);
    }

    private void ClearManager()
    {
        _manager?.Dispose();
        _manager = null;
        Content = null;
    }
}
