using System;
using System.Net;

namespace PCL.Core.IO.Net.Http.Proxying;

public interface ISystemProxySource : IDisposable
{
    IWebProxy? Proxy { get; }
    void Refresh();
}
