using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.Logging;

namespace PCL.Frontend.Avalonia.Workflows;

/// <summary>
/// Keeps slider values available in memory while coalescing durable file writes.
/// UI interactions explicitly flush on completion; keyboard changes also save
/// after a short quiet period.
/// </summary>
internal sealed class FrontendDeferredSettingsWriter
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(250);
    private readonly object _sync = new();
    private readonly HashSet<CommonFileProvider> _pending = [];
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background);

    public FrontendDeferredSettingsWriter()
    {
        _timer.Tick += (_, _) =>
        {
            try
            {
                Flush();
            }
            catch (Exception ex)
            {
                // Keep failed providers pending so a later flush can retry.
                LogWrapper.Warn(ex, "Failed to save pending slider settings.");
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Start();
            }
        };
    }

    public void SetDeferred<T>(CommonFileProvider provider, string key, T value)
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (_sync)
        {
            provider.Set(key, value);
            _pending.Add(provider);
        }
        RestartTimer();
    }

    public void SetDeferred<T>(string path, Func<CommonFileProvider> providerFactory, string key, T value)
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (_sync)
        {
            // Instance providers are normally opened afresh. Reuse the pending
            // provider during a drag so earlier edits in this file are retained.
            var provider = FindPending(path) ?? providerFactory();
            provider.Set(key, value);
            _pending.Add(provider);
        }
        RestartTimer();
    }

    public void Sync(CommonFileProvider provider)
    {
        lock (_sync)
        {
            provider.Sync();
            _pending.Remove(provider);
        }
        StopTimerIfSaved();
    }

    public void UpdateAndSync(string path, Func<CommonFileProvider> providerFactory, Action<CommonFileProvider> update)
    {
        lock (_sync)
        {
            var provider = FindPending(path) ?? providerFactory();
            update(provider);
            provider.Sync();
            _pending.Remove(provider);
        }
        StopTimerIfSaved();
    }

    public void Flush()
    {
        lock (_sync)
        {
            foreach (var provider in _pending.ToArray())
            {
                provider.Sync();
                _pending.Remove(provider);
            }
        }
        StopTimerIfSaved();
    }

    private CommonFileProvider? FindPending(string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return _pending.FirstOrDefault(provider => string.Equals(provider.FilePath, path, comparison));
    }

    private void RestartTimer()
    {
        _timer.Stop();
        _timer.Interval = SaveDelay;
        _timer.Start();
    }

    private void StopTimerIfSaved()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(StopTimerIfSaved);
            return;
        }

        lock (_sync)
        {
            if (_pending.Count == 0) _timer.Stop();
        }
    }
}
