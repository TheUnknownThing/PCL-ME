using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.E2E;

/// <summary>Real HTTP transfers with controlled payloads and deterministic cancellation points.</summary>
internal sealed class ExtensionTestRepository : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<string, (byte[] Data, Task? Gate)> _files = new();
    private readonly ConcurrentBag<Task> _requests = [];
    private readonly Task _accept;
    public string Url { get; }
    public ConcurrentBag<string> Requests { get; } = [];
    public ExtensionTestRepository()
    {
        _listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accept = AcceptAsync();
    }
    public string Add(string path, byte[] data, Task? gate = null) { _files[path] = (data, gate); return Url + path; }
    public string AddJson(string path, object value) => Add(path, JsonSerializer.SerializeToUtf8Bytes(value));
    private async Task AcceptAsync()
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_lifetime.Token);
                _requests.Add(ServeAsync(client));
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var line = await reader.ReadLineAsync(_lifetime.Token);
                var path = line!.Split(' ')[1];
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(_lifetime.Token))) { }
                Requests.Add(path);
                var found = _files.TryGetValue(path, out var file);
                if (file.Gate is { } gate) await gate.WaitAsync(_lifetime.Token);
                var bytes = found ? file.Data : [];
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, _lifetime.Token);
                await stream.WriteAsync(bytes, _lifetime.Token);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel(); _listener.Stop(); await _accept; await Task.WhenAll(_requests); _lifetime.Dispose();
    }
}

internal sealed class ExtensionTestProvider : IExtensionResourceProvider
{
    public Dictionary<string, ExtensionResourceRelease[]> Releases { get; } = new(StringComparer.Ordinal);
    public ConcurrentBag<ExtensionResourceQuery> Queries { get; } = [];
    public Func<ExtensionResourceQuery, CancellationToken, ValueTask<ExtensionResourcePage>>? Search { get; set; }
    public Func<string, CancellationToken, ValueTask<IReadOnlyList<ExtensionResourceRelease>>>? FetchReleases { get; set; }
    public ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken token)
    {
        Queries.Add(query);
        if (Search is not null) return Search(query, token);
        var matches = Releases.Where(pair => pair.Key.Contains(query.Text, StringComparison.OrdinalIgnoreCase) && pair.Value.Any(release => release.Kind == query.Kind)).ToArray();
        return ValueTask.FromResult(new ExtensionResourcePage
        {
            Projects = matches.Skip(query.Offset).Take(query.Limit).Select(pair => new ExtensionResourceProject { ProviderId = "assigned-by-host", ProjectId = pair.Key, Title = pair.Key, Kind = query.Kind }).ToArray(),
            HasMore = matches.Length > query.Offset + query.Limit
        });
    }
    public ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(string projectId, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return FetchReleases is null ? ValueTask.FromResult<IReadOnlyList<ExtensionResourceRelease>>(Releases.GetValueOrDefault(projectId) ?? []) : FetchReleases(projectId, token);
    }
}
