using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherIdentityStorageServiceTest
{
    [TestMethod]
    public void TryReadPersistedLauncherIdReturnsNullWhenFileDoesNotExist()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var launcherIdPath = LauncherIdentityStorageService.GetPersistedLauncherIdPath(rootPath);

            var result = LauncherIdentityStorageService.TryReadPersistedLauncherId(launcherIdPath);

            Assert.IsNull(result);
        }
        finally
        {
            DeleteTempDirectory(rootPath);
        }
    }

    [TestMethod]
    public void PersistLauncherIdCreatesDirectoryAndSupportsRoundTripRead()
    {
        var rootPath = CreateTempDirectory();
        try
        {
            var sharedDataPath = Path.Combine(rootPath, "portable", "shared");
            var launcherIdPath = LauncherIdentityStorageService.GetPersistedLauncherIdPath(sharedDataPath);

            LauncherIdentityStorageService.PersistLauncherId(launcherIdPath, "ABCD-EFGH-IJKL-MNOP");

            Assert.IsTrue(File.Exists(launcherIdPath));
            Assert.AreEqual("ABCD-EFGH-IJKL-MNOP", LauncherIdentityStorageService.TryReadPersistedLauncherId(launcherIdPath));
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
