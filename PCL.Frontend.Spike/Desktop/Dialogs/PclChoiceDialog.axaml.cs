using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclChoiceDialog : Window
{
    private readonly ListBox _choiceListBox;

    public PclChoiceDialog(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId,
        string confirmText)
    {
        InitializeComponent();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        SelectionSummaryTextBlock.Text = $"可选项：{options.Count}";
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmCommand = new ActionCommand(Confirm);
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;

        _choiceListBox = this.FindControl<ListBox>("ChoiceListBox")
            ?? throw new InvalidOperationException("选择对话框未找到列表。");
        _choiceListBox.ItemsSource = options;
        _choiceListBox.DoubleTapped += (_, _) => Confirm();
        _choiceListBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Id, selectedId, StringComparison.Ordinal))
                                      ?? options.FirstOrDefault();

        KeyDown += OnDialogKeyDown;
        Opened += (_, _) => _choiceListBox.Focus();
    }

    public string ConfirmText { get; }

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
        Close((_choiceListBox.SelectedItem as PclChoiceDialogOption)?.Id);
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

internal sealed record PclChoiceDialogOption(
    string Id,
    string Title,
    string Summary);
