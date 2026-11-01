using Avalonia.Controls;
using Avalonia;
using Avalonia.Layout;

namespace PCL.Extensions.Avalonia;

public enum ExtensionViewLocation { Page, InstanceTab, Settings }
public enum ExtensionActionLocation { Menu, Toolbar, Tray, InstanceMenu }

public sealed record ExtensionView
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public ExtensionViewLocation Location { get; init; }
}

public sealed record ExtensionViewContext(string? InstanceId = null);
public sealed record ExtensionAction(string Id, string Title, string CommandId, ExtensionActionLocation Location, string? TitleKey = null, string? IconData = null);

public sealed record ExtensionWindowOptions
{
    public bool IsRecoverySurface { get; init; }
    public bool ShowWithOwner { get; init; }
}

/// <summary>A transient native surface hosted inside the launcher window.</summary>
public sealed record ExtensionOverlayOptions
{
    public double MaxWidth { get; init; } = 680;
    public Thickness Margin { get; init; } = new(24, 76, 24, 24);
    public HorizontalAlignment HorizontalAlignment { get; init; } = HorizontalAlignment.Stretch;
    public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Top;
    public bool DismissOnOutsideClick { get; init; } = true;
    public bool DismissOnEscape { get; init; } = true;
}

public interface IExtensionOverlay : IDisposable
{
    bool IsOpen { get; }
    event Action? Closed;
}

/// <summary>Native views run on the launcher UI thread and use the launcher's Avalonia assemblies.</summary>
public interface IExtensionUi
{
    IDisposable RegisterView(ExtensionView view, Func<ExtensionViewContext, Control> create);
    IDisposable RegisterAction(ExtensionAction action);
    ValueTask ShowViewAsync(string viewId, ExtensionViewContext? context = null, CancellationToken cancellationToken = default);
    IExtensionOverlay ShowOverlay(Control content, ExtensionOverlayOptions? options = null);
    IDisposable ShowWindow(Window window, ExtensionWindowOptions? options = null);
    ValueTask ShowDialogAsync(Window dialog, CancellationToken cancellationToken = default);
    void Dispatch(Action action);
}
