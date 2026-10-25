using System.Text.Json;
using Avalonia.Headless;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Test;

[TestClass]
[DoNotParallelize]
public sealed class FrontendDeferredSettingsTest
{
    [TestMethod]
    public async Task DeferredSettings_KeepLatestValuesInMemoryAndWriteEachFileOnce()
    {
        await RunOnUiThread(async () =>
        {
            using var fixture = new SettingsFixture();
            var writer = new FrontendDeferredSettingsWriter();
            for (var i = 0; i < 100; i++)
            {
                writer.SetDeferred(fixture.Local, "opacity", i);
                writer.SetDeferred(fixture.Local, "blur", i * 2);
                writer.SetDeferred(fixture.Shared, "speed", i);
            }
            Assert.AreEqual(99, fixture.Local.Get<int>("opacity"));
            Assert.AreEqual(198, fixture.Local.Get<int>("blur"));
            Assert.AreEqual(0, fixture.Local.WriteCount);
            Assert.IsFalse(File.Exists(fixture.Local.FilePath));

            writer.Flush();
            writer.Flush();
            Assert.AreEqual(1, fixture.Local.WriteCount);
            Assert.AreEqual(1, fixture.Shared.WriteCount);
            using var saved = JsonDocument.Parse(File.ReadAllText(fixture.Local.FilePath));
            Assert.AreEqual(99, saved.RootElement.GetProperty("opacity").GetInt32());
            Assert.AreEqual(198, saved.RootElement.GetProperty("blur").GetInt32());
            await Task.Delay(350);
            Assert.AreEqual(1, fixture.Local.WriteCount, "An explicit flush must cancel the delayed write.");
        });
    }

    [TestMethod]
    public async Task DeferredSettings_AutosaveAfterTheLastChangeAndDoNotRepeatImmediateWrites()
    {
        await RunOnUiThread(async () =>
        {
            using var fixture = new SettingsFixture();
            // Stretch the quiet period so slow CI runners cannot deliver the second
            // change after the first one legitimately flushed, and so the delayed
            // write lands well past the waits below even under scheduling jitter.
            var writer = new FrontendDeferredSettingsWriter { SaveDelay = TimeSpan.FromSeconds(1) };
            writer.SetDeferred(fixture.Local, "opacity", 1);
            await Task.Delay(400);
            writer.SetDeferred(fixture.Local, "opacity", 2);
            await Task.Delay(650);
            Assert.AreEqual(0, fixture.Local.WriteCount, "A later change must restart the quiet period.");
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (fixture.Local.WriteCount == 0 && DateTime.UtcNow < deadline) await Task.Delay(25);
            Assert.AreEqual(1, fixture.Local.WriteCount);

            writer.SetDeferred(fixture.Local, "opacity", 3);
            fixture.Local.Set("theme", "dark");
            writer.Sync(fixture.Local);
            await Task.Delay(350);
            Assert.AreEqual(2, fixture.Local.WriteCount);
            using var saved = JsonDocument.Parse(File.ReadAllText(fixture.Local.FilePath));
            Assert.AreEqual(3, saved.RootElement.GetProperty("opacity").GetInt32());
            Assert.AreEqual("dark", saved.RootElement.GetProperty("theme").GetString());
        });
    }

    [TestMethod]
    public async Task DeferredSettings_KeepFailedWritesPendingForRetry()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new SettingsFixture();
            var writer = new FrontendDeferredSettingsWriter();
            writer.SetDeferred(fixture.Local, "opacity", 42);
            fixture.Local.FailWrites = true;
            Assert.Throws<IOException>(writer.Flush);
            fixture.Local.FailWrites = false;
            writer.Flush();
            Assert.AreEqual(1, fixture.Local.WriteCount);
            Assert.IsTrue(File.Exists(fixture.Local.FilePath));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task DeferredSettings_ReuseInstanceProviderAndPreserveAnImmediateEdit()
    {
        await RunOnUiThread(() =>
        {
            using var fixture = new SettingsFixture();
            var writer = new FrontendDeferredSettingsWriter();
            var opens = 0;
            CommonFileProvider Open()
            {
                opens++;
                return fixture.Local;
            }
            writer.SetDeferred(fixture.Local.FilePath, Open, "ram", 3);
            writer.SetDeferred(fixture.Local.FilePath, Open, "ram", 8);
            writer.UpdateAndSync(fixture.Local.FilePath, Open, provider => provider.Set("title", "Minecraft"));
            writer.Flush();
            Assert.AreEqual(1, opens);
            Assert.AreEqual(1, fixture.Local.WriteCount);
            using var saved = JsonDocument.Parse(File.ReadAllText(fixture.Local.FilePath));
            Assert.AreEqual(8, saved.RootElement.GetProperty("ram").GetInt32());
            Assert.AreEqual("Minecraft", saved.RootElement.GetProperty("title").GetString());
            return Task.CompletedTask;
        });
    }

    private static async Task RunOnUiThread(Func<Task> action)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(FrontendAnimationTest.AnimationTestApplication));
        try
        {
            await session.Dispatch(async () => { await action(); return true; }, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await Task.Run(session.Dispose).ConfigureAwait(false);
        }
    }

    private sealed class SettingsFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pcl-settings-test", Guid.NewGuid().ToString("N"));
        public CountingProvider Local { get; }
        public CountingProvider Shared { get; }

        public SettingsFixture()
        {
            Local = new CountingProvider(Path.Combine(_root, "local.json"));
            Shared = new CountingProvider(Path.Combine(_root, "shared.json"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class CountingProvider(string path) : CommonFileProvider(path)
    {
        private readonly Dictionary<string, object?> _values = [];
        public int WriteCount { get; private set; }
        public bool FailWrites { get; set; }
        public override T Get<T>(string key) => (T)_values[key]!;
        public override void Set<T>(string key, T value) => _values[key] = value;
        public override bool Exists(string key) => _values.ContainsKey(key);
        public override void Remove(string key) => _values.Remove(key);
        protected override void WriteToStream(Stream stream)
        {
            if (FailWrites) throw new IOException("Simulated write failure.");
            JsonSerializer.Serialize(stream, _values);
            WriteCount++;
        }
    }
}
