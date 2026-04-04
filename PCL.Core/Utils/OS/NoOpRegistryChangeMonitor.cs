using System;

namespace PCL.Core.Utils.OS;

internal sealed class NoOpRegistryChangeMonitor : IRegistryChangeMonitor
{
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
