using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherSharedEncryptionKeyServiceTest
{
    [TestMethod]
    public void ResolveOrCreateReturnsExistingPersistedKey()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var expectedKey = new byte[] { 4, 5, 6, 7 };
            var keyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(rootPath);
            var secretStore = new FakeLauncherPlatformSecretStore();
            var envelope = LauncherVersionedDataService.Serialize(
                LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(expectedKey, keyPath, secretStore));
            LauncherSecretKeyStorageService.PersistKeyEnvelope(keyPath, envelope);

            var result = LauncherSharedEncryptionKeyService.ResolveOrCreate(rootPath, explicitKeyOverride: null, secretStore);

            CollectionAssert.AreEqual(expectedKey, result);
        }
        finally
        {
            DeleteTempDirectory(rootPath);
        }
    }

    [TestMethod]
    public void ResolveOrCreateGeneratesAndPersistsNewKeyWhenMissing()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var secretStore = new FakeLauncherPlatformSecretStore();
            var result = LauncherSharedEncryptionKeyService.ResolveOrCreate(rootPath, explicitKeyOverride: null, secretStore);

            Assert.AreEqual(32, result.Length);
            var keyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(rootPath);
            Assert.IsTrue(File.Exists(keyPath));
            var persistedEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(keyPath);
            Assert.IsNotNull(persistedEnvelope);

            var persistedKey = LauncherStoredKeyEnvelopeService.ReadKey(
                LauncherVersionedDataService.Parse(persistedEnvelope!),
                keyPath,
                secretStore);
            CollectionAssert.AreEqual(result, persistedKey);
        }
        finally
        {
            DeleteTempDirectory(rootPath);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pcl-ce-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string rootPath)
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, true);
        }
    }

    private sealed class FakeLauncherPlatformSecretStore : ILauncherPlatformSecretStore
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsSupported => true;

        public byte[] ReadSecret(string secretId) => _values[secretId];

        public void WriteSecret(string secretId, byte[] secretValue) => _values[secretId] = secretValue;
    }
}
