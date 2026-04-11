using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Desktop.Animation;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Desktop.ShellViews.Right;

internal sealed partial class DownloadInstallShellRightPaneView : UserControl
{
    private readonly ScrollViewer _installScrollViewer;
    private readonly StackPanel _minecraftCatalogStage;
    private readonly StackPanel _selectionStage;
    private FrontendShellViewModel? _observedShell;
    private bool _hasAppliedInitialStageVisibility;
    private DownloadInstallStage _currentStage = DownloadInstallStage.Catalog;
    private int _stageAnimationVersion;

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
        DetachedFromVisualTree += (_, _) =>
        {
            ObserveShell(null);
            _stageAnimationVersion++;
            _hasAppliedInitialStageVisibility = false;
        };
        ConfigureStageMotion();
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
        var version = ++_stageAnimationVersion;
        Dispatcher.UIThread.Post(
            async () => await SyncStageVisibilityAsync(version),
            DispatcherPriority.Background);
    }

    private async Task SyncStageVisibilityAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        var targetStage = ResolveTargetStage();
        if (!_hasAppliedInitialStageVisibility || VisualRoot is null)
        {
            ApplyStageVisibility(targetStage);
            _currentStage = targetStage;
            _hasAppliedInitialStageVisibility = true;
            return;
        }

        if (_currentStage == targetStage
            && _minecraftCatalogStage.IsVisible == (targetStage == DownloadInstallStage.Catalog)
            && _selectionStage.IsVisible == (targetStage == DownloadInstallStage.Selection))
        {
            return;
        }

        switch (targetStage)
        {
            case DownloadInstallStage.Selection:
                await TransitionToSelectionStageAsync(version);
                break;
            default:
                await TransitionToMinecraftCatalogStageAsync(version);
                break;
        }

        if (version == _stageAnimationVersion)
        {
            _currentStage = targetStage;
        }
    }

    private DownloadInstallStage ResolveTargetStage()
    {
        return _observedShell?.ShowDownloadInstallSelectionStage == true
            ? DownloadInstallStage.Selection
            : DownloadInstallStage.Catalog;
    }

    private void ApplyStageVisibility(DownloadInstallStage stage)
    {
        _minecraftCatalogStage.IsVisible = stage == DownloadInstallStage.Catalog;
        _selectionStage.IsVisible = stage == DownloadInstallStage.Selection;
        if (stage == DownloadInstallStage.Selection)
        {
            _installScrollViewer.ScrollToHome();
        }
    }

    private async Task TransitionToSelectionStageAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        _installScrollViewer.ScrollToHome();
        await HideAnimatedAsync(_minecraftCatalogStage, version);
        await ShowAnimatedAsync(_selectionStage, version);
    }

    private async Task TransitionToMinecraftCatalogStageAsync(int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        _installScrollViewer.ScrollToHome();
        await HideAnimatedAsync(_selectionStage, version);
        await ShowAnimatedAsync(_minecraftCatalogStage, version);
    }

    private async Task ShowAnimatedAsync(Control control, int version)
    {
        if (version != _stageAnimationVersion)
        {
            return;
        }

        if (control.IsVisible)
        {
            return;
        }

        control.IsVisible = true;
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        if (version != _stageAnimationVersion)
        {
            return;
        }

        await Motion.PlayEnterAsync(control);
    }

    private async Task HideAnimatedAsync(Control control, int version)
    {
        if (version != _stageAnimationVersion || !control.IsVisible)
        {
            return;
        }

        await Motion.PlayExitAsync(control);
        if (version != _stageAnimationVersion)
        {
            return;
        }

        control.IsVisible = false;
    }

    private void ConfigureStageMotion()
    {
        ConfigureStage(_minecraftCatalogStage, enterOffsetX: -48, exitOffsetX: -48);
        ConfigureStage(_selectionStage, enterOffsetX: 48, exitOffsetX: 48);
    }

    private static void ConfigureStage(Control control, double enterOffsetX, double exitOffsetX)
    {
        Motion.SetInitialOpacity(control, 0);
        Motion.SetOffsetX(control, enterOffsetX);
        Motion.SetOffsetY(control, 0);
        Motion.SetExitOffsetX(control, exitOffsetX);
        Motion.SetExitOffsetY(control, 0);
        Motion.SetOvershootTranslation(control, false);
    }

    private enum DownloadInstallStage
    {
        Catalog,
        Selection
    }
}
