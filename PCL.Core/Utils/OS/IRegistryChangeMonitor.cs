using System;

namespace PCL.Core.Utils.OS;

internal interface IRegistryChangeMonitor : IDisposable
{
    event EventHandler? Changed;
}
