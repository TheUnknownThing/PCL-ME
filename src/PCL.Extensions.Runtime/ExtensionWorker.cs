using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

internal sealed record WorkerMessage(long Id, string? Method = null, JsonElement Payload = default, bool Response = false, string? Error = null, bool Cancel = false);

internal sealed class ExtensionWorkerClient : IExtensionWorker
{
    private readonly Process _process;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _write = new(1);
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationTokenRegistration _shutdown;
    private long _nextId;
    private int _disposed;

    public ExtensionWorkerClient(IReadOnlyList<string> command, string assembly, string type, Action<string> log, CancellationToken lifetime)
    {
        if (command.Count == 0) throw new NotSupportedException("This host does not provide extension worker processes.");
        if (!File.Exists(assembly)) throw new FileNotFoundException("Worker assembly not found.", assembly);
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        var start = new ProcessStartInfo(command[0]) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in command.Skip(1)) start.ArgumentList.Add(argument);
        start.ArgumentList.Add("extension-worker");
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(type);
        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the extension worker.");
        _shutdown = lifetime.Register(Dispose);
        _ = ReadResponsesAsync();
        _ = ReadErrorsAsync(log);
    }

    public async ValueTask<JsonElement> RequestAsync(string method, JsonElement payload, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        var id = Interlocked.Increment(ref _nextId);
        var result = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.TryAdd(id, result);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var cancellation = deadline.Token.Register(() =>
        {
            result.TrySetCanceled(deadline.Token);
            _ = CancelRequestAsync(id);
        });
        try
        {
            await SendAsync(new(id, method, payload), deadline.Token);
            return await result.Task;
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task CancelRequestAsync(long id)
    {
        try { await SendAsync(new(id, Cancel: true), _lifetime.Token); }
        catch (Exception) when (Volatile.Read(ref _disposed) != 0 || _process.HasExited) { }
        catch (Exception) { /* A failed write is also observed by the response reader. */ }
    }

    private async Task SendAsync(WorkerMessage message, CancellationToken token)
    {
        var line = JsonSerializer.Serialize(message, WorkerWire.Options);
        if (line.Length > WorkerWire.MaximumMessageCharacters) throw new InvalidDataException("Worker message is too large.");
        await _write.WaitAsync(token);
        try { await _process.StandardInput.WriteLineAsync(line.AsMemory(), token); await _process.StandardInput.FlushAsync(token); }
        finally { _write.Release(); }
    }

    private async Task ReadResponsesAsync()
    {
        Exception failure = new IOException("The extension worker exited.");
        try
        {
            while (await WorkerWire.ReadLineAsync(_process.StandardOutput, _lifetime.Token) is { } line)
            {
                var message = JsonSerializer.Deserialize<WorkerMessage>(line, WorkerWire.Options) ?? throw new InvalidDataException("Invalid worker response.");
                if (!message.Response || !_pending.TryGetValue(message.Id, out var pending)) continue;
                if (message.Error is { } error) pending.TrySetException(new InvalidOperationException(error));
                else pending.TrySetResult(message.Payload);
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            foreach (var request in _pending.Values) request.TrySetException(failure);
            Dispose();
        }
    }

    private async Task ReadErrorsAsync(Action<string> log)
    {
        try
        {
            while (await WorkerWire.ReadLineAsync(_process.StandardError, _lifetime.Token) is { } line) log(line);
        }
        catch (Exception) { /* Closing the process also closes its diagnostic stream. */ }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        foreach (var request in _pending.Values) request.TrySetException(new ObjectDisposedException(nameof(IExtensionWorker)));
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                // A killed process releases its assembly file locks only once it has
                // actually exited; callers may delete package directories right
                // after disposal, so the kill must settle before returning.
                _process.WaitForExit(TimeSpan.FromSeconds(3));
            }
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
        _shutdown.Unregister();
        _process.Dispose();
    }
}

internal static class WorkerWire
{
    public const int MaximumMessageCharacters = 4 * 1024 * 1024;
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault };
    public static async ValueTask<string?> ReadLineAsync(TextReader reader, CancellationToken token)
    {
        // Bound allocation before accepting a line from extension code.
        var line = new System.Text.StringBuilder();
        var buffer = new char[1];
        while (await reader.ReadAsync(buffer.AsMemory(), token) != 0)
        {
            if (buffer[0] == '\n') return line.ToString();
            if (buffer[0] == '\r') continue;
            if (line.Length >= MaximumMessageCharacters) throw new InvalidDataException("Worker message is too large.");
            line.Append(buffer[0]);
        }
        return line.Length == 0 ? null : line.ToString();
    }
}

public static class ExtensionWorkerServer
{
    public static async Task RunAsync(string assembly, string entryType, CancellationToken cancellationToken = default)
    {
        using var load = new WorkerAssemblyScope(Path.GetFullPath(assembly));
        var type = load.Context.LoadFromAssemblyPath(Path.GetFullPath(assembly)).GetType(entryType, throwOnError: true)!;
        if (!typeof(IExtensionWorkerHandler).IsAssignableFrom(type)) throw new InvalidDataException("Worker entry types must implement IExtensionWorkerHandler.");
        await using var handler = (IExtensionWorkerHandler)Activator.CreateInstance(type)!;
        var output = Console.Out;
        Console.SetOut(Console.Error);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var write = new SemaphoreSlim(1);
        var requests = new ConcurrentDictionary<long, CancellationTokenSource>();
        var tasks = new ConcurrentDictionary<long, Task>();
        try
        {
            while (await WorkerWire.ReadLineAsync(Console.In, lifetime.Token) is { } line)
            {
                var message = JsonSerializer.Deserialize<WorkerMessage>(line, WorkerWire.Options) ?? throw new InvalidDataException("Invalid worker request.");
                if (message.Cancel)
                {
                    if (requests.TryGetValue(message.Id, out var canceled))
                        try { canceled.Cancel(); } catch (ObjectDisposedException) { }
                    continue;
                }
                if (message.Method is null || requests.Count >= 32) throw new InvalidDataException("Invalid or excessive worker requests.");
                var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                if (!requests.TryAdd(message.Id, request)) { request.Dispose(); throw new InvalidDataException("Duplicate request ID."); }
                var task = Task.Run(async () =>
                {
                    WorkerMessage response;
                    try { response = new(message.Id, Payload: await handler.HandleAsync(message.Method, message.Payload, request.Token), Response: true); }
                    catch (Exception ex) { response = new(message.Id, Response: true, Error: ex.Message); }
                    try
                    {
                        var json = JsonSerializer.Serialize(response, WorkerWire.Options);
                        if (json.Length > WorkerWire.MaximumMessageCharacters) json = JsonSerializer.Serialize(new WorkerMessage(message.Id, Response: true, Error: "Worker response is too large."), WorkerWire.Options);
                        await write.WaitAsync(lifetime.Token);
                        try { await output.WriteLineAsync(json); await output.FlushAsync(); }
                        finally { write.Release(); }
                    }
                    finally { requests.TryRemove(message.Id, out _); request.Dispose(); }
                }, lifetime.Token);
                tasks[message.Id] = task;
                _ = task.ContinueWith(_ => tasks.TryRemove(message.Id, out var ignored), TaskScheduler.Default);
            }
        }
        finally
        {
            lifetime.Cancel();
            try { await Task.WhenAll(tasks.Values).WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception) { }
        }
    }

    private sealed class WorkerAssemblyScope(string assembly) : IDisposable
    {
        public PluginLoadContext Context { get; } = new(assembly);
        public void Dispose() => Context.Unload();
    }
}
