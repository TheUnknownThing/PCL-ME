using System.Text.Json;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Runtime;

public sealed record ExtensionCommandInfo(string? OwnerId, ExtensionCommand Definition, bool CanExecute);

public sealed partial class ExtensionHost
{
    private sealed record CommandEntry(string? Owner, ExtensionCommand Command, Func<CancellationToken, ValueTask>? Execute, Func<bool>? CanExecute);
    private sealed record SearchEntry(string? Owner, Func<ExtensionSearchQuery, CancellationToken, ValueTask<IReadOnlyList<ExtensionSearchResult>>> Search);
    private sealed record Subscription(string Owner, string Topic, Action<ExtensionEvent> Handler);
    private readonly Dictionary<string, CommandEntry> _commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SearchEntry> _providers = new(StringComparer.Ordinal);
    private readonly List<Subscription> _subscriptions = [];
    private readonly object _registrations = new();

    public IReadOnlyList<ExtensionCommandInfo> Commands
    {
        get
        {
            lock (_registrations) return _commands.Values.Select(entry => new ExtensionCommandInfo(entry.Owner,
                entry.Command with { Shortcut = _state.Get<string?>("shortcut." + entry.Command.Id, entry.Command.Shortcut) }, CanExecute(entry))).ToArray();
        }
    }

    public void SetShortcut(string commandId, string? shortcut)
    {
        lock (_registrations) if (!_commands.ContainsKey(commandId)) throw new KeyNotFoundException(commandId);
        _state.Set("shortcut." + commandId, shortcut);
        Changed?.Invoke();
    }

    private bool CanExecute(CommandEntry entry)
    {
        if (entry.Owner is { } owner && (SafeMode || !Get(owner).IsEnabled || Get(owner).Status is ExtensionStatus.Faulted or ExtensionStatus.Activating)) return false;
        try { return entry.CanExecute?.Invoke() ?? true; }
        catch (Exception ex) { Log(entry.Owner ?? "launcher", ExtensionLogLevel.Error, ex.Message); return false; }
    }

    private void PrepareCommands(ExtensionInfo info)
    {
        if (!info.IsEnabled) return;
        lock (_registrations)
        {
            foreach (var command in info.Manifest.Commands)
                if (!_commands.ContainsKey(command.Id)) _commands.Add(command.Id, new(info.Manifest.Id, command, null, null));
        }
    }

    private void RemoveCommands(string owner)
    {
        lock (_registrations)
            foreach (var id in _commands.Where(pair => pair.Value.Owner == owner).Select(pair => pair.Key).ToArray()) _commands.Remove(id);
    }

    public IDisposable RegisterHostCommand(ExtensionCommand command, Func<CancellationToken, ValueTask> execute, Func<bool>? canExecute = null)
        => RegisterCommand(null, command, execute, canExecute);

    internal IDisposable RegisterCommand(string? owner, ExtensionCommand command, Func<CancellationToken, ValueTask> execute, Func<bool>? canExecute)
    {
        if (owner is not null && !command.Id.StartsWith(owner + ".", StringComparison.Ordinal))
            throw new ArgumentException("Command IDs must use the extension namespace.", nameof(command));
        var entry = new CommandEntry(owner, command, execute, canExecute);
        lock (_registrations)
        {
            if (_commands.TryGetValue(command.Id, out var previous) && (previous.Owner != owner || previous.Execute is not null))
                throw new InvalidOperationException($"Command already registered: {command.Id}");
            _commands[command.Id] = entry;
        }
        return new ExtensionRegistration(() =>
        {
            lock (_registrations)
            {
                if (ReferenceEquals(_commands.GetValueOrDefault(command.Id), entry)) _commands.Remove(command.Id);
            }
        });
    }

