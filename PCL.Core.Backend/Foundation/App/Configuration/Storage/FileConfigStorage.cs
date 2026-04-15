using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App.Configuration.Storage;

/// <summary>
/// File-backed storage repository.
/// </summary>
public class FileConfigStorage : ConfigStorage
{
    /// <summary>
    /// Key-value file instance.
    /// </summary>
    public IKeyValueFileProvider File { get; }

    private readonly Channel<(string, Action)> _writeActionChannel;
    private readonly CancellationTokenSource _writeActionCts;
    private readonly ManualResetEventSlim _writeStopEvent = new(true);

    public FileConfigStorage(IKeyValueFileProvider file)
    {
        File = file;
        _writeActionChannel = Channel.CreateUnbounded<(string, Action)>();
        _writeActionCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            _writeStopEvent.Reset();
            const long syncInterval = 10000; // ms
            var lastSyncTick = 0L;
            var cancelToken = _writeActionCts.Token;
            var writeActionMap = new Dictionary<string, Action>();
            var reader = _writeActionChannel.Reader;
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    // Read and merge buffered operations.
                    var (key, action) = await reader.ReadAsync(cancelToken);
                    writeActionMap[key] = action;
                    if (Environment.TickCount64 - lastSyncTick < syncInterval || cancelToken.IsCancellationRequested) continue;
                    // Sync the file.
                    Sync();
                    lastSyncTick = Environment.TickCount64;
                    writeActionMap.Clear();
                }
            }
            catch (OperationCanceledException) { /* ignoring*/ }
            finally
            {
                // Perform one final sync at shutdown.
                Sync();
            }
            _writeStopEvent.Set();
            return;
            void Sync()
            {
                try
                {
                    LogWrapper.Trace("Config", $"Saving {File.FilePath}");
                    foreach (var action in writeActionMap.Values) action();
                    File.Sync();
                }
                catch (Exception ex)
                {
                    const string message = "Failed to save configuration file.";
                    LogWrapper.Error(ex, "Config", message);
                    ConfigStorageHooks.SaveFailureHandler?.Invoke(new ConfigStorageSaveFailureContext(
                        this,
                        File.FilePath,
                        message,
                        ex));
                }
            }
        });
    }

    protected override void OnStop()
    {
        _writeActionCts.Cancel();
        _writeStopEvent.Wait();
        _writeStopEvent.Dispose();
    }

    protected override bool OnAccess<TKey, TValue>(
        StorageAction action,
        ref TKey key,
        [NotNullWhen(true)] ref TValue value,
        object? argument)
    {
        if (key is not string strKey) throw new NotSupportedException($"Key '{key}' is not supported");
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
        switch (action)
        {
            case StorageAction.Get:
                if (!File.Exists(strKey)) return false;
                value = File.Get<TValue>(strKey);
                return true;
            case StorageAction.Exists:
                // Exists always uses a bool value, so unsafe assignment is valid here.
                if (typeof(TValue) == typeof(bool)) Unsafe.As<TValue, bool>(ref value) = File.Exists(strKey);
                else throw new InvalidOperationException($"Storage action '{StorageAction.Exists}' must have a boolean value.");
                return true;
            case StorageAction.Set:
                var localValue = value;
                _writeActionChannel.Writer.TryWrite((strKey, () => File.Set(strKey, localValue)));
                return false;
            case StorageAction.Delete:
                _writeActionChannel.Writer.TryWrite((strKey, () => File.Remove(strKey)));
                return false;
            default: throw new InvalidOperationException($"Invalid storage action: {action}");
        }
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
    }

    public override string ToString() => $"{base.ToString()} ({File.FilePath})";
}
