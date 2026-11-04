using Avalonia.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class SetupJavaRightPaneView : UserControl
{
    public SetupJavaRightPaneView()
    {
        InitializeComponent();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Classes.Set("compact", e.NewSize.Width < 520);
    }
}
