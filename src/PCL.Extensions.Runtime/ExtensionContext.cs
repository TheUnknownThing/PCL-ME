using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

internal sealed class ExtensionContext : IExtensionContext, IExtensionCommands, IExtensionSearch, IExtensionEvents, IDisposable
{
    private readonly ExtensionHost _host;
    private readonly ExtensionInfo _info;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private readonly List<IDisposable> _owned = [];
    private IReadOnlyDictionary<Type, object> _services = new Dictionary<Type, object>();
    private bool _disposed;

    public ExtensionContext(ExtensionHost host, ExtensionInfo info, ILauncherServices launcher, string dataDirectory)
    {
        _host = host;
        _token = _lifetime.Token;
        _info = info;
        Launcher = launcher;
        DataDirectory = dataDirectory;
        Directory.CreateDirectory(dataDirectory);
    }
    public ExtensionManifest Manifest => _info.Manifest;
    public string PackageDirectory => _info.Directory;
    public string DataDirectory { get; }
    public CancellationToken Lifetime => _token;
    public IExtensionSettings Settings => _info.Settings;
    public IExtensionLocalizer Text => _info.Text;
    public IExtensionCommands Commands => this;
    public IExtensionSearch Search => this;
    public IExtensionEvents Events => this;
    public ILauncherServices Launcher { get; }
    internal void SetServices(IReadOnlyDictionary<Type, object> services) => _services = services;
    public T GetService<T>() where T : class => _services.TryGetValue(typeof(T), out var service)
        ? (T)service : throw new NotSupportedException($"Extension service is unavailable: {typeof(T).FullName}");

    public T Own<T>(T resource) where T : IDisposable
    {
        lock (_owned)
        {
            if (!_disposed) { _owned.Add(resource); return resource; }
        }
        resource.Dispose();
        throw new ObjectDisposedException(nameof(IExtensionContext));
    }

    public void RunBackground(Func<CancellationToken, Task> operation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var token = Lifetime;
        _ = Task.Run(async () =>
        {
            try { await operation(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) Log(ExtensionLogLevel.Warning, "Background work failed after cancellation.", ex);
                else await _host.ReportFaultAsync(Manifest.Id, ex);
            }
        }, token);
    }
    public void Log(ExtensionLogLevel level, string message, Exception? exception = null) => _host.Log(Manifest.Id, level, message + (exception is null ? "" : "\n" + exception));
    public ValueTask DisableAsync(CancellationToken cancellationToken = default) => _host.SetEnabledAsync(Manifest.Id, false, cancellationToken);
    public IExtensionWorker StartWorker(ExtensionWorkerOptions options) => Own(new ExtensionWorkerClient(
        _host.WorkerCommand, ExtensionPackage.ResolvePackagePath(PackageDirectory, options.Assembly), options.EntryType,
        message => Log(ExtensionLogLevel.Warning, message), Lifetime));

    public IDisposable Register(ExtensionCommand command, Func<CancellationToken, ValueTask> execute, Func<bool>? canExecute = null)
        => Own(_host.RegisterCommand(Manifest.Id, command, execute, canExecute));
    public ValueTask ExecuteAsync(string commandId, CancellationToken cancellationToken = default)
        => _host.ExecuteCommandAsync(commandId, cancellationToken);
    public IDisposable Register(string providerId, Func<ExtensionSearchQuery, CancellationToken, ValueTask<IReadOnlyList<ExtensionSearchResult>>> search)
        => Own(_host.RegisterSearch(Manifest.Id, providerId, search));
    public ValueTask<IReadOnlyList<ExtensionSearchResult>> QueryAsync(ExtensionSearchQuery query, CancellationToken cancellationToken = default)
        => _host.QueryAsync(query, cancellationToken);
    public ValueTask ExecuteAsync(ExtensionSearchResult result, CancellationToken cancellationToken = default)
        => _host.ExecuteSearchResultAsync(result, cancellationToken);
    public IDisposable Subscribe(string topic, Action<ExtensionEvent> handler) => Own(_host.Subscribe(Manifest.Id, topic, handler));
    public void Publish(string topic, JsonElement data)
    {
        if (!topic.StartsWith(Manifest.Id + ".", StringComparison.Ordinal)) throw new ArgumentException("Events must use the extension namespace.", nameof(topic));
        _host.PublishHostEvent(topic, data);
    }

    internal void Cancel()
    {
        try { _lifetime.Cancel(); }
        catch (AggregateException ex) { Log(ExtensionLogLevel.Error, "A cancellation callback failed.", ex); }
    }
    public void Dispose()
    {
        IDisposable[] resources;
        lock (_owned)
        {
            if (_disposed) return;
            _disposed = true;
            resources = _owned.AsEnumerable().Reverse().ToArray();
            _owned.Clear();
        }
        foreach (var resource in resources)
        {
            try { resource.Dispose(); }
            catch (Exception ex) { Log(ExtensionLogLevel.Error, "Resource cleanup failed.", ex); }
        }
        _services = new Dictionary<Type, object>();
        _lifetime.Dispose();
    }
}

public sealed class ExtensionRegistration(Action dispose) : IDisposable
{
    private Action? _dispose = dispose;
    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}
