using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Extensions.Contracts;

namespace WorkflowExtension;

/// <summary>The catalog is data. Loading a catalog never loads or executes assemblies.</summary>
public sealed class RepositoryProvider(IExtensionContext context) : IExtensionResourceProvider, IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private readonly SemaphoreSlim _refresh = new(1);
    private Catalog _catalog = new();
    private string _url = "";
    private DateTimeOffset _updated;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async ValueTask<Catalog> ReadAsync(CancellationToken token)
    {
        var url = context.Settings.Get("catalogUrl", "");
        if (string.IsNullOrWhiteSpace(url)) return new();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new InvalidDataException(context.Text.Get("catalog.invalid"));
        await _refresh.WaitAsync(token);
        try
        {
            if (url == _url && DateTimeOffset.UtcNow - _updated < TimeSpan.FromSeconds(30)) return _catalog;
            await using var input = await _http.GetStreamAsync(uri, token);
            var catalog = await JsonSerializer.DeserializeAsync<Catalog>(input, Json, token) ?? throw new InvalidDataException(context.Text.Get("catalog.invalid"));
            if (catalog.Projects.Select(project => project.Id).Distinct(StringComparer.Ordinal).Count() != catalog.Projects.Count) throw new InvalidDataException(context.Text.Get("catalog.invalid"));
            _catalog = catalog; _url = url; _updated = DateTimeOffset.UtcNow;
            return catalog;
        }
        finally { _refresh.Release(); }
    }
    public async ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken token)
    {
        var catalog = await ReadAsync(token);
        var projects = catalog.Projects.Where(project => project.Kind == query.Kind &&
            (project.Title + " " + project.Description).Contains(query.Text, StringComparison.CurrentCultureIgnoreCase) &&
            project.Releases.Any(release => (string.IsNullOrEmpty(query.MinecraftVersion) || release.MinecraftVersions.Count == 0 || release.MinecraftVersions.Contains(query.MinecraftVersion)) &&
                (string.IsNullOrEmpty(query.Loader) || release.Loaders.Count == 0 || release.Loaders.Contains(query.Loader, StringComparer.OrdinalIgnoreCase)))).ToArray();
        return new() { Projects = projects.Skip(query.Offset).Take(query.Limit).Select(project => new ExtensionResourceProject
        { ProviderId = "example.workflows.catalog", ProjectId = project.Id, Title = project.Title, Description = project.Description, Kind = project.Kind }).ToArray(),
            HasMore = projects.Length > query.Offset + query.Limit };
    }
    public async ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(string projectId, CancellationToken token)
        => (await ReadAsync(token)).Projects.SingleOrDefault(project => project.Id == projectId)?.Releases ?? [];
    public void Dispose() => _http.Dispose();

    public sealed record Catalog { public IReadOnlyList<Project> Projects { get; init; } = []; }
    public sealed record Project
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public string Description { get; init; } = "";
        public ExtensionResourceKind Kind { get; init; }
        public IReadOnlyList<ExtensionResourceRelease> Releases { get; init; } = [];
    }
}
