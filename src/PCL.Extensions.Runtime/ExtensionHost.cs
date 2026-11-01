using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

public enum ExtensionStatus { Disabled, Inactive, Activating, Active, Faulted }
public sealed record ExtensionDiagnostic(DateTimeOffset Time, string ExtensionId, ExtensionLogLevel Level, string Message);

public sealed class ExtensionInfo
{
    public ExtensionManifest Manifest { get; internal set; } = null!;
    public string Directory { get; internal set; } = "";
    public bool IsBuiltIn { get; internal init; }
    public bool IsEnabled { get; internal set; }
    public bool RestartRequired { get; internal set; }
    public bool RemovalPending { get; internal set; }
    public ExtensionStatus Status { get; internal set; }
    public string? Error { get; internal set; }
    public required IExtensionSettings Settings { get; init; }
    public IExtensionLocalizer Text { get; internal set; } = null!;
    internal Func<IExtension>? Factory { get; set; }
    internal IExtension? Instance { get; set; }
    internal ExtensionContext? Context { get; set; }
    internal PluginLoadContext? LoadContext { get; set; }
}

/// <summary>Owns discovery, activation, package operations and every extension registration.</summary>
public sealed partial class ExtensionHost : IAsyncDisposable
{
    private readonly string _root;
    private readonly ILauncherServices _launcher;
    private readonly Func<string> _locale;
    private readonly Func<Func<ValueTask>, ValueTask> _dispatch;
    private readonly Func<IExtensionContext, IReadOnlyDictionary<Type, object>> _services;
    private readonly SemaphoreSlim _mutation = new(1);
    private readonly Dictionary<string, ExtensionInfo> _extensions = new(StringComparer.Ordinal);
    private readonly ExtensionSettings _state;
    private readonly List<ExtensionDiagnostic> _diagnostics = [];
    private readonly int _uiMajor;
    internal IReadOnlyList<string> WorkerCommand { get; }
    private bool _disposed;
    private readonly CancellationTokenSource _shutdown = new();

