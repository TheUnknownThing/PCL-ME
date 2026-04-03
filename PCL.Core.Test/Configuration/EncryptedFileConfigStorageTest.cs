using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Core.Test.Configuration;

[TestClass]
public sealed class EncryptedFileConfigStorageTest
{
    [TestMethod]
    public void SetValueUsesInjectedProtectorBeforeWritingSourceStorage()
    {
        var source = new MemoryConfigStorage();
        var storage = new EncryptedFileConfigStorage(
            source,
            protect: value => $"enc::{value}",
            unprotect: value => value?.Replace("enc::", string.Empty) ?? string.Empty);

        storage.SetValue("token", "secret-value");

        Assert.AreEqual("enc::secret-value", source.Entries["token"]);
    }

    [TestMethod]
    public void GetValueUsesInjectedUnprotectorBeforeDeserializingObjects()
    {
        var source = new MemoryConfigStorage();
        source.Entries["profile"] = "enc::{\"Name\":\"Alex\",\"Level\":3}";
        var storage = new EncryptedFileConfigStorage(
            source,
            protect: value => $"enc::{value}",
            unprotect: value => value?.Replace("enc::", string.Empty) ?? string.Empty);

        var hasValue = storage.GetValue<TestConfig>("profile", out var profile);

        Assert.IsTrue(hasValue);
        Assert.IsNotNull(profile);
        Assert.AreEqual("Alex", profile.Name);
        Assert.AreEqual(3, profile.Level);
    }

    private sealed record TestConfig(string Name, int Level);

    private sealed class MemoryConfigStorage : ConfigStorage
    {
        public Dictionary<string, string?> Entries { get; } = [];

        protected override bool OnAccess<TKey, TValue>(StorageAction action, ref TKey key, [NotNullWhen(true)] ref TValue value, object? argument)
        {
            var stringKey = key?.ToString() ?? string.Empty;
            switch (action)
            {
                case StorageAction.Set:
                    Entries[stringKey] = value?.ToString();
                    return false;
                case StorageAction.Get:
                    if (!Entries.TryGetValue(stringKey, out var rawValue))
                    {
                        return false;
                    }

                    object? boxed = rawValue;
                    value = (TValue)boxed!;
                    return true;
                case StorageAction.Exists:
                {
                    object existsValue = Entries.ContainsKey(stringKey);
                    value = (TValue)existsValue;
                    return true;
                }
                case StorageAction.Delete:
                    Entries.Remove(stringKey);
                    return false;
                default:
                    return false;
            }
        }
    }
}
