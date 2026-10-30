using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.Models;

/// <summary>Owns decoded pet frames and their presentation times.</summary>
internal sealed class FrontendDesktopPetImage(Bitmap[] frames, TimeSpan[] durations, int repetitionCount) : IDisposable
{
    private readonly long[] _frameEnds = GetFrameEnds(durations);
    public Bitmap FirstFrame => frames[0];
    internal IReadOnlyList<Bitmap> Frames => frames;

    public (Bitmap Image, TimeSpan? Remaining) GetFrame(TimeSpan elapsed)
    {
        if (frames.Length == 1) return (FirstFrame, null);
        var ticks = Math.Max(0, elapsed.Ticks);
        var duration = _frameEnds[^1];
        if (repetitionCount >= 0 && ticks / duration > repetitionCount) return (frames[^1], null);
        ticks %= duration;
        var index = 0;
        while (ticks >= _frameEnds[index]) index++;
        return (frames[index], TimeSpan.FromTicks(_frameEnds[index] - ticks));
    }

    private static long[] GetFrameEnds(TimeSpan[] durations)
    {
        var ends = new long[durations.Length];
        long total = 0;
        for (var i = 0; i < durations.Length; i++) ends[i] = total += durations[i].Ticks;
        return ends;
    }

    public void Dispose()
    {
        foreach (var frame in frames) frame.Dispose();
    }
}
