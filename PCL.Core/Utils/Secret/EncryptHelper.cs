using PCL.Core.App;
using PCL.Core.Utils.Encryption;

namespace PCL.Core.Utils.Secret;

public static class EncryptHelper
{
    public static (IEncryptionProvider Provider, uint Version) DefaultProvider => LauncherDataProtectionRuntime.DefaultProvider;

    public static string SecretEncrypt(string? data)
    {
        return LauncherDataProtectionRuntime.Protect(data);
    }

    public static string SecretDecrypt(string? data)
    {
        return LauncherDataProtectionRuntime.Unprotect(data);
    }
}
