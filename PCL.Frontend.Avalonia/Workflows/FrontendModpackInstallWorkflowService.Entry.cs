using System.IO.Compression;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendModpackInstallWorkflowService
{
    internal static string ModpackText(
        II18nService? i18n,
        string key,
        params (string Name, object? Value)[] args)
    {
        var dictionary = args.Length == 0
            ? EmptyI18nArgs
            : args.ToDictionary(arg => arg.Name, arg => arg.Value, StringComparer.Ordinal);
        var template = i18n?.T(key, dictionary) ?? GetModpackFallbackTemplate(key);
        return FormatTemplate(template, dictionary);
    }

    private static string GetModpackFallbackTemplate(string key)
    {
        return key switch
        {
            "resource_detail.modpack.workflow.status.inspecting_manifest" => "Inspecting modpack manifest...",
            "resource_detail.modpack.workflow.status.preparing_install_plan" => "Preparing instance install plan...",
            "resource_detail.modpack.workflow.status.extracting_overrides" => "Extracting modpack overrides...",
            "resource_detail.modpack.workflow.status.downloading_bundled_files" => "Downloading bundled modpack files...",
            "resource_detail.modpack.workflow.status.processing_file" => "Processing {file_name}...",
            "resource_detail.modpack.workflow.status.bundled_files_ready" => "Bundled modpack files are ready.",
            "resource_detail.modpack.workflow.status.file_ready" => "{file_name} is ready",
            "resource_detail.modpack.workflow.status.installing_game" => "Installing game and loaders...",
            "resource_detail.modpack.workflow.status.repairing_game_files" => "Completing game files...",
            "resource_detail.modpack.workflow.status.finalizing_instance" => "Finalizing instance directory...",
            "resource_detail.modpack.workflow.status.completed" => "Modpack installation completed.",
            "resource_detail.modpack.workflow.errors.unsupported_package_kind" => "Only PCL Standard, Modrinth, and CurseForge modpacks are supported for automatic installation.",
            "resource_detail.modpack.workflow.errors.modrinth_missing_minecraft_version" => "The Modrinth modpack is missing Minecraft version information.",
            "resource_detail.modpack.workflow.errors.curseforge_missing_minecraft_version" => "The CurseForge modpack is missing Minecraft version information.",
            "resource_detail.modpack.workflow.errors.curseforge_recommended_forge_unsupported" => "This CurseForge modpack uses recommended Forge, which is not supported for automatic installation yet.",
            "resource_detail.modpack.workflow.errors.curseforge_missing_files" => "Some CurseForge files referenced by the modpack no longer exist, so automatic installation cannot continue.",
            "resource_detail.modpack.workflow.errors.pcl_missing_addons" => "This PCL Standard modpack does not include the required game version addon metadata.",
            "resource_detail.modpack.workflow.errors.pcl_missing_game_version" => "This PCL Standard modpack does not include Minecraft version information.",
            "resource_detail.modpack.workflow.errors.missing_critical_file" => "The modpack is missing a required file: {path}",
            "resource_detail.modpack.workflow.errors.minecraft_choice_missing" => "No available installation plan was found for Minecraft {version}.",
            "resource_detail.modpack.workflow.errors.loader_choice_missing" => "No installation plan was found for {option_title} {requested_version}.",
            "resource_detail.modpack.workflow.errors.file_download_failed" => "Failed to download modpack file: {file_name}",
            "resource_detail.modpack.workflow.errors.curseforge_metadata_empty" => "CurseForge file metadata response was empty.",
            "resource_detail.modpack.workflow.errors.curseforge_metadata_missing_data" => "CurseForge file metadata did not include a data array.",
            "resource_detail.modpack.workflow.errors.file_path_outside_instance" => "The modpack file path escapes the instance directory: {relative_path}",
            "resource_detail.modpack.workflow.errors.archive_illegal_path" => "The modpack archive contains an invalid path: {entry_path}",
            "resource_detail.modpack.workflow.errors.rar_unsupported" => "RAR archives are not supported. Extract and recompress the modpack as a ZIP archive, then try again.",
            "resource_detail.modpack.workflow.errors.archive_open_failed" => "Failed to open the modpack archive. The file may be corrupted or use an unsupported archive format.",
            "resource_detail.modpack.workflow.errors.json_parse_failed" => "Failed to parse the JSON file inside the modpack: {entry_path}",
            "resource_detail.modpack.task.canceling" => "Canceling modpack installation...",
            "resource_detail.modpack.task.queued" => "Added to the task center",
            "resource_detail.modpack.task.completed" => "Modpack installation completed",
            "resource_detail.modpack.task.canceled" => "Modpack installation was canceled",
            "resource_detail.modpack.task.copying_archive" => "Copying modpack archive...",
            "resource_detail.modpack.task.downloading_archive" => "Downloading modpack archive...",
            "resource_detail.modpack.task.source_missing" => "The modpack source is missing.",
            _ => key
        };
    }

    private static string FormatTemplate(string template, IReadOnlyDictionary<string, object?> args)
    {
        foreach (var pair in args)
        {
            var replacement = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            template = template.Replace("{" + pair.Key + "}", replacement, StringComparison.Ordinal);
        }

        return template;
    }

    public static async Task<FrontendModpackInstallResult> InstallDownloadedArchiveAsync(
        FrontendModpackInstallRequest request,
        Action<FrontendModpackInstallStatus>? onStatusChanged = null,
        TimeSpan? requestTimeout = null,
        FrontendDownloadTransferOptions? downloadOptions = null,
        CancellationToken cancelToken = default,
        II18nService? i18n = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancelToken.ThrowIfCancellationRequested();
        var effectiveTimeout = NormalizeDownloadTimeout(requestTimeout);
        using var httpClient = CreateDownloadHttpClient(effectiveTimeout);
        var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;

        Directory.CreateDirectory(request.TargetDirectory);
        ReportStatus(onStatusChanged, 0.02, ModpackText(i18n, "resource_detail.modpack.workflow.status.inspecting_manifest"));

        var package = InspectPackage(request.ArchivePath, request.CommunitySourcePreference, httpClient, cancelToken, i18n);
        cancelToken.ThrowIfCancellationRequested();

        ReportStatus(onStatusChanged, 0.08, ModpackText(i18n, "resource_detail.modpack.workflow.status.preparing_install_plan"));
        var installRequest = BuildInstallRequest(package, request, i18n);

        var extractRoot = CreateTempDirectory("pcl-modpack-");
        var downloadedFiles = new List<string>();
        var reusedFiles = new List<string>();
        var resolvedFiles = package.Files
            .Select(file => file.Resolve(request.TargetDirectory))
            .ToArray();
        try
        {
            ReportStatus(onStatusChanged, 0.16, ModpackText(i18n, "resource_detail.modpack.workflow.status.extracting_overrides"));
            ExtractArchiveToDirectory(
                request.ArchivePath,
                extractRoot,
                progress => ReportStatus(onStatusChanged, 0.16 + progress * 0.16, ModpackText(i18n, "resource_detail.modpack.workflow.status.extracting_overrides")),
                cancelToken);

            ApplyOverrides(package, extractRoot, request.TargetDirectory);
            cancelToken.ThrowIfCancellationRequested();

            if (resolvedFiles.Length > 0)
            {
                var completedCount = 0;
                foreach (var file in resolvedFiles)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file.TargetPath);
                    var progressBase = 0.34 + completedCount / (double)resolvedFiles.Length * 0.26;
                    ReportStatus(
                        onStatusChanged,
                        progressBase,
                        string.IsNullOrWhiteSpace(fileName)
                            ? ModpackText(i18n, "resource_detail.modpack.workflow.status.downloading_bundled_files")
                            : ModpackText(i18n, "resource_detail.modpack.workflow.status.processing_file", ("file_name", fileName)),
                        RemainingFileCount: resolvedFiles.Length - completedCount);

                    if (await EnsurePackFileAsync(file, request.CommunitySourcePreference, httpClient, downloadOptions, speedLimiter, cancelToken, i18n).ConfigureAwait(false))
                    {
                        downloadedFiles.Add(file.TargetPath);
                    }
                    else
                    {
                        reusedFiles.Add(file.TargetPath);
                    }

                    completedCount += 1;
                    ReportStatus(
                        onStatusChanged,
                        0.34 + completedCount / (double)resolvedFiles.Length * 0.26,
                        string.IsNullOrWhiteSpace(fileName)
                            ? ModpackText(i18n, "resource_detail.modpack.workflow.status.bundled_files_ready")
                            : ModpackText(i18n, "resource_detail.modpack.workflow.status.file_ready", ("file_name", fileName)),
                        RemainingFileCount: resolvedFiles.Length - completedCount);
                }
            }

            cancelToken.ThrowIfCancellationRequested();
            ReportStatus(onStatusChanged, 0.62, ModpackText(i18n, "resource_detail.modpack.workflow.status.installing_game"));

            var applyResult = await Task.Run(
                () => FrontendInstallWorkflowService.Apply(
                    installRequest,
                    (phase, message) =>
                    {
                        var mappedProgress = phase switch
                        {
                            FrontendInstallApplyPhase.PrepareManifest => 0.68,
                            FrontendInstallApplyPhase.DownloadSupportFiles => 0.74,
                            FrontendInstallApplyPhase.Finalize => 0.95,
                            _ => 0.68
                        };
                        ReportStatus(onStatusChanged, mappedProgress, message);
                    },
                    snapshot =>
                    {
                        ReportStatus(
                            onStatusChanged,
                            0.74 + snapshot.Progress * 0.21,
                            string.IsNullOrWhiteSpace(snapshot.CurrentFileName)
                                ? ModpackText(i18n, "resource_detail.modpack.workflow.status.repairing_game_files")
                                : ModpackText(i18n, "resource_detail.modpack.workflow.status.processing_file", ("file_name", snapshot.CurrentFileName)),
                            snapshot.SpeedBytesPerSecond,
                            snapshot.RemainingFileCount,
                            snapshot.CurrentFileName);
                    },
                    downloadOptions,
                    i18n,
                    cancelToken),
                cancelToken).ConfigureAwait(false);

            downloadedFiles.AddRange(applyResult.DownloadedFiles);
            reusedFiles.AddRange(applyResult.ReusedFiles);

            ReportStatus(onStatusChanged, 0.97, ModpackText(i18n, "resource_detail.modpack.workflow.status.finalizing_instance"));
            FinalizeInstalledInstance(package, request);
            TryDeleteFile(request.ArchivePath);

            ReportStatus(onStatusChanged, 1d, ModpackText(i18n, "resource_detail.modpack.workflow.status.completed"));
            return new FrontendModpackInstallResult(
                request.InstanceName,
                request.TargetDirectory,
                downloadedFiles,
                reusedFiles);
        }
        finally
        {
            TryDeleteDirectory(extractRoot);
        }
    }

    public static string SuggestInstanceName(string archivePath)
    {
        var fallbackName = Path.GetFileNameWithoutExtension(archivePath);
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return fallbackName;
        }

        try
        {
            using var archive = OpenArchiveRead(archivePath);
            var (kind, baseFolder) = DetectPackageKind(archive);
            var entryPath = kind switch
            {
                FrontendModpackPackageKind.Modrinth => baseFolder + "modrinth.index.json",
                FrontendModpackPackageKind.CurseForge => baseFolder + "manifest.json",
                FrontendModpackPackageKind.Mcbbs => ResolveMcbbsMetadataEntryPath(archive, baseFolder, i18n: null),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(entryPath))
            {
                return fallbackName;
            }

            var root = ReadJsonObjectFromEntry(archive, entryPath);
            var packageName = root["name"]?.GetValue<string>()?.Trim();
            return string.IsNullOrWhiteSpace(packageName) ? fallbackName : packageName;
        }
        catch
        {
            return fallbackName;
        }
    }
    private static void ReportStatus(
        Action<FrontendModpackInstallStatus>? callback,
        double progress,
        string message,
        double? speedBytesPerSecond = null,
        int? RemainingFileCount = null,
        string? currentFileName = null)
    {
        callback?.Invoke(new FrontendModpackInstallStatus(
            Math.Clamp(progress, 0d, 1d),
            message,
            speedBytesPerSecond,
            RemainingFileCount,
            currentFileName));
    }

    private static string CreateTempDirectory(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout)
    {
        var safeTimeout = FrontendDownloadTransferService.ResolveDownloadHttpClientTimeout(timeout);
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            safeTimeout,
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
    }

    private static TimeSpan NormalizeDownloadTimeout(TimeSpan? requestTimeout)
    {
        var timeout = requestTimeout ?? TimeSpan.FromSeconds(8);
        if (timeout <= TimeSpan.Zero)
        {
            return TimeSpan.FromSeconds(8);
        }

        return timeout;
    }

    internal static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    internal static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}

