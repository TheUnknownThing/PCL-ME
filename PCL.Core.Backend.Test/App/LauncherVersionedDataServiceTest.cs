using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherVersionedDataServiceTest
{
    [TestMethod]
    public void SerializeAndParseRoundTripPreservesVersionAndPayload()
    {
        var payload = new byte[] { 1, 3, 5, 7 };
        var encoded = LauncherVersionedDataService.Serialize(new LauncherVersionedData(42, payload));
        var result = LauncherVersionedDataService.Parse(encoded);

        Assert.AreEqual(42u, result.Version);
        CollectionAssert.AreEqual(payload, result.Data);
    }

    [TestMethod]
    public void IsValidRejectsUnknownPayload()
    {
        var result = LauncherVersionedDataService.IsValid(new byte[] { 1, 2, 3, 4 });

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ParseBase64MatchesBinaryParse()
    {
        var encoded = LauncherVersionedDataService.Serialize(new LauncherVersionedData(5, [8, 6, 4]));
        var base64 = Convert.ToBase64String(encoded);
        var result = LauncherVersionedDataService.ParseBase64(base64);

        Assert.AreEqual(5u, result.Version);
        CollectionAssert.AreEqual(new byte[] { 8, 6, 4 }, result.Data);
    }
}
