using PCL.Core.App.Essentials;

namespace PCL.Core.Utils.Secret;

internal static class LegacySecretKeyProvider
{
    public static string? LegacyDecryptKey => LegacySecretRuntimeService.LegacyDecryptKey;
}
