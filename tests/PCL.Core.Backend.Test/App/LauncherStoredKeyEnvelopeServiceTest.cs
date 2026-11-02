using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherStoredKeyEnvelopeServiceTest
{
    [TestMethod]
    public void ReadKeyReturnsRawPayloadForPortableEnvelope()
    {
        var key = new byte[] { 1, 2, 3, 4 };
        var result = LauncherStoredKeyEnvelopeService.ReadKey(
            new LauncherVersionedData(2, key),
            persistedPath: "/tmp/pcl-test/UserKey.bin",
            secretStore: new FakeLauncherPlatformSecretStore());

        CollectionAssert.AreEqual(key, result);
    }

    [TestMethod]
    public void CreateStoredKeyEnvelopeUsesPlatformSecretStoreOnNonWindows()
    {
        var key = new byte[] { 9, 8, 7, 6 };
        var persistedPath = "/tmp/pcl-test/UserKey.bin";
        var secretStore = new FakeLauncherPlatformSecretStore();
        var envelope = LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(key, persistedPath, secretStore);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(1u, envelope.Version);
            CollectionAssert.AreEqual(key, LauncherStoredKeyEnvelopeService.ReadKey(envelope, persistedPath, secretStore));
            return;
        }

        Assert.AreEqual(3u, envelope.Version);
        CollectionAssert.AreEqual(key, LauncherStoredKeyEnvelopeService.ReadKey(envelope, persistedPath, secretStore));
    }

    [TestMethod]
    public void TryUpgradeStoredKeyEnvelopeMigratesLegacyRawEnvelopeWhenSecretStoreIsAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var key = new byte[] { 1, 3, 5, 7 };
        var persistedPath = "/tmp/pcl-test/UserKey.bin";
        var secretStore = new FakeLauncherPlatformSecretStore();

        var upgradedEnvelope = LauncherStoredKeyEnvelopeService.TryUpgradeStoredKeyEnvelope(
            new LauncherVersionedData(2, key),
            key,
            persistedPath,
            secretStore);

        Assert.IsNotNull(upgradedEnvelope);
        Assert.AreEqual(3u, upgradedEnvelope.Value.Version);
        CollectionAssert.AreEqual(
            key,
            LauncherStoredKeyEnvelopeService.ReadKey(upgradedEnvelope.Value, persistedPath, secretStore));
    }

    [TestMethod]
    public void CreateStoredKeyEnvelopeFallsBackToPortableEnvelopeWhenSecretStoreWriteFails()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var key = new byte[] { 9, 7, 5, 3 };
        var persistedPath = "/tmp/pcl-test/UserKey.bin";

        var envelope = LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(
            key,
            persistedPath,
            new ThrowingLauncherPlatformSecretStore());

        Assert.AreEqual(2u, envelope.Version);
        CollectionAssert.AreEqual(key, envelope.Data);
    }

    [TestMethod]
    public void TryUpgradeStoredKeyEnvelopeReturnsNullWhenSecretStoreWriteFails()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var key = new byte[] { 2, 4, 6, 8 };
        var persistedPath = "/tmp/pcl-test/UserKey.bin";

        var upgradedEnvelope = LauncherStoredKeyEnvelopeService.TryUpgradeStoredKeyEnvelope(
            new LauncherVersionedData(2, key),
            key,
            persistedPath,
            new ThrowingLauncherPlatformSecretStore());

        Assert.IsNull(upgradedEnvelope);
    }

    private sealed class FakeLauncherPlatformSecretStore : ILauncherPlatformSecretStore
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsSupported => true;

        public byte[] ReadSecret(string secretId) => _values[secretId];

        public void WriteSecret(string secretId, byte[] secretValue) => _values[secretId] = secretValue;
    }

    private sealed class ThrowingLauncherPlatformSecretStore : ILauncherPlatformSecretStore
    {
        public bool IsSupported => true;

        public byte[] ReadSecret(string secretId) => throw new InvalidOperationException("store unavailable");

        public void WriteSecret(string secretId, byte[] secretValue) =>
            throw new InvalidOperationException("store unavailable");
    }
}
