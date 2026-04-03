namespace PCL.Core.Utils.Secret;

[System.Obsolete("Use LauncherIdentity or WindowsDeviceIdentityProvider instead.")]
public class Identify
{
    public static byte[] RawId { get => field ??= WindowsDeviceIdentityProvider.GetRawId(); } = null!;
    public static string LauncherId { get => field ??= WindowsDeviceIdentityProvider.GetLauncherId(); } = null!;
}
