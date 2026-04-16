using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Enums;
using Ae.Dns.Protocol.Records;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.IO.Net.Http.Proxying;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendHttpProxyService
{
    private const string ProxyTypeKey = "SystemHttpProxyType";
    private const string ProxyAddressKey = "SystemHttpProxy";
    private const string ProxyUsernameKey = "SystemHttpProxyCustomUsername";
    private const string ProxyPasswordKey = "SystemHttpProxyCustomPassword";
    private const string DnsOverHttpsKey = "SystemNetEnableDoH";

    private static readonly ProxyManager LauncherProxyManager = new();
    private static readonly Uri[] DnsOverHttpsEndpoints =
    [
        new("https://doh.pub/"),
        new("https://doh.pysio.online/"),
        new("https://cloudflare-dns.com/")
    ];
    private static readonly TimeSpan DnsOverHttpsTimeout = TimeSpan.FromSeconds(5);
    private static volatile bool _isDnsOverHttpsEnabled = true;
    public static Uri ProxyConnectivityProbeUri { get; } = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
    public static bool IsDnsOverHttpsEnabled => _isDnsOverHttpsEnabled;

    static FrontendHttpProxyService()
    {
        HttpClient.DefaultProxy = LauncherProxyManager;
    }

    public static void ApplyStoredProxySettings(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        ApplyConfiguration(ResolveConfiguration(runtimePaths, sharedConfig));
    }

    public static void ApplyStoredDnsSettings(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        ApplyDnsOverHttpsSetting(ReadConfiguredDnsOverHttpsEnabled(runtimePaths));
    }

    public static string ReadConfiguredProxyAddress(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        return ReadProtectedValue(runtimePaths, ProxyAddressKey) ?? string.Empty;
    }

    public static string ReadConfiguredProxyUsername(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return ReadSecretValue(runtimePaths, sharedConfig, ProxyUsernameKey, allowPlainTextFallback: true) ?? string.Empty;
    }

    public static string ReadConfiguredProxyPassword(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return ReadSecretValue(runtimePaths, sharedConfig, ProxyPasswordKey, allowPlainTextFallback: true) ?? string.Empty;
    }

    public static bool ReadConfiguredDnsOverHttpsEnabled(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return sharedConfig.Exists(DnsOverHttpsKey)
            ? sharedConfig.Get<bool>(DnsOverHttpsKey)
            : true;
    }

    public static void ApplyDnsOverHttpsSetting(bool isEnabled)
    {
        _isDnsOverHttpsEnabled = isEnabled;
    }

    internal static FrontendResolvedProxyConfiguration ResolveConfiguration(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        return ResolveConfiguration(runtimePaths, sharedConfig);
    }

    internal static FrontendResolvedProxyConfiguration ResolveConfiguration(
        FrontendRuntimePaths runtimePaths,
        JsonFileProvider sharedConfig)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(sharedConfig);

        var selectedMode = ReadProxyMode(sharedConfig);
        var addressRaw = ReadProtectedValue(runtimePaths, ProxyAddressKey);
        var address = TryParseProxyUri(addressRaw);
        var username = ReadSecretValue(runtimePaths, sharedConfig, ProxyUsernameKey, allowPlainTextFallback: true);
        var password = ReadSecretValue(runtimePaths, sharedConfig, ProxyPasswordKey, allowPlainTextFallback: true);
        (username, password) = MergeUriCredentials(address, username, password);

        var effectiveMode = selectedMode == ProxyMode.CustomProxy && address is null
            ? ProxyMode.NoProxy
            : selectedMode;

        return new FrontendResolvedProxyConfiguration(
            effectiveMode,
            address,
            CreateCredential(username, password));
    }

    internal static FrontendResolvedProxyConfiguration BuildConfiguration(
        int proxyTypeIndex,
        string? proxyAddress,
        string? proxyUsername,
        string? proxyPassword)
    {
        var selectedMode = Math.Clamp(proxyTypeIndex, 0, 2) switch
        {
            0 => ProxyMode.NoProxy,
            2 => ProxyMode.CustomProxy,
            _ => ProxyMode.SystemProxy
        };
        var address = TryParseProxyUri(proxyAddress);
        (proxyUsername, proxyPassword) = MergeUriCredentials(address, proxyUsername, proxyPassword);
        var effectiveMode = selectedMode == ProxyMode.CustomProxy && address is null
            ? ProxyMode.NoProxy
            : selectedMode;

        return new FrontendResolvedProxyConfiguration(
            effectiveMode,
            address,
            CreateCredential(proxyUsername, proxyPassword));
    }

    public static HttpClient CreateHttpClient(
        FrontendResolvedProxyConfiguration configuration,
        TimeSpan timeout,
        string? userAgent = null)
    {
        return CreateHttpClient(
            configuration,
            timeout,
            userAgent,
            DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
    }

    public static HttpClient CreateHttpClient(
        FrontendResolvedProxyConfiguration configuration,
        TimeSpan timeout,
        string? userAgent,
        DecompressionMethods automaticDecompression,
        bool useDnsOverHttps = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = automaticDecompression
        };
        if (useDnsOverHttps)
        {
            handler.ConnectCallback = ConnectAsync;
        }

        switch (configuration.Mode)
        {
            case ProxyMode.NoProxy:
                handler.UseProxy = false;
                break;
            case ProxyMode.CustomProxy:
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(configuration.CustomProxyAddress!)
                {
                    BypassProxyOnLocal = true,
                    Credentials = configuration.CustomProxyCredentials
                };
                break;
            default:
                handler.UseProxy = true;
                handler.Proxy = WebRequest.DefaultWebProxy;
                break;
        }

        var client = new HttpClient(handler)
        {
            Timeout = timeout
        };
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent.Trim());
        }

        return client;
    }

    public static HttpClient CreateLauncherHttpClient(
        TimeSpan timeout,
        string? userAgent = null,
        DecompressionMethods automaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        bool useDnsOverHttps = true)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = automaticDecompression,
            UseProxy = true,
            Proxy = LauncherProxyManager
        };
        if (useDnsOverHttps)
        {
            handler.ConnectCallback = ConnectAsync;
        }

        var client = new HttpClient(handler)
        {
            Timeout = timeout
        };
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent.Trim());
        }

        return client;
    }

    public static async Task<IPAddress[]> ResolveHostAddressesAsync(string hostOrIp, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostOrIp);

        var normalizedHost = hostOrIp.Trim().TrimEnd('.');
        if (IPAddress.TryParse(normalizedHost, out var addressLiteral))
        {
            return [addressLiteral];
        }

        if (_isDnsOverHttpsEnabled)
        {
            var addresses = await QueryHostAddressesOverHttpsAsync(normalizedHost, cancellationToken).ConfigureAwait(false);
            if (addresses.Length > 0)
            {
                return addresses;
            }
        }

        return await Dns.GetHostAddressesAsync(normalizedHost, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<FrontendDnsSrvRecord>> QuerySrvRecordsOverHttpsAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        var name = $"_minecraft._tcp.{domain.Trim().TrimEnd('.')}";
        foreach (var endpoint in DnsOverHttpsEndpoints)
        {
            try
            {
                using var httpClient = CreateLauncherHttpClient(
                    DnsOverHttpsTimeout,
                    automaticDecompression: DecompressionMethods.None,
                    useDnsOverHttps: false);
                httpClient.BaseAddress = endpoint;
                using var dnsClient = new DnsHttpClient(httpClient);
                var response = await dnsClient.Query(
                    DnsQueryFactory.CreateQuery(name, DnsQueryType.SRV),
                    cancellationToken).ConfigureAwait(false);
                if (response.Answers.Count == 0)
                {
                    continue;
                }

                var parsed = new List<FrontendDnsSrvRecord>();
                foreach (var answer in response.Answers)
                {
                    if (answer.Resource is not DnsUnknownResource rawResource)
                    {
                        continue;
                    }

                    var srvRecord = new FrontendDnsSrvResource();
                    var offset = 0;
                    var rawBytes = rawResource.Raw.ToArray();
                    srvRecord.ReadBytes(rawBytes, ref offset, rawBytes.Length);
                    if (srvRecord.Target == ".")
                    {
                        continue;
                    }

                    parsed.Add(new FrontendDnsSrvRecord(
                        srvRecord.Priority,
                        srvRecord.Weight,
                        srvRecord.Port,
                        srvRecord.Target));
                }

                if (parsed.Count == 0)
                {
                    continue;
                }

                var ordered = new List<FrontendDnsSrvRecord>(parsed.Count);
                foreach (var group in parsed.GroupBy(record => record.Priority).OrderBy(group => group.Key))
                {
                    var pool = group.ToList();
                    while (pool.Count > 0)
                    {
                        ordered.Add(PopSrvRecordByWeight(pool));
                    }
                }

                return ordered;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Try the next DoH endpoint.
            }
        }

        return [];
    }

    internal static Uri? TryParseProxyUri(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var candidate = rawValue.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "http://" + candidate;
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
               !string.IsNullOrWhiteSpace(uri.Host)
            ? uri
            : null;
    }

    private static void ApplyConfiguration(FrontendResolvedProxyConfiguration configuration)
    {
        LauncherProxyManager.RefreshSystemProxy();
        LauncherProxyManager.CustomProxyAddress = configuration.CustomProxyAddress;
        LauncherProxyManager.CustomProxyCredentials = configuration.CustomProxyCredentials;
        LauncherProxyManager.Mode = configuration.Mode;
        HttpClient.DefaultProxy = LauncherProxyManager;
    }

    private static ProxyMode ReadProxyMode(JsonFileProvider sharedConfig)
    {
        var value = sharedConfig.Exists(ProxyTypeKey)
            ? sharedConfig.Get<int>(ProxyTypeKey)
            : 1;

        return Math.Clamp(value, 0, 2) switch
        {
            0 => ProxyMode.NoProxy,
            2 => ProxyMode.CustomProxy,
            _ => ProxyMode.SystemProxy
        };
    }

    private static string? ReadProtectedValue(FrontendRuntimePaths runtimePaths, string key)
    {
        return LauncherFrontendRuntimeStateService.TryReadProtectedString(
            runtimePaths.SharedConfigDirectory,
            runtimePaths.SharedConfigPath,
            key);
    }

    private static string? ReadSecretValue(
        FrontendRuntimePaths runtimePaths,
        JsonFileProvider sharedConfig,
        string key,
        bool allowPlainTextFallback)
    {
        if (!sharedConfig.Exists(key))
        {
            return null;
        }

        string rawValue;
        try
        {
            rawValue = sharedConfig.Get<string>(key);
        }
        catch
        {
            return null;
        }

        var decrypted = LauncherFrontendRuntimeStateService.TryUnprotectString(
            runtimePaths.SharedConfigDirectory,
            rawValue);
        if (decrypted is not null)
        {
            return decrypted;
        }

        return allowPlainTextFallback
            ? rawValue
            : null;
    }

    private static NetworkCredential? CreateCredential(string? username, string? password)
    {
        return string.IsNullOrWhiteSpace(username)
            ? null
            : new NetworkCredential(username.Trim(), password ?? string.Empty);
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint
                       ?? throw new InvalidOperationException("The launcher HTTP client is missing a target DnsEndPoint.");
        var addresses = await ResolveHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        Exception? lastError = null;
        foreach (var address in OrderAddresses(addresses))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(address, endpoint.Port, cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                lastError = ex;
                socket.Dispose();
            }
        }

        throw lastError ?? new SocketException((int)SocketError.NotConnected);
    }

    private static async Task<IPAddress[]> QueryHostAddressesOverHttpsAsync(string host, CancellationToken cancellationToken)
    {
        foreach (var endpoint in DnsOverHttpsEndpoints)
        {
            try
            {
                using var httpClient = CreateLauncherHttpClient(
                    DnsOverHttpsTimeout,
                    automaticDecompression: DecompressionMethods.None,
                    useDnsOverHttps: false);
                httpClient.BaseAddress = endpoint;
                using var dnsClient = new DnsHttpClient(httpClient);
                var addresses = new List<IPAddress>();
                await AppendQueryAddressesAsync(dnsClient, host, DnsQueryType.AAAA, addresses, cancellationToken).ConfigureAwait(false);
                await AppendQueryAddressesAsync(dnsClient, host, DnsQueryType.A, addresses, cancellationToken).ConfigureAwait(false);
                if (addresses.Count > 0)
                {
                    return addresses
                        .Distinct()
                        .ToArray();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Try the next DoH endpoint.
            }
        }

        return [];
    }

    private static async Task AppendQueryAddressesAsync(
        DnsHttpClient dnsClient,
        string host,
        DnsQueryType queryType,
        ICollection<IPAddress> addresses,
        CancellationToken cancellationToken)
    {
        var response = await dnsClient.Query(
            DnsQueryFactory.CreateQuery(host, queryType),
            cancellationToken).ConfigureAwait(false);
        foreach (var answer in response.Answers)
        {
            if (answer.Resource is not DnsUnknownResource rawResource)
            {
                continue;
            }

            var address = TryParseAddress(queryType, rawResource.Raw.ToArray());
            if (address is not null)
            {
                addresses.Add(address);
            }
        }
    }

    private static IPAddress? TryParseAddress(DnsQueryType queryType, byte[] bytes)
    {
        return queryType switch
        {
            DnsQueryType.A when bytes.Length == 4 => new IPAddress(bytes),
            DnsQueryType.AAAA when bytes.Length == 16 => new IPAddress(bytes),
            _ => null
        };
    }

    private static IEnumerable<IPAddress> OrderAddresses(IEnumerable<IPAddress> addresses)
    {
        return addresses
            .OrderByDescending(address => address.AddressFamily == AddressFamily.InterNetworkV6)
            .ThenBy(address => address.ToString(), StringComparer.Ordinal);
    }

    private static FrontendDnsSrvRecord PopSrvRecordByWeight(List<FrontendDnsSrvRecord> pool)
    {
        var totalWeight = pool.Sum(record => record.Weight);
        if (totalWeight <= 0)
        {
            var index = Random.Shared.Next(pool.Count);
            var chosen = pool[index];
            pool.RemoveAt(index);
            return chosen;
        }

        var threshold = Random.Shared.Next(1, totalWeight + 1);
        var cumulative = 0;
        for (var index = 0; index < pool.Count; index++)
        {
            cumulative += pool[index].Weight;
            if (cumulative < threshold)
            {
                continue;
            }

            var chosen = pool[index];
            pool.RemoveAt(index);
            return chosen;
        }

        var last = pool[^1];
        pool.RemoveAt(pool.Count - 1);
        return last;
    }

    private static (string? Username, string? Password) MergeUriCredentials(
        Uri? address,
        string? configuredUsername,
        string? configuredPassword)
    {
        if (address is null ||
            string.IsNullOrWhiteSpace(address.UserInfo) ||
            (!string.IsNullOrWhiteSpace(configuredUsername) || !string.IsNullOrEmpty(configuredPassword)))
        {
            return (configuredUsername, configuredPassword);
        }

        var parts = address.UserInfo.Split(':', 2);
        var username = parts.Length > 0 ? Uri.UnescapeDataString(parts[0]) : configuredUsername;
        var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : configuredPassword;
        return (username, password);
    }
}

