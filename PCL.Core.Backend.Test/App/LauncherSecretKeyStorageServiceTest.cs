using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherSecretKeyStorageServiceTest
{
    [TestMethod]
    public void TryReadPersistedKeyEnvelopeReturnsNullWhenFileDoesNotExist()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var keyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(rootPath);

            var result = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(keyPath);

            Assert.IsNull(result);
        }
        finally
        {
            DeleteTempDirectory(rootPath);
        }
    }

    [TestMethod]
    public void PersistKeyEnvelopeCreatesDirectoryAndSupportsRoundTripRead()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var sharedDataPath = Path.Combine(rootPath, "portable", "shared");
            var keyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(sharedDataPath);
            byte[] expectedEnvelope = [9, 8, 7, 6, 5, 4];

            LauncherSecretKeyStorageService.PersistKeyEnvelope(keyPath, expectedEnvelope);

            Assert.IsTrue(File.Exists(keyPath));
            CollectionAssert.AreEqual(expectedEnvelope, LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(keyPath));
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
