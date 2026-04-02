using System;

namespace PCL.Core.Utils.OS;

public partial class RegistryChangeMonitor : IDisposable
{
    private readonly IRegistryChangeMonitor _inner;

    public RegistryChangeMonitor(string keyPath)
    {
        _inner = RegistryChangeMonitorProvider.Create(keyPath);
    }

    public event EventHandler? Changed
    {
        add => _inner.Changed += value;
        remove => _inner.Changed -= value;
    }

    public void Dispose()
    {
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }
}
