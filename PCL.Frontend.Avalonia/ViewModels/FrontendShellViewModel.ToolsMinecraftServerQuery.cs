using System.IO;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Core.Link.McPing;
using PCL.Core.Link.McPing.Model;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private static readonly Color MinecraftServerQueryBackgroundColor = Color.Parse("#F3F6FA");
    private static readonly TimeSpan MinecraftServerQueryHappyEyeballsStagger = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MinecraftServerQueryConnectTimeout = TimeSpan.FromSeconds(2.5);
    private static readonly Regex MinecraftServerQueryBracketedIpv6 =
        new(@"^\[(?<ip>.+?)\](?::(?<port>\d{1,5}))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MinecraftServerQueryTrailingPort =
        new(@":(?<port>\d{1,5})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<char, IBrush> MinecraftServerQueryMotdColorMap =
        new Dictionary<char, IBrush>
        {
            ['0'] = Brush.Parse("#333333"),
            ['1'] = Brush.Parse("#003087"),
            ['2'] = Brush.Parse("#008000"),
            ['3'] = Brush.Parse("#007A7A"),
            ['4'] = Brush.Parse("#A10000"),
            ['5'] = Brush.Parse("#800080"),
            ['6'] = Brush.Parse("#CC7000"),
            ['7'] = Brush.Parse("#666666"),
            ['8'] = Brush.Parse("#444444"),
            ['9'] = Brush.Parse("#0044CC"),
            ['a'] = Brush.Parse("#009900"),
            ['b'] = Brush.Parse("#00A1A1"),
            ['c'] = Brush.Parse("#CC0000"),
            ['d'] = Brush.Parse("#C200C2"),
            ['e'] = Brush.Parse("#B3A000"),
            ['f'] = Brush.Parse("#888888")
        };

    private Bitmap? _minecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    private string _minecraftServerQueryAddress = string.Empty;
    private string _minecraftServerQueryTitle = string.Empty;
    private IBrush _minecraftServerQueryTitleBrush = Brushes.White;
    private string _minecraftServerQueryPlayerCount = "-/-";
    private string _minecraftServerQueryLatency = string.Empty;
    private IBrush _minecraftServerQueryLatencyBrush = Brushes.White;
    private string? _minecraftServerQueryPlayerTooltip;
    private IReadOnlyList<MinecraftServerQueryMotdLineViewModel> _minecraftServerQueryMotdLines = [];
    private bool _hasMinecraftServerQueryResult;
    private CancellationTokenSource? _minecraftServerQueryCancellationTokenSource;
    private int _minecraftServerQueryRequestVersion;

    public ActionCommand QueryMinecraftServerCommand => _queryMinecraftServerCommand;

    public string MinecraftServerQueryAddressWatermark => LT("shell.tools.test.server_query.address_watermark");

    public string MinecraftServerQueryQueryButtonText => LT("shell.tools.test.server_query.query");

    public string MinecraftServerQueryAddress
    {
        get => _minecraftServerQueryAddress;
        set => SetProperty(ref _minecraftServerQueryAddress, value);
    }

    public string MinecraftServerQueryTitle
    {
        get => _minecraftServerQueryTitle;
        private set => SetProperty(ref _minecraftServerQueryTitle, value);
    }

    public IBrush MinecraftServerQueryTitleBrush
    {
        get => _minecraftServerQueryTitleBrush;
        private set => SetProperty(ref _minecraftServerQueryTitleBrush, value);
    }

    public string MinecraftServerQueryPlayerCount
    {
        get => _minecraftServerQueryPlayerCount;
        private set => SetProperty(ref _minecraftServerQueryPlayerCount, value);
    }

    public string MinecraftServerQueryLatency
    {
        get => _minecraftServerQueryLatency;
        private set
        {
            if (SetProperty(ref _minecraftServerQueryLatency, value))
            {
                RaisePropertyChanged(nameof(HasMinecraftServerQueryLatency));
            }
        }
    }

    public IBrush MinecraftServerQueryLatencyBrush
    {
        get => _minecraftServerQueryLatencyBrush;
        private set => SetProperty(ref _minecraftServerQueryLatencyBrush, value);
    }

    public string? MinecraftServerQueryPlayerTooltip
    {
        get => _minecraftServerQueryPlayerTooltip;
        private set => SetProperty(ref _minecraftServerQueryPlayerTooltip, value);
    }

    public IReadOnlyList<MinecraftServerQueryMotdLineViewModel> MinecraftServerQueryMotdLines
    {
        get => _minecraftServerQueryMotdLines;
        private set
        {
            if (SetProperty(ref _minecraftServerQueryMotdLines, value))
            {
                RaisePropertyChanged(nameof(HasMinecraftServerQueryMotd));
            }
        }
    }

    public bool HasMinecraftServerQueryResult
    {
        get => _hasMinecraftServerQueryResult;
        private set => SetProperty(ref _hasMinecraftServerQueryResult, value);
    }

    public bool HasMinecraftServerQueryMotd => MinecraftServerQueryMotdLines.Count > 0;

    public bool HasMinecraftServerQueryLatency => !string.IsNullOrWhiteSpace(MinecraftServerQueryLatency);

    public Bitmap? MinecraftServerQueryLogo
    {
        get => _minecraftServerQueryLogo;
        private set => SetProperty(ref _minecraftServerQueryLogo, value);
    }

    public Bitmap? MinecraftServerQueryBackgroundImage => LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png");

    private void InitializeMinecraftServerQuerySurface()
    {
        ResetMinecraftServerQuerySurface();
    }

    private void ResetMinecraftServerQuerySurface()
    {
        _minecraftServerQueryRequestVersion++;
        _minecraftServerQueryCancellationTokenSource?.Cancel();
        _minecraftServerQueryCancellationTokenSource?.Dispose();
        _minecraftServerQueryCancellationTokenSource = null;
        MinecraftServerQueryAddress = string.Empty;
        MinecraftServerQueryTitle = string.Empty;
        MinecraftServerQueryTitleBrush = Brushes.White;
        MinecraftServerQueryPlayerCount = "-/-";
        MinecraftServerQueryLatency = string.Empty;
        MinecraftServerQueryLatencyBrush = Brushes.White;
        MinecraftServerQueryPlayerTooltip = null;
        MinecraftServerQueryMotdLines = [];
        MinecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
        HasMinecraftServerQueryResult = false;
    }

    private void OpenMinecraftServerInspector(string address)
    {
        var resolvedAddress = (address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(resolvedAddress))
        {
            AddActivity(
                LT("shell.tools.test.server_query.open_activity"),
                LT("shell.tools.test.server_query.empty_address"));
            return;
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Tools, LauncherFrontendSubpageKey.ToolsTest),
            LT("shell.tools.test.server_query.navigation", ("address", resolvedAddress)));
        Dispatcher.UIThread.Post(() =>
        {
            MinecraftServerQueryAddress = resolvedAddress;
            _ = QueryMinecraftServerAsync();
        });
    }

    private async Task QueryMinecraftServerAsync()
    {
        var address = (MinecraftServerQueryAddress ?? string.Empty).Trim().Replace("：", ":");
        MinecraftServerQueryAddress = address;

        var requestVersion = Interlocked.Increment(ref _minecraftServerQueryRequestVersion);
        _minecraftServerQueryCancellationTokenSource?.Cancel();
        _minecraftServerQueryCancellationTokenSource?.Dispose();

        using var cancellationTokenSource = new CancellationTokenSource();
        _minecraftServerQueryCancellationTokenSource = cancellationTokenSource;
        ApplyMinecraftServerQueryLoadingState();

        try
        {
            var reachableAddress = await ResolveMinecraftServerQueryEndpointAsync(address, cancellationTokenSource.Token);
            using var queryService = McPingServiceFactory.CreateService(reachableAddress.Ip, reachableAddress.Port);
            var result = await queryService.PingAsync(cancellationTokenSource.Token);
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (result is null)
            {
                throw new InvalidOperationException(LT("shell.tools.test.server_query.no_result"));
            }

            if (requestVersion != _minecraftServerQueryRequestVersion)
            {
                return;
            }

            ApplyMinecraftServerQuerySuccessState(result);
            AddActivity(
                LT("shell.tools.test.server_query.query_activity"),
                $"{address} • {result.Players.Online}/{result.Players.Max} • {result.Latency}ms");
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            // Ignore stale or refreshed requests.
        }
        catch (Exception ex)
        {
            if (requestVersion != _minecraftServerQueryRequestVersion)
            {
                return;
            }

            ApplyMinecraftServerQueryErrorState(ex.Message);
            AddFailureActivity(LT("shell.tools.test.server_query.query_failure"), ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_minecraftServerQueryCancellationTokenSource, cancellationTokenSource))
            {
                _minecraftServerQueryCancellationTokenSource = null;
            }
        }
    }

    private void ApplyMinecraftServerQueryLoadingState()
    {
        HasMinecraftServerQueryResult = true;
        MinecraftServerQueryTitle = LT("shell.tools.test.server_query.loading");
        MinecraftServerQueryTitleBrush = Brushes.White;
        MinecraftServerQueryPlayerCount = "-/-";
        MinecraftServerQueryLatency = string.Empty;
        MinecraftServerQueryLatencyBrush = Brushes.White;
        MinecraftServerQueryPlayerTooltip = null;
        MinecraftServerQueryMotdLines = [];
        MinecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyMinecraftServerQuerySuccessState(McPingResult result)
    {
        HasMinecraftServerQueryResult = true;
        MinecraftServerQueryTitle = LT("shell.tools.test.server_query.result_title");
        MinecraftServerQueryTitleBrush = Brushes.White;
        MinecraftServerQueryPlayerCount = $"{result.Players.Online}/{result.Players.Max}";
        MinecraftServerQueryLatency = $"{result.Latency}ms";
        MinecraftServerQueryLatencyBrush = GetMinecraftServerQueryLatencyBrush(result.Latency);
        MinecraftServerQueryPlayerTooltip = result.Players.Samples?.Any() == true
            ? string.Join(Environment.NewLine, result.Players.Samples.Select(sample => sample.Name))
            : null;
        MinecraftServerQueryMotdLines = BuildMinecraftServerQueryMotdLines(result.Description);
        MinecraftServerQueryLogo = DecodeMinecraftServerQueryLogo(result.Favicon)
            ?? LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyMinecraftServerQueryErrorState(string message)
    {
        HasMinecraftServerQueryResult = true;
        MinecraftServerQueryTitle = LT("shell.tools.test.server_query.error", ("message", message));
        MinecraftServerQueryTitleBrush = Brushes.Red;
        MinecraftServerQueryPlayerCount = "-/-";
        MinecraftServerQueryLatency = string.Empty;
        MinecraftServerQueryLatencyBrush = Brushes.White;
        MinecraftServerQueryPlayerTooltip = null;
        MinecraftServerQueryMotdLines = [];
        MinecraftServerQueryLogo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private static IBrush GetMinecraftServerQueryLatencyBrush(long latency)
    {
        var code = latency < 150 ? 'a' : latency < 400 ? '6' : 'c';
        return MinecraftServerQueryMotdColorMap[code];
    }

    private static Bitmap? DecodeMinecraftServerQueryLogo(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        try
        {
            var payload = rawValue;
            var commaIndex = payload.IndexOf(',');
            if (commaIndex >= 0)
            {
                payload = payload[(commaIndex + 1)..];
            }

            var bytes = Convert.FromBase64String(payload);
            using var stream = new MemoryStream(bytes, writable: false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<MinecraftServerQueryMotdLineViewModel> BuildMinecraftServerQueryMotdLines(string? motd)
    {
        if (string.IsNullOrWhiteSpace(motd))
        {
            return [];
        }

        var currentBrush = MinecraftServerQueryMotdColorMap['f'];
        var isBold = false;
        var isItalic = false;
        var lines = new List<MinecraftServerQueryMotdLineViewModel>();

        foreach (var rawLine in RegexPatterns.NewLine.Split(motd).Take(2))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var segments = new List<MinecraftServerQueryMotdSegmentViewModel>();
            foreach (var part in RegexPatterns.MotdCode.Split(line))
            {
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (part.Length == 2 && part[0] == '§')
                {
                    var code = char.ToLowerInvariant(part[1]);
                    if (MinecraftServerQueryMotdColorMap.TryGetValue(code, out var brush))
                    {
                        currentBrush = brush;
                        isBold = false;
                        isItalic = false;
                    }
                    else
                    {
                        switch (code)
                        {
                            case 'l':
                                isBold = true;
                                break;
                            case 'o':
                                isItalic = true;
                                break;
                            case 'r':
                                currentBrush = MinecraftServerQueryMotdColorMap['f'];
                                isBold = false;
                                isItalic = false;
                                break;
                        }
                    }

                    continue;
                }

                if (RegexPatterns.HexColor.IsMatch(part))
                {
                    currentBrush = new SolidColorBrush(AdjustMinecraftServerQueryHexColor(Color.Parse(part)));
                    isBold = false;
                    isItalic = false;
                    continue;
                }

                segments.Add(new MinecraftServerQueryMotdSegmentViewModel(
                    part,
                    currentBrush,
                    isBold ? FontWeight.Bold : FontWeight.Normal,
                    isItalic ? FontStyle.Italic : FontStyle.Normal));
            }

            if (segments.Count > 0)
            {
                lines.Add(new MinecraftServerQueryMotdLineViewModel(segments));
            }
        }

        return lines;
    }

    private static Color AdjustMinecraftServerQueryHexColor(Color inputColor)
    {
        if (GetMinecraftServerQueryContrastRatio(inputColor, MinecraftServerQueryBackgroundColor) >= 4.5)
        {
            return inputColor;
        }

        var r = inputColor.R / 255.0;
        var g = inputColor.G / 255.0;
        var b = inputColor.B / 255.0;
        var max = Math.Max(Math.Max(r, g), b);
        var min = Math.Min(Math.Min(r, g), b);
        var lightness = (max + min) / 2.0;
        double saturation;
        double hue;

        if (Math.Abs(max - min) < double.Epsilon)
        {
            hue = 0.0;
            saturation = 0.0;
        }
        else
        {
            var delta = max - min;
            saturation = lightness > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);
            hue = max switch
            {
                _ when Math.Abs(max - r) < double.Epsilon => (g - b) / delta + (g < b ? 6.0 : 0.0),
                _ when Math.Abs(max - g) < double.Epsilon => (b - r) / delta + 2.0,
                _ => (r - g) / delta + 4.0
            };
            hue /= 6.0;
        }

        var nextLightness = lightness;
        var adjustedColor = inputColor;
        while (nextLightness > 0.1 && GetMinecraftServerQueryContrastRatio(adjustedColor, MinecraftServerQueryBackgroundColor) < 4.5)
        {
            nextLightness -= 0.05;
            double newR;
            double newG;
            double newB;

            if (Math.Abs(saturation) < double.Epsilon)
            {
                newR = nextLightness;
                newG = nextLightness;
                newB = nextLightness;
            }
            else
            {
                var q = nextLightness < 0.5
                    ? nextLightness * (1.0 + saturation)
                    : nextLightness + saturation - nextLightness * saturation;
                var p = 2.0 * nextLightness - q;
                newR = HueToRgb(p, q, hue + 1.0 / 3.0);
                newG = HueToRgb(p, q, hue);
                newB = HueToRgb(p, q, hue - 1.0 / 3.0);
            }

            adjustedColor = Color.FromRgb(
                (byte)(newR * 255),
                (byte)(newG * 255),
                (byte)(newB * 255));
        }

        return GetMinecraftServerQueryContrastRatio(adjustedColor, MinecraftServerQueryBackgroundColor) < 4.5
            ? Color.FromRgb(85, 85, 85)
            : adjustedColor;
    }

    private static double GetMinecraftServerQueryContrastRatio(Color foreground, Color background)
    {
        var first = GetMinecraftServerQueryRelativeLuminance(foreground);
        var second = GetMinecraftServerQueryRelativeLuminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static double GetMinecraftServerQueryRelativeLuminance(Color color)
    {
        var red = GetMinecraftServerQueryLinearChannel(color.R / 255.0);
        var green = GetMinecraftServerQueryLinearChannel(color.G / 255.0);
        var blue = GetMinecraftServerQueryLinearChannel(color.B / 255.0);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static double GetMinecraftServerQueryLinearChannel(double value)
    {
        return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1.0;
        }

        if (t > 1)
        {
            t -= 1.0;
        }

        if (t < 1.0 / 6.0)
        {
            return p + (q - p) * 6.0 * t;
        }

        if (t < 0.5)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        }

        return p;
    }

    private async Task<(string Ip, int Port)> ResolveMinecraftServerQueryEndpointAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException(LT("shell.tools.test.server_query.validation.empty"), nameof(address));
        }

        var normalizedAddress = NormalizeMinecraftServerQueryAddress(address);
        var (hostOrIp, explicitPort) = ParseMinecraftServerQueryHostAndPort(normalizedAddress);
        if (explicitPort is { } directPort)
        {
            ValidateMinecraftServerQueryPort(directPort);
            var reachable = await ResolveMinecraftServerQueryReachableAsync(hostOrIp, directPort, cancellationToken).ConfigureAwait(false);
            if (reachable is not null)
            {
                return reachable.Value;
            }

            var fallbackIp = await ResolveMinecraftServerQueryFirstIpAsync(hostOrIp, cancellationToken).ConfigureAwait(false);
            return (fallbackIp ?? hostOrIp, directPort);
        }

        if (IPAddress.TryParse(hostOrIp, out _))
        {
            var reachable = await ResolveMinecraftServerQueryReachableAsync(hostOrIp, 25565, cancellationToken).ConfigureAwait(false);
            return reachable ?? (hostOrIp, 25565);
        }

        var idnHost = ToMinecraftServerQueryAsciiIdn(hostOrIp);
        var srvOrdered = await QueryMinecraftServerSrvOrderedAsync(idnHost, cancellationToken).ConfigureAwait(false);
        if (srvOrdered.Count > 0)
        {
            foreach (var srv in srvOrdered)
            {
                var targetHost = TrimMinecraftServerQueryTrailingDot(srv.Target);
                var reachable = await ResolveMinecraftServerQueryReachableAsync(targetHost, srv.Port, cancellationToken).ConfigureAwait(false);
                if (reachable is not null)
                {
                    return reachable.Value;
                }
            }

            var first = srvOrdered[0];
            var firstIp = await ResolveMinecraftServerQueryFirstIpAsync(
                TrimMinecraftServerQueryTrailingDot(first.Target),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return (firstIp, first.Port);
            }
        }

        var ip = await ResolveMinecraftServerQueryFirstIpAsync(idnHost, cancellationToken).ConfigureAwait(false);
        return (ip ?? idnHost, 25565);
    }

    private static string NormalizeMinecraftServerQueryAddress(string input)
    {
        var value = input.Trim();
        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex > 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        while (value.EndsWith("/", StringComparison.Ordinal))
        {
            value = value[..^1];
        }

        return value;
    }

    private void ValidateMinecraftServerQueryPort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new FormatException(LT("shell.tools.test.server_query.validation.invalid_port", ("port", port)));
        }
    }

    private static string ToMinecraftServerQueryAsciiIdn(string host)
    {
        try
        {
            var idn = new IdnMapping();
            var trimmedHost = TrimMinecraftServerQueryTrailingDot(host);
            return idn.GetAscii(trimmedHost) + (host.EndsWith(".", StringComparison.Ordinal) ? "." : string.Empty);
        }
        catch
        {
            return host;
        }
    }

    private static string TrimMinecraftServerQueryTrailingDot(string host)
    {
        return host.EndsWith(".", StringComparison.Ordinal) ? host[..^1] : host;
    }

    private (string HostOrIp, int? Port) ParseMinecraftServerQueryHostAndPort(string input)
    {
        var bracketedIpv6Match = MinecraftServerQueryBracketedIpv6.Match(input);
        if (bracketedIpv6Match.Success)
        {
            var ip = bracketedIpv6Match.Groups["ip"].Value;
            if (!IPAddress.TryParse(ip, out _))
            {
                throw new FormatException(LT("shell.tools.test.server_query.validation.invalid_ipv6"));
            }

            var portGroup = bracketedIpv6Match.Groups["port"];
            if (portGroup.Success)
            {
                var port = int.Parse(portGroup.Value, CultureInfo.InvariantCulture);
                ValidateMinecraftServerQueryPort(port);
                return (ip, port);
            }

            return (ip, null);
        }

        if (IPAddress.TryParse(input, out _))
        {
            return (input, null);
        }

        var trailingPortMatch = MinecraftServerQueryTrailingPort.Match(input);
        if (trailingPortMatch.Success)
        {
            var colonCount = input.Count(character => character == ':');
            if (colonCount == 1)
            {
                var port = int.Parse(trailingPortMatch.Groups["port"].Value, CultureInfo.InvariantCulture);
                ValidateMinecraftServerQueryPort(port);
                var host = input[..^trailingPortMatch.Value.Length];
                if (string.IsNullOrWhiteSpace(host))
                {
                    throw new FormatException(LT("shell.tools.test.server_query.validation.invalid_host"));
                }

                return (host, port);
            }
        }

        return (input, null);
    }

    private static async Task<List<FrontendDnsSrvRecord>> QueryMinecraftServerSrvOrderedAsync(string domain, CancellationToken cancellationToken)
    {
        var records = await FrontendHttpProxyService.QuerySrvRecordsAsync(
            TrimMinecraftServerQueryTrailingDot(domain),
            cancellationToken).ConfigureAwait(false);
        return records.ToList();
    }

    private static async Task<(string Ip, int Port)?> ResolveMinecraftServerQueryReachableAsync(
        string hostOrIp,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            if (IPAddress.TryParse(hostOrIp, out var literalIp))
            {
                var result = await ConnectMinecraftServerQueryEndpointAsync(literalIp, port, cancellationToken).ConfigureAwait(false);
                return result.ok ? (literalIp.ToString(), port) : null;
            }

            var addresses = await FrontendHttpProxyService.ResolveHostAddressesAsync(
                TrimMinecraftServerQueryTrailingDot(hostOrIp),
                cancellationToken).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                return null;
            }

            var ipv6Addresses = addresses.Where(address => address.AddressFamily == AddressFamily.InterNetworkV6).ToArray();
            var ipv4Addresses = addresses.Where(address => address.AddressFamily == AddressFamily.InterNetwork).ToArray();

            var winner = await ConnectMinecraftServerQueryAnyAsync(ipv6Addresses, port, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
            if (winner is not null)
            {
                return (winner, port);
            }

            winner = await ConnectMinecraftServerQueryAnyAsync(
                ipv4Addresses,
                port,
                MinecraftServerQueryHappyEyeballsStagger,
                cancellationToken).ConfigureAwait(false);
            return winner is null ? null : (winner, port);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<string?> ResolveMinecraftServerQueryFirstIpAsync(string hostOrIp, CancellationToken cancellationToken)
    {
        try
        {
            if (IPAddress.TryParse(hostOrIp, out var ipAddress))
            {
                return ipAddress.ToString();
            }

            var addresses = await FrontendHttpProxyService.ResolveHostAddressesAsync(
                TrimMinecraftServerQueryTrailingDot(hostOrIp),
                cancellationToken).ConfigureAwait(false);
            var chosen = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetworkV6)
                         ?? addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            return chosen?.ToString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<string?> ConnectMinecraftServerQueryAnyAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (addresses.Count == 0)
        {
            return null;
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = addresses
            .Select(address => ConnectMinecraftServerQueryEndpointAsync(address, port, linkedCancellationTokenSource.Token))
            .ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);
            tasks.Remove(completedTask);
            var (ok, ip) = await completedTask.ConfigureAwait(false);
            if (!ok)
            {
                continue;
            }

            try
            {
                await linkedCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore linked cancellation disposal races.
            }

            return ip;
        }

        return null;
    }

    private static async Task<(bool ok, string ip)> ConnectMinecraftServerQueryEndpointAsync(
        IPAddress ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            using var timeoutCancellationTokenSource = new CancellationTokenSource(MinecraftServerQueryConnectTimeout);
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource.Token);
            await socket.ConnectAsync(new IPEndPoint(ipAddress, port), linkedCancellationTokenSource.Token).ConfigureAwait(false);
            return (true, ipAddress.ToString());
        }
        catch
        {
            return (false, ipAddress.ToString());
        }
    }
}

internal sealed class MinecraftServerQueryMotdLineViewModel(
    IReadOnlyList<MinecraftServerQueryMotdSegmentViewModel> segments)
{
    public IReadOnlyList<MinecraftServerQueryMotdSegmentViewModel> Segments { get; } = segments;
}

internal sealed class MinecraftServerQueryMotdSegmentViewModel(
    string text,
    IBrush foregroundBrush,
    FontWeight fontWeight,
    FontStyle fontStyle)
{
    public string Text { get; } = text;

    public IBrush ForegroundBrush { get; } = foregroundBrush;

    public FontWeight FontWeight { get; } = fontWeight;

    public FontStyle FontStyle { get; } = fontStyle;
}
