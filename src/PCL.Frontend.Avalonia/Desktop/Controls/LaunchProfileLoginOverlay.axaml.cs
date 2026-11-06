using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.UI.Animation;
using PCL.UI.Controls;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed partial class LaunchProfileLoginOverlay : UserControl
{
    private LauncherViewModel? _launcher;
    private IInputElement? _previousFocus;
    private bool _isRenderedOpen;
    private int _animationVersion;

    public LaunchProfileLoginOverlay()
    {
        InitializeComponent();
        PclModalMotion.ResetToClosedState(LoginBackdrop, LoginCard);
        DataContextChanged += (_, _) => ObserveLauncher();
        AttachedToVisualTree += (_, _) => ObserveLauncher();
        DetachedFromVisualTree += OnDetached;
        OverlayRoot.SizeChanged += (_, _) =>
        {
            LoginCard.MaxWidth = Math.Max(0, OverlayRoot.Bounds.Width - 56);
            LoginCard.MaxHeight = Math.Max(0, OverlayRoot.Bounds.Height - 56);
        };
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void ObserveLauncher()
    {
        if (_launcher is not null) _launcher.PropertyChanged -= OnLauncherPropertyChanged;
        _launcher = DataContext as LauncherViewModel;
        if (_launcher is not null) _launcher.PropertyChanged += OnLauncherPropertyChanged;
        QueueSync();
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_launcher is not null)
        {
            if (_launcher.IsLaunchProfilePopupVisible && _launcher.BackLaunchProfileCommand.CanExecute(null))
                _launcher.BackLaunchProfileCommand.Execute(null);
            _launcher.PropertyChanged -= OnLauncherPropertyChanged;
        }
        _launcher = null;
        _animationVersion++;
        _isRenderedOpen = false;
        OverlayRoot.IsVisible = false;
        PclModalMotion.ResetToClosedState(LoginBackdrop, LoginCard);
    }

    private void OnLauncherPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.IsLaunchProfilePopupVisible)
            or nameof(LauncherViewModel.LaunchProfilePopupTitle)) QueueSync();
    }

    private void QueueSync() => Dispatcher.UIThread.Post(() => _ = SyncOverlayAsync(), DispatcherPriority.Render);

    private async Task SyncOverlayAsync()
    {
        if (VisualRoot is null) return;
        var shouldShow = _launcher?.IsLaunchProfilePopupVisible == true;
        if (shouldShow)
        {
            LoginPopupTitle.Text = _launcher!.LaunchProfilePopupTitle;
            AccountSelection.IsVisible = _launcher.ShowLaunchProfileSelection;
            LoginMethodChooser.IsVisible = _launcher.ShowLaunchProfileChooser;
            OfflineEditor.IsVisible = _launcher.ShowLaunchOfflineEditor;
            MicrosoftEditor.IsVisible = _launcher.ShowLaunchMicrosoftEditor;
            AuthlibEditor.IsVisible = _launcher.ShowLaunchAuthlibEditor;
            LoginCard.Width = _launcher.ShowLaunchAuthlibEditor ? 480 : 420;
            Dispatcher.UIThread.Post(FocusCurrentStep, DispatcherPriority.Loaded);
        }
        if (shouldShow == _isRenderedOpen) return;

        var version = ++_animationVersion;
        _isRenderedOpen = shouldShow;
        if (shouldShow)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            OverlayRoot.IsVisible = true;
            OverlayRoot.IsHitTestVisible = true;
            PclModalMotion.ResetToClosedState(LoginBackdrop, LoginCard);
            FocusCurrentStep();
            await PclModalMotion.PlayOpenAsync(LoginBackdrop, LoginCard,
                () => version == _animationVersion && _isRenderedOpen);
        }
        else
        {
            // Keep the backdrop blocking clicks until the closing animation ends.
            await PclModalMotion.PlayCloseAsync(LoginBackdrop, LoginCard,
                () => version == _animationVersion && !_isRenderedOpen);
            if (version != _animationVersion || _isRenderedOpen) return;
            OverlayRoot.IsVisible = false;
            OverlayRoot.IsHitTestVisible = false;
            _previousFocus?.Focus();
            _previousFocus = null;
        }
    }

    private void FocusCurrentStep()
    {
        if (!_isRenderedOpen || _launcher?.IsLaunchProfilePopupVisible != true) return;
        Control step = _launcher.ShowLaunchOfflineEditor ? OfflineEditor
            : _launcher.ShowLaunchAuthlibEditor ? AuthlibEditor
            : _launcher.ShowLaunchMicrosoftEditor ? MicrosoftEditor
            : _launcher.ShowLaunchProfileSelection ? AccountSelection
            : LoginMethodChooser;
        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (focus is Visual visual && visual.IsEffectivelyVisible
            && (visual.GetVisualAncestors().Contains(step) || visual.GetVisualAncestors().Contains(LoginActions))) return;
        var target = (Control?)step.GetVisualDescendants().OfType<PclListItem>()
            .FirstOrDefault(control => control.IsSelected && control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
            ?? step.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(control => control is TextBox && control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
            ?? step.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(control => control.IsEffectivelyVisible && control.IsEffectivelyEnabled)
            ?? LoginActions.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(control => control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
        target?.Focus();
    }

    private void ProfileEntry_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(e.Source, sender) || e.Key is not (Key.Enter or Key.Space)) return;
        if (sender is PclListItem { Command: { } command } && command.CanExecute(null)) command.Execute(null);
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_launcher?.IsLaunchProfilePopupVisible != true || _launcher.IsPromptOverlayVisible) return;
        if (e.Key == Key.Escape)
        {
            if (_launcher.BackLaunchProfileCommand.CanExecute(null)) _launcher.BackLaunchProfileCommand.Execute(null);
            e.Handled = true;
        }
    }
}
