namespace PCL.Frontend.Avalonia;

internal enum AvaloniaHintTheme
{
    Info,
    Success,
    Error
}

internal delegate void AvaloniaHintHandler(string message, AvaloniaHintTheme theme);

internal static class AvaloniaHintBus
{
    public static event AvaloniaHintHandler? OnShow;

    public static void Show(string message, AvaloniaHintTheme theme = AvaloniaHintTheme.Info)
    {
        OnShow?.Invoke(message, theme);
    }
}