    public async ValueTask ExecuteCommandAsync(string id, CancellationToken cancellationToken = default)
    {
        CommandEntry entry;
        lock (_registrations) entry = _commands.GetValueOrDefault(id) ?? throw new KeyNotFoundException($"Command not found: {id}");
        if (!CanExecute(entry)) throw new InvalidOperationException($"Command is unavailable: {id}");
        if (entry.Owner is { } owner)
        {
            await ActivateAsync(owner, cancellationToken);
            lock (_registrations) entry = _commands.GetValueOrDefault(id) ?? throw new InvalidOperationException($"The extension did not register {id}.");
        }
        if (entry.Execute is null) throw new InvalidOperationException($"Command has no handler: {id}");
        try
        {
            await _dispatch(async () =>
            {
                if (!CanExecute(entry)) throw new InvalidOperationException($"Command is unavailable: {id}");
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    entry.Owner is { } extensionId ? Get(extensionId).Context?.Lifetime ?? CancellationToken.None : CancellationToken.None);
                await entry.Execute(linked.Token);
            });
        }
        catch (OperationCanceledException) { throw; }
        catch (ExtensionOperationException) { throw; }
        catch (Exception ex)
        {
            var stillRegistered = false;
            lock (_registrations) stillRegistered = ReferenceEquals(_commands.GetValueOrDefault(id), entry);
            if (stillRegistered && entry.Owner is { } extensionId) await ReportFaultAsync(extensionId, ex);
            throw;
        }
    }

    public IDisposable RegisterHostSearch(string id, Func<ExtensionSearchQuery, CancellationToken, ValueTask<IReadOnlyList<ExtensionSearchResult>>> search)
        => RegisterSearch(null, id, search);

    internal IDisposable RegisterSearch(string? owner, string id, Func<ExtensionSearchQuery, CancellationToken, ValueTask<IReadOnlyList<ExtensionSearchResult>>> search)
    {
        if (owner is not null && !id.StartsWith(owner + ".", StringComparison.Ordinal)) throw new ArgumentException("Search providers must use the extension namespace.", nameof(id));
        var entry = new SearchEntry(owner, search);
        lock (_registrations) _providers.Add(id, entry);
        return new ExtensionRegistration(() => { lock (_registrations) if (ReferenceEquals(_providers.GetValueOrDefault(id), entry)) _providers.Remove(id); });
    }

    public async ValueTask<IReadOnlyList<ExtensionSearchResult>> QueryAsync(ExtensionSearchQuery query, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var text = query.Text.Trim();
        var limit = Math.Clamp(query.Limit, 1, 100);
        KeyValuePair<string, SearchEntry>[] providers;
        lock (_registrations) providers = _providers.ToArray();
        var results = await Task.WhenAll(providers.Select(async pair =>
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                // Invoke on a worker so even a synchronous provider cannot block input.
                var found = await Task.Run(async () => await pair.Value.Search(query with { Text = text, Limit = limit }, timeout.Token), timeout.Token)
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                return found.Take(100).Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Title))
                    .Select(item => item with
                    {
                        ProviderId = pair.Key,
                        Score = Rank(item, text)
                    }).ToArray();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return []; }
            catch (TimeoutException) { Log(pair.Value.Owner ?? "launcher", ExtensionLogLevel.Warning, "Search provider timed out: " + pair.Key); return []; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log(pair.Value.Owner ?? "launcher", ExtensionLogLevel.Error, "Search provider " + pair.Key + ": " + ex.Message); return []; }
        }));
        cancellationToken.ThrowIfCancellationRequested();
        return results.SelectMany(items => items)
            .Where(item => query.Kind is null || item.Kind == query.Kind)
            .OrderByDescending(item => item.Score).ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .DistinctBy(item => item.CommandId is { } command ? "command:" + command
                : item.Route is { } route && item.Arguments is null ? route + ":" + item.Title : item.ProviderId + ":" + item.Id)
            .Take(limit).ToArray();
    }

    private static double Rank(ExtensionSearchResult result, string query)
    {
        var score = double.IsFinite(result.Score) ? Math.Clamp(result.Score, -1, 1) : 0;
        if (result.Title.Equals(query, StringComparison.OrdinalIgnoreCase)) return score + 10;
        if (result.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return score + 5;
        return score + (result.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : 0);
    }

    public async ValueTask ExecuteSearchResultAsync(ExtensionSearchResult result, CancellationToken cancellationToken = default)
    {
        lock (_registrations)
            if (result.ProviderId is null || !_providers.ContainsKey(result.ProviderId)) throw new InvalidOperationException("The search provider is no longer available.");
        if (result.CommandId is { } command) await ExecuteCommandAsync(command, cancellationToken);
        else if (result.Route is { } route) await _dispatch(() => _launcher.NavigateAsync(route, result.Arguments, cancellationToken));
    }

    internal IDisposable Subscribe(string owner, string topic, Action<ExtensionEvent> handler)
    {
        var subscription = new Subscription(owner, topic, handler);
        lock (_registrations) _subscriptions.Add(subscription);
        return new ExtensionRegistration(() => { lock (_registrations) _subscriptions.Remove(subscription); });
    }

    public void PublishHostEvent(string topic, JsonElement data)
    {
        Subscription[] listeners;
        lock (_registrations) listeners = _subscriptions.Where(item => item.Topic == topic).ToArray();
        foreach (var listener in listeners)
        {
            try { listener.Handler(new(topic, data)); }
            catch (Exception ex) { Log(listener.Owner, ExtensionLogLevel.Error, "Event handler: " + ex.Message); }
        }
    }
}
