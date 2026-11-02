using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PCL.Frontend.Avalonia.ViewModels;

internal abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    internal static bool TryNormalizeSelectionIndex(int value, int optionCount, out int normalizedValue)
    {
        normalizedValue = 0;
        // Avalonia can transiently clear ComboBox.SelectedIndex to -1 while ItemsSource is being rebound.
        // Treat that as a view refresh artifact instead of user intent so existing selections remain stable.
        if (value < 0 || optionCount <= 0)
        {
            return false;
        }

        normalizedValue = Math.Clamp(value, 0, optionCount - 1);
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }
}
