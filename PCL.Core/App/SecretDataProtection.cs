using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class SecretDataProtection
{
    public static string Protect(string? data)
    {
        return EncryptHelper.SecretEncrypt(data);
    }

    public static string Unprotect(string? data)
    {
        return EncryptHelper.SecretDecrypt(data);
    }
}
