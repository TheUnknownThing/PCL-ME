using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendDesktopPetImageService
{
    private const int MaximumImageSide = 256;

    public static string Import(string dataDirectory, string sourcePath)
    {
        using var bitmap = Load(sourcePath);
        using var output = new MemoryStream();
        bitmap.Save(output);
        var bytes = output.ToArray();
        var name = Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".png";
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
            return Load(path);
        }
        catch
        {
            // Missing or damaged saved images fall back to the bundled mascot.
            return null;
        }
    }

    private static Bitmap Load(string path)
    {
        if (new FileInfo(path).Length > 10 * 1024 * 1024) throw new InvalidDataException("The pet image is too large.");
        using var stream = File.OpenRead(path);
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
}
