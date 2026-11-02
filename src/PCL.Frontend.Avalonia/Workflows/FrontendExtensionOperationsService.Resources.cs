using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendExtensionOperationsService
{
    private static readonly HttpClient ExtensionDownloadClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromMinutes(2));
    private static readonly (string Folder, ExtensionResourceKind Kind)[] ResourceFolders =
    [("mods", ExtensionResourceKind.Mod), ("resourcepacks", ExtensionResourceKind.ResourcePack), ("shaderpacks", ExtensionResourceKind.Shader),
        ("saves", ExtensionResourceKind.World), ("skins", ExtensionResourceKind.Skin)];
    private sealed record PreparedInstall(ExtensionResourceInstallPlan Preview, string Root, string Fingerprint,
        FrontendExtensionResourceProviderRegistration[] Providers, IReadOnlyDictionary<string, string> Targets);
    private readonly Dictionary<string, PreparedInstall> _plans = new(StringComparer.Ordinal);

    public IDisposable Register(ExtensionResourceProviderDefinition definition, IExtensionResourceProvider provider)
    {
        ValidateContributionId(_context, definition.Id);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Title);
        if (definition.Kinds.Count == 0 || definition.Kinds.Any(kind => !Enum.IsDefined(kind))) throw new ArgumentException("A resource provider needs supported kinds.");
        definition = definition with { Kinds = Array.AsReadOnly(definition.Kinds.Distinct().ToArray()) };
        return _context.Own(_services.RegisterProvider(definition, provider, _context));
    }
    public IReadOnlyList<ExtensionResourceProviderDefinition> GetProviders()
    { _context.Lifetime.ThrowIfCancellationRequested(); return _services.GetProviders().Select(item => item.Definition).ToArray(); }

    private FrontendExtensionResourceProviderRegistration Provider(string id) => _services.GetProviders().FirstOrDefault(item => item.Definition.Id == id)
        ?? throw new ExtensionOperationException("provider.unavailable", "The resource provider is unavailable: " + id);

    private async ValueTask<T> InvokeProviderAsync<T>(FrontendExtensionResourceProviderRegistration provider, Func<IExtensionResourceProvider, CancellationToken, ValueTask<T>> invoke, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _context.Lifetime, provider.Context?.Lifetime ?? CancellationToken.None);
        var task = Task.Run(async () => await invoke(provider.Provider, linked.Token), linked.Token);
        try { return await task.WaitAsync(TimeSpan.FromSeconds(30), linked.Token); }
        finally
        {
            linked.Cancel();
            if (!task.IsCompleted) _ = task.ContinueWith(failed => _ = failed.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    public async ValueTask<ExtensionResourcePage> SearchAsync(ExtensionResourceQuery query, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        if (!Enum.IsDefined(query.Kind) || query.Limit is < 1 or > 100 || query.Offset < 0) throw new ArgumentException("Invalid resource query.");
        if (query.Offset > 0 && query.ProviderId is null) throw new ArgumentException("Select a provider when requesting subsequent pages.");
        var providers = _services.GetProviders().Where(item => item.Definition.Kinds.Contains(query.Kind)
            && (query.ProviderId is null || item.Definition.Id == query.ProviderId)).ToArray();
        if (query.ProviderId is not null && providers.Length == 0) throw new ExtensionOperationException("provider.unavailable", "The requested resource provider is unavailable.");
        var pages = await Task.WhenAll(providers.Select(async provider =>
        {
            try
            {
                var page = await InvokeProviderAsync(provider, (service, token) => service.SearchAsync(query, token), linked.Token);
                return page with { Projects = page.Projects.Take(query.Limit).Select(project => project with { ProviderId = provider.Definition.Id, Kind = query.Kind }).ToArray() };
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _services.Log(provider.Context?.Manifest.Id ?? "launcher", ex);
                if (query.ProviderId is not null) throw new ExtensionOperationException("provider.failed", ex.Message);
                return new ExtensionResourcePage();
            }
        }));
        // Federated first pages retain each provider's quota. Pagination is per provider.
        return new() { Projects = pages.SelectMany(page => page.Projects).ToArray(), HasMore = pages.Any(page => page.HasMore) };
    }

    public async ValueTask<IReadOnlyList<ExtensionResourceRelease>> GetReleasesAsync(ExtensionResourceReference resource, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource.ProjectId);
        var provider = Provider(resource.ProviderId);
        try
        {
            var result = await InvokeProviderAsync(provider, (service, token) => service.GetReleasesAsync(resource.ProjectId, token), linked.Token);
            return result.Where(item => resource.ReleaseId is null || item.Id == resource.ReleaseId).Take(4096).Select(FreezeRelease).ToArray();
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _services.Log(provider.Context?.Manifest.Id ?? "launcher", ex);
            throw new ExtensionOperationException("provider.failed", ex.Message);
        }
    }

    private static ExtensionResourceRelease FreezeRelease(ExtensionResourceRelease release) => release with
    {
        MinecraftVersions = Array.AsReadOnly(release.MinecraftVersions.ToArray()), Loaders = Array.AsReadOnly(release.Loaders.ToArray()),
        Dependencies = Array.AsReadOnly(release.Dependencies.Select(item => item with { }).ToArray())
    };

    public async ValueTask<ExtensionResourceInstallPlan> PrepareInstallAsync(ExtensionResourceInstallRequest request, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        if (request.Resources.Count is < 1 or > 256) throw new ArgumentException("An installation needs between 1 and 256 resources.");
        var instance = await GetAsync(request.InstanceId, linked.Token);
        var entries = new List<ExtensionResourcePlanEntry>();
        var resolved = new Dictionary<(string, string), string>();
        var visiting = new HashSet<(string, string)>();
        var registrations = new HashSet<FrontendExtensionResourceProviderRegistration>();
        async ValueTask Resolve(ExtensionResourceReference reference)
        {
            linked.Token.ThrowIfCancellationRequested();
            var key = (reference.ProviderId, reference.ProjectId);
            if (visiting.Contains(key)) throw new ExtensionOperationException("resource.cycle", "Resource dependencies contain a cycle.");
            if (resolved.TryGetValue(key, out var selected))
            {
                if (reference.ReleaseId is not null && reference.ReleaseId != selected) throw new ExtensionOperationException("resource.conflict", "Dependencies require conflicting resource versions.");
                return;
            }
            if (resolved.Count + visiting.Count >= 256) throw new ExtensionOperationException("resource.limit", "The dependency graph exceeds 256 resources.");
            visiting.Add(key);
            var provider = Provider(reference.ProviderId);
            registrations.Add(provider);
            var release = (await GetReleasesAsync(reference, linked.Token)).FirstOrDefault(item =>
                (item.MinecraftVersions.Count == 0 || item.MinecraftVersions.Contains(instance.Instance.MinecraftVersion!, StringComparer.OrdinalIgnoreCase)) &&
                (item.Kind != ExtensionResourceKind.Mod || item.Loaders.Count == 0 || item.Loaders.Contains(instance.Loader!, StringComparer.OrdinalIgnoreCase)))
                ?? throw new ExtensionOperationException("resource.incompatible", "No compatible release is available for " + reference.ProjectId);
            if (!provider.Definition.Kinds.Contains(release.Kind)) throw new ExtensionOperationException("resource.kind", "The provider returned an unsupported resource kind.");
            ValidateRelease(release);
            foreach (var dependency in release.Dependencies) await Resolve(dependency);
            var destination = Destination(instance, request.WorldPath, release);
            if (entries.Any(item => PathsEqual(item.Destination, destination))) throw new ExtensionOperationException("resource.conflict", "Two resources use the same destination.");
            var replaced = instance.Resources.Where(item => item.Kind == release.Kind && item.ProviderId == reference.ProviderId && item.ProjectId == reference.ProjectId && !PathsEqual(item.Path, destination) &&
                    (release.Kind != ExtensionResourceKind.DataPack || PathsEqual(Path.GetDirectoryName(item.Path)!, Path.GetDirectoryName(destination)!)))
                .Select(item => item.Path).Concat(File.Exists(destination) || Directory.Exists(destination) ? [destination] : []).ToArray();
            entries.Add(new() { Resource = reference with { ReleaseId = release.Id }, Release = release, Destination = destination, ReplacedPaths = Array.AsReadOnly(replaced) });
            visiting.Remove(key); resolved[key] = release.Id;
        }
        foreach (var reference in request.Resources) await Resolve(reference);
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var claimedPaths = new HashSet<string>(pathComparer);
        foreach (var entry in entries)
            foreach (var path in entry.ReplacedPaths.Append(entry.Destination).Distinct(pathComparer))
                if (!claimedPaths.Add(path)) throw new ExtensionOperationException("resource.conflict", "A resource destination overlaps another resource's replacement.");
        var plan = new ExtensionResourceInstallPlan { Id = Guid.NewGuid().ToString("N"), InstanceId = request.InstanceId, Entries = Array.AsReadOnly(entries.ToArray()) };
        var targets = await Task.Run(() => entries.SelectMany(entry => entry.ReplacedPaths.Append(entry.Destination))
            .Append(Path.Combine(instance.Instance.Directory, instance.Instance.Name + ".json"))
            .Append(Path.Combine(instance.GameDirectory, "PCL", "downloaded-resources.v1.json"))
            .Distinct(StringComparer.Ordinal).ToDictionary(path => path, path => TargetRevision(path, linked.Token), StringComparer.Ordinal), linked.Token);
        lock (_plans)
        {
            if (_plans.Count >= 32) _plans.Remove(_plans.Keys.First());
            _plans.Add(plan.Id, new(plan, instance.GameDirectory, Fingerprint(instance), registrations.ToArray(), targets));
        }
        return plan;
    }

    public async ValueTask ApplyInstallAsync(string planId, IProgress<ExtensionTaskProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        PreparedInstall plan;
        lock (_plans)
            if (!_plans.Remove(planId, out plan!)) throw new ExtensionOperationException("plan.unavailable", "The installation plan has expired or was already applied.");
        foreach (var provider in plan.Providers)
            if (!ReferenceEquals(Provider(provider.Definition.Id), provider)) throw new ExtensionOperationException("plan.stale", "A resource provider changed. Prepare a new plan.");
        using var providersLifetime = CancellationTokenSource.CreateLinkedTokenSource(plan.Providers.Select(provider => provider.Context?.Lifetime ?? CancellationToken.None).Append(linked.Token).ToArray());
        using var providerCancellation = providersLifetime.Token.Register(() => linked.Cancel());
        await _services.Mutation.WaitAsync(linked.Token);
        string? staging = null;
        try
        {
            await CheckMutationAsync();
            await ValidatePlanAsync(plan, linked.Token);
            staging = Path.Combine(plan.Root, ".pcl-extension-stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            var artifacts = new List<(ExtensionResourcePlanEntry Entry, string Staged)>();
            var options = _services.DownloadOptions();
            var limiter = options.MaxBytesPerSecond is { } speed ? new FrontendDownloadSpeedLimiter(speed) : null;
            for (var index = 0; index < plan.Preview.Entries.Count; index++)
            {
                var entry = plan.Preview.Entries[index];
                var path = Path.Combine(staging, index.ToString());
                await FrontendDownloadTransferService.DownloadToPathAsync(ExtensionDownloadClient, entry.Release.DownloadUrl, path,
                    speedLimiter: limiter, stalledTransferTimeout: options.StalledTransferTimeout, maxAttempts: options.MaxFileDownloadAttempts,
                    cancelToken: linked.Token);
                await Task.Run(() => VerifyHashes(path, entry.Release, linked.Token), linked.Token);
                if (entry.Release.Kind == ExtensionResourceKind.World)
                {
                    var extracted = path + ".world";
                    await Task.Run(() => ExtractWorld(path, extracted, linked.Token), linked.Token);
                    path = extracted;
                }
                artifacts.Add((entry, path));
                progress?.Report(new() { Fraction = (index + 1d) / plan.Preview.Entries.Count * .9, Message = entry.Release.Title, RemainingFiles = plan.Preview.Entries.Count - index - 1 });
            }
            await CheckMutationAsync();
            await ValidatePlanAsync(plan, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            await Task.Run(() => CommitResources(plan, staging, artifacts, linked.Token), linked.Token);
            await _services.Refresh(CancellationToken.None);
            _services.Publish(ExtensionTopics.ResourcesChanged, plan.Preview.InstanceId);
            _services.Publish(ExtensionTopics.InstanceUpdated, plan.Preview.InstanceId);
            progress?.Report(new() { Fraction = 1, RemainingFiles = 0 });
        }
        finally
        {
            try { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true); }
            finally { _services.Mutation.Release(); }
        }
    }

    private async ValueTask ValidatePlanAsync(PreparedInstall plan, CancellationToken token)
    {
        var instance = await GetAsync(plan.Preview.InstanceId, token);
        if (!PathsEqual(instance.GameDirectory, plan.Root) || Fingerprint(instance) != plan.Fingerprint)
            throw new ExtensionOperationException("plan.stale", "The instance changed after this plan was prepared. Prepare a new plan.");
        foreach (var provider in plan.Providers)
            if (!ReferenceEquals(Provider(provider.Definition.Id), provider)) throw new ExtensionOperationException("plan.stale", "A resource provider changed. Prepare a new plan.");
        await Task.Run(() =>
        {
            foreach (var (path, revision) in plan.Targets)
                if (TargetRevision(path, token) != revision) throw new ExtensionOperationException("plan.stale", "An installation target changed after review. Prepare a new plan.");
        }, token);
    }

    private static void CommitResources(PreparedInstall plan, string staging,
        List<(ExtensionResourcePlanEntry Entry, string Staged)> artifacts, CancellationToken token)
    {
        var backups = new List<(string Original, string Backup)>();
        var installed = new List<string>();
        var indexPath = SafePath(plan.Root, "PCL/downloaded-resources.v1.json");
        var indexBytes = File.Exists(indexPath) ? File.ReadAllBytes(indexPath) : null;
        try
        {
            foreach (var (entry, staged) in artifacts)
            {
                token.ThrowIfCancellationRequested();
                var destination = SafePath(plan.Root, Path.GetRelativePath(plan.Root, entry.Destination).Replace('\\', '/'));
                foreach (var existing in entry.ReplacedPaths.Append(destination).Distinct(StringComparer.Ordinal))
                {
                    SafePath(plan.Root, Path.GetRelativePath(plan.Root, existing).Replace('\\', '/'));
                    if (!File.Exists(existing) && !Directory.Exists(existing)) continue;
                    var backup = Path.Combine(staging, "backup-" + backups.Count);
                    Move(existing, backup); backups.Add((existing, backup));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Move(staged, destination); installed.Add(destination);
                FrontendInstanceDownloadedResourceIndexService.RecordInstalledResource(plan.Root,
                        new(KindName(entry.Release.Kind), entry.Resource.ProviderId, entry.Resource.ProjectId, entry.Release.Title,
                            entry.Release.Title, entry.Release.Id, entry.Release.Id, entry.Release.FileName, entry.Release.Kind == ExtensionResourceKind.World ? null : entry.Release.Sha1,
                            entry.Release.Kind == ExtensionResourceKind.World ? null : entry.Release.Sha512, destination, DateTimeOffset.UtcNow), requirePersistence: true);
                foreach (var replaced in entry.ReplacedPaths.Where(path => !PathsEqual(path, destination)))
                    FrontendInstanceDownloadedResourceIndexService.RemoveResourceRecord(plan.Root, replaced, requirePersistence: true);
                token.ThrowIfCancellationRequested();
            }
        }
        catch
        {
            foreach (var path in installed.AsEnumerable().Reverse()) { if (Directory.Exists(path)) Directory.Delete(path, true); else File.Delete(path); }
            foreach (var (original, backup) in backups.AsEnumerable().Reverse()) Move(backup, original);
            if (indexBytes is null) { if (File.Exists(indexPath)) File.Delete(indexPath); }
            else
            {
                var restored = Path.Combine(staging, "restored-index.json");
                File.WriteAllBytes(restored, indexBytes); File.Move(restored, indexPath, overwrite: true);
            }
            throw;
        }
    }
    private static void Move(string source, string target) { if (Directory.Exists(source)) Directory.Move(source, target); else File.Move(source, target); }
    private static void ValidateRelease(ExtensionResourceRelease release)
    {
        if (string.IsNullOrWhiteSpace(release.Id) || string.IsNullOrWhiteSpace(release.Title) || !Enum.IsDefined(release.Kind)) throw new InvalidDataException("Invalid resource release.");
        var name = release.FileName;
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(['/', '\\', ':']) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            throw new InvalidDataException("Resource file names must be single valid file names.");
        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new InvalidDataException("Resource downloads require HTTP or HTTPS.");
        foreach (var (hash, length) in new[] { (release.Sha1, 40), (release.Sha256, 64), (release.Sha512, 128) })
            if (hash is not null && (hash.Length != length || !hash.All(Uri.IsHexDigit))) throw new InvalidDataException("Invalid resource checksum.");
    }
    private static string Destination(ExtensionInstanceDetails instance, string? world, ExtensionResourceRelease release)
    {
        if (release.Kind == ExtensionResourceKind.DataPack)
        {
            if (world is null || !instance.Resources.Any(item => item.Kind == ExtensionResourceKind.World && PathsEqual(item.Path, world) && Directory.Exists(world)))
                throw new ExtensionOperationException("world.required", "Select an existing world before installing datapacks.");
            return SafePath(instance.GameDirectory, Path.GetRelativePath(instance.GameDirectory, world).Replace('\\', '/') + "/datapacks/" + release.FileName);
        }
        var folder = ResourceFolders.First(item => item.Kind == release.Kind).Folder;
        var file = release.Kind == ExtensionResourceKind.World ? Path.GetFileNameWithoutExtension(release.FileName) : release.FileName;
        return SafePath(instance.GameDirectory, folder + "/" + file);
    }
    private static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(part => part is "" or "." or "..")) throw new InvalidDataException("Resource paths must stay inside the game directory.");
        var path = root; RejectLink(root);
        foreach (var part in relative.Split('/')) { path = Path.Combine(path, part); RejectLink(path); }
        return path;
    }
    private static string Fingerprint(ExtensionInstanceDetails instance) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        instance.Instance.MinecraftVersion + "|" + instance.Loader + "|" + instance.LoaderVersion + "|" + string.Join('\n', instance.Resources.Select(item => item.Path + ":" + item.Sha1)))));
    private static string TargetRevision(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); RejectLink(path);
        if (File.Exists(path)) return "file:" + HashFile(path, SHA256.Create(), token);
        if (!Directory.Exists(path)) return "missing";
        var parts = new List<string>();
        void Read(string directory)
        {
            token.ThrowIfCancellationRequested(); RejectLink(directory);
            foreach (var child in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
            {
                RejectLink(child);
                if (Directory.Exists(child)) { parts.Add("directory:" + child); Read(child); }
                else { var file = new FileInfo(child); parts.Add(child + ":" + file.Length + ":" + file.LastWriteTimeUtc.Ticks); }
            }
        }
        Read(path);
        return "directory:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));
    }
    private static void VerifyHashes(string path, ExtensionResourceRelease release, CancellationToken token)
    {
        foreach (var (expected, algorithm) in new (string?, Func<HashAlgorithm>)[] { (release.Sha1, SHA1.Create), (release.Sha256, SHA256.Create), (release.Sha512, SHA512.Create) })
            if (expected is not null && !string.Equals(expected, HashFile(path, algorithm(), token), StringComparison.OrdinalIgnoreCase))
                throw new ExtensionOperationException("resource.checksum", "Resource checksum verification failed: " + release.FileName);
    }
    private static void ExtractWorld(string archive, string target, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 100_000 || zip.Entries.Sum(item => item.Length) > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("The world archive exceeds the installation limits.");
        var worldEntry = zip.Entries.FirstOrDefault(item => item.FullName is "level.dat" || item.FullName.EndsWith("/level.dat", StringComparison.Ordinal))
                         ?? throw new InvalidDataException("The world archive contains no level.dat.");
        var prefix = worldEntry.FullName[..^"level.dat".Length];
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000) throw new InvalidDataException("World archives cannot contain symbolic links.");
            if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var name = entry.FullName[prefix.Length..].TrimEnd('/');
            if (name.Length == 0) continue;
            var path = SafePath(target, name);
            if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(path);
            else { Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path); }
        }
    }
    private static string KindName(ExtensionResourceKind kind) => kind switch
    { ExtensionResourceKind.ResourcePack => "resourcepack", ExtensionResourceKind.DataPack => "datapack", _ => kind.ToString().ToLowerInvariant() };
    private static string ProviderId(string source) => source.ToLowerInvariant() switch { "modrinth" => "pcl.modrinth", "curseforge" => "pcl.curseforge", _ => source };
    private static void ValidateContributionId(IExtensionContext context, string id)
    {
        context.Lifetime.ThrowIfCancellationRequested();
        if (!id.StartsWith(context.Manifest.Id + ".", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(id[(context.Manifest.Id.Length + 1)..]))
            throw new ArgumentException("Contribution IDs must use the extension namespace.", nameof(id));
    }

}
