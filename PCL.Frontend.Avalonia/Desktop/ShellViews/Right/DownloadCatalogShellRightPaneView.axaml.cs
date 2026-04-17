using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadCatalogShellRightPaneView : UserControl
{
    private readonly StackPanel _contentHost;
    private readonly PCL.Frontend.Avalonia.Desktop.Controls.PclCard _loadingCard;
    private FrontendShellViewModel? _observedShell;
    private int _visualStateVersion;

    public DownloadCatalogShellRightPaneView()
    {
        InitializeComponent();
        _contentHost = this.FindControl<StackPanel>("ContentHost")
            ?? throw new InvalidOperationException("The download catalog page did not contain the content host.");
        _loadingCard = this.FindControl<PCL.Frontend.Avalonia.Desktop.Controls.PclCard>("LoadingCard")
            ?? throw new InvalidOperationException("The download catalog page did not contain the loading card.");
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) =>
        {
            ObserveShell(null);
            _visualStateVersion++;
        };
        ConfigureSurfaceMotion();
        ScheduleVisualStateSync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleVisualStateSync();
    }

    private void ObserveShell(FrontendShellViewModel? shell)
    {
        if (ReferenceEquals(_observedShell, shell))
        {
            return;
        }

        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged -= OnShellPropertyChanged;
        }

        _observedShell = shell;
        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(FrontendShellViewModel.ShowDownloadCatalogLoadingCard)
            or nameof(FrontendShellViewModel.ShowDownloadCatalogContent)))
        {
            return;
        }

        ScheduleVisualStateSync();
    }

    private void ConfigureSurfaceMotion()
    {
        ConfigureAnimatedSurface(_contentHost, enterOffsetY: -8, exitOffsetY: -8, staggerChildren: true, delayMilliseconds: 32);
        ConfigureAnimatedSurface(_loadingCard, enterOffsetY: 10, exitOffsetY: 8);
    }

    private void ScheduleVisualStateSync()
    {
        var version = ++_visualStateVersion;
        Dispatcher.UIThread.Post(
            async () => await SyncVisualStateAsync(version),
            DispatcherPriority.Background);
    }

    private async Task SyncVisualStateAsync(int version)
    {
        if (version != _visualStateVersion)
        {
            return;
        }

        var showLoading = _observedShell?.ShowDownloadCatalogLoadingCard == true;
        var showContent = _observedShell?.ShowDownloadCatalogContent == true;

        if (VisualRoot is null)
        {
            _loadingCard.IsVisible = showLoading;
            _contentHost.IsVisible = showContent;
            return;
        }

        if (showLoading)
        {
            await HideAnimatedAsync(_contentHost, version);
            await ShowAnimatedAsync(_loadingCard, version);
            return;
        }

        await HideAnimatedAsync(_loadingCard, version);
        if (showContent)
        {
            await ShowAnimatedAsync(_contentHost, version);
        }
        else
        {
            await HideAnimatedAsync(_contentHost, version);
        }
    }

    private async Task ShowAnimatedAsync(Control control, int version)
    {
        if (version != _visualStateVersion)
        {
            return;
        }

        if (control.IsVisible)
        {
            return;
        }

        control.IsVisible = true;
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _visualStateVersion)
        {
            return;
        }

        await Motion.PlayEnterAsync(control);
    }

    private async Task HideAnimatedAsync(Control control, int version)
    {
        if (version != _visualStateVersion || !control.IsVisible)
        {
            return;
        }

        await Motion.PlayExitAsync(control);
        if (version != _visualStateVersion)
        {
            return;
        }

        control.IsVisible = false;
    }

    private static void ConfigureAnimatedSurface(
        Control control,
        double enterOffsetY,
        double exitOffsetY,
        bool staggerChildren = false,
        int delayMilliseconds = 0)
    {
        Motion.SetInitialOpacity(control, 0);
        Motion.SetOffsetX(control, 0);
        Motion.SetOffsetY(control, enterOffsetY);
        Motion.SetExitOffsetX(control, 0);
        Motion.SetExitOffsetY(control, exitOffsetY);
        Motion.SetOvershootTranslation(control, true);
        Motion.SetStaggerChildren(control, staggerChildren);
        Motion.SetStaggerStep(control, 38);
        Motion.SetDelay(control, delayMilliseconds);
    }
}
