using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

// For fixed option lists whose indices keep their meaning when labels are translated.
internal static class PclComboBoxSelection
{
    public static readonly AttachedProperty<int> SelectedIndexProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, int>(
            "SelectedIndex", typeof(PclComboBoxSelection), -1, defaultBindingMode: BindingMode.TwoWay);

    private static readonly AttachedProperty<bool> RestoreQueuedProperty =
        AvaloniaProperty.RegisterAttached<ComboBox, bool>("RestoreQueued", typeof(PclComboBoxSelection));

    static PclComboBoxSelection()
    {
        SelectedIndexProperty.Changed.AddClassHandler<ComboBox>((combo, _) => ScheduleRestore(combo));
        ItemsControl.ItemsSourceProperty.Changed.AddClassHandler<ComboBox>((combo, _) => ScheduleRestore(combo));
        ComboBox.SelectedIndexProperty.Changed.AddClassHandler<ComboBox>((combo, _) =>
        {
            // Replacing ItemsSource clears the control's selection to -1. Keep the
            // bound index intact; valid user selections still update it in both directions.
            if (combo.IsSet(SelectedIndexProperty) && combo.SelectedIndex >= 0)
            {
                combo.SetCurrentValue(SelectedIndexProperty, combo.SelectedIndex);
            }
        });
    }

    public static int GetSelectedIndex(ComboBox combo) => combo.GetValue(SelectedIndexProperty);

    public static void SetSelectedIndex(ComboBox combo, int value) => combo.SetValue(SelectedIndexProperty, value);

    private static void ScheduleRestore(ComboBox combo)
    {
        if (!combo.IsSet(SelectedIndexProperty) || combo.GetValue(RestoreQueuedProperty))
        {
            return;
        }

        combo.SetValue(RestoreQueuedProperty, true);
        // Let ItemsSource and selection bindings settle before restoring the index.
        Dispatcher.UIThread.Post(() =>
        {
            combo.SetValue(RestoreQueuedProperty, false);
            var index = GetSelectedIndex(combo);
            if (index >= -1 && index < combo.ItemCount && combo.SelectedIndex != index)
            {
                combo.SetCurrentValue(ComboBox.SelectedIndexProperty, index);
            }
        }, DispatcherPriority.Background);
    }
}
