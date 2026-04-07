using System;
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
            var envelope = LauncherVersionedDataService.Serialize(
                LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(expectedKey));
            LauncherSecretKeyStorageService.PersistKeyEnvelope(keyPath, envelope);

            var result = LauncherSharedEncryptionKeyService.ResolveOrCreate(rootPath);

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
            var result = LauncherSharedEncryptionKeyService.ResolveOrCreate(rootPath);

            Assert.AreEqual(32, result.Length);
            var keyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(rootPath);
            Assert.IsTrue(File.Exists(keyPath));
            var persistedEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(keyPath);
            Assert.IsNotNull(persistedEnvelope);

            var persistedKey = LauncherStoredKeyEnvelopeService.ReadKey(
                LauncherVersionedDataService.Parse(persistedEnvelope!));
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
}
