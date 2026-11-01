using System.Text.Json;

namespace PCL.Extensions.Contracts;

public sealed record ExtensionWorkerOptions(string Assembly, string EntryType);

/// <summary>A background process owned by an extension's activation lifetime.</summary>
public interface IExtensionWorker : IDisposable
{
    ValueTask<JsonElement> RequestAsync(string method, JsonElement payload, CancellationToken cancellationToken = default);
}

/// <summary>Implemented by a worker entry type. Standard output is reserved for the protocol.</summary>
public interface IExtensionWorkerHandler : IAsyncDisposable
{
    ValueTask<JsonElement> HandleAsync(string method, JsonElement payload, CancellationToken cancellationToken);
}
