using System;
using System.Net;

namespace PCL.Core.IO.Net.Http.Proxying;

public class ProxyManager : IWebProxy, IDisposable
{
    private readonly object _lock = new();
    private readonly WebProxy _customWebProxy = new() { BypassProxyOnLocal = true };
    private readonly ISystemProxySource _systemProxySource;
    private ProxyMode _mode = ProxyMode.SystemProxy;

    public ProxyManager(ISystemProxySource? systemProxySource = null)
    {
        _systemProxySource = systemProxySource ?? new DefaultSystemProxySource();
        RefreshSystemProxy();
    }

    public virtual void RefreshSystemProxy()
    {
        lock (_lock)
        {
            _systemProxySource.Refresh();
            _ApplyBypassOnLocal();
        }
    }

    public ProxyMode Mode
    {
        get { lock (_lock) return _mode; }
        set { lock (_lock) _mode = value; }
    }

    public Uri? CustomProxyAddress
    {
        get { lock (_lock) return _customWebProxy.Address; }
        set { lock (_lock) _customWebProxy.Address = value; }
    }

    public ICredentials? CustomProxyCredentials
    {
        get { lock (_lock) return _customWebProxy.Credentials; }
        set { lock (_lock) _customWebProxy.Credentials = value; }
    }

    public bool BypassOnLocal
    {
        get { lock (_lock) return field; }
        set
        {
            lock (_lock)
            {
                field = value;
                _ApplyBypassOnLocal();
            }
        }
    } = true;

    public Uri? GetProxy(Uri destination)
    {
        lock (_lock)
        {
            return _mode switch
            {
                ProxyMode.NoProxy => null,
                ProxyMode.SystemProxy => _GetSystemProxyUri(destination),
                ProxyMode.CustomProxy => _customWebProxy.GetProxy(destination),
                _ => null
            };
        }
    }

    public bool IsBypassed(Uri host)
    {
        lock (_lock)
        {
            return _mode switch
            {
                ProxyMode.NoProxy => true,
                ProxyMode.SystemProxy => _IsSystemProxyBypassed(host),
                ProxyMode.CustomProxy => _customWebProxy.IsBypassed(host),
                _ => true
            };
        }
    }

    public ICredentials? Credentials
    {
        get
        {
            lock (_lock)
            {
                return _mode == ProxyMode.CustomProxy
                    ? _customWebProxy.Credentials
                    : null;
            }
        }
        set
        {
            lock (_lock)
            {
                _customWebProxy.Credentials = value;
            }
        }
    }

    public void Dispose()
    {
        _systemProxySource.Dispose();
        GC.SuppressFinalize(this);
    }

    private Uri? _GetSystemProxyUri(Uri destination)
    {
        var systemProxy = _systemProxySource.Proxy;
        if (systemProxy == null || systemProxy.IsBypassed(destination)) return null;
        return systemProxy.GetProxy(destination);
    }

    private bool _IsSystemProxyBypassed(Uri host)
    {
        var systemProxy = _systemProxySource.Proxy;
        return systemProxy == null || systemProxy.IsBypassed(host);
    }

    private void _ApplyBypassOnLocal()
    {
        _customWebProxy.BypassProxyOnLocal = BypassOnLocal;
        if (_systemProxySource.Proxy is WebProxy systemWebProxy)
            systemWebProxy.BypassProxyOnLocal = BypassOnLocal;
    }
}
