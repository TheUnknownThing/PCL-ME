using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class SharedRouteShellRightPaneView : UserControl
{
    private readonly Border? _liveLogSurface;

    public SharedRouteShellRightPaneView()
    {
        InitializeComponent();
        _liveLogSurface = this.FindControl<Border>("LiveLogSurface");
    }

    private void LiveLogConsole_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        _liveLogSurface?.Classes.Add("focused");
    }

    private void LiveLogConsole_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        _liveLogSurface?.Classes.Remove("focused");
    }

    private void LiveLogConsole_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _liveLogSurface?.Classes.Add("pointerover");
    }

    private void LiveLogConsole_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _liveLogSurface?.Classes.Remove("pointerover");
    }
}
