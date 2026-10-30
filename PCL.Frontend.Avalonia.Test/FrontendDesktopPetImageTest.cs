using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;
using SkiaSharp;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendDesktopPetImageTest
{
    // Use real offscreen drawing to verify decoded colors and transparency as well
    // as timing. Keep the worker alive, like the shared frontend test session.
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(ImageTestApplication)));

    [TestMethod]
    public async Task GifImport_PreservesFramesTimingTransparencyAndDisposal()
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            var bytes = GifFixture.Create(3, 1, 0,
                new(0, [1, 0, 0], 10, 1),
                new(1, [2], 20, 3),
                new(2, [3], 5, 2),
                new(1, [2], 10, 1));
            File.WriteAllBytes(fixture.Source, bytes);
            var managed = FrontendDesktopPetImageService.Import(fixture.Root, fixture.Source);
            Assert.AreEqual(".gif", Path.GetExtension(managed));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(managed));
            Assert.AreEqual(managed, FrontendDesktopPetImageService.Import(fixture.Root, fixture.Source));
            File.Delete(fixture.Source);
            using var image = FrontendDesktopPetImageService.TryLoadAnimated(managed);
            Assert.IsNotNull(image);
            Assert.AreEqual(4, image.Frames.Count);
            AssertPixels(image.Frames[0], SKColors.Red, SKColors.Transparent, SKColors.Transparent);
            AssertPixels(image.Frames[1], SKColors.Red, SKColors.Lime, SKColors.Transparent);
            AssertPixels(image.Frames[2], SKColors.Red, SKColors.Transparent, SKColors.Blue);
            AssertPixels(image.Frames[3], SKColors.Red, SKColors.Lime, SKColors.Transparent);
            Assert.AreSame(image.Frames[0], image.GetFrame(TimeSpan.FromMilliseconds(99)).Image);
            Assert.AreSame(image.Frames[1], image.GetFrame(TimeSpan.FromMilliseconds(100)).Image);
            Assert.AreSame(image.Frames[2], image.GetFrame(TimeSpan.FromMilliseconds(300)).Image);
            Assert.AreSame(image.FirstFrame, image.GetFrame(TimeSpan.FromMilliseconds(450)).Image);
            Assert.AreEqual(TimeSpan.FromMilliseconds(50), image.GetFrame(TimeSpan.FromMilliseconds(300)).Remaining);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task GifPlayback_ClampsUnspecifiedDelaysAndHonorsFiniteLoops()
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            File.WriteAllBytes(fixture.Source, GifFixture.Create(1, 1, 1,
                new(0, [1], 0, 1), new(0, [2], 1, 1), new(0, [3], 2, 1)));
            using var image = FrontendDesktopPetImageService.TryLoadAnimated(fixture.Source);
            Assert.IsNotNull(image);
            Assert.AreSame(image.Frames[0], image.GetFrame(TimeSpan.Zero).Image);
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), image.GetFrame(TimeSpan.Zero).Remaining);
            Assert.AreSame(image.Frames[1], image.GetFrame(TimeSpan.FromMilliseconds(100)).Image);
            Assert.AreSame(image.Frames[2], image.GetFrame(TimeSpan.FromMilliseconds(200)).Image);
            Assert.AreSame(image.Frames[0], image.GetFrame(TimeSpan.FromMilliseconds(220)).Image);
            Assert.AreSame(image.Frames[^1], image.GetFrame(TimeSpan.FromMilliseconds(440)).Image);
            Assert.IsNull(image.GetFrame(TimeSpan.FromMilliseconds(440)).Remaining);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task GifFrames_AreScaledOnceAndStaticConsumersUseTheFirstFrame()
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            File.WriteAllBytes(fixture.Source, GifFixture.Create(512, 256, 0,
                new(0, [1], 10, 1), new(0, [2], 10, 1)));
            using var image = FrontendDesktopPetImageService.TryLoadAnimated(fixture.Source);
            Assert.IsNotNull(image);
            Assert.IsTrue(image.Frames.All(frame => frame.PixelSize == new PixelSize(256, 128)));
            using var preview = FrontendDesktopPetImageService.TryLoad(fixture.Source);
            Assert.IsNotNull(preview);
            Assert.AreEqual(new PixelSize(256, 128), preview.PixelSize);
            using var sprite = new DesktopPetSprite(animateCustomImage: false);
            sprite.SetImagePath(fixture.Source);
            var first = sprite.CurrentImage;
            sprite.AdvanceAnimation(TimeSpan.FromMilliseconds(150));
            Assert.AreSame(first, sprite.CurrentImage, "Tray rendering must stay on the first GIF frame.");
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow(2, 1, 257)]
    [DataRow(1024, 1024, 65)]
    [DataRow(256, 256, 129)]
    public async Task GifImport_RejectsExcessiveFrameCountOrPixelBudgets(int width, int height, int count)
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            File.WriteAllBytes(fixture.Source, GifFixture.Create(width, height, 0,
                Enumerable.Range(0, count).Select(i => new GifFrame(0, [(byte)(i % 3 + 1)], 10, 1)).ToArray()));
            Assert.Throws<InvalidDataException>(() => FrontendDesktopPetImageService.Import(fixture.Root, fixture.Source));
            Assert.IsNull(FrontendDesktopPetImageService.TryLoadAnimated(fixture.Source));
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Root, "DesktopPet")));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    [DataRow("GIF87a")]
    [DataRow("GIF89a")]
    public async Task GifSingleFrame_LoadsAsAStaticImage(string signature)
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            var bytes = GifFixture.Create(1, 1, 0, new GifFrame(0, [1], 10, 1));
            System.Text.Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 0);
            File.WriteAllBytes(fixture.Source, bytes);
            using var image = FrontendDesktopPetImageService.TryLoadAnimated(fixture.Source);
            Assert.IsNotNull(image);
            Assert.AreEqual(1, image.Frames.Count);
            Assert.AreSame(image.FirstFrame, image.GetFrame(TimeSpan.FromHours(1)).Image);
            Assert.IsNull(image.GetFrame(TimeSpan.Zero).Remaining);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task GifImport_RejectsADamagedFrameBeforeSaving()
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            var bytes = GifFixture.Create(3, 1, 0, new GifFrame(0, [1, 2, 3], 10, 1));
            File.WriteAllBytes(fixture.Source, bytes[..^5]);
            Assert.Throws<InvalidDataException>(() => FrontendDesktopPetImageService.Import(fixture.Root, fixture.Source));
            Assert.IsNull(FrontendDesktopPetImageService.TryLoadAnimated(fixture.Source));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task StaticSprite_ReusesDecodedImagePausesWhenHiddenAndFallsBackWhenDamaged()
    {
        await Run(async () =>
        {
            using var fixture = new GifFixture();
            File.WriteAllBytes(fixture.Source, GifFixture.Create(1, 1, 0,
                new(0, [1], 10, 1), new(0, [2], 10, 1)));
            using var sprite = new DesktopPetSprite();
            var window = new Window { Content = sprite };
            window.PropertyChanged += (_, e) =>
            {
                if (e.Property == Visual.IsVisibleProperty) sprite.SyncAnimation();
            };
            try
            {
                // Custom pets are documented as static images, so an animated file only shows its first frame.
                sprite.SetImagePath(fixture.Source);
                var first = sprite.CustomImage;
                Assert.IsNotNull(first);
                window.Show();
                Assert.IsFalse(sprite.IsAnimationRunning, "Custom pets are static and must not animate.");
                sprite.AdvanceAnimation(TimeSpan.FromMilliseconds(120));
                Assert.AreSame(first, sprite.CurrentImage);
                window.Hide();
                Assert.IsFalse(sprite.IsAnimationRunning);
                sprite.SetImagePath(fixture.Source);
                Assert.AreSame(first, sprite.CustomImage, "Settings changes must reuse the decoded image.");
                File.WriteAllText(fixture.Source + ".gif", "GIF89a damaged");
                sprite.SetImagePath(fixture.Source + ".gif");
                Assert.IsNull(sprite.CurrentImage);
                Assert.IsNull(sprite.CustomImage);
            }
            finally { window.Close(); }
        });
    }

    private static void AssertPixels(Bitmap bitmap, params SKColor[] expected)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        using var decoded = SKBitmap.Decode(stream.ToArray());
        for (var x = 0; x < expected.Length; x++)
        {
            var actual = decoded.GetPixel(x, 0);
            Assert.AreEqual(expected[x].Alpha, actual.Alpha, $"Pixel {x} alpha");
            if (actual.Alpha != 0) Assert.AreEqual(expected[x], actual, $"Pixel {x} color");
        }
    }

    [TestMethod]
    public async Task CustomPetImport_RejectsAnimatedImagesButAcceptsStaticOnes()
    {
        await Run(() =>
        {
            using var fixture = new GifFixture();
            File.WriteAllBytes(fixture.Source, GifFixture.Create(1, 1, 0, new GifFrame(0, [1], 10, 1)));
            Assert.Throws<InvalidDataException>(() =>
                FrontendDesktopPetImageService.Import(fixture.Root, fixture.Source, allowAnimation: false));

            var png = Path.Combine(fixture.Root, "custom.png");
            using (var source = AssetLoader.Open(new Uri("avares://PCL.Frontend.Avalonia/Assets/icon.png")))
            using (var output = File.Create(png)) source.CopyTo(output);
            var imported = FrontendDesktopPetImageService.Import(fixture.Root, png, allowAnimation: false);
            Assert.AreEqual(".png", Path.GetExtension(imported));
            using var image = FrontendDesktopPetImageService.TryLoadAnimated(imported);
            Assert.IsNotNull(image);
            Assert.AreEqual(1, image.Frames.Count);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task BundledPresets_ExposeIdleAndInteractionAnimations()
    {
        await Run(() =>
        {
            var creeper = PetPresetCatalog.Find("creeper_gif");
            Assert.IsTrue(creeper.IsAnimated);
            Assert.AreEqual(PetClickAnimationMode.WaitIdleLoopEnd, creeper.ClickMode);
            using var idle = PetPresetCatalog.LoadAnimation(creeper, click: false, maximumSide: 128);
            using var click = PetPresetCatalog.LoadAnimation(creeper, click: true, maximumSide: 128);
            Assert.IsNotNull(idle);
            Assert.IsNotNull(click);
            Assert.AreEqual(10, idle!.Frames.Count);
            Assert.AreEqual(68, click!.Frames.Count);
            Assert.IsTrue(idle.TotalDuration > TimeSpan.Zero);
            Assert.IsTrue(Math.Max(idle.Frames[0].PixelSize.Width, idle.Frames[0].PixelSize.Height) <= 128);

            var campfire = PetPresetCatalog.Find("campfire_gif");
            Assert.IsTrue(campfire.RepeatsIdleOnClick);
            using var fire = PetPresetCatalog.LoadAnimation(campfire, click: false, maximumSide: 128);
            Assert.IsNotNull(fire);
            Assert.AreEqual(40, fire!.Frames.Count);

            Assert.AreEqual("creeper_gif", PetPresetCatalog.Find("missing").Id);
            return Task.CompletedTask;
        });
    }

    private static async Task Run(Func<Task> action) => await Session.Value.Dispatch(async () =>
    {
        await action();
        return true;
    }, CancellationToken.None);

    public sealed class ImageTestApplication : Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ImageTestApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    private sealed record GifFrame(int Left, byte[] Pixels, int Delay, int Disposal);

    private sealed class GifFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "pcl-gif-test-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(Root, "source.gif");
        public GifFixture() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, recursive: true);

        // Tiny deterministic GIFs cover partial frames and both disposal modes
        // without binary fixtures, external codecs, or timing-dependent files.
        public static byte[] Create(int width, int height, int repetitions, params GifFrame[] frames)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("GIF89a"u8);
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            writer.Write(new byte[] { 0x81, 0, 0, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255 });
            writer.Write(new byte[] { 0x21, 0xff, 11 });
            writer.Write("NETSCAPE2.0"u8);
            writer.Write(new byte[] { 3, 1 });
            writer.Write((ushort)repetitions);
            writer.Write((byte)0);
            foreach (var frame in frames)
            {
                writer.Write(new byte[] { 0x21, 0xf9, 4, (byte)((frame.Disposal << 2) | 1) });
                writer.Write((ushort)frame.Delay);
                writer.Write(new byte[] { 0, 0, 0x2c });
                writer.Write((ushort)frame.Left);
                writer.Write((ushort)0);
                writer.Write((ushort)frame.Pixels.Length);
                writer.Write((ushort)1);
                writer.Write(new byte[] { 0, 2 });
                var codes = frame.Pixels.SelectMany(pixel => new[] { 4, (int)pixel }).Append(5);
                var bits = 0;
                var bitCount = 0;
                var packed = new List<byte>();
                foreach (var code in codes)
                {
                    bits |= code << bitCount;
                    bitCount += 3;
                    while (bitCount >= 8)
                    {
                        packed.Add((byte)bits);
                        bits >>= 8;
                        bitCount -= 8;
                    }
                }
                if (bitCount > 0) packed.Add((byte)bits);
                writer.Write((byte)packed.Count);
                writer.Write(packed.ToArray());
                writer.Write((byte)0);
            }
            writer.Write((byte)0x3b);
            return stream.ToArray();
        }
    }
}
