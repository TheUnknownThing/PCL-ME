using System;
using System.Net;
using PCL.Core.IO.Net.Http.Proxying;
using PCL.Core.Logging;

namespace PCL.Core.IO.Net.Http.Client;

public sealed class HttpProxyManager : ProxyManager
{
    public static readonly HttpProxyManager Instance = new();

    public enum ProxyMode
    {
        NoProxy,
        SystemProxy,
        CustomProxy
    }

    private HttpProxyManager() : base(_CreateSystemProxySource()) { }

    public new ProxyMode Mode
    {
        get => (ProxyMode)(int)base.Mode;
        set => base.Mode = (PCL.Core.IO.Net.Http.Proxying.ProxyMode)(int)value;
    }

    private static ISystemProxySource _CreateSystemProxySource()
    {
        if (OperatingSystem.IsWindows()) return new WindowsRegistrySystemProxySource();
        LogWrapper.Info("Proxy", "使用平台默认系统代理提供器");
        return new DefaultSystemProxySource();
    }
}
