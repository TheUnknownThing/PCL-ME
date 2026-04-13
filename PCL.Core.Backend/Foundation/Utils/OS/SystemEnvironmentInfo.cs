namespace PCL.Core.Utils.OS;

public static class SystemEnvironmentInfo
{
    public static SystemEnvironmentSnapshot GetSnapshot()
    {
        return SystemEnvironmentSourceProvider.Current.GetSnapshot();
    }
}
