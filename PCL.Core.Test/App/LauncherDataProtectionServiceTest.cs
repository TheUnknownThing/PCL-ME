using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherDataProtectionServiceTest
{
    [TestMethod]
    public void ProtectAndUnprotectRoundTripPreservesPlainText()
    {
        byte[] encryptionKey =
        [
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32
        ];
        const string plainText = "portable secret payload";

        var encrypted = LauncherDataProtectionService.Protect(plainText, encryptionKey);
        var envelope = LauncherVersionedDataService.Parse(Convert.FromBase64String(encrypted));
        var decrypted = LauncherDataProtectionService.Unprotect(encrypted, encryptionKey);

        Assert.AreEqual(LauncherDataProtectionService.DefaultProvider.Version, envelope.Version);
        Assert.AreNotEqual(plainText, encrypted);
        Assert.AreEqual(plainText, decrypted);
    }

    [TestMethod]
    public void ProtectReturnsEmptyStringForEmptyPayload()
    {
        byte[] encryptionKey =
        [
            32, 31, 30, 29, 28, 27, 26, 25,
            24, 23, 22, 21, 20, 19, 18, 17,
            16, 15, 14, 13, 12, 11, 10, 9,
            8, 7, 6, 5, 4, 3, 2, 1
        ];

        var encrypted = LauncherDataProtectionService.Protect(string.Empty, encryptionKey);
        var decrypted = LauncherDataProtectionService.Unprotect(string.Empty, encryptionKey);

        Assert.AreEqual(string.Empty, encrypted);
        Assert.AreEqual(string.Empty, decrypted);
    }
}
