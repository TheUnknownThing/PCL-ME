using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclConfirmDialog : PclAnimatedDialog<bool>
{
    public PclConfirmDialog(
        string title,
        string message,
        string confirmText,
        bool isDanger)
    {
        InitializeComponent();
        InitializeDialogAnimation();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmColorType = isDanger ? PclButtonColorState.Red : PclButtonColorState.Highlight;
        ConfirmCommand = new ActionCommand(Confirm);
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;

        if (isDanger)
        {
            TitleTextBlock.Foreground = Brush.Parse("#D33232");
            OverlayBorder.Background = Brush.Parse("#8C500000");
        }

        KeyDown += OnDialogKeyDown;
        Opened += (_, _) => AlignToOwnerBounds();
    }

    public string ConfirmText { get; }

    public PclButtonColorState ConfirmColorType { get; }

    public ActionCommand ConfirmCommand { get; }

    public ActionCommand CancelCommand { get; }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        else if (e.Key == Key.Enter && ConfirmCommand.CanExecute(null))
        {
            e.Handled = true;
            Confirm();
        }
    }

    private void AlignToOwnerBounds()
    {
        if (Owner is not Window owner)
        {
            return;
        }

        Position = owner.Position;
        Width = Math.Max(owner.Bounds.Width, 320);
        Height = Math.Max(owner.Bounds.Height, 240);
    }

    private void Confirm()
    {
        CloseWithAnimation(true);
    }

    private void Cancel()
    {
        CloseWithAnimation(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
