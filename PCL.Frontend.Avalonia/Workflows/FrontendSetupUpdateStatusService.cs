using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendSetupUpdateStatusService
{
    private const string GithubReleaseBaseUrl = "https://github.com/TheUnknownThing/PCL-CE/releases";
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static FrontendSetupUpdateStatus CreateDefault()
    {
        var currentVersion = ReadCurrentVersion();
        return new FrontendSetupUpdateStatus(
            UpdateSurfaceState.Checking,
            CurrentVersionName: $"PCL-ME {FormatVersionName(currentVersion.VersionName)}",
            CurrentVersionDescription: "正在检查更新...",
            AvailableUpdateName: string.Empty,
            AvailableUpdatePublisher: string.Empty,
            AvailableUpdateSummary: string.Empty,
            AvailableUpdateSource: string.Empty,
            AvailableUpdateSha256: string.Empty,
            AvailableUpdateChangelog: null,
            AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
            AvailableUpdateDownloadUrl: null);
    }

    public static FrontendSetupUpdateStatus CreateChecking()
    {
        var currentVersion = ReadCurrentVersion();
        return new FrontendSetupUpdateStatus(
            UpdateSurfaceState.Checking,
            CurrentVersionName: $"PCL-ME {FormatVersionName(currentVersion.VersionName)}",
            CurrentVersionDescription: "正在检查更新...",
            AvailableUpdateName: string.Empty,
            AvailableUpdatePublisher: string.Empty,
            AvailableUpdateSummary: string.Empty,
            AvailableUpdateSource: string.Empty,
            AvailableUpdateSha256: string.Empty,
            AvailableUpdateChangelog: null,
            AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
            AvailableUpdateDownloadUrl: null);
    }

    public static async Task<FrontendSetupUpdateStatus> QueryAsync(
        int selectedUpdateChannelIndex,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = ReadCurrentVersion();

        try
        {
            var channel = ResolveChannel(currentVersion.IsBeta, selectedUpdateChannelIndex);
            var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? UpdateArchitecture.Arm64
                : UpdateArchitecture.X64;
            var latestVersion = await QueryLatestVersionAsync(channel, architecture, cancellationToken);
            var currentSemVer = SemVer.Parse(currentVersion.VersionName);
            var latestSemVer = SemVer.Parse(latestVersion.VersionName);
            var hasUpdate = latestSemVer > currentSemVer
                            || latestSemVer == currentSemVer && latestVersion.VersionCode > currentVersion.VersionCode;

            if (hasUpdate)
            {
                return new FrontendSetupUpdateStatus(
                    UpdateSurfaceState.Available,
                    CurrentVersionName: $"PCL-ME {FormatVersionName(currentVersion.VersionName)}",
                    CurrentVersionDescription: $"发现新版本：{FormatVersionName(latestVersion.VersionName)}",
                    AvailableUpdateName: $"PCL-ME {FormatVersionName(latestVersion.VersionName)}",
                    AvailableUpdatePublisher: $"by PCL-ME Contributors • {latestVersion.SourceName}",
                    AvailableUpdateSummary: ExtractSummary(latestVersion.Changelog),
                    AvailableUpdateSource: latestVersion.SourceName,
                    AvailableUpdateSha256: latestVersion.Sha256,
                    AvailableUpdateChangelog: latestVersion.Changelog,
                    AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(latestVersion.VersionName),
                    AvailableUpdateDownloadUrl: latestVersion.DownloadUrl);
            }

            return new FrontendSetupUpdateStatus(
                UpdateSurfaceState.Latest,
                CurrentVersionName: $"PCL-ME {FormatVersionName(currentVersion.VersionName)}",
                CurrentVersionDescription: "已是最新版本",
                AvailableUpdateName: string.Empty,
                AvailableUpdatePublisher: string.Empty,
                AvailableUpdateSummary: string.Empty,
                AvailableUpdateSource: latestVersion.SourceName,
                AvailableUpdateSha256: latestVersion.Sha256,
                AvailableUpdateChangelog: latestVersion.Changelog,
                AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
                AvailableUpdateDownloadUrl: latestVersion.DownloadUrl);
        }
        catch (Exception ex)
        {
            return new FrontendSetupUpdateStatus(
                UpdateSurfaceState.Error,
                CurrentVersionName: $"PCL-ME {FormatVersionName(currentVersion.VersionName)}",
                CurrentVersionDescription: $"检查更新时出错: {ex.Message}",
                AvailableUpdateName: string.Empty,
                AvailableUpdatePublisher: string.Empty,
                AvailableUpdateSummary: string.Empty,
                AvailableUpdateSource: string.Empty,
                AvailableUpdateSha256: string.Empty,
                AvailableUpdateChangelog: null,
                AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
                AvailableUpdateDownloadUrl: null);
        }
    }

    public static string BuildVersionReleaseUrl(string versionName)
    {
        return $"{GithubReleaseBaseUrl}/v{TrimVersionPrefix(versionName)}";
    }

    private static async Task<RemoteVersionInfo> QueryLatestVersionAsync(
        UpdateChannel channel,
        UpdateArchitecture architecture,
        CancellationToken cancellationToken)
    {
        var sources = new List<Func<CancellationToken, Task<RemoteVersionInfo>>>
        {
            token => QueryMinioAsync("Pysio", "https://s3.pysio.online/pcl2-ce/", channel, architecture, token),
            token => QueryMinioAsync("Naids", "https://staticassets.naids.com/resources/pclce/", channel, architecture, token),
            token => QueryMinioAsync("GitHub", "https://github.com/PCL-Community/PCL2_CE_Server/raw/main/", channel, architecture, token)
        };

        List<Exception> failures = [];
        foreach (var source in sources)
        {
            try
            {
                return await source(cancellationToken);
            }
            catch (Exception ex)
            {
                failures.Add(ex);
            }
        }

        throw failures.Count switch
        {
            0 => new InvalidOperationException("没有可用的更新源。"),
            1 => failures[0],
            _ => new AggregateException("所有更新源均不可用。", failures)
        };
    }

    private static async Task<RemoteVersionInfo> QueryMinioAsync(
        string sourceName,
        string baseUrl,
        UpdateChannel channel,
        UpdateArchitecture architecture,
        CancellationToken cancellationToken)
    {
        var channelName = channel == UpdateChannel.Beta ? "fr" : "sr";
        var archName = architecture == UpdateArchitecture.Arm64 ? "arm64" : "x64";
        var url = $"{baseUrl}apiv2/updates/updates-{channelName}{archName}.json";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<MinioUpdateEnvelope>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"{sourceName} 返回了空结果。");
        var asset = payload.Assets?.FirstOrDefault()
            ?? throw new InvalidOperationException($"{sourceName} 没有可用更新资源。");
        if (asset.Version is null || string.IsNullOrWhiteSpace(asset.Version.Name))
        {
            throw new InvalidOperationException($"{sourceName} 版本信息不完整。");
        }

        return new RemoteVersionInfo(
            asset.Version.Name,
            asset.Version.Code,
            asset.Sha256 ?? string.Empty,
            asset.Changelog ?? string.Empty,
            asset.Downloads?.FirstOrDefault(),
            sourceName);
    }

    private static LocalVersionInfo ReadCurrentVersion()
    {
        var metadataPath = FrontendLauncherAssetLocator.GetPath("metadata.json");
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException("未找到启动器版本元数据。", metadataPath);
        }

        using var stream = File.OpenRead(metadataPath);
        var metadata = JsonSerializer.Deserialize<LauncherMetadata>(stream)
            ?? throw new InvalidOperationException("无法读取启动器版本元数据。");
        var versionName = metadata.Version.Base + (string.IsNullOrWhiteSpace(metadata.Version.Suffix) ? string.Empty : $"-{metadata.Version.Suffix}");
        return new LocalVersionInfo(
            versionName,
            metadata.Version.Code,
            IsBeta: versionName.Contains("beta", StringComparison.OrdinalIgnoreCase));
    }

    private static UpdateChannel ResolveChannel(bool currentVersionIsBeta, int selectedUpdateChannelIndex)
    {
        return currentVersionIsBeta || selectedUpdateChannelIndex == 1
            ? UpdateChannel.Beta
            : UpdateChannel.Stable;
    }

    private static string ExtractSummary(string changelog)
    {
        if (string.IsNullOrWhiteSpace(changelog))
        {
            return "开发者似乎忘记提供更新摘要了...也许你可以点击下方看看完整更新日志？";
        }

        const string summaryStart = "<summary>";
        const string summaryEnd = "</summary>";
        var startIndex = changelog.IndexOf(summaryStart, StringComparison.OrdinalIgnoreCase);
        var endIndex = changelog.IndexOf(summaryEnd, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0 && endIndex > startIndex)
        {
            var summary = changelog.Substring(startIndex + summaryStart.Length, endIndex - startIndex - summaryStart.Length).Trim();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }

        return "开发者似乎忘记提供更新摘要了...也许你可以点击下方看看完整更新日志？";
    }

    private static string FormatVersionName(string versionName)
    {
        var trimmed = TrimVersionPrefix(versionName);
        if (!trimmed.Contains('-', StringComparison.Ordinal))
        {
            return trimmed;
        }

        var separatorIndex = trimmed.IndexOf('-');
        var baseVersion = trimmed[..separatorIndex];
        var suffix = trimmed[(separatorIndex + 1)..]
            .Replace('.', ' ')
            .Replace("beta", "Beta", StringComparison.OrdinalIgnoreCase)
            .Replace("rc", "RC", StringComparison.OrdinalIgnoreCase);
        return $"{baseVersion} {suffix}";
    }

    private static string TrimVersionPrefix(string versionName)
    {
        return versionName.StartsWith('v') ? versionName[1..] : versionName;
    }

    private sealed record LocalVersionInfo(
        string VersionName,
        int VersionCode,
        bool IsBeta);

    private sealed record RemoteVersionInfo(
        string VersionName,
        int VersionCode,
        string Sha256,
        string Changelog,
        string? DownloadUrl,
        string SourceName);

    private sealed record LauncherMetadata(
        [property: JsonPropertyName("version")] LauncherMetadataVersion Version);

    private sealed record LauncherMetadataVersion(
        [property: JsonPropertyName("base")] string Base,
        [property: JsonPropertyName("suffix")] string? Suffix,
        [property: JsonPropertyName("code")] int Code);

    private sealed record MinioUpdateEnvelope(
        [property: JsonPropertyName("assets")] IReadOnlyList<MinioUpdateAsset>? Assets);

    private sealed record MinioUpdateAsset(
        [property: JsonPropertyName("version")] MinioUpdateAssetVersion? Version,
        [property: JsonPropertyName("downloads")] IReadOnlyList<string>? Downloads,
        [property: JsonPropertyName("sha256")] string? Sha256,
        [property: JsonPropertyName("changelog")] string? Changelog);

    private sealed record MinioUpdateAssetVersion(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("code")] int Code);

    private enum UpdateChannel
    {
        Stable,
        Beta
    }

    private enum UpdateArchitecture
    {
        X64,
        Arm64
    }

}