    public ExtensionHost(string root, ILauncherServices launcher, Func<string> locale,
        Func<Func<ValueTask>, ValueTask> dispatch,
        Func<IExtensionContext, IReadOnlyDictionary<Type, object>> services,
        int uiMajor = 11, bool safeMode = false, IReadOnlyList<string>? workerCommand = null)
    {
        _root = Path.GetFullPath(root);
        _launcher = launcher;
        _locale = locale;
        _dispatch = dispatch;
        _services = services;
        _uiMajor = uiMajor;
        WorkerCommand = workerCommand ?? [];
        SafeMode = safeMode;
        Directory.CreateDirectory(_root);
        var statePath = Path.Combine(_root, "state.json");
        try { _state = new ExtensionSettings(statePath); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            File.Move(statePath, statePath + ".invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _state = new ExtensionSettings(statePath);
            Log("launcher", ExtensionLogLevel.Error, "Extension state was invalid and has been preserved for recovery: " + ex.Message);
        }
        ApplyPendingPackages();
    }

    public bool SafeMode { get; }
    public IReadOnlyList<ExtensionInfo> Extensions => _extensions.Values.OrderBy(info => info.Manifest.Id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<ExtensionDiagnostic> Diagnostics { get { lock (_diagnostics) return _diagnostics.ToArray(); } }
    public event Action? Changed;

    public void AddBuiltIn(ExtensionManifest manifest, string packageDirectory, Func<IExtension> factory, bool enabledByDefault = true)
    {
        ValidateApi(manifest);
        Add(manifest, packageDirectory, factory, builtIn: true, enabledByDefault);
    }

    public void Discover()
    {
        var packages = Path.Combine(_root, "Packages");
        Directory.CreateDirectory(packages);
        foreach (var directory in Directory.EnumerateDirectories(packages).Order(StringComparer.Ordinal))
        {
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Extension directories cannot be symbolic links.");
                var manifest = ExtensionPackage.ReadManifest(directory);
                ValidateApi(manifest);
                Add(manifest, directory, null, builtIn: false, enabledByDefault: false);
            }
            catch (Exception ex) { Log(Path.GetFileName(directory), ExtensionLogLevel.Error, ex.Message); }
        }
        Changed?.Invoke();
    }

    private void Add(ExtensionManifest manifest, string directory, Func<IExtension>? factory, bool builtIn, bool enabledByDefault)
    {
        if (_extensions.ContainsKey(manifest.Id)) throw new InvalidDataException($"Duplicate extension ID: {manifest.Id}");
        var enabled = _state.Get("enabled." + manifest.Id, enabledByDefault);
        var interrupted = _state.Get("activating", "") == manifest.Id;
        var settingsPath = Path.Combine(_root, "Data", manifest.Id, "settings.json");
        ExtensionSettings settings;
        string? settingsError = null;
        try { settings = new ExtensionSettings(settingsPath); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            File.Move(settingsPath, settingsPath + ".invalid-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            settings = new ExtensionSettings(settingsPath);
            settingsError = "Extension settings were invalid and have been preserved for recovery. " + ex.Message;
            enabled = false;
            _state.Set("enabled." + manifest.Id, false);
            Log(manifest.Id, ExtensionLogLevel.Error, settingsError);
        }
        if (interrupted)
        {
            enabled = false;
            _state.Set("enabled." + manifest.Id, false);
            _state.Set("activating", "");
        }
        var info = new ExtensionInfo
        {
            Manifest = manifest, Directory = directory, Factory = factory, IsBuiltIn = builtIn,
            IsEnabled = enabled, Status = interrupted || settingsError is not null ? ExtensionStatus.Faulted : enabled ? ExtensionStatus.Inactive : ExtensionStatus.Disabled,
            Error = settingsError ?? (interrupted ? "The previous activation was interrupted. The extension was disabled." : null),
            Settings = settings,
            Text = new ExtensionLocalizer(directory, manifest.DefaultLocale, _locale)
        };
        _extensions.Add(manifest.Id, info);
        PrepareCommands(info);
    }

    private void ValidateApi(ExtensionManifest manifest)
    {
        if (manifest.ApiVersion != ExtensionPackage.ApiVersion) throw new NotSupportedException($"Unsupported extension API: {manifest.ApiVersion}");
        if (manifest.AvaloniaMajorVersion is int major && major != _uiMajor)
            throw new NotSupportedException($"This extension needs Avalonia {major}; the launcher uses Avalonia {_uiMajor}.");
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (SafeMode) return;
        foreach (var info in Extensions.Where(info => info.IsEnabled && info.Manifest.StartWithLauncher))
        {
            if (_disposed) break;
            try { await ActivateAsync(info.Manifest.Id, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { /* The fault and its diagnostic are retained per extension. */ }
        }
    }

    public async ValueTask ActivateAsync(string id, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (SafeMode) throw new InvalidOperationException("Extensions are disabled for this session.");
        await _mutation.WaitAsync(cancellationToken);
        try { await ActivateCoreAsync(Get(id), new HashSet<string>(StringComparer.Ordinal), cancellationToken); }
        finally { _mutation.Release(); }
    }

    private async ValueTask ActivateCoreAsync(ExtensionInfo info, HashSet<string> path, CancellationToken cancellationToken)
    {
        if (info.Status == ExtensionStatus.Active) return;
        if (!info.IsEnabled || info.RemovalPending) throw new InvalidOperationException($"Extension is disabled: {info.Manifest.Id}");
        if (!path.Add(info.Manifest.Id)) throw new InvalidOperationException("Extension dependency cycle: " + string.Join(" -> ", path));
        try
        {
            foreach (var dependency in info.Manifest.Dependencies)
            {
                var required = Get(dependency.Id);
                if (Version.Parse(required.Manifest.Version) < Version.Parse(dependency.MinimumVersion))
                    throw new InvalidOperationException($"{info.Manifest.Id} requires {dependency.Id} {dependency.MinimumVersion}.");
                await ActivateCoreAsync(required, path, cancellationToken);
            }
            info.Status = ExtensionStatus.Activating;
            info.Error = null;
            _state.Set("activating", info.Manifest.Id);
            Changed?.Invoke();
            await _dispatch(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new ExtensionContext(this, info, _launcher, Path.Combine(_root, "Data", info.Manifest.Id));
                info.Context = context;
                context.SetServices(_services(context));
                info.Instance = info.Factory is not null ? info.Factory() : Load(info);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, context.Lifetime, _shutdown.Token);
                var activation = info.Instance.ActivateAsync(context, linked.Token).AsTask();
                try { await activation.WaitAsync(TimeSpan.FromSeconds(10), linked.Token); }
                catch
                {
                    if (!activation.IsCompleted)
                    {
                        info.RestartRequired = true;
                        _ = activation.ContinueWith(task => { if (task.Exception is { } ex) Log(info.Manifest.Id, ExtensionLogLevel.Error, ex.Message); }, TaskScheduler.Default);
                    }
                    throw;
                }
            });
            info.Status = ExtensionStatus.Active;
            if (!_activationOrder.Contains(info.Manifest.Id)) _activationOrder.Add(info.Manifest.Id);
            Log(info.Manifest.Id, ExtensionLogLevel.Information, "Activated.");
        }
        catch (Exception ex)
        {
            await StopCoreAsync(info);
            info.Status = ExtensionStatus.Faulted;
            info.Error = ex.Message;
            Log(info.Manifest.Id, ExtensionLogLevel.Error, ex.ToString());
            throw;
        }
        finally
        {
            path.Remove(info.Manifest.Id);
            if (_state.Get("activating", "") == info.Manifest.Id) _state.Set("activating", "");
            Changed?.Invoke();
        }
    }

    private static IExtension Load(ExtensionInfo info)
    {
        var path = ExtensionPackage.ResolvePackagePath(info.Directory, info.Manifest.EntryAssembly!);
        info.LoadContext ??= new PluginLoadContext(path);
        var assembly = info.LoadContext.LoadFromAssemblyPath(path);
        var type = assembly.GetType(info.Manifest.EntryType!, throwOnError: true)!;
        if (!typeof(IExtension).IsAssignableFrom(type)) throw new InvalidDataException("The entry type must implement IExtension.");
        return (IExtension)(Activator.CreateInstance(type) ?? throw new InvalidDataException("The entry type needs a public parameterless constructor."));
    }

    public async ValueTask SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var info = Get(id);
            if (info.RemovalPending) throw new InvalidOperationException("This extension is scheduled for removal.");
            if (!enabled)
            {
                var dependent = Extensions.FirstOrDefault(other => other.IsEnabled && other.Manifest.Dependencies.Any(dependency => dependency.Id == id));
                if (dependent is not null) throw new InvalidOperationException($"Required by {dependent.Manifest.Name}. Disable that extension first.");
                info.IsEnabled = false;
                await StopCoreAsync(info);
                RemoveCommands(id);
                info.Status = ExtensionStatus.Disabled;
            }
            else
            {
                info.IsEnabled = true;
                info.Error = null;
                info.Status = info.Instance is not null ? ExtensionStatus.Active : ExtensionStatus.Inactive;
                PrepareCommands(info);
            }
            _state.Set("enabled." + id, enabled);
            if (enabled && !SafeMode) await ActivateCoreAsync(info, new(StringComparer.Ordinal), cancellationToken);
        }
        finally { _mutation.Release(); Changed?.Invoke(); }
    }

