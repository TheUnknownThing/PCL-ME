using System.Collections.Concurrent;
using PCL.Extensions.Contracts;

namespace PCL.Frontend.Avalonia.Desktop.Extensions;

internal sealed partial class FrontendExtensionSession
{
    private readonly CancellationTokenSource _lifecycleLifetime = new();
    private readonly Dictionary<string, FileSystemWatcher> _lifecycleWatchers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _lifecyclePaths = new(StringComparer.Ordinal);
    private Dictionary<string, LauncherInstance> _knownInstances = new(StringComparer.Ordinal);
    private int _lifecycleRevision;
    private bool _lifecycleStarted;

    private async ValueTask StartLifecycleAsync()
    {
        _knownInstances = (await GetInstancesAsync(_lifecycleLifetime.Token)).ToDictionary(item => item.Id, StringComparer.Ordinal);
        _lifecycleStarted = true;
        RefreshLifecycleWatchers();
    }
    private void RefreshLifecycleWatchers()
    {
        if (!_lifecycleStarted || _disposed) return;
        var folders = _launcher.GetExtensionLauncherFolders();
        foreach (var removed in _lifecycleWatchers.Keys.Except(folders).ToArray())
        {
            _lifecycleWatchers[removed].Dispose();
            _lifecycleWatchers.Remove(removed);
        }
        foreach (var folder in folders)
        {
            if (_lifecycleWatchers.ContainsKey(folder)) continue;
            var watch = Directory.Exists(folder) ? folder : Path.GetDirectoryName(folder);
            if (watch is null || !Directory.Exists(watch)) continue;
            var watcher = new FileSystemWatcher(watch) { IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
            void Changed(object sender, FileSystemEventArgs args)
            {
                // macOS also reports parent-directory changes for cache writes. Reacting to those
                // would reload compositions, rewrite their caches and invalidate active editors.
                if (args.ChangeType == WatcherChangeTypes.Changed && Directory.Exists(args.FullPath)) return;
                if (!args.FullPath.StartsWith(folder + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
                var relative = Path.GetRelativePath(folder, args.FullPath).Replace('\\', '/');
                if (relative.Split('/').Any(part => part.StartsWith(".pcl-", StringComparison.Ordinal))) return;
                if (!relative.StartsWith("versions/", StringComparison.Ordinal) &&
                    !new[] { "mods/", "resourcepacks/", "shaderpacks/", "saves/", "skins/" }.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal))) return;
                // Reading compositions may update their caches; those writes are not game changes.
                if (relative.Contains("/PCL/", StringComparison.Ordinal)) return;
                _lifecyclePaths[args.FullPath] = 0;
                if (args is RenamedEventArgs renamed) _lifecyclePaths[renamed.OldFullPath] = 0;
                var revision = Interlocked.Increment(ref _lifecycleRevision);
                _ = ObserveLifecycleAsync(revision);
            }
            watcher.Created += Changed; watcher.Changed += Changed; watcher.Deleted += Changed; watcher.Renamed += (sender, args) => Changed(sender, args);
            watcher.Error += (_, args) => Host.LogContributionError("launcher", args.GetException());
            watcher.EnableRaisingEvents = true;
            _lifecycleWatchers.Add(folder, watcher);
        }
    }
    private async Task ObserveLifecycleAsync(int revision)
    {
        try
        {
            await Task.Delay(250, _lifecycleLifetime.Token);
            if (revision != Volatile.Read(ref _lifecycleRevision)) return;
            await DispatchAsync(async () =>
            {
                if (_disposed || revision != Volatile.Read(ref _lifecycleRevision)) return;
                var paths = _lifecyclePaths.Keys.Where(path => _lifecyclePaths.TryRemove(path, out _)).ToArray();
                var instances = (await GetInstancesAsync(_lifecycleLifetime.Token)).ToDictionary(item => item.Id, StringComparer.Ordinal);
                foreach (var removed in _knownInstances.Keys.Except(instances.Keys)) PublishLifecycle(ExtensionTopics.InstanceRemoved, removed);
                foreach (var (id, instance) in instances)
                {
                    if (!_knownInstances.ContainsKey(id)) { PublishLifecycle(ExtensionTopics.InstanceCreated, id); continue; }
                    var root = Path.GetDirectoryName(Path.GetDirectoryName(instance.Directory))!;
                    var relevant = paths.Where(path => path.StartsWith(instance.Directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !path.StartsWith(Path.Combine(root, "versions") + Path.DirectorySeparatorChar, StringComparison.Ordinal)).ToArray();
                    if (relevant.Length == 0) continue;
                    PublishLifecycle(ExtensionTopics.InstanceUpdated, id);
                    if (relevant.Any(path => new[] { "mods", "resourcepacks", "shaderpacks", "saves", "skins" }.Any(part =>
                            Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Contains(part))))
                        PublishLifecycle(ExtensionTopics.ResourcesChanged, id);
                }
                _knownInstances = instances;
                _launcher.RefreshExtensionInstances();
            });
        }
        catch (OperationCanceledException) when (_lifecycleLifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!_disposed) Host.LogContributionError("launcher", ex); }
    }
}
