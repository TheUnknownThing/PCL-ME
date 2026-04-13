using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Http.Proxying;

namespace PCL.Core.Test;

[TestClass]
public class ProxyManagerTest
{
    [TestMethod]
    public void Constructor_RefreshesSystemProxySource()
    {
        var source = new FakeSystemProxySource();
        using var manager = new ProxyManager(source);

        Assert.AreEqual(1, source.RefreshCount);
        Assert.AreEqual(new Uri("http://system.proxy:8080/"), manager.GetProxy(new Uri("http://example.com/")));
        Assert.IsFalse(manager.IsBypassed(new Uri("http://example.com/")));
    }

    [TestMethod]
    public void NoProxyMode_BypassesAllRequests()
    {
        using var manager = new ProxyManager(new FakeSystemProxySource())
        {
            Mode = ProxyMode.NoProxy
        };

        Assert.IsTrue(manager.IsBypassed(new Uri("http://example.com/")));
        Assert.IsNull(manager.GetProxy(new Uri("http://example.com/")));
    }

    [TestMethod]
    public void CustomProxyMode_UsesCustomProxyAndCredentials()
    {
        using var manager = new ProxyManager(new FakeSystemProxySource())
        {
            Mode = ProxyMode.CustomProxy,
            CustomProxyAddress = new Uri("http://custom.proxy:9090/"),
            Credentials = new NetworkCredential("user", "pass")
        };

        Assert.AreEqual(new Uri("http://custom.proxy:9090/"), manager.GetProxy(new Uri("http://example.com/")));
        Assert.AreEqual("user", ((NetworkCredential)manager.Credentials!).UserName);
    }

    private sealed class FakeSystemProxySource : ISystemProxySource
    {
        private readonly WebProxy _proxy = new(new Uri("http://system.proxy:8080/"));

        public int RefreshCount { get; private set; }
        public IWebProxy Proxy => _proxy;

        public void Refresh()
        {
            RefreshCount++;
        }

        public void Dispose() { }
    }
}
