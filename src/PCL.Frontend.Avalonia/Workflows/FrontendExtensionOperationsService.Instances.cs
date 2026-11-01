using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed partial class FrontendExtensionOperationsService : IExtensionInstances, IExtensionResources, IExtensionResourceProviders
{
    private readonly FrontendExtensionOperationsServices _services;
    private readonly IExtensionContext _context;
    public FrontendExtensionOperationsService(FrontendExtensionOperationsServices services, IExtensionContext context) { _services = services; _context = context; }

    private CancellationTokenSource Link(CancellationToken token) => CancellationTokenSource.CreateLinkedTokenSource(_context.Lifetime, token);

    public async ValueTask<ExtensionInstanceDetails> GetAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        var instance = (await _services.Launcher.GetInstancesAsync(linked.Token)).FirstOrDefault(item => item.Id == instanceId)
                       ?? throw new ExtensionOperationException("instance.not-found", "The instance no longer exists.");
        return await Task.Run(() => ReadInstance(instance, linked.Token), linked.Token);
    }

    private ExtensionInstanceDetails ReadInstance(LauncherInstance instance, CancellationToken token)
    {
        var launcher = Path.GetDirectoryName(Path.GetDirectoryName(instance.Directory))!;
        SafePath(launcher, "versions/" + instance.Name);
        var composition = FrontendInstanceCompositionService.Compose(_services.Paths, instance.Name,
            FrontendInstanceCompositionService.LoadMode.Lightweight, _services.Text, launcher);
        var root = composition.Selection.IndieDirectory;
        var profile = FrontendVersionManifestInspector.ReadProfile(launcher, instance.Name);
        var loaders = FrontendVersionManifestInspector.CreateLoaderMap(profile);
        var loader = loaders.FirstOrDefault();
        var resources = new List<ExtensionInstalledResource>();
        void AddResource(string path, ExtensionResourceKind kind)
        {
            token.ThrowIfCancellationRequested(); RejectLink(path);
            var isFile = File.Exists(path);
            var record = FrontendInstanceDownloadedResourceIndexService.FindRecordForArtifact(root, path, KindName(kind))?.Entry;
            resources.Add(new()
            {
                Path = path, FileName = Path.GetFileName(path), Kind = kind,
                ProviderId = record is null ? null : ProviderId(record.Source), ProjectId = record?.ProjectId, ReleaseId = record?.ReleaseId,
                Enabled = !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".old", StringComparison.OrdinalIgnoreCase),
                Sha1 = isFile ? HashFile(path, SHA1.Create(), token) : null
            });
        }
        foreach (var (folder, kind) in ResourceFolders)
        {
            var directory = Path.Combine(root, folder);
            if (!Directory.Exists(directory)) continue;
            RejectLink(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal)) AddResource(path, kind);
        }
        foreach (var world in resources.Where(item => item.Kind == ExtensionResourceKind.World).ToArray())
        {
            var directory = Path.Combine(world.Path, "datapacks");
            if (!Directory.Exists(directory)) continue;
            RejectLink(directory);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal)) AddResource(path, ExtensionResourceKind.DataPack);
        }
        return new() { Instance = instance, GameDirectory = root, Loader = loader.Key, LoaderVersion = loader.Value, Resources = resources.AsReadOnly() };
    }

    public async ValueTask<LauncherInstance> CreateAsync(ExtensionInstanceCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftVersion);
        using var linked = Link(cancellationToken);
        await _services.Mutation.WaitAsync(linked.Token);
        string? staging = null;
        try
        {
            await CheckMutationAsync();
            var launcher = await LauncherDirectoryAsync();
            var target = NewInstancePath(launcher, request.Name);
            if (request.MinecraftManifestUrl is { } manifestUrl)
            {
                if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) throw new ArgumentException("Custom Minecraft manifests require HTTP or HTTPS.");
                using var manifest = JsonDocument.Parse(await ExtensionDownloadClient.GetStringAsync(uri, linked.Token));
                if (!manifest.RootElement.TryGetProperty("id", out var id) || id.GetString() != request.MinecraftVersion)
                    throw new ExtensionOperationException("version.mismatch", "The Minecraft manifest does not match the requested version.");
            }
            var stagingName = ".pcl-create-" + Guid.NewGuid().ToString("N");
            staging = SafePath(launcher, "versions/" + stagingName);
            var sourceIndex = _services.DownloadSourceIndex();
            await Task.Run(() =>
            {
                var minecraft = request.MinecraftManifestUrl is { } url
                    ? new FrontendInstallChoice("minecraft:" + request.MinecraftVersion, request.MinecraftVersion, "", request.MinecraftVersion, FrontendInstallChoiceKind.Minecraft, ManifestUrl: url)
                    : FrontendInstallWorkflowService.GetMinecraftCatalogChoices(request.MinecraftVersion, sourceIndex)
                        .FirstOrDefault(item => item.Version == request.MinecraftVersion)
                        ?? throw new ExtensionOperationException("version.not-found", "The requested Minecraft version is unavailable.");
                linked.Token.ThrowIfCancellationRequested();
                FrontendInstallChoice? loader = null;
                if (request.Loader is { Length: > 0 })
                {
                    loader = FrontendInstallWorkflowService.GetSupportedChoices(LoaderTitle(request.Loader), request.MinecraftVersion, sourceIndex)
                        .FirstOrDefault(item => request.LoaderVersion is null || item.Version == request.LoaderVersion)
                        ?? throw new ExtensionOperationException("loader.not-found", "The requested loader version is unavailable.");
                }
                FrontendInstallWorkflowService.Apply(new(launcher, stagingName, sourceIndex, minecraft, loader,
                    null, null, null, null, null, null, true, true, false), downloadOptions: _services.DownloadOptions(), i18n: _services.Text, cancelToken: linked.Token);
                RenameInstance(staging, stagingName, request.Name);
                linked.Token.ThrowIfCancellationRequested();
                Directory.Move(staging, target);
            }, linked.Token);
            await _services.Refresh(CancellationToken.None);
            var instance = new LauncherInstance(target, request.Name, target, request.MinecraftVersion, false);
            _services.Publish(ExtensionTopics.InstanceCreated, instance.Id);
            _services.Publish(ExtensionTopics.InstanceInstalled, instance.Id, true);
            return instance;
        }
        finally
        {
            try { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true); }
            finally { _services.Mutation.Release(); }
        }
    }

    public async ValueTask<LauncherInstance> CloneAsync(ExtensionInstanceCloneRequest request, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        await _services.Mutation.WaitAsync(linked.Token);
        string? staging = null;
        try
        {
            await CheckMutationAsync();
            var source = await GetAsync(request.SourceInstanceId, linked.Token);
            var launcher = Path.GetDirectoryName(Path.GetDirectoryName(source.Instance.Directory))!;
            var target = NewInstancePath(launcher, request.Name);
            staging = Path.Combine(launcher, "versions", ".pcl-clone-" + Guid.NewGuid().ToString("N"));
            await Task.Run(() =>
            {
                CopyTree(source.Instance.Directory, staging, linked.Token);
                if (!PathsEqual(source.GameDirectory, source.Instance.Directory))
                    foreach (var name in new[] { "mods", "resourcepacks", "shaderpacks", "saves", "config", "screenshots", "options.txt", "servers.dat" })
                    {
                        var path = Path.Combine(source.GameDirectory, name);
                        if (Directory.Exists(path)) CopyTree(path, Path.Combine(staging, name), linked.Token);
                        else if (File.Exists(path)) { RejectLink(path); File.Copy(path, Path.Combine(staging, name), true); }
                    }
                RenameInstance(staging, source.Instance.Name, request.Name);
                var config = FrontendRuntimePaths.OpenInstanceConfigProvider(staging);
                config.Set("VersionArgumentIndieV2", true); config.Sync();
                linked.Token.ThrowIfCancellationRequested();
                Directory.Move(staging, target);
            }, linked.Token);
            await _services.Refresh(CancellationToken.None);
            _services.Publish(ExtensionTopics.InstanceCreated, target);
            _services.Publish(ExtensionTopics.InstanceInstalled, target, true);
            return new(target, request.Name, target, source.Instance.MinecraftVersion, false);
        }
        finally
        {
            try { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, true); }
            finally { _services.Mutation.Release(); }
        }
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        await _services.Refresh(linked.Token);
    }

    public async ValueTask<string> RemoveAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        await _services.Mutation.WaitAsync(linked.Token);
        try
        {
            await CheckMutationAsync();
            var instance = await GetAsync(instanceId, linked.Token);
            RejectLink(instance.Instance.Directory);
            var launcher = Path.GetDirectoryName(Path.GetDirectoryName(instance.Instance.Directory))!;
            var retained = SafePath(launcher, "PCL/RemovedInstances/" + Guid.NewGuid().ToString("N") + "-" + instance.Instance.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(retained)!);
            linked.Token.ThrowIfCancellationRequested();
            Directory.Move(instance.Instance.Directory, retained);
            await _services.Refresh(CancellationToken.None);
            _services.Publish(ExtensionTopics.InstanceRemoved, instanceId);
            return retained;
        }
        finally { _services.Mutation.Release(); }
    }

    private ValueTask CheckMutationAsync()
    {
        _context.Lifetime.ThrowIfCancellationRequested();
        return _services.CheckMutation(_context);
    }
    private ValueTask<string> LauncherDirectoryAsync() => _services.LauncherDirectory();
    private static string NewInstancePath(string launcher, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.') || name != name.Trim() || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new ArgumentException("An instance name must be a single valid directory name.");
        var path = Path.Combine(launcher, "versions", name);
        SafePath(launcher, "versions/" + name);
        if (File.Exists(path) || Directory.Exists(path)) throw new ExtensionOperationException("instance.exists", "An instance with this name already exists.");
        return path;
    }
    private static void RenameInstance(string directory, string previousName, string name)
    {
        var previousManifest = Path.Combine(directory, previousName + ".json");
        var manifest = JsonNode.Parse(File.ReadAllText(previousManifest))!.AsObject();
        manifest["id"] = name;
        if (manifest["jar"]?.GetValue<string>() == previousName) manifest["jar"] = name;
        File.WriteAllText(Path.Combine(directory, name + ".json"), manifest.ToJsonString());
        File.Delete(previousManifest);
        var previousJar = Path.Combine(directory, previousName + ".jar");
        if (File.Exists(previousJar)) File.Move(previousJar, Path.Combine(directory, name + ".jar"));
    }

    private static void CopyTree(string source, string destination, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); RejectLink(source);
        Directory.CreateDirectory(destination);
        foreach (var path in Directory.EnumerateFileSystemEntries(source))
        {
            token.ThrowIfCancellationRequested(); RejectLink(path);
            var target = Path.Combine(destination, Path.GetFileName(path));
            if (Directory.Exists(path)) CopyTree(path, target, token);
            else File.Copy(path, target, true);
        }
    }
    private static string HashFile(string path, HashAlgorithm algorithm, CancellationToken token)
    {
        using (algorithm)
        using (var stream = File.OpenRead(path))
        {
            var buffer = new byte[81920];
            int count;
            while ((count = stream.Read(buffer)) > 0) { token.ThrowIfCancellationRequested(); algorithm.TransformBlock(buffer, 0, count, buffer, 0); }
            algorithm.TransformFinalBlock([], 0, 0);
            return Convert.ToHexString(algorithm.Hash!).ToLowerInvariant();
        }
    }
    private static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new ExtensionOperationException("path.link", "Managed extension operations cannot traverse symbolic links.");
    }
    private static bool PathsEqual(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string LoaderTitle(string loader) => loader.ToLowerInvariant() switch
    {
        "fabric" => "Fabric", "legacyfabric" => "Legacy Fabric", "forge" => "Forge", "neoforge" => "NeoForge",
        "quilt" => "Quilt", "cleanroom" => "Cleanroom", "labymod" => "LabyMod", _ => throw new ArgumentException("Unsupported loader.")
    };
}
