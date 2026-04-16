using System;
using System.Linq;
using System.Security.Cryptography;
using PCL.Core.Utils.Hash;

namespace PCL.Core.App.Essentials;

public static class LauncherSecretKeyResolutionService
{
    public static LauncherSecretKeyResolutionPlan Resolve(LauncherSecretKeyResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadPersistedKey);
        ArgumentNullException.ThrowIfNull(request.ProtectGeneratedKey);

        var explicitKey = ParseExplicitKeyOverride(request.ExplicitKeyOverride);
        if (explicitKey is not null)
        {
            return new LauncherSecretKeyResolutionPlan(
                explicitKey,
                LauncherSecretKeySource.EnvironmentOverride,
                PersistedKeyEnvelope: null,
                ShouldPersist: false);
        }

        if (request.PersistedKeyEnvelope is not null)
        {
            var persistedEnvelope = LauncherVersionedDataService.Parse(request.PersistedKeyEnvelope);
            return new LauncherSecretKeyResolutionPlan(
                request.ReadPersistedKey(persistedEnvelope),
                LauncherSecretKeySource.PersistedFile,
                PersistedKeyEnvelope: null,
                ShouldPersist: false);
        }

        var generatedKey = request.GenerateKey?.Invoke() ?? GenerateRandomKey();
        return new LauncherSecretKeyResolutionPlan(
            generatedKey,
            LauncherSecretKeySource.GeneratedKey,
            LauncherVersionedDataService.Serialize(request.ProtectGeneratedKey(generatedKey)),
            ShouldPersist: true);
    }

    public static byte[]? ParseExplicitKeyOverride(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim();
        try
        {
            if (normalized.Length == 64 && normalized.All(Uri.IsHexDigit))
            {
                return Convert.FromHexString(normalized);
            }

            return SHA256Provider.Instance.ComputeHash(normalized);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("The PCL_ENCRYPTION_KEY environment variable could not be converted into a valid key.", ex);
        }
    }

    public static byte[] GenerateRandomKey()
    {
        var randomKey = new byte[32];
        RandomNumberGenerator.Fill(randomKey);
        return randomKey;
    }
}

public sealed record LauncherSecretKeyResolutionRequest(
    string? ExplicitKeyOverride,
    byte[]? PersistedKeyEnvelope,
    Func<LauncherVersionedData, byte[]> ReadPersistedKey,
    Func<byte[], LauncherVersionedData> ProtectGeneratedKey,
    Func<byte[]>? GenerateKey = null);

public sealed record LauncherSecretKeyResolutionPlan(
    byte[] Key,
    LauncherSecretKeySource Source,
    byte[]? PersistedKeyEnvelope,
    bool ShouldPersist);

public enum LauncherSecretKeySource
{
    EnvironmentOverride = 0,
    PersistedFile = 1,
    GeneratedKey = 2
}
