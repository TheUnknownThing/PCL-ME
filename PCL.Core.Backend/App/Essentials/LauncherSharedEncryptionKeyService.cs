using System;

namespace PCL.Core.App.Essentials;

public static class LauncherSharedEncryptionKeyService
{
    public static byte[] ResolveOrCreate(string sharedDataPath, string? explicitKeyOverride = null)
    {
        return ResolveOrCreate(sharedDataPath, explicitKeyOverride, secretStore: null);
    }

    internal static byte[] ResolveOrCreate(
        string sharedDataPath,
        string? explicitKeyOverride,
        ILauncherPlatformSecretStore? secretStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);

        var persistedKeyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(sharedDataPath);
        var persistedKeyEnvelope = LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(persistedKeyPath);
        var resolution = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: explicitKeyOverride,
            PersistedKeyEnvelope: persistedKeyEnvelope,
            ReadPersistedKey: data => LauncherStoredKeyEnvelopeService.ReadKey(data, persistedKeyPath, secretStore),
            ProtectGeneratedKey: key => LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope(key, persistedKeyPath, secretStore)));

        if (resolution.ShouldPersist && resolution.PersistedKeyEnvelope is not null)
        {
            LauncherSecretKeyStorageService.PersistKeyEnvelope(persistedKeyPath, resolution.PersistedKeyEnvelope);
        }
        else if (persistedKeyEnvelope is not null)
        {
            var persistedKey = LauncherVersionedDataService.Parse(persistedKeyEnvelope);
            var upgradedEnvelope = LauncherStoredKeyEnvelopeService.TryUpgradeStoredKeyEnvelope(
                persistedKey,
                resolution.Key,
                persistedKeyPath,
                secretStore);
            if (upgradedEnvelope is not null)
            {
                LauncherSecretKeyStorageService.PersistKeyEnvelope(
                    persistedKeyPath,
                    LauncherVersionedDataService.Serialize(upgradedEnvelope.Value));
            }
        }

        return resolution.Key;
    }
}