internal sealed class FrontendManagedModpackInstallTask(
    string title,
    FrontendModpackInstallRequest request,
    TimeSpan requestTimeout,
    FrontendDownloadTransferOptions? downloadOptions = null,
    Action<string>? onStarted = null,
    Action<FrontendModpackInstallResult>? onCompleted = null,
    Action<string>? onFailed = null,
    II18nService? i18n = null) : PCL.Core.App.Tasks.ITask, PCL.Core.App.Tasks.ITaskProgressive, PCL.Core.App.Tasks.ITaskProgressStatus, PCL.Core.App.Tasks.ITaskCancelable
{
    private readonly CancellationTokenSource _cancellation = new();
    private double _progress;
    private PCL.Core.App.Tasks.TaskProgressStatusSnapshot _progressStatus = new("0%", "0 B/s", 1, null);

    public string Title { get; } = title;

    public PCL.Core.App.Tasks.TaskProgressStatusSnapshot ProgressStatus => _progressStatus;

    public event PCL.Core.App.Tasks.TaskStateEvent StateChanged = delegate { };

    public event PCL.Core.App.Tasks.TaskProgressEvent ProgressChanged = delegate { };

    public event PCL.Core.App.Tasks.TaskProgressStatusEvent ProgressStatusChanged = delegate { };

    public void Cancel()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Cancel();
        PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.canceling"));
    }

    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _cancellation.Token);
        var token = linkedCts.Token;
        PublishState(PCL.Core.App.Tasks.TaskState.Waiting, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.queued"));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(request.ArchivePath)!);
            onStarted?.Invoke(request.ArchivePath);
            await PrepareArchiveAsync(token, requestTimeout).ConfigureAwait(false);

            var result = await FrontendModpackInstallWorkflowService.InstallDownloadedArchiveAsync(
                request,
                status => UpdateFromInstallStatus(status),
                requestTimeout,
                downloadOptions,
                token,
                i18n).ConfigureAwait(false);

            PublishProgress(1d);
            PublishProgressStatus(new PCL.Core.App.Tasks.TaskProgressStatusSnapshot("100%", "0 B/s", 0, null));
            PublishState(PCL.Core.App.Tasks.TaskState.Success, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.completed"));
            onCompleted?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            CleanupFailedInstall();
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    "0 B/s",
                    _progressStatus.RemainingFileCount,
                    null));
            PublishState(PCL.Core.App.Tasks.TaskState.Canceled, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.canceled"));
        }
        catch (Exception ex)
        {
            CleanupFailedInstall();
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero):0.#}%",
                    "0 B/s",
                    _progressStatus.RemainingFileCount,
                    null));
            PublishState(PCL.Core.App.Tasks.TaskState.Failed, ex.Message);
            onFailed?.Invoke(ex.Message);
        }
    }

    private async Task PrepareArchiveAsync(CancellationToken token, TimeSpan timeout)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceArchivePath))
        {
            PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.copying_archive"));
            await CopyArchiveAsync(request.SourceArchivePath!, token).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            PublishState(PCL.Core.App.Tasks.TaskState.Running, FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.downloading_archive"));
            await DownloadArchiveAsync(request.SourceUrl!, token, timeout).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException(FrontendModpackInstallWorkflowService.ModpackText(i18n, "resource_detail.modpack.task.source_missing"));
    }

    private async Task DownloadArchiveAsync(string sourceUrl, CancellationToken token, TimeSpan timeout)
    {
        using var client = CreateDownloadHttpClient(timeout);
        var speedLimiter = downloadOptions?.MaxBytesPerSecond is long speedLimit
            ? new FrontendDownloadSpeedLimiter(speedLimit)
            : null;
        var stalledTransferTimeout = downloadOptions?.StalledTransferTimeout ?? timeout;
        var maxAttempts = Math.Max(1, downloadOptions?.MaxFileDownloadAttempts ?? 3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var sourceStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                var contentLength = response.Content.Headers.ContentLength.GetValueOrDefault();
                var lastReportedBytes = 0L;
                var lastReportedAt = Environment.TickCount64;
                await FrontendDownloadTransferService.CopyToPathAsync(
                    sourceStream,
                    request.ArchivePath,
                    totalRead =>
                    {
                        var progress = contentLength > 0
                            ? Math.Clamp(totalRead / (double)contentLength, 0d, 1d)
                            : 0d;
                        PublishProgress(progress * 0.35);

                        var now = Environment.TickCount64;
                        if (now - lastReportedAt < 250)
                        {
                            return;
                        }

                        var elapsedSeconds = Math.Max((now - lastReportedAt) / 1000d, 0.001d);
                        var speed = (totalRead - lastReportedBytes) / elapsedSeconds;
                        PublishProgressStatus(
                            new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                                speed > 0d ? $"{FormatBytes(speed)}/s" : "0 B/s",
                                1,
                                null));
                        lastReportedBytes = totalRead;
                        lastReportedAt = now;
                    },
                    speedLimiter,
                    stalledTransferTimeout,
                    token).ConfigureAwait(false);
                break;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                PublishState(PCL.Core.App.Tasks.TaskState.Running, $"Archive download failed; retrying ({attempt + 1}/{maxAttempts}): {ex.Message}");
                PublishProgressStatus(
                    new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                        $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                        "0 B/s",
                        1,
                        null));
            }
        }

        PublishProgressStatus(
            new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                "0 B/s",
                1,
                null));
    }

    private async Task CopyArchiveAsync(string sourcePath, CancellationToken token)
    {
        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var targetStream = new FileStream(request.ArchivePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var totalLength = sourceStream.Length;
        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var read = await sourceStream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await targetStream.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            totalRead += read;

            var progress = totalLength > 0
                ? Math.Clamp(totalRead / (double)totalLength, 0d, 1d)
                : 1d;
            PublishProgress(progress * 0.35);
            PublishProgressStatus(
                new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                    $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                    "0 B/s",
                    1,
                    null));
        }
    }

    private static HttpClient CreateDownloadHttpClient(TimeSpan timeout)
    {
        var safeTimeout = FrontendDownloadTransferService.ResolveDownloadHttpClientTimeout(timeout);
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            safeTimeout,
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli);
    }

    private void UpdateFromInstallStatus(FrontendModpackInstallStatus status)
    {
        PublishProgress(0.35 + status.Progress * 0.65);
        PublishProgressStatus(
            new PCL.Core.App.Tasks.TaskProgressStatusSnapshot(
                $"{Math.Round(_progress * 100, 1, MidpointRounding.AwayFromZero)}%",
                status.SpeedBytesPerSecond is > 0d
                    ? $"{FormatBytes(status.SpeedBytesPerSecond.Value)}/s"
                    : "0 B/s",
                status.RemainingFileCount,
                null));
        PublishState(PCL.Core.App.Tasks.TaskState.Running, status.Message);
    }

    private void CleanupFailedInstall()
    {
        FrontendModpackInstallWorkflowService.TryDeleteFile(request.ArchivePath);
        FrontendModpackInstallWorkflowService.TryDeleteDirectory(request.TargetDirectory);
    }

    private void PublishState(PCL.Core.App.Tasks.TaskState state, string message)
    {
        StateChanged(state, message);
    }

    private void PublishProgress(double value)
    {
        _progress = Math.Clamp(value, 0d, 1d);
        ProgressChanged(_progress);
    }

    private void PublishProgressStatus(PCL.Core.App.Tasks.TaskProgressStatusSnapshot snapshot)
    {
        _progressStatus = snapshot;
        ProgressStatusChanged(snapshot);
    }

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(bytes, 0d);
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex += 1;
        }

        return unitIndex == 0 ? $"{Math.Round(value):0} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }

}
