using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using PCL.Frontend.Avalonia.Desktop.Animation;

namespace PCL.Frontend.Avalonia.Desktop.Dialogs;

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
        _overlayBorder = FindRequiredControl<Control>("OverlayBorder");
        _messagePanel = FindRequiredControl<Control>("MessagePanel");
        PclModalMotion.ResetToClosedState(_overlayBorder, _messagePanel);
        Opened += OnDialogOpened;
    }

    protected T FindRequiredControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name)
               ?? throw new InvalidOperationException($"对话框缺少必需控件：{name}");
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
