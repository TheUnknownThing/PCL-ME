using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.Dialogs;

internal sealed partial class PclChoiceDialog : PclAnimatedDialog<string?>
{
    private readonly ListBox _choiceListBox;
    private string? _selectedId;

    public PclChoiceDialog(
        string title,
        string message,
        IReadOnlyList<PclChoiceDialogOption> options,
        string? selectedId,
        string confirmText)
    {
        InitializeComponent();
        InitializeDialogAnimation();

        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        ConfirmText = string.IsNullOrWhiteSpace(confirmText) ? "确定" : confirmText;
        ConfirmCommand = new ActionCommand(Confirm, () => !string.IsNullOrWhiteSpace(_selectedId));
        CancelCommand = new ActionCommand(Cancel);
        DataContext = this;
        MessageHost.IsVisible = !string.IsNullOrWhiteSpace(message);

        _choiceListBox = this.FindControl<ListBox>("ChoiceListBox")
            ?? throw new InvalidOperationException("选择对话框未找到列表。");
        _choiceListBox.ItemsSource = options;
        _choiceListBox.DoubleTapped += (_, _) => Confirm();
        _choiceListBox.SelectionChanged += (_, _) => UpdateSelectionState();
        _choiceListBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Id, selectedId, StringComparison.Ordinal))
                                      ?? options.FirstOrDefault();
        UpdateSelectionState();

        KeyDown += OnDialogKeyDown;
        Opened += (_, _) =>
        {
            AlignToOwnerBounds();
            _choiceListBox.Focus();
        };
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
        else if (e.Key == Key.Enter && ConfirmCommand.CanExecute(null))
        {
            e.Handled = true;
            Confirm();
        }
    }

    private void Confirm()
    {
        if (!ConfirmCommand.CanExecute(null))
        {
            return;
        }

        CloseWithAnimation(_selectedId);
    }

    private void Cancel()
    {
        CloseWithAnimation(null);
    }

    private void UpdateSelectionState()
    {
        _selectedId = (_choiceListBox.SelectedItem as PclChoiceDialogOption)?.Id;
        ConfirmCommand.NotifyCanExecuteChanged();
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

internal sealed record PclChoiceDialogOption(
    string Id,
    string Title,
    string Summary);