internal sealed record FrontendResolvedProxyConfiguration(
    ProxyMode Mode,
    Uri? CustomProxyAddress,
    NetworkCredential? CustomProxyCredentials);

internal sealed record FrontendDnsSrvRecord(
    int Priority,
    int Weight,
    int Port,
    string Target);

internal sealed class FrontendDnsSrvResource
{
    public ushort Priority { get; private set; }
    public ushort Weight { get; private set; }
    public ushort Port { get; private set; }
    public string Target { get; private set; } = string.Empty;

    public void ReadBytes(byte[] raw, ref int offset, int length)
    {
        if (offset + 6 > length)
        {
            throw new FormatException("SRV record length is insufficient.");
        }

        Priority = (ushort)((raw[offset] << 8) | raw[offset + 1]);
        offset += 2;
        Weight = (ushort)((raw[offset] << 8) | raw[offset + 1]);
        offset += 2;
        Port = (ushort)((raw[offset] << 8) | raw[offset + 1]);
        offset += 2;
        Target = ReadDomainName(raw, ref offset, length);
    }

    private static string ReadDomainName(byte[] raw, ref int offset, int length)
    {
        var labels = new List<string>();
        while (offset < length)
        {
            var labelLength = raw[offset++];
            if (labelLength == 0)
            {
                break;
            }

            if (offset + labelLength > length)
            {
                throw new FormatException("SRV domain label is out of range.");
            }

            labels.Add(System.Text.Encoding.ASCII.GetString(raw, offset, labelLength));
            offset += labelLength;
        }

        return string.Join('.', labels);
    }
}
