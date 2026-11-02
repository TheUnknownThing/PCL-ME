using System.Net;

namespace PCL.Core.IO.Net.Http.Proxying;

public sealed class DefaultSystemProxySource : ISystemProxySource
{
    public IWebProxy? Proxy => WebRequest.DefaultWebProxy;

    public void Refresh() { }

    public void Dispose() { }
}
