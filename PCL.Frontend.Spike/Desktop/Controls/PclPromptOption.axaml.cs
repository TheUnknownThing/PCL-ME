using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace PCL.Frontend.Spike.Desktop.Controls;

internal sealed partial class PclPromptOption : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PclPromptOption, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> DetailProperty =
        AvaloniaProperty.Register<PclPromptOption, string>(nameof(Detail), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PclPromptOption, ICommand?>(nameof(Command));

    public PclPromptOption()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
