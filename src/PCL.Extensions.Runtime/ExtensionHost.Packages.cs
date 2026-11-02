using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

public sealed partial class ExtensionHost
{
    public async ValueTask<ExtensionInfo> InstallAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _mutation.WaitAsync(cancellationToken);
        var staging = Path.Combine(_root, "Staging", Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = await Task.Run(() => ExtensionPackage.Extract(archivePath, staging), cancellationToken);
            ValidateApi(manifest);
            if (_extensions.TryGetValue(manifest.Id, out var existing))
            {
                if (existing.IsBuiltIn) throw new InvalidOperationException("Bundled extensions are updated with the launcher.");
                if (Version.Parse(manifest.Version) <= Version.Parse(existing.Manifest.Version))
                    throw new InvalidOperationException("The package must have a newer version than the installed extension.");
                if (existing.LoadContext is not null)
                {
                    var pending = Path.Combine(_root, "Pending", manifest.Id);
                    if (Directory.Exists(pending)) Directory.Delete(pending, recursive: true);
                    Directory.CreateDirectory(Path.GetDirectoryName(pending)!);
                    Directory.Move(staging, pending);
                    existing.RestartRequired = true;
                    return existing;
                }
                var backup = existing.Directory + ".backup";
                Directory.Move(existing.Directory, backup);
                try { Directory.Move(staging, existing.Directory); }
                catch { Directory.Move(backup, existing.Directory); throw; }
                Directory.Delete(backup, recursive: true);
                RemoveCommands(manifest.Id);
                existing.Manifest = manifest;
                existing.Text = new ExtensionLocalizer(existing.Directory, manifest.DefaultLocale, _locale);
                PrepareCommands(existing);
                return existing;
            }
            var destination = Path.Combine(_root, "Packages", manifest.Id);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(staging, destination);
            Add(manifest, destination, null, builtIn: false, enabledByDefault: false);
            return Get(manifest.Id);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            _mutation.Release();
            Changed?.Invoke();
        }
    }

    public async ValueTask UninstallAsync(string id, CancellationToken cancellationToken = default)
    {
        if (Get(id).IsBuiltIn) throw new InvalidOperationException("Bundled extensions can be disabled but cannot be removed.");
        await SetEnabledAsync(id, false, cancellationToken);
        await _mutation.WaitAsync(cancellationToken);
        try
        {
            var info = Get(id);
            if (info.IsBuiltIn) throw new InvalidOperationException("Bundled extensions can be disabled but cannot be removed.");
            if (info.LoadContext is not null)
            {
                _state.Set("remove." + id, true);
                info.RemovalPending = true;
                info.RestartRequired = true;
            }
            else
            {
                Directory.Delete(info.Directory, recursive: true);
                _extensions.Remove(id);
            }
        }
        finally { _mutation.Release(); Changed?.Invoke(); }
    }

    private void ApplyPendingPackages()
    {
        var packages = Path.Combine(_root, "Packages");
        Directory.CreateDirectory(packages);
        foreach (var directory in Directory.EnumerateDirectories(packages))
        {
            var id = Path.GetFileName(directory);
            if (!_state.Get("remove." + id, false)) continue;
            Directory.Delete(directory, recursive: true);
            _state.Set("remove." + id, false);
            var update = Path.Combine(_root, "Pending", id);
            if (Directory.Exists(update)) Directory.Delete(update, recursive: true);
        }
        var pending = Path.Combine(_root, "Pending");
        if (!Directory.Exists(pending)) return;
        foreach (var directory in Directory.EnumerateDirectories(pending))
        {
            try
            {
                var manifest = ExtensionPackage.ReadManifest(directory);
                ValidateApi(manifest);
                var destination = Path.Combine(packages, manifest.Id);
                var backup = destination + ".backup";
                if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
                if (Directory.Exists(destination)) Directory.Move(destination, backup);
                try { Directory.Move(directory, destination); }
                catch { if (Directory.Exists(backup)) Directory.Move(backup, destination); throw; }
                if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
            }
            catch (Exception ex) { Log(Path.GetFileName(directory), ExtensionLogLevel.Error, "Package update: " + ex.Message); }
        }
    }
}
