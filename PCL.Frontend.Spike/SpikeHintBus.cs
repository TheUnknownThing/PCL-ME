namespace PCL.Frontend.Spike;

internal enum SpikeHintTheme
{
    Info,
    Success,
    Error
}

internal delegate void SpikeHintHandler(string message, SpikeHintTheme theme);

internal static class SpikeHintBus
{
    public static event SpikeHintHandler? OnShow;

    public static void Show(string message, SpikeHintTheme theme = SpikeHintTheme.Info)
    {
        OnShow?.Invoke(message, theme);
    }
}
