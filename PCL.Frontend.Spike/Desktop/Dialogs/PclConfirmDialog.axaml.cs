using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.Desktop.Controls;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclConfirmDialog : Window
{
    public PclConfirmDialog(
        string title,
        string message,
        string confirmText,
        bool isDanger)
    {
        InitializeComponent();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmColorType = isDanger ? PclButtonColorState.Red : PclButtonColorState.Highlight;
        ConfirmCommand = new ActionCommand(Confirm);
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;
        KeyDown += OnDialogKeyDown;
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
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Confirm();
        }
    }

    private void Confirm()
    {
        Close(true);
    }

    private void Cancel()
    {
        Close(false);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
