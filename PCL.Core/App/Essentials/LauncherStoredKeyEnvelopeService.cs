using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.App.Essentials;

public static class LauncherStoredKeyEnvelopeService
{
    private static readonly byte[] IdentifyEntropy = Encoding.UTF8.GetBytes("PCL CE Encryption Key");

    public static byte[] ReadKey(LauncherVersionedData data)
    {
        return data.Version switch
        {
            1 => ReadWindowsProtectedKey(data.Data),
            2 => data.Data,
            _ => throw new NotSupportedException("Unsupported launcher key version.")
        };
    }

    public static LauncherVersionedData CreateStoredKeyEnvelope(byte[] randomKey)
    {
        ArgumentNullException.ThrowIfNull(randomKey);

        if (OperatingSystem.IsWindows())
        {
            return new LauncherVersionedData(
                Version: 1,
                Data: ProtectedData.Protect(randomKey, IdentifyEntropy, DataProtectionScope.CurrentUser));
        }

        return new LauncherVersionedData(
            Version: 2,
            Data: randomKey);
    }

    private static byte[] ReadWindowsProtectedKey(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Protected launcher keys are only supported on Windows.");
        }

        return ProtectedData.Unprotect(data, IdentifyEntropy, DataProtectionScope.CurrentUser);
    }
}
