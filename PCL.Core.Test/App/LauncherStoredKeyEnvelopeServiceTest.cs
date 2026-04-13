using System;
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
        var result = LauncherStoredKeyEnvelopeService.ReadKey(new LauncherVersionedData(2, key));

        CollectionAssert.AreEqual(key, result);
    }

    [TestMethod]
    public void CreateStoredKeyEnvelopeUsesCurrentPlatformStorageMode()
    {
        var key = new byte[] { 9, 8, 7, 6 };
        var envelope = LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(key);

        if (OperatingSystem.IsWindows())
        {
            Assert.AreEqual(1u, envelope.Version);
            CollectionAssert.AreEqual(key, LauncherStoredKeyEnvelopeService.ReadKey(envelope));
            return;
        }

        Assert.AreEqual(2u, envelope.Version);
        CollectionAssert.AreEqual(key, envelope.Data);
    }
}
