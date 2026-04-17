using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Http.Proxying;
using PCL.Core.Testing;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Core.Backend.Test.App;

[TestClass]
[DoNotParallelize]
public sealed class FrontendHttpProxyServiceTest
{
    private IWebProxy? _originalDefaultProxy;
    private FrontendSecureDnsConfiguration _originalSecureDnsConfiguration;

    [TestInitialize]
    public void CaptureDefaultProxy()
    {
        _originalDefaultProxy = HttpClient.DefaultProxy;
        _originalSecureDnsConfiguration = FrontendHttpProxyService.CurrentSecureDnsConfiguration;
    }

    [TestCleanup]
    public void RestoreDefaultProxy()
    {
        if (_originalDefaultProxy is not null)
        {
            HttpClient.DefaultProxy = _originalDefaultProxy;
        }

        FrontendHttpProxyService.ApplySecureDnsConfiguration(_originalSecureDnsConfiguration);
    }

    [TestMethod]
    public void ApplyStoredProxySettings_UsesProtectedCustomProxyCredentials()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var shellActionService = new FrontendShellActionService(
            runtimePaths,
            new FrontendPlatformAdapter(),
            () => { },
            new DictionaryI18nService());

        shellActionService.PersistSharedValue("SystemHttpProxyType", 2);
        shellActionService.PersistProtectedSharedValue("SystemHttpProxy", "http://proxy.example:8080");
        shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomUsername", "proxy-user");
        shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomPassword", "proxy-pass");

        FrontendHttpProxyService.ApplyStoredProxySettings(runtimePaths);

