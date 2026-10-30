using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Desktop;

internal static class DesktopPetTrayIconFactory
{
    public static WindowIcon Create(FrontendDesktopSettings settings)
    {
        // Render a static frame independently of the live pet's size and reaction state.
        using var sprite = new DesktopPetSprite(animateCustomImage: false);
        sprite.SetAppearance(settings.PetAppearance);
        sprite.SetImagePath(settings.PetImagePath);
        var canvas = new Border
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(4, 0),
            Child = sprite
        };
        canvas.Measure(new Size(32, 32));
        canvas.Arrange(new Rect(0, 0, 32, 32));
        using var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
        bitmap.Render(canvas);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return new WindowIcon(stream);
    }
}
