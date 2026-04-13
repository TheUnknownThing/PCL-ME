using System;
using System.Net;
using Microsoft.Win32;
using PCL.Core.IO.Net.Http.Proxying;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;

namespace PCL.Core.IO.Net.Http.Client;

internal sealed class WindowsRegistrySystemProxySource : ISystemProxySource
{
    private readonly object _lock = new();
    private readonly WebProxy _systemWebProxy = new() { BypassProxyOnLocal = true };
    private const string ProxyRegPathFull = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string ProxyRegPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private readonly IRegistryChangeMonitor _proxyMonitor = RegistryChangeMonitorProvider.Create(ProxyRegPath);

    public WindowsRegistrySystemProxySource()
    {
        Refresh();
        _proxyMonitor.Changed += _onSystemProxyChanged;
    }

    public IWebProxy Proxy
    {
        get
        {
            lock (_lock)
            {
                return _systemWebProxy;
            }
        }
    }

    public void Refresh()
    {
        lock (_lock)
        {
            try
            {
                var isSystemProxyEnabled = (int)(Registry.GetValue(ProxyRegPathFull, "ProxyEnable", 0) ?? 0);
                var systemProxyAddress = Registry.GetValue(ProxyRegPathFull, "ProxyServer", string.Empty) as string;
                if (systemProxyAddress is not null && !systemProxyAddress.StartsWith("http")) systemProxyAddress = $"http://{systemProxyAddress}/";
                _systemWebProxy.Address = string.IsNullOrEmpty(systemProxyAddress) || isSystemProxyEnabled == 0
                    ? null
                    : new Uri(systemProxyAddress);
                LogWrapper.Info("Proxy",
                    $"已从操作系统更新代理设置，系统代理状态：{isSystemProxyEnabled}|{systemProxyAddress}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Proxy", "获取系统代理时出现异常");
            }
        }
    }

    public void Dispose()
    {
        _proxyMonitor.Dispose();
        GC.SuppressFinalize(this);
    }

    private void _onSystemProxyChanged(object? sender, EventArgs e)
    {
        Refresh();
    }
}
