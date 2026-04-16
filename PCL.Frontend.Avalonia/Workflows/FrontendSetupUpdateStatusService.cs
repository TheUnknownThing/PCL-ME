using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Core.Utils;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendSetupUpdateStatusService
{
    private const string GithubApiReleasesUrl = "https://api.github.com/repos/TheUnknownThing/PCL-ME/releases?per_page=20";
    private const string GithubApiVersion = "2022-11-28";
    private const string GithubApiUserAgent = "PCL-ME-Frontend-Avalonia";
    private const string GithubReleaseBaseUrl = "https://github.com/TheUnknownThing/PCL-ME/releases";
    private static readonly HttpClient HttpClient = FrontendHttpProxyService.CreateLauncherHttpClient(TimeSpan.FromSeconds(10));

    public static FrontendSetupUpdateStatus CreateDefault(II18nService i18n)
    {
        var currentVersion = ReadCurrentVersion(i18n);
        return new FrontendSetupUpdateStatus(
            UpdateSurfaceState.Checking,
            CurrentVersionName: BuildVersionDisplayName(currentVersion.VersionName, i18n),
            CurrentVersionDescription: i18n.T("setup.update.states.checking"),
            AvailableUpdateName: string.Empty,
            AvailableUpdatePublisher: string.Empty,
            AvailableUpdateSummary: string.Empty,
            AvailableUpdateSource: string.Empty,
            AvailableUpdateSha256: string.Empty,
            AvailableUpdateChangelog: null,
            AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
            AvailableUpdateDownloadUrl: null,
            CurrentVersionTag: currentVersion.VersionName,
            AvailableUpdateTag: null);
    }

    public static FrontendSetupUpdateStatus CreateChecking(II18nService i18n)
    {
        var currentVersion = ReadCurrentVersion(i18n);
        return new FrontendSetupUpdateStatus(
            UpdateSurfaceState.Checking,
            CurrentVersionName: BuildVersionDisplayName(currentVersion.VersionName, i18n),
            CurrentVersionDescription: i18n.T("setup.update.states.checking"),
            AvailableUpdateName: string.Empty,
            AvailableUpdatePublisher: string.Empty,
            AvailableUpdateSummary: string.Empty,
            AvailableUpdateSource: string.Empty,
            AvailableUpdateSha256: string.Empty,
            AvailableUpdateChangelog: null,
            AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
            AvailableUpdateDownloadUrl: null,
            CurrentVersionTag: currentVersion.VersionName,
            AvailableUpdateTag: null);
    }

    public static async Task<FrontendSetupUpdateStatus> QueryAsync(
        int selectedUpdateChannelIndex,
        II18nService i18n,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = ReadCurrentVersion(i18n);

        try
        {
            var channel = ResolveChannel(currentVersion.IsBeta, selectedUpdateChannelIndex);
            var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? UpdateArchitecture.Arm64
                : UpdateArchitecture.X64;
            var latestVersion = await QueryLatestVersionAsync(channel, architecture, i18n, cancellationToken);
            var currentSemVer = SemVer.Parse(currentVersion.VersionName);
            var latestSemVer = SemVer.Parse(latestVersion.VersionName);
            var hasUpdate = latestSemVer > currentSemVer
                            || latestSemVer == currentSemVer && latestVersion.VersionCode > currentVersion.VersionCode;

            if (hasUpdate)
            {
                return new FrontendSetupUpdateStatus(
                    UpdateSurfaceState.Available,
                    CurrentVersionName: BuildVersionDisplayName(currentVersion.VersionName, i18n),
                    CurrentVersionDescription: i18n.T(
                        "setup.update.states.available",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["version"] = FormatVersionName(latestVersion.VersionName)
                        }),
                    AvailableUpdateName: BuildVersionDisplayName(latestVersion.VersionName, i18n),
                    AvailableUpdatePublisher: i18n.T(
                        "setup.update.available.publisher",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["source"] = latestVersion.SourceName
                        }),
                    AvailableUpdateSummary: ExtractSummary(latestVersion.Changelog, i18n),
                    AvailableUpdateSource: latestVersion.SourceName,
                    AvailableUpdateSha256: latestVersion.Sha256,
                    AvailableUpdateChangelog: latestVersion.Changelog,
                    AvailableUpdateReleaseUrl: latestVersion.ReleaseUrl,
                    AvailableUpdateDownloadUrl: latestVersion.DownloadUrl,
                    CurrentVersionTag: currentVersion.VersionName,
                    AvailableUpdateTag: latestVersion.VersionName);
            }

            return new FrontendSetupUpdateStatus(
                UpdateSurfaceState.Latest,
                CurrentVersionName: BuildVersionDisplayName(currentVersion.VersionName, i18n),
                CurrentVersionDescription: i18n.T("setup.update.states.latest"),
                AvailableUpdateName: string.Empty,
                AvailableUpdatePublisher: string.Empty,
                AvailableUpdateSummary: string.Empty,
                AvailableUpdateSource: latestVersion.SourceName,
                AvailableUpdateSha256: latestVersion.Sha256,
                AvailableUpdateChangelog: latestVersion.Changelog,
                AvailableUpdateReleaseUrl: latestVersion.ReleaseUrl,
                AvailableUpdateDownloadUrl: latestVersion.DownloadUrl,
                CurrentVersionTag: currentVersion.VersionName,
                AvailableUpdateTag: latestVersion.VersionName);
        }
        catch (Exception ex)
        {
            return new FrontendSetupUpdateStatus(
                UpdateSurfaceState.Error,
                CurrentVersionName: BuildVersionDisplayName(currentVersion.VersionName, i18n),
                CurrentVersionDescription: i18n.T(
                    "setup.update.states.error",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["message"] = ex.Message
                    }),
                AvailableUpdateName: string.Empty,
                AvailableUpdatePublisher: string.Empty,
                AvailableUpdateSummary: string.Empty,
                AvailableUpdateSource: string.Empty,
                AvailableUpdateSha256: string.Empty,
                AvailableUpdateChangelog: null,
                AvailableUpdateReleaseUrl: BuildVersionReleaseUrl(currentVersion.VersionName),
                AvailableUpdateDownloadUrl: null,
                CurrentVersionTag: currentVersion.VersionName,
                AvailableUpdateTag: null);
        }
    }

    public static FrontendSetupUpdateStatus Relocalize(FrontendSetupUpdateStatus status, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(i18n);

        return status.SurfaceState switch
        {
            UpdateSurfaceState.Available when !string.IsNullOrWhiteSpace(status.AvailableUpdateTag) => status with
            {
                CurrentVersionName = BuildVersionDisplayName(status.CurrentVersionTag, i18n),
                CurrentVersionDescription = i18n.T(
                    "setup.update.states.available",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["version"] = FormatVersionName(status.AvailableUpdateTag)
                    }),
                AvailableUpdateName = BuildVersionDisplayName(status.AvailableUpdateTag, i18n),
                AvailableUpdatePublisher = i18n.T(
                    "setup.update.available.publisher",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["source"] = status.AvailableUpdateSource
                    }),
                AvailableUpdateSummary = ExtractSummary(status.AvailableUpdateChangelog, i18n)
            },
            UpdateSurfaceState.Latest => status with
            {
                CurrentVersionName = BuildVersionDisplayName(status.CurrentVersionTag, i18n),
                CurrentVersionDescription = i18n.T("setup.update.states.latest")
            },
            UpdateSurfaceState.Error => status with
            {
                CurrentVersionName = BuildVersionDisplayName(status.CurrentVersionTag, i18n)
            },
            _ => status with
            {
                CurrentVersionName = BuildVersionDisplayName(status.CurrentVersionTag, i18n),
                CurrentVersionDescription = i18n.T("setup.update.states.checking")
            }
        };
    }

    public static string BuildVersionReleaseUrl(string versionName)
    {
        return $"{GithubReleaseBaseUrl}/v{TrimVersionPrefix(versionName)}";
    }

    private static async Task<RemoteVersionInfo> QueryLatestVersionAsync(
        UpdateChannel channel,
        UpdateArchitecture architecture,
        II18nService i18n,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GithubApiReleasesUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd(GithubApiUserAgent);
        request.Headers.Add("X-GitHub-Api-Version", GithubApiVersion);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<IReadOnlyList<GithubRelease>>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(i18n.T("setup.update.errors.github_empty_result"));
        return SelectLatestGithubRelease(releases, channel, ResolveCurrentAssetSuffix(architecture), i18n);
    }

    internal static RemoteVersionInfo SelectLatestGithubRelease(
        IReadOnlyList<GithubRelease> releases,
        UpdateChannel channel,
        string? requiredAssetSuffix,
        II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(releases);

        var candidates = releases
            .Where(release => !release.Draft)
            .Where(release => channel == UpdateChannel.Beta || !release.Prerelease)
            .Select(release => new
            {
                Release = release,
                VersionName = TrimVersionPrefix(release.TagName),
                DownloadUrl = SelectAssetDownloadUrl(release.Assets, requiredAssetSuffix)
            })
            .Select(candidate =>
            {
                try
                {
                    return new
                    {
                        candidate.Release,
                        candidate.VersionName,
                        Version = SemVer.Parse(candidate.VersionName),
                        candidate.DownloadUrl
                    };
                }
                catch
                {
                    return null;
                }
            })
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderByDescending(candidate => candidate.Version)
            .ThenByDescending(candidate => candidate.Release.PublishedAt ?? DateTimeOffset.MinValue)
            .ToArray();

        var selected = candidates.FirstOrDefault();
        if (selected is null)
        {
            throw new InvalidOperationException(i18n.T("setup.update.errors.no_release"));
        }

        return new RemoteVersionInfo(
            selected.VersionName,
            VersionCode: 0,
            Sha256: string.Empty,
            Changelog: selected.Release.Body ?? string.Empty,
            DownloadUrl: selected.DownloadUrl,
            ReleaseUrl: string.IsNullOrWhiteSpace(selected.Release.HtmlUrl) ? BuildVersionReleaseUrl(selected.VersionName) : selected.Release.HtmlUrl,
            SourceName: "GitHub");
    }

    private static string? ResolveCurrentAssetSuffix(UpdateArchitecture architecture)
    {
        if (OperatingSystem.IsWindows())
        {
            return architecture == UpdateArchitecture.Arm64 ? "win-arm64" : "win-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return architecture == UpdateArchitecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return architecture == UpdateArchitecture.Arm64 ? "linux-arm64" : "linux-x64";
        }

        return null;
    }

    private static string? SelectAssetDownloadUrl(IReadOnlyList<GithubReleaseAsset>? assets, string? requiredAssetSuffix)
    {
        if (assets is null || assets.Count == 0)
        {
            return null;
        }

        var archives = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name))
            .Where(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .Where(asset => asset.Name.StartsWith("PCL-ME-", StringComparison.OrdinalIgnoreCase))
            .Where(asset =>
                asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(requiredAssetSuffix))
        {
            var exactMatch = archives.FirstOrDefault(asset => asset.Name.Contains(requiredAssetSuffix, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null)
            {
                return exactMatch.BrowserDownloadUrl;
            }
        }

        return archives.FirstOrDefault()?.BrowserDownloadUrl;
    }

    private static LocalVersionInfo ReadCurrentVersion(II18nService i18n)
    {
        var metadataPath = FrontendLauncherAssetLocator.GetPath("metadata.json");
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException(i18n.T("setup.update.errors.metadata_missing"), metadataPath);
        }

        using var stream = File.OpenRead(metadataPath);
        var metadata = JsonSerializer.Deserialize<LauncherMetadata>(stream)
            ?? throw new InvalidOperationException(i18n.T("setup.update.errors.metadata_invalid"));
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

    private static string ExtractSummary(string? changelog, II18nService i18n)
    {
        if (string.IsNullOrWhiteSpace(changelog))
        {
            return i18n.T("setup.update.available.summary_missing");
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

        return i18n.T("setup.update.available.summary_missing");
    }

    private static string BuildVersionDisplayName(string versionName, II18nService i18n)
    {
        return i18n.T(
            "setup.update.version_name",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = FormatVersionName(versionName)
            });
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

    internal sealed record RemoteVersionInfo(
        string VersionName,
        int VersionCode,
        string Sha256,
        string Changelog,
        string? DownloadUrl,
        string ReleaseUrl,
        string SourceName);

    private sealed record LauncherMetadata(
        [property: JsonPropertyName("version")] LauncherMetadataVersion Version);

    private sealed record LauncherMetadataVersion(
        [property: JsonPropertyName("base")] string Base,
        [property: JsonPropertyName("suffix")] string? Suffix,
        [property: JsonPropertyName("code")] int Code);

    internal sealed record GithubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("assets")] IReadOnlyList<GithubReleaseAsset>? Assets);

    internal sealed record GithubReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);

    internal enum UpdateChannel
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
