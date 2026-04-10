using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadInstallShellRightPaneView : UserControl
{
    private readonly ScrollViewer _installScrollViewer;
    private readonly StackPanel _minecraftCatalogStage;
    private readonly StackPanel _selectionStage;
    private FrontendShellViewModel? _observedShell;

    public DownloadInstallShellRightPaneView()
    {
        InitializeComponent();
        _installScrollViewer = this.FindControl<ScrollViewer>("InstallScrollViewer")
            ?? throw new InvalidOperationException("安装页面未找到滚动容器。");
        _minecraftCatalogStage = this.FindControl<StackPanel>("MinecraftCatalogStage")
            ?? throw new InvalidOperationException("安装页面未找到 Minecraft 版本选择阶段。");
        _selectionStage = this.FindControl<StackPanel>("SelectionStage")
            ?? throw new InvalidOperationException("安装页面未找到加载器选择阶段。");
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ObserveShell(null);
        ScheduleStageSync();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleStageSync();
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
            nameof(FrontendShellViewModel.ShowDownloadInstallMinecraftCatalog)
            or nameof(FrontendShellViewModel.ShowDownloadInstallSelectionStage)))
        {
            return;
        }

        ScheduleStageSync();
    }

    private void ScheduleStageSync()
    {
        Dispatcher.UIThread.Post(SyncStageVisibility, DispatcherPriority.Background);
    }

    private void SyncStageVisibility()
    {
        var showCatalog = _observedShell?.ShowDownloadInstallMinecraftCatalog != false;
        var showSelection = _observedShell?.ShowDownloadInstallSelectionStage == true;

        _minecraftCatalogStage.IsVisible = showCatalog;
        _selectionStage.IsVisible = showSelection;

        if (showSelection)
        {
            _installScrollViewer.ScrollToHome();
        }
    }
}
