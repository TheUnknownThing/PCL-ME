using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{

    private static readonly string[] CommunityProjectKnownLoaders = ["Forge", "NeoForge", "Fabric", "Quilt", "OptiFine", "Iris"];

    private static readonly string[] CommunityProjectReservedInstanceNames =
    [
        "CON", "PRN", "AUX", "CLOCK$", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "LPT¹", "LPT²", "LPT³"
    ];

    private const string CommunityProjectInstallModeCurrentOnlyValue = "current-only";

    private const string CommunityProjectInstallModeWithDependenciesValue = "with-dependencies";

    private static readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectModInstallModeOptions =
    [
        new DownloadResourceFilterOptionViewModel("resource_detail.install_modes.current_only", CommunityProjectInstallModeCurrentOnlyValue),
        new DownloadResourceFilterOptionViewModel("resource_detail.install_modes.with_dependencies", CommunityProjectInstallModeWithDependenciesValue)
    ];

    private static readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> CommunityProjectSingleInstallModeOptions =
    [
        new DownloadResourceFilterOptionViewModel("resource_detail.install_modes.single_resource", CommunityProjectInstallModeCurrentOnlyValue)
    ];

    private string _communityProjectDependencyReleaseTitle = string.Empty;

    private sealed record CommunityProjectNavigationState(
        string ProjectId,
        string TitleHint,
        LauncherFrontendSubpageKey? OriginSubpage,
        string VersionFilter,
        string LoaderFilter)
    {
        public string TitleHintOrProjectId => string.IsNullOrWhiteSpace(TitleHint) ? ProjectId : TitleHint;
    }

}



internal sealed class FrontendManagedFileDownloadTask(
    string title,
    string sourceUrl,
    string targetPath,
    TimeSpan requestTimeout,
    FrontendDownloadTransferOptions? downloadOptions = null,
    Action<string>? onStarted = null,
    Action<string>? onCompleted = null,
    Action<string>? onFailed = null,
    string? userAgent = null) : ITask, ITaskProgressive, ITaskProgressStatus, ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", 1, null);

    public string Title { get; } = title;

    public TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event TaskStateEvent StateChanged = delegate { };

    public event TaskProgressEvent ProgressChanged = delegate { };

    public event TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        StateChanged(TaskState.Running, "Canceling download...");
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var token = linkedCts.Token;
        StateChanged(TaskState.Waiting, "Queued in Task Center");

        try
        {
            using var client = CreateDownloadHttpClient(requestTimeout, userAgent);
            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
                ? new FrontendDownloadSpeedLimiter(speedLimit)
                : null;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using (var sourceStream = await response.Content.ReadAsStreamAsync(token))
            {
                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
                var lastReportedBytes = 0L;
                var lastReportedAt = Environment.TickCount64;
                StateChanged(TaskState.Running, "Downloading file...");
                onStarted?.Invoke(targetPath);

                await FrontendDownloadTransferService.CopyToPathAsync(
                    sourceStream,
                    targetPath,
                    totalRead =>
                    {
                        var progress = contentLength > 0
                            ? Math.Clamp(totalRead / (double)contentLength, 0d, 1d)
                            : 0d;
                        ProgressChanged(progress);

                        var now = Environment.TickCount64;
                        if (now - lastReportedAt < 250)
                        {
                            return;
                        }

                        var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                        var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                        PublishProgressStatus(progress, speed);
                        lastReportedAt = now;
                        lastReportedBytes = totalRead;
                        StateChanged(TaskState.Running, $"Downloading {Path.GetFileName(targetPath)}...");
                    },
                    speedLimiter,
                    token);
            }

            ProgressChanged(1d);
            PublishProgressStatus(1d, 0d, 0);
            StateChanged(TaskState.Success, $"Saved to {targetPath}");
            onCompleted?.Invoke(targetPath);
        }
        catch (OperationCanceledException)
        {
            CleanupPartialDownload();
            PublishProgressStatus(0d, 0d);
            StateChanged(TaskState.Canceled, "Download canceled");
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} download canceled");
            throw;
        }
        catch (Exception ex)
        {
            CleanupPartialDownload();
            PublishProgressStatus(0d, 0d);
            StateChanged(TaskState.Failed, ex.Message);
            onFailed?.Invoke($"{Path.GetFileName(targetPath)} download failed");
            throw;
        }
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout, string? userAgent)
    {
        var safeTimeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(8) : timeout;
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            safeTimeout,
            userAgent,
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
    }

    private void CleanupPartialDownload()
    {
        try
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private void PublishProgressStatus(double progress, double speedBytesPerSecond, int? remainingFileCount = 1)
    {
        _progressStatus = new TaskProgressStatusSnapshot(
            $"{Math.Round(progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
            $"{FormatBytes(speedBytesPerSecond)}/s",
            remainingFileCount,
            null);
        ProgressStatusChanged(_progressStatus);
    }

    private static string FormatBytes(double value)
    {
        var absolute = Math.Max(value, 0d);
        return absolute switch
        {
            >= 1024d * 1024d * 1024d => $"{absolute / (1024d * 1024d * 1024d):0.##} GB",
            >= 1024d * 1024d => $"{absolute / (1024d * 1024d):0.##} MB",
            >= 1024d => $"{absolute / 1024d:0.##} KB",
            _ => $"{absolute:0} B"
        };
    }

}
