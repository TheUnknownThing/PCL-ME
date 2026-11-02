namespace PCL.Core.Utils.OS;

internal interface ISystemEnvironmentSource
{
    SystemEnvironmentSnapshot GetSnapshot();
}
