using System.Text.Json;

namespace PCL.Extensions.Contracts;

public interface IExtension : IAsyncDisposable
{
    ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken);
}

public interface IExtensionContext
{
    ExtensionManifest Manifest { get; }
    string PackageDirectory { get; }
    string DataDirectory { get; }
    CancellationToken Lifetime { get; }
    IExtensionSettings Settings { get; }
    IExtensionLocalizer Text { get; }
    IExtensionCommands Commands { get; }
    IExtensionSearch Search { get; }
    IExtensionEvents Events { get; }
    ILauncherServices Launcher { get; }
    T GetService<T>() where T : class;
    T Own<T>(T resource) where T : IDisposable;
    void RunBackground(Func<CancellationToken, Task> operation);
    void Log(ExtensionLogLevel level, string message, Exception? exception = null);
    ValueTask DisableAsync(CancellationToken cancellationToken = default);
    IExtensionWorker StartWorker(ExtensionWorkerOptions options);
}

public enum ExtensionLogLevel { Information, Warning, Error }

public interface IExtensionSettings
{
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
    event Action<string>? Changed;
}

public interface IExtensionLocalizer
{
    string Locale { get; }
    string Get(string key, string? fallback = null);
}

public interface IExtensionCommands
{
    IDisposable Register(ExtensionCommand command, Func<CancellationToken, ValueTask> execute, Func<bool>? canExecute = null);
    ValueTask ExecuteAsync(string commandId, CancellationToken cancellationToken = default);
}

public enum ExtensionSearchKind { Page, Setting, Instance, Command, Help, Custom }

public sealed record ExtensionSearchQuery(string Text, int Limit = 30, ExtensionSearchKind? Kind = null);

public sealed record ExtensionSearchResult
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public string? CommandId { get; init; }
    public string? Route { get; init; }
    public IReadOnlyDictionary<string, string>? Arguments { get; init; }
    public double Score { get; init; }
    public string? ProviderId { get; init; }
    public ExtensionSearchKind Kind { get; init; } = ExtensionSearchKind.Custom;
    public string? IconData { get; init; }
    public string? Shortcut { get; init; }
}

public interface IExtensionSearch
{
    IDisposable Register(string providerId, Func<ExtensionSearchQuery, CancellationToken, ValueTask<IReadOnlyList<ExtensionSearchResult>>> search);
    ValueTask<IReadOnlyList<ExtensionSearchResult>> QueryAsync(ExtensionSearchQuery query, CancellationToken cancellationToken = default);
    ValueTask ExecuteAsync(ExtensionSearchResult result, CancellationToken cancellationToken = default);
}

public sealed record ExtensionEvent(string Topic, JsonElement Data);

public interface IExtensionEvents
{
    IDisposable Subscribe(string topic, Action<ExtensionEvent> handler);
    void Publish(string topic, JsonElement data);
}

public sealed record LauncherInstance(string Id, string Name, string Directory, string? MinecraftVersion, bool IsSelected);
public sealed record LauncherLocation(string Route, string Title, string Description, ExtensionSearchKind Kind = ExtensionSearchKind.Page, string Keywords = "");

public interface ILauncherServices
{
    ValueTask<IReadOnlyList<LauncherInstance>> GetInstancesAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<LauncherLocation> GetLocations();
    ValueTask NavigateAsync(string route, IReadOnlyDictionary<string, string>? arguments = null, CancellationToken cancellationToken = default);
    ValueTask LaunchAsync(string instanceId, CancellationToken cancellationToken = default);
    void ShowLauncher();
    void Notify(string message, bool isError = false);
    ValueTask<string?> PickFileAsync(string title, IReadOnlyList<string> patterns, CancellationToken cancellationToken = default);
}
