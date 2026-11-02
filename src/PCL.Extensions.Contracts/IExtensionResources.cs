namespace PCL.Extensions.Contracts;

public enum ExtensionResourceKind { Mod, ResourcePack, Shader, World, DataPack, Skin }

public sealed record ExtensionResourceQuery
{
    public ExtensionResourceKind Kind { get; init; }
    public string Text { get; init; } = "";
    public string? MinecraftVersion { get; init; }
    public string? Loader { get; init; }
    public string? ProviderId { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; } = 20;
}

public sealed record ExtensionResourceProject
{
    public required string ProviderId { get; init; }
    public required string ProjectId { get; init; }
    public required string Title { get; init; }
    public string Description { get; init; } = "";
    public ExtensionResourceKind Kind { get; init; }
}

public sealed record ExtensionResourcePage
{
    public IReadOnlyList<ExtensionResourceProject> Projects { get; init; } = [];
    public bool HasMore { get; init; }
}

public sealed record ExtensionResourceReference
{
    public required string ProviderId { get; init; }
    public required string ProjectId { get; init; }
    public string? ReleaseId { get; init; }
}

public sealed record ExtensionResourceRelease
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FileName { get; init; }
    public required string DownloadUrl { get; init; }
    public ExtensionResourceKind Kind { get; init; }
    public IReadOnlyList<string> MinecraftVersions { get; init; } = [];
    public IReadOnlyList<string> Loaders { get; init; } = [];
    public string? Sha1 { get; init; }
    public string? Sha256 { get; init; }
    public string? Sha512 { get; init; }
    public IReadOnlyList<ExtensionResourceReference> Dependencies { get; init; } = [];
}

public sealed record ExtensionResourceProviderDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public IReadOnlyList<ExtensionResourceKind> Kinds { get; init; } = [];
}

public interface IExtensionResourceProvider
{
    ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(string projectId, CancellationToken cancellationToken);
}

public interface IExtensionResourceProviders
{
    IDisposable Register(ExtensionResourceProviderDefinition definition, IExtensionResourceProvider provider);
    IReadOnlyList<ExtensionResourceProviderDefinition> GetProviders();
}

public sealed record ExtensionInstalledResource
{
    public required string Path { get; init; }
    public required string FileName { get; init; }
    public ExtensionResourceKind Kind { get; init; }
    public string? ProviderId { get; init; }
    public string? ProjectId { get; init; }
    public string? ReleaseId { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Sha1 { get; init; }
}

public sealed record ExtensionResourceInstallRequest
{
    public required string InstanceId { get; init; }
    /// <summary>Required when installing datapacks. Must identify an existing world in this instance.</summary>
    public string? WorldPath { get; init; }
    public IReadOnlyList<ExtensionResourceReference> Resources { get; init; } = [];
}

public sealed record ExtensionResourcePlanEntry
{
    public required ExtensionResourceReference Resource { get; init; }
    public required ExtensionResourceRelease Release { get; init; }
    public required string Destination { get; init; }
    public IReadOnlyList<string> ReplacedPaths { get; init; } = [];
}

/// <summary>A preview. Apply uses the host-owned plan identified by Id, never caller-edited entries.</summary>
public sealed record ExtensionResourceInstallPlan
{
    public required string Id { get; init; }
    public required string InstanceId { get; init; }
    public IReadOnlyList<ExtensionResourcePlanEntry> Entries { get; init; } = [];
}

public interface IExtensionResources
{
    ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(ExtensionResourceReference resource, CancellationToken cancellationToken = default);
    ValueTask<ExtensionResourceInstallPlan> PrepareInstallAsync(ExtensionResourceInstallRequest request, CancellationToken cancellationToken = default);
    ValueTask ApplyInstallAsync(string planId, IProgress<ExtensionTaskProgress>? progress = null, CancellationToken cancellationToken = default);
}
