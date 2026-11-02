using System.Buffers;
using System.Diagnostics;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed class FrontendDownloadSpeedLimiter
{
    private readonly object _syncRoot = new();
    private readonly long _bytesPerSecond;
    private double _availableBytes;
    private long _lastTimestamp;

    public FrontendDownloadSpeedLimiter(long bytesPerSecond)
    {
        _bytesPerSecond = Math.Max(1L, bytesPerSecond);
        _availableBytes = _bytesPerSecond;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    public async ValueTask<int> ReserveAsync(int desiredBytes, CancellationToken cancelToken = default)
    {
        if (desiredBytes <= 0)
        {
            return 0;
        }

        while (true)
        {
            TimeSpan delay;
            lock (_syncRoot)
            {
                Refill_NoLock();
                if (_availableBytes >= 1d)
                {
                    var grantedBytes = (int)Math.Min(
                        desiredBytes,
                        Math.Max(1d, Math.Floor(_availableBytes)));
                    _availableBytes -= grantedBytes;
                    return grantedBytes;
                }

                delay = TimeSpan.FromSeconds((1d - _availableBytes) / _bytesPerSecond);
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancelToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    public void Refund(int bytes)
    {
        if (bytes <= 0)
        {
            return;
        }

        lock (_syncRoot)
        {
            Refill_NoLock();
            _availableBytes = Math.Min(_bytesPerSecond, _availableBytes + bytes);
        }
    }

    private void Refill_NoLock()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedSeconds = (now - _lastTimestamp) / (double)Stopwatch.Frequency;
        if (elapsedSeconds > 0d)
        {
            _availableBytes = Math.Min(_bytesPerSecond, _availableBytes + elapsedSeconds * _bytesPerSecond);
            _lastTimestamp = now;
        }
    }
}

internal static class FrontendDownloadTransferService
{
    private const int DefaultBufferSize = 81920;
    private static readonly TimeSpan DefaultStalledTransferTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan DefaultInitialStalledTransferTimeout = TimeSpan.FromSeconds(30);
    private const int DefaultMaxAttempts = 3;

    public sealed record RetrySnapshot(
        int NextAttempt,
        int MaxAttempts,
        string Url,
        Exception LastError);

    public static TimeSpan ResolveDownloadHttpClientTimeout(TimeSpan? configuredTransferTimeout)
    {
        if (configuredTransferTimeout is { } timeout && timeout > DefaultInitialStalledTransferTimeout)
        {
            return timeout;
        }

        return DefaultInitialStalledTransferTimeout;
    }

    public static void DownloadToPath(
        HttpClient httpClient,
        string url,
        string outputPath,
        Action<long>? onProgress = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        TimeSpan? stalledTransferTimeout = null,
        int maxAttempts = DefaultMaxAttempts,
        Action<RetrySnapshot>? onRetry = null,
        CancellationToken cancelToken = default)
    {
        DownloadToPathAsync(
                httpClient,
                url,
                outputPath,
                onProgress,
                speedLimiter,
                stalledTransferTimeout,
                maxAttempts,
                onRetry,
                cancelToken)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task DownloadToPathAsync(
        HttpClient httpClient,
        string url,
        string outputPath,
        Action<long>? onProgress = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        TimeSpan? stalledTransferTimeout = null,
        int maxAttempts = DefaultMaxAttempts,
        Action<RetrySnapshot>? onRetry = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var attempts = Math.Max(1, maxAttempts);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
                await CopyToPathAsync(
                        source,
                        outputPath,
                        onProgress,
                        speedLimiter,
                        stalledTransferTimeout,
                        cancelToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                lastError = ex;
                onRetry?.Invoke(new RetrySnapshot(attempt + 1, attempts, url, ex));
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Unable to download file: {url}", lastError);
    }

    public static async Task CopyToPathAsync(
        Stream source,
        string outputPath,
        Action<long>? onProgress = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        TimeSpan? stalledTransferTimeout = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var target = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, DefaultBufferSize, true);
        await CopyAsync(source, target, onProgress, speedLimiter, stalledTransferTimeout, cancelToken).ConfigureAwait(false);
    }

    public static async Task CopyAsync(
        Stream source,
        Stream target,
        Action<long>? onProgress = null,
        FrontendDownloadSpeedLimiter? speedLimiter = null,
        TimeSpan? stalledTransferTimeout = null,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        var effectiveStallTimeout = NormalizeStalledTransferTimeout(stalledTransferTimeout);
        var initialStallTimeout = ResolveInitialStalledTransferTimeout(effectiveStallTimeout);
        long transferredBytes = 0;
        try
        {
            while (true)
            {
                var requestedBytes = speedLimiter is null
                    ? DefaultBufferSize
                    : await speedLimiter.ReserveAsync(DefaultBufferSize, cancelToken).ConfigureAwait(false);
                var read = await ReadWithStallTimeoutAsync(
                        source,
                        buffer.AsMemory(0, requestedBytes),
                        transferredBytes == 0 ? initialStallTimeout : effectiveStallTimeout,
                        cancelToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    speedLimiter?.Refund(requestedBytes);
                    break;
                }

                if (read < requestedBytes)
                {
                    speedLimiter?.Refund(requestedBytes - read);
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancelToken).ConfigureAwait(false);
                transferredBytes += read;
                onProgress?.Invoke(transferredBytes);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static TimeSpan? NormalizeStalledTransferTimeout(TimeSpan? timeout)
    {
        return timeout is { } value && value > TimeSpan.Zero
            ? value
            : DefaultStalledTransferTimeout;
    }

    private static TimeSpan ResolveInitialStalledTransferTimeout(TimeSpan? stalledTransferTimeout)
    {
        if (stalledTransferTimeout is not { } timeout)
        {
            return DefaultInitialStalledTransferTimeout;
        }

        return timeout > DefaultInitialStalledTransferTimeout
            ? timeout
            : DefaultInitialStalledTransferTimeout;
    }

    private static async ValueTask<int> ReadWithStallTimeoutAsync(
        Stream source,
        Memory<byte> buffer,
        TimeSpan? stalledTransferTimeout,
        CancellationToken cancelToken)
    {
        if (stalledTransferTimeout is not { } timeout)
        {
            return await source.ReadAsync(buffer, cancelToken).ConfigureAwait(false);
        }

        using var stallCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        stallCancellation.CancelAfter(timeout);
        try
        {
            return await source.ReadAsync(buffer, stallCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancelToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Download stalled for {timeout.TotalSeconds:0.#} seconds without receiving data.");
        }
    }
}