    private async ValueTask StopCoreAsync(ExtensionInfo info)
    {
        _activationOrder.Remove(info.Manifest.Id);
        var context = info.Context;
        info.Context = null;
        var instance = info.Instance;
        info.Instance = null;
        context?.Cancel();
        await _dispatch(async () =>
        {
            try { if (instance is not null) await instance.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception ex) { info.RestartRequired = true; Log(info.Manifest.Id, ExtensionLogLevel.Error, "Shutdown: " + ex.Message); }
            finally { context?.Dispose(); }
        });
    }

    public async Task ReportFaultAsync(string id, Exception exception)
    {
        if (_disposed) return;
        await _mutation.WaitAsync();
        try
        {
            if (_disposed || !_extensions.ContainsKey(id)) return;
            var info = Get(id);
            bool DependsOn(ExtensionInfo candidate, HashSet<string> visited)
                => visited.Add(candidate.Manifest.Id) && candidate.Manifest.Dependencies.Any(dependency =>
                    dependency.Id == id || _extensions.TryGetValue(dependency.Id, out var required) && DependsOn(required, visited));
            foreach (var dependent in _activationOrder.AsEnumerable().Reverse().Select(Get).Where(candidate => candidate.Manifest.Id != id && DependsOn(candidate, new(StringComparer.Ordinal))).ToArray())
            {
                await StopCoreAsync(dependent);
                dependent.Status = ExtensionStatus.Faulted;
                dependent.Error = $"Dependency {id} failed: {exception.Message}";
                Log(dependent.Manifest.Id, ExtensionLogLevel.Error, dependent.Error);
            }
            await StopCoreAsync(info);
            info.Status = ExtensionStatus.Faulted;
            info.Error = exception.Message;
            Log(id, ExtensionLogLevel.Error, exception.ToString());
        }
        finally { _mutation.Release(); Changed?.Invoke(); }
    }

