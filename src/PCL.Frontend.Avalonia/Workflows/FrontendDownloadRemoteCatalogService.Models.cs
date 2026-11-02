using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendDownloadRemoteCatalogService
{
    private sealed record RemoteCatalogCacheKey(int SourceIndex, string PreferredMinecraftVersion);

    private sealed record RouteCatalogCacheKey(
        LauncherFrontendSubpageKey Route,
        int SourceIndex,
        string PreferredMinecraftVersion);

    private sealed record RemoteCatalogCacheEntry(
        DateTimeOffset FetchedAtUtc,
        IReadOnlyDictionary<LauncherFrontendSubpageKey, FrontendDownloadCatalogState> States);

    private sealed record RouteCatalogCacheEntry(
        DateTimeOffset FetchedAtUtc,
        FrontendDownloadCatalogState State);

    private sealed record GoldCatalogDescriptor(
        string IntroTitle,
        string IntroBody,
        string LoadingText,
        IReadOnlyList<FrontendDownloadCatalogAction> Actions);

    private sealed record RemoteSource(string DisplayName, string Url, bool IsOfficial);

    private sealed record RemotePayload<T>(RemoteSource Source, T Value);

    private sealed record OptiFineCatalogEntry(
        string MinecraftVersion,
        string DisplayName,
        bool IsPreview,
        string ReleaseTime,
        string? RequiredForgeVersion,
        string TargetUrl,
        string SuggestedFileName);

    private sealed record ForgeVersionCatalogEntry(
        string MinecraftVersion,
        string VersionName,
        string FileVersion,
        string Category,
        string FileExtension,
        bool IsRecommended,
        string ReleaseTime,
        string TargetUrl,
        string SuggestedFileName);

    private sealed record NeoForgeCatalogEntry(
        string MinecraftVersion,
        string Title,
        bool IsPreview,
        string TargetUrl);

    private sealed record LiteLoaderCatalogEntry(
        string MinecraftVersion,
        string FileName,
        bool IsPreview,
        bool IsLegacy,
        long Timestamp,
        string TargetUrl);

    private sealed class VersionTextComparer : IComparer<string>
    {
        public static VersionTextComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var leftTokens = Regex.Matches(left, @"\d+|[^\d]+").Select(match => match.Value).ToArray();
            var rightTokens = Regex.Matches(right, @"\d+|[^\d]+").Select(match => match.Value).ToArray();
            var tokenCount = Math.Min(leftTokens.Length, rightTokens.Length);
            for (var index = 0; index < tokenCount; index++)
            {
                var leftToken = leftTokens[index];
                var rightToken = rightTokens[index];
                var leftIsNumber = int.TryParse(leftToken, out var leftNumber);
                var rightIsNumber = int.TryParse(rightToken, out var rightNumber);
                var comparison = leftIsNumber && rightIsNumber
                    ? leftNumber.CompareTo(rightNumber)
                    : string.Compare(leftToken, rightToken, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftTokens.Length.CompareTo(rightTokens.Length);
        }
    }
}
