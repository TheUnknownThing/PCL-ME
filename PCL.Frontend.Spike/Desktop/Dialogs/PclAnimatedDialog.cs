using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Frontend.Spike.Desktop.Animation;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal abstract class PclAnimatedDialog<TResult> : Window
{
    private Control? _overlayBorder;
    private Control? _messagePanel;
    private bool _enterAnimationStarted;
    private bool _closeAnimationRunning;
    private bool _allowImmediateClose;
    private TResult _closeResult = default!;

    protected void InitializeDialogAnimation()
    {
        _overlayBorder = this.FindControl<Control>("OverlayBorder")
            ?? throw new InvalidOperationException("对话框遮罩层未找到。");
        _messagePanel = this.FindControl<Control>("MessagePanel")
            ?? throw new InvalidOperationException("对话框主体未找到。");
        PclModalMotion.ResetToClosedState(_overlayBorder, _messagePanel);
        Opened += OnDialogOpened;
    }

    protected void CloseWithAnimation(TResult result)
    {
        _closeResult = result;
        BeginCloseAnimation();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!_allowImmediateClose && !_closeAnimationRunning)
        {
            e.Cancel = true;
            BeginCloseAnimation();
        }

        base.OnClosing(e);
    }

    private async void OnDialogOpened(object? sender, EventArgs e)
    {
        if (_enterAnimationStarted)
        {
            return;
        }

        _enterAnimationStarted = true;
        if (_closeAnimationRunning || _overlayBorder is null || _messagePanel is null)
        {
            return;
        }

        await PclModalMotion.PlayOpenAsync(
            _overlayBorder,
            _messagePanel,
            () => !_closeAnimationRunning);
    }

    private void BeginCloseAnimation()
    {
        if (_closeAnimationRunning)
        {
            return;
        }

        _closeAnimationRunning = true;
        _ = RunCloseAnimationAsync();
    }

    private async Task RunCloseAnimationAsync()
    {
        if (_overlayBorder is not null && _messagePanel is not null)
        {
            await PclModalMotion.PlayCloseAsync(
                _overlayBorder,
                _messagePanel,
                () => _closeAnimationRunning);
        }

        _allowImmediateClose = true;
        Close(_closeResult);
    }
}