        var proxy = HttpClient.DefaultProxy as ProxyManager;
        Assert.IsNotNull(proxy);
        Assert.AreEqual(ProxyMode.CustomProxy, proxy.Mode);
        Assert.AreEqual(new Uri("http://proxy.example:8080/"), proxy.CustomProxyAddress);
        var credential = proxy.CustomProxyCredentials as NetworkCredential;
        Assert.IsNotNull(credential);
        Assert.AreEqual("proxy-user", credential.UserName);
        Assert.AreEqual("proxy-pass", credential.Password);
    }

    [TestMethod]
    public void ResolveConfiguration_ReadsLegacyPlainTextCredentials()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var shellActionService = new FrontendShellActionService(
            runtimePaths,
            new FrontendPlatformAdapter(),
            () => { },
            new DictionaryI18nService());

        shellActionService.PersistSharedValue("SystemHttpProxyType", 2);
        shellActionService.PersistProtectedSharedValue("SystemHttpProxy", "proxy.example:9090");
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        sharedConfig.Set("SystemHttpProxyCustomUsername", "legacy-user");
        sharedConfig.Set("SystemHttpProxyCustomPassword", "legacy-pass");
        sharedConfig.Sync();

        var configuration = FrontendHttpProxyService.ResolveConfiguration(runtimePaths);

        Assert.AreEqual(ProxyMode.CustomProxy, configuration.Mode);
        Assert.AreEqual(new Uri("http://proxy.example:9090/"), configuration.CustomProxyAddress);
        Assert.IsNotNull(configuration.CustomProxyCredentials);
        Assert.AreEqual("legacy-user", configuration.CustomProxyCredentials.UserName);
        Assert.AreEqual("legacy-pass", configuration.CustomProxyCredentials.Password);
    }

    [TestMethod]
    public void BuildConfiguration_UsesCurrentCustomProxyFormValues()
    {
        var configuration = FrontendHttpProxyService.BuildConfiguration(
            proxyTypeIndex: 2,
            proxyAddress: "http://127.0.0.1:7890",
            proxyUsername: "live-user",
            proxyPassword: "live-pass");

        Assert.AreEqual(ProxyMode.CustomProxy, configuration.Mode);
        Assert.AreEqual(new Uri("http://127.0.0.1:7890/"), configuration.CustomProxyAddress);
        Assert.IsNotNull(configuration.CustomProxyCredentials);
        Assert.AreEqual("live-user", configuration.CustomProxyCredentials.UserName);
        Assert.AreEqual("live-pass", configuration.CustomProxyCredentials.Password);
    }

    [TestMethod]
    public void ReadConfiguredDnsOverHttpsEnabled_DefaultsToTrue()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());

        var isEnabled = FrontendHttpProxyService.ReadConfiguredDnsOverHttpsEnabled(runtimePaths);

        Assert.IsTrue(isEnabled);
    }

    [TestMethod]
    public void ApplyStoredDnsSettings_UsesPersistedValue()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var shellActionService = new FrontendShellActionService(
            runtimePaths,
            new FrontendPlatformAdapter(),
            () => { },
            new DictionaryI18nService());

        shellActionService.PersistSharedValue("SystemNetEnableDoH", false);

        FrontendHttpProxyService.ApplyStoredDnsSettings(runtimePaths);

        Assert.IsFalse(FrontendHttpProxyService.IsDnsOverHttpsEnabled);
    }

    [TestMethod]
    public void ReadConfiguredSecureDnsConfiguration_UsesNewKeys()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var shellActionService = new FrontendShellActionService(
            runtimePaths,
            new FrontendPlatformAdapter(),
            () => { },
            new DictionaryI18nService());

        shellActionService.PersistSharedValue("SystemNetDnsMode", (int)FrontendSecureDnsMode.DnsOverTls);
        shellActionService.PersistSharedValue("SystemNetDnsProvider", (int)FrontendSecureDnsProvider.Cloudflare);

        var configuration = FrontendHttpProxyService.ReadConfiguredSecureDnsConfiguration(runtimePaths);

        Assert.AreEqual(FrontendSecureDnsMode.DnsOverTls, configuration.Mode);
        Assert.AreEqual(FrontendSecureDnsProvider.Cloudflare, configuration.Provider);
    }

    [TestMethod]
    public void ApplyStoredDnsSettings_UsesPersistedModeAndProvider()
    {
        using var environment = new FrontendRuntimePathTestEnvironment();
        var runtimePaths = FrontendRuntimePaths.Resolve(new FrontendPlatformAdapter());
        var shellActionService = new FrontendShellActionService(
            runtimePaths,
            new FrontendPlatformAdapter(),
            () => { },
            new DictionaryI18nService());

        shellActionService.PersistSharedValue("SystemNetDnsMode", (int)FrontendSecureDnsMode.DnsOverTls);
        shellActionService.PersistSharedValue("SystemNetDnsProvider", (int)FrontendSecureDnsProvider.Google);

        FrontendHttpProxyService.ApplyStoredDnsSettings(runtimePaths);

        Assert.AreEqual(FrontendSecureDnsMode.DnsOverTls, FrontendHttpProxyService.CurrentSecureDnsConfiguration.Mode);
        Assert.AreEqual(FrontendSecureDnsProvider.Google, FrontendHttpProxyService.CurrentSecureDnsConfiguration.Provider);
    }

    [TestMethod]
    public async Task ResolveHostAddressesAsync_ReturnsAddressLiteralWithoutLookup()
    {
        var addresses = await FrontendHttpProxyService.ResolveHostAddressesAsync("127.0.0.1");

        CollectionAssert.AreEqual(new[] { IPAddress.Loopback }, addresses);
    }

    private sealed class FrontendRuntimePathTestEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public FrontendRuntimePathTestEnvironment()
        {
            RootDirectory = CreateTempDirectory();
            DataDirectory = Path.Combine(RootDirectory, "data");
            SharedDataDirectory = Path.Combine(RootDirectory, "shared");
            SharedLocalDataDirectory = Path.Combine(RootDirectory, "shared-local");
            TempDirectory = Path.Combine(RootDirectory, "temp");

            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SharedDataDirectory);
            Directory.CreateDirectory(SharedLocalDataDirectory);
            Directory.CreateDirectory(TempDirectory);

            SetEnvironmentVariable("PCL_PATH", DataDirectory);
            SetEnvironmentVariable("PCL_PATH_SHARED", SharedDataDirectory);
            SetEnvironmentVariable("PCL_PATH_LOCAL", SharedLocalDataDirectory);
            SetEnvironmentVariable("PCL_PATH_TEMP", TempDirectory);
            SetEnvironmentVariable("PCL_PORTABLE", "0");
            SetEnvironmentVariable("PCL_ENCRYPTION_KEY", "frontend-http-proxy-test-key");
            SetEnvironmentVariable("HOME", RootDirectory);
            SetEnvironmentVariable("USERPROFILE", RootDirectory);
        }

        public string RootDirectory { get; }
        public string DataDirectory { get; }
        public string SharedDataDirectory { get; }
        public string SharedLocalDataDirectory { get; }
        public string TempDirectory { get; }

        public void Dispose()
        {
            foreach (var pair in _originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            Directory.Delete(RootDirectory, recursive: true);
        }

        private void SetEnvironmentVariable(string key, string value)
        {
            if (!_originalValues.ContainsKey(key))
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
            }

            Environment.SetEnvironmentVariable(key, value);
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "pcl-http-proxy-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
