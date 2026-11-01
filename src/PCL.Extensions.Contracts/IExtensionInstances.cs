namespace PCL.Extensions.Contracts;

public sealed record ExtensionInstanceDetails
{
    public required LauncherInstance Instance { get; init; }
    public required string GameDirectory { get; init; }
    public string? Loader { get; init; }
    public string? LoaderVersion { get; init; }
    public IReadOnlyList<ExtensionInstalledResource> Resources { get; init; } = [];
}

public sealed record ExtensionInstanceCreateRequest
{
    public required string Name { get; init; }
    public required string MinecraftVersion { get; init; }
    /// <summary>Optional HTTP(S) manifest from a custom catalog; its id must match MinecraftVersion.</summary>
    public string? MinecraftManifestUrl { get; init; }
    public string? Loader { get; init; }
    public string? LoaderVersion { get; init; }
}

public sealed record ExtensionInstanceCloneRequest
{
    public required string SourceInstanceId { get; init; }
    public required string Name { get; init; }
}

public interface IExtensionInstances
{
    ValueTask<ExtensionInstanceDetails> GetAsync(string instanceId, CancellationToken cancellationToken = default);
    ValueTask<LauncherInstance> CreateAsync(ExtensionInstanceCreateRequest request, CancellationToken cancellationToken = default);
    ValueTask<LauncherInstance> CloneAsync(ExtensionInstanceCloneRequest request, CancellationToken cancellationToken = default);
    /// <summary>Moves the instance into the launcher's recovery directory and returns the retained path.</summary>
    ValueTask<string> RemoveAsync(string instanceId, CancellationToken cancellationToken = default);
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);
}
