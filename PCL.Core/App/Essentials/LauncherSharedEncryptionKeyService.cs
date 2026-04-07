using System;

namespace PCL.Core.App.Essentials;

public static class LauncherSharedEncryptionKeyService
{
    public static byte[] ResolveOrCreate(string sharedDataPath, string? explicitKeyOverride = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedDataPath);

        var persistedKeyPath = LauncherSecretKeyStorageService.GetPersistedKeyPath(sharedDataPath);
        var resolution = LauncherSecretKeyResolutionService.Resolve(new LauncherSecretKeyResolutionRequest(
            ExplicitKeyOverride: explicitKeyOverride,
            PersistedKeyEnvelope: LauncherSecretKeyStorageService.TryReadPersistedKeyEnvelope(persistedKeyPath),
            ReadPersistedKey: LauncherStoredKeyEnvelopeService.ReadKey,
            ProtectGeneratedKey: LauncherStoredKeyEnvelopeService.CreateStoredKeyEnvelope));

        if (resolution.ShouldPersist && resolution.PersistedKeyEnvelope is not null)
        {
            LauncherSecretKeyStorageService.PersistKeyEnvelope(persistedKeyPath, resolution.PersistedKeyEnvelope);
        }

        return resolution.Key;
    }
}
