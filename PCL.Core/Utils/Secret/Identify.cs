using System;
using System.Text;
using PCL.Core.App;

namespace PCL.Core.Utils.Secret;

[System.Obsolete("Use LauncherIdentity or WindowsDeviceIdentityProvider instead.")]
public class Identify
{
    public static byte[] RawId
    {
        get
        {
            if (field is not null)
            {
                return field;
            }

            field = OperatingSystem.IsWindows()
                ? WindowsDeviceIdentityProvider.GetRawId()
                : Encoding.UTF8.GetBytes(LauncherIdentity.LauncherId);
            return field;
        }
    } = null!;

    public static string LauncherId { get => field ??= LauncherIdentity.LauncherId; } = null!;
}
