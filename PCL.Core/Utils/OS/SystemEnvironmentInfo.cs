namespace PCL.Core.Utils.OS;

public static class SystemEnvironmentInfo
{
    public static SystemEnvironmentSnapshot GetSnapshot() => SystemEnvironmentSourceProvider.Current.GetSnapshot();
}