    public ExtensionInfo Get(string id) => _extensions.TryGetValue(id, out var info) ? info : throw new KeyNotFoundException($"Extension not found: {id}");

    internal void Log(string id, ExtensionLogLevel level, string message)
    {
        if (message.Length > 16_384) message = message[..16_384] + "…";
        lock (_diagnostics)
        {
            _diagnostics.Add(new(DateTimeOffset.UtcNow, id, level, message));
            if (_diagnostics.Count > 500) _diagnostics.RemoveAt(0);
        }
    }

    public void LocaleChanged()
    {
        PublishHostEvent("launcher.locale.changed", JsonSerializer.SerializeToElement(_locale()));
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _shutdown.Cancel(); }
        catch (AggregateException ex) { Log("launcher", ExtensionLogLevel.Error, "An activation cancellation callback failed: " + ex.Message); }
        await _mutation.WaitAsync();
        try
        {
            // Dependencies activate before dependents; stop in the opposite order.
            foreach (var info in _activationOrder.AsEnumerable().Reverse().Select(Get).ToArray()) await StopCoreAsync(info);
            _commands.Clear();
            _providers.Clear();
            _subscriptions.Clear();
            foreach (var info in Extensions) info.LoadContext?.Unload();
        }
        finally { _mutation.Release(); }
    }

    private readonly List<string> _activationOrder = [];
}

internal sealed class PluginLoadContext(string entryPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(entryPath);
    protected override Assembly? Load(AssemblyName name)
    {
        var shared = name.Name is "PCL.Extensions.Contracts" or "PCL.Extensions.Avalonia" or "PCL.UI" or "SkiaSharp"
                     || name.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true;
        if (shared)
        {
            try { return Default.LoadFromAssemblyName(name); }
            catch (FileNotFoundException) when (name.Name?.StartsWith("Avalonia.", StringComparison.Ordinal) == true)
            { /* Extension-owned Avalonia control libraries may supply their own assembly. */ }
        }
        if (name.Name is "PCL.Frontend.Avalonia" or "PCL.Core.Backend")
            throw new InvalidOperationException("Extensions must reference the public SDK, not launcher implementation assemblies.");
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
    protected override nint LoadUnmanagedDll(string name)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(name);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
