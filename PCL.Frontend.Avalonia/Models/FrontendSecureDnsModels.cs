namespace PCL.Frontend.Avalonia.Models;

internal enum FrontendSecureDnsMode
{
    System = 0,
    DnsOverHttps = 1,
    DnsOverTls = 2
}

internal enum FrontendSecureDnsProvider
{
    Auto = 0,
    DnsPod = 1,
    Cloudflare = 2,
    Google = 3
}

internal readonly record struct FrontendSecureDnsConfiguration(
    FrontendSecureDnsMode Mode,
    FrontendSecureDnsProvider Provider)
{
    public static FrontendSecureDnsConfiguration Default => new(
        FrontendSecureDnsMode.System,
        FrontendSecureDnsProvider.Auto);

    public bool IsSecureTransportEnabled => Mode != FrontendSecureDnsMode.System;
}
