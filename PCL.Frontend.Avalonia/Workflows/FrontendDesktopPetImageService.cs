using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Frontend.Avalonia.Models;
using SkiaSharp;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDesktopPetImageService
{
    private const int MaximumImageSide = 256;
    private const int MaximumFileBytes = 10 * 1024 * 1024;
    private const int MaximumGifFrames = 256;
    private const long MaximumDecodedPixels = 64 * 1024 * 1024;
    private const long MaximumCachedPixels = 8 * 1024 * 1024;

    public static string Import(string dataDirectory, string sourcePath)
    {
        var bytes = ReadSource(sourcePath);
        var gif = IsGif(bytes);
        if (gif)
        {
            // Preserve timing, looping, transparency, and disposal instructions.
            // Validate every frame before the managed copy can become a setting.
            using var validated = LoadGif(bytes);
        }
        else
        {
            using var bitmap = Load(bytes);
            using var output = new MemoryStream();
            bitmap.Save(output);
            bytes = output.ToArray();
        }
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + (gif ? ".gif" : ".png");
        var directory = Path.Combine(dataDirectory, "DesktopPet");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, name);
        // A managed copy survives moving the source, and identical imports share an asset.
        // Keep previous assets so settings snapshots can still refer to them.
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return target;
    }

    public static Bitmap? TryLoad(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Load(ReadSource(path));
        }
        catch
        {
            // Missing or damaged saved images fall back to the bundled mascot.
            return null;
        }
    }

    public static FrontendDesktopPetImage? TryLoadAnimated(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var bytes = ReadSource(path);
            return IsGif(bytes) ? LoadGif(bytes) : new([Load(bytes)], [TimeSpan.Zero], 0);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ReadSource(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumFileBytes) throw new InvalidDataException("The pet image is too large.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static bool IsGif(byte[] bytes) => bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8);

    private static Bitmap Load(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new Bitmap(stream);
        try
        {
            var size = bitmap.PixelSize;
            var longestSide = Math.Max(size.Width, size.Height);
            if (size.Width <= 0 || size.Height <= 0 || longestSide > 4096)
            {
                throw new InvalidDataException("The pet image has unsupported dimensions.");
            }
            if (longestSide <= MaximumImageSide) return bitmap;
            var ratio = MaximumImageSide / (double)longestSide;
            var scaled = bitmap.CreateScaledBitmap(new PixelSize(
                Math.Max(1, (int)Math.Round(size.Width * ratio)),
                Math.Max(1, (int)Math.Round(size.Height * ratio))));
            bitmap.Dispose();
            return scaled;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static FrontendDesktopPetImage LoadGif(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Invalid GIF image.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var count = codec.FrameCount;
        var longestSide = Math.Max(info.Width, info.Height);
        var ratio = Math.Min(1, MaximumImageSide / (double)longestSide);
        var size = new PixelSize(Math.Max(1, (int)Math.Round(info.Width * ratio)), Math.Max(1, (int)Math.Round(info.Height * ratio)));
        if (info.Width <= 0 || info.Height <= 0 || longestSide > 4096 || count is < 1 or > MaximumGifFrames
            || (long)info.Width * info.Height * count > MaximumDecodedPixels
            || (long)size.Width * size.Height * count > MaximumCachedPixels)
        {
            throw new InvalidDataException("The GIF has unsupported dimensions or too many decoded pixels or frames.");
        }

        var metadata = codec.FrameInfo;
        var frames = new List<Bitmap>(count);
        var durations = new TimeSpan[count];
        using var canvas = new SKBitmap(info);
        using var scaled = new SKBitmap(new SKImageInfo(size.Width, size.Height, info.ColorType, info.AlphaType));
        using var restore = metadata.Any(frame => frame.DisposalMethod == SKCodecAnimationDisposalMethod.RestorePrevious) ? new SKBitmap(info) : null;
        using var canvasPixels = canvas.PeekPixels();
        using var restorePixels = restore?.PeekPixels();
        var prior = -1;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var restorePrevious = metadata[i].DisposalMethod == SKCodecAnimationDisposalMethod.RestorePrevious;
                if (restorePrevious && prior >= 0) canvasPixels.ReadPixels(info, restore!.GetPixels(), restore.RowBytes);
                var result = codec.GetPixels(info, canvas.GetPixels(), canvas.RowBytes, new SKCodecOptions(i, prior));
                if (result != SKCodecResult.Success) throw new InvalidDataException("The GIF contains a damaged frame.");
                if (!canvas.ScalePixels(scaled, SKFilterQuality.Medium)) throw new InvalidDataException("Could not resize GIF frame.");
                frames.Add(new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, scaled.GetPixels(), size, new Vector(96, 96), scaled.RowBytes));
                // Very short/unspecified delays follow browser playback behavior.
                durations[i] = TimeSpan.FromMilliseconds(metadata[i].Duration < 20 ? 100 : metadata[i].Duration);
                if (restorePrevious)
                {
                    if (prior >= 0) restorePixels!.ReadPixels(info, canvas.GetPixels(), canvas.RowBytes);
                }
                else prior = i;
            }
            return new(frames.ToArray(), durations, codec.RepetitionCount);
        }
        catch
        {
            foreach (var frame in frames) frame.Dispose();
            throw;
        }
    }
}
