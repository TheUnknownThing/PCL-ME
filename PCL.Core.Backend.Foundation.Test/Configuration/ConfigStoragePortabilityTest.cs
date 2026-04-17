using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Core.Test;

[TestClass]
public class ConfigStoragePortabilityTest
{
    [TestCleanup]
    public void Cleanup()
    {
        ConfigStorageHooks.AccessFailureHandler = null;
        ConfigStorageHooks.SaveFailureHandler = null;
        ConfigStorageHooks.EnableTrace = false;
    }

    [TestMethod]
    public void ConfigMigration_PrefersHighestPriorityAmongShortestPaths()
    {
        var root = CreateTempDirectory();
        try
        {
            var start = Path.Combine(root, "start.txt");
            var middleA = Path.Combine(root, "middle-a.txt");
            var middleB = Path.Combine(root, "middle-b.txt");
            var target = Path.Combine(root, "target.txt");
            File.WriteAllText(start, "origin");

            var migrated = ConfigMigration.Migrate(target,
            [
                new ConfigMigration { From = start, To = middleA, Priority = 1, OnMigration = (_, to) => File.WriteAllText(to, "path-a") },
                new ConfigMigration { From = middleA, To = target, Priority = 1, OnMigration = (_, to) => File.WriteAllText(to, "path-a") },
                new ConfigMigration { From = start, To = middleB, Priority = 5, OnMigration = (_, to) => File.WriteAllText(to, "path-b") },
                new ConfigMigration { From = middleB, To = target, Priority = 5, OnMigration = (_, to) => File.WriteAllText(to, "path-b") }
            ]);

            Assert.IsTrue(migrated);
            Assert.AreEqual("path-b", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void YamlFileProvider_LoadsFromSiblingJson_WhenYamlMissing()
    {
        var root = CreateTempDirectory();
        try
        {
            var yamlPath = Path.Combine(root, "config.v1.yml");
            var jsonPath = Path.Combine(root, "config.v1.json");
            File.WriteAllText(jsonPath, """{"Flag":true,"Name":"PCL"}""");

            var provider = new YamlFileProvider(yamlPath);

            Assert.IsTrue(provider.Exists("Flag"));
            Assert.IsTrue(provider.Get<bool>("Flag"));
            Assert.AreEqual("PCL", provider.Get<string>("Name"));
            Assert.IsTrue(File.Exists(yamlPath));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void ConfigStorage_InvokesAccessFailureHook()
    {
        ConfigStorageAccessFailureContext? captured = null;
        ConfigStorageHooks.AccessFailureHandler = context =>
        {
            captured = context;
            return true;
        };

        var storage = new ThrowingStorage();
        var exists = storage.Exists("boom");

        Assert.IsFalse(exists);
        Assert.IsNotNull(captured);
        Assert.AreEqual(StorageAction.Exists, captured.Action);
        Assert.AreEqual("boom", captured.Key);
    }

    [TestMethod]
    public void FileConfigStorage_InvokesSaveFailureHook()
    {
        ConfigStorageSaveFailureContext? captured = null;
        ConfigStorageHooks.SaveFailureHandler = context => captured = context;

        var storage = new FileConfigStorage(new ThrowingFileProvider());
        storage.SetValue("alpha", 1);
        SpinWait.SpinUntil(() => captured is not null, TimeSpan.FromSeconds(1));
        storage.Stop();

        Assert.IsNotNull(captured);
        Assert.AreEqual("/virtual/config.yml", captured.FilePath);
        Assert.AreEqual("Failed to save configuration file.", captured.Message);
        Assert.IsInstanceOfType<IOException>(captured.Exception);
        Assert.AreEqual("sync failure", captured.Exception.Message);
    }

    [TestMethod]
    public void FileConfigStorage_Stop_DrainsQueuedWrites()
    {
        var provider = new BlockingFileProvider();
        var storage = new FileConfigStorage(provider);

        storage.SetValue("alpha", 1);
        storage.SetValue("beta", 2);
        var stopTask = Task.Run(storage.Stop);

        Assert.IsTrue(provider.FirstWriteStarted.Wait(TimeSpan.FromSeconds(1)));
        provider.ReleaseFirstWrite.Set();
        Assert.IsTrue(stopTask.Wait(TimeSpan.FromSeconds(1)));
        Assert.AreEqual(1, provider.Values["alpha"]);
        Assert.AreEqual(2, provider.Values["beta"]);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pcl-config-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class ThrowingStorage : ConfigStorage
    {
        protected override bool OnAccess<TKey, TValue>(StorageAction action, ref TKey key, [NotNullWhen(true)] ref TValue value, object? argument)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class ThrowingFileProvider : IKeyValueFileProvider
    {
        public string FilePath => "/virtual/config.yml";
        public T Get<T>(string key) => throw new NotSupportedException();
        public void Set<T>(string key, T value) { }
        public bool Exists(string key) => false;
        public void Remove(string key) { }
        public void Sync() => throw new IOException("sync failure");
    }

    private sealed class BlockingFileProvider : IKeyValueFileProvider
    {
        public string FilePath => "/virtual/config.yml";
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);
        public ManualResetEventSlim FirstWriteStarted { get; } = new(false);
        public ManualResetEventSlim ReleaseFirstWrite { get; } = new(false);

        public T Get<T>(string key) => (T)Values[key]!;

        public void Set<T>(string key, T value)
        {
            if (string.Equals(key, "alpha", StringComparison.Ordinal))
            {
                FirstWriteStarted.Set();
                ReleaseFirstWrite.Wait(TimeSpan.FromSeconds(1));
            }

            Values[key] = value;
        }

        public bool Exists(string key) => Values.ContainsKey(key);

        public void Remove(string key) => Values.Remove(key);

        public void Sync() { }
    }
}
