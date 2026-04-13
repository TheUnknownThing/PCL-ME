using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclTextInputDialog : Window
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

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmCommand = new ActionCommand(Confirm);
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;

        _inputTextBox = this.FindControl<TextBox>("InputTextBox")
            ?? throw new InvalidOperationException("输入对话框未找到文本框。");
        _inputTextBox.Text = initialText ?? string.Empty;
        _inputTextBox.Watermark = placeholderText ?? string.Empty;
        _inputTextBox.KeyDown += InputTextBoxOnKeyDown;
        Opened += (_, _) =>
        {
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
        Close(_inputTextBox.Text);
    }

    private void Cancel()
    {
        Close(null);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
