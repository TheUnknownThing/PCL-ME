using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Essentials;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class LauncherSecretKeyResolutionServiceTest
{
    [TestMethod]
    public void ResolvePrefersEnvironmentOverrideWithoutPersistence()
    {
        var explicitKey = Convert.ToHexString(new byte[32]).ToLowerInvariant();
        var plan = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: explicitKey,
            PersistedKeyEnvelope: LauncherVersionedDataService.Serialize(new LauncherVersionedData(2, [1, 2, 3])),
            ReadPersistedKey: _ => throw new AssertFailedException("Persisted key should not be read when an explicit override exists."),
            ProtectGeneratedKey: _ => throw new AssertFailedException("Generated key should not be created when an explicit override exists.")));

        Assert.AreEqual(LauncherSecretKeySource.EnvironmentOverride, plan.Source);
        Assert.IsFalse(plan.ShouldPersist);
        Assert.IsNull(plan.PersistedKeyEnvelope);
        CollectionAssert.AreEqual(new byte[32], plan.Key);
    }

    [TestMethod]
    public void ResolveUsesPersistedKeyBeforeGeneratingNewKey()
    {
        var persistedEnvelope = LauncherVersionedDataService.Serialize(new LauncherVersionedData(7, [1, 2, 3]));
        var plan = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: null,
            PersistedKeyEnvelope: persistedEnvelope,
            ReadPersistedKey: envelope =>
            {
                Assert.AreEqual(7u, envelope.Version);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, envelope.Data);
                return [4, 5, 6];
            },
            ProtectGeneratedKey: _ => throw new AssertFailedException("Generated key should not be created when a persisted key exists.")));

        Assert.AreEqual(LauncherSecretKeySource.PersistedFile, plan.Source);
        Assert.IsFalse(plan.ShouldPersist);
        Assert.IsNull(plan.PersistedKeyEnvelope);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, plan.Key);
    }

    [TestMethod]
    public void ResolveGeneratesNewPersistedEnvelopeWhenNoExistingKeyExists()
    {
        var generatedKey = new byte[] { 9, 8, 7, 6 };
        var plan = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: null,
            PersistedKeyEnvelope: null,
            ReadPersistedKey: _ => throw new AssertFailedException("Persisted key should not be read when none exists."),
            ProtectGeneratedKey: key => new LauncherVersionedData(2, [.. key, 5]),
            GenerateKey: () => generatedKey));

        Assert.AreEqual(LauncherSecretKeySource.GeneratedKey, plan.Source);
        Assert.IsTrue(plan.ShouldPersist);
        CollectionAssert.AreEqual(generatedKey, plan.Key);
        Assert.IsNotNull(plan.PersistedKeyEnvelope);

        var envelope = LauncherVersionedDataService.Parse(plan.PersistedKeyEnvelope!);
        Assert.AreEqual(2u, envelope.Version);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7, 6, 5 }, envelope.Data);
    }

    [TestMethod]
    public void ParseExplicitKeyOverrideHashesPlainTextFallback()
    {
        var result = LauncherSecretKeyResolutionService.ParseExplicitKeyOverride("plain-text-key");

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(
            SHA256Provider.Instance.ComputeHash("plain-text-key"),
            result!);
    }
}
