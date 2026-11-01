using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PCL.Extensions.Avalonia;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed class ExtensionContextActions : AvaloniaObject
{
    public static readonly AttachedProperty<string?> ResourcePathProperty = AvaloniaProperty.RegisterAttached<ExtensionContextActions, Control, string?>("ResourcePath");
    public static readonly AttachedProperty<ExtensionContextActionLocation> LocationProperty = AvaloniaProperty.RegisterAttached<ExtensionContextActions, Control, ExtensionContextActionLocation>("Location");
    public static string? GetResourcePath(Control control) => control.GetValue(ResourcePathProperty);
    public static void SetResourcePath(Control control, string? value) => control.SetValue(ResourcePathProperty, value);
    public static ExtensionContextActionLocation GetLocation(Control control) => control.GetValue(LocationProperty);
    public static void SetLocation(Control control, ExtensionContextActionLocation value) => control.SetValue(LocationProperty, value);

    static ExtensionContextActions()
    {
        ResourcePathProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            if (GetResourcePath(control) is not null)
                control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        });
    }
    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right || sender is not Control control || GetResourcePath(control) is not { } path ||
            TopLevel.GetTopLevel(control)?.DataContext is not LauncherViewModel { ExtensionSession: { } session } launcher || launcher.ExtensionSelectedInstanceId is not { } instance) return;
        var location = GetLocation(control);
        if (location == ExtensionContextActionLocation.Resource && string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "mods", StringComparison.OrdinalIgnoreCase))
            location = ExtensionContextActionLocation.Mod;
        var menu = session.CreateContributionMenu(location, new() { InstanceId = instance, ResourcePath = path });
        if (menu.ItemCount == 0) return;
        e.Handled = true;
        // Open after the release is routed so popup light-dismiss does not consume the same pointer event.
        var anchor = TopLevel.GetTopLevel(control) ?? control;
        Dispatcher.UIThread.Post(() => menu.Open(anchor), DispatcherPriority.Input);
    }
}
