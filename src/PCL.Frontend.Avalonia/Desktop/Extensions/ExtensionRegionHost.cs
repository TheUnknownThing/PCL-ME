using Avalonia;
using Avalonia.Controls;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

/// <summary>A semantic mount point. Pane implementations never handle extension registrations.</summary>
internal sealed class ExtensionRegionHost : StackPanel
{
    public static readonly StyledProperty<string> RegionProperty = AvaloniaProperty.Register<ExtensionRegionHost, string>(nameof(Region), "");
    public string Region { get => GetValue(RegionProperty); set => SetValue(RegionProperty, value); }
    private FrontendExtensionSession? _session;

    public ExtensionRegionHost()
    {
        Spacing = 15;
        AttachedToVisualTree += (_, _) =>
        {
            _session = (TopLevel.GetTopLevel(this)?.DataContext as LauncherViewModel)?.ExtensionSession;
            _session?.AttachRegion(this);
        };
        DetachedFromVisualTree += (_, _) => { _session?.DetachRegion(this); _session = null; };
    }
}
