using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclTextInputDialog : PclAnimatedDialog<string?>
{
    private readonly TextBox _inputTextBox;

    public PclTextInputDialog(
        string title,
        string message,
        string initialText,
        string confirmText,
        string? placeholderText)
    {
        InitializeComponent();
        InitializeDialogAnimation();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmCommand = new ActionCommand(Confirm);
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;
        MessageHost.IsVisible = !string.IsNullOrWhiteSpace(message);

        _inputTextBox = this.FindControl<TextBox>("InputTextBox")
            ?? throw new InvalidOperationException("输入对话框未找到文本框。");
        _inputTextBox.Text = initialText ?? string.Empty;
        _inputTextBox.Watermark = placeholderText ?? string.Empty;
        _inputTextBox.KeyDown += InputTextBoxOnKeyDown;
        Opened += (_, _) =>
        {
            AlignToOwnerBounds();
            _inputTextBox.Focus();
            _inputTextBox.SelectionStart = 0;
            _inputTextBox.SelectionEnd = _inputTextBox.Text?.Length ?? 0;
        };
    }

    public string ConfirmText { get; }

    public ActionCommand ConfirmCommand { get; }

    public ActionCommand CancelCommand { get; }

    private void InputTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        Confirm();
    }

    private void Confirm()
    {
        CloseWithAnimation(_inputTextBox.Text);
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

    private void Cancel()
    {
        CloseWithAnimation(null);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
