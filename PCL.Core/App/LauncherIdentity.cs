using PCL.Core.Utils.Secret;

namespace PCL.Core.App;

public static class LauncherIdentity
{
    public static string LauncherId => Identify.LauncherId;
}
