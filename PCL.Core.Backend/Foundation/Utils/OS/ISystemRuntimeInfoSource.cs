namespace PCL.Core.Utils.OS;

internal interface ISystemRuntimeInfoSource
{
    SystemRuntimeSnapshot GetSnapshot();
}
