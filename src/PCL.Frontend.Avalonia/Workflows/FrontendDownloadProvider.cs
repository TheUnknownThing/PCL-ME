namespace PCL.Frontend.Avalonia.Workflows;

internal enum FrontendDownloadSourcePreference
{
    MirrorPreferred = 0,
    OfficialPreferred = 1,
    OfficialOnly = 2
}

internal sealed class FrontendDownloadProvider
{
    private const string BmclApiRoot = "https://bmclapi2.bangbang93.com";
    private const string AssetObjectRoot = "https://resources.download.minecraft.net";

    private static readonly IReadOnlyList<(string MatchText, string ReplacementText)> PrimaryRewrites =
    [
        ("https://bmclapi2.bangbang93.com", BmclApiRoot),
        ("https://launchermeta.mojang.com", BmclApiRoot),
        ("https://piston-meta.mojang.com", BmclApiRoot),
        ("https://piston-data.mojang.com", BmclApiRoot),
        ("https://launcher.mojang.com", BmclApiRoot),
        ("https://libraries.minecraft.net", BmclApiRoot + "/libraries"),
        ("http://files.minecraftforge.net/maven", BmclApiRoot + "/maven"),
        ("https://files.minecraftforge.net/maven", BmclApiRoot + "/maven"),
        ("https://maven.minecraftforge.net", BmclApiRoot + "/maven"),
        ("https://maven.neoforged.net/api/maven/versions/releases/", BmclApiRoot + "/neoforge/meta/api/maven/details/releases/"),
        ("https://maven.neoforged.net/releases/", BmclApiRoot + "/maven/"),
        ("https://dl.liteloader.com/versions/versions.json", BmclApiRoot + "/maven/com/mumfrey/liteloader/versions.json"),
        ("http://dl.liteloader.com/versions/versions.json", BmclApiRoot + "/maven/com/mumfrey/liteloader/versions.json"),
        ("https://dl.liteloader.com/versions", BmclApiRoot + "/maven"),
        ("http://dl.liteloader.com/versions", BmclApiRoot + "/maven"),
        ("https://meta.fabricmc.net", BmclApiRoot + "/fabric-meta"),
        ("https://maven.fabricmc.net", BmclApiRoot + "/maven")
    ];

    private static readonly IReadOnlyList<(string MatchText, string ReplacementText)> FallbackRewrites =
    [
        ("https://api.modrinth.com", "https://mod.mcimirror.top/modrinth"),
        ("https://cdn.modrinth.com", "https://mod.mcimirror.top"),
        ("https://api.curseforge.com", "https://mod.mcimirror.top/curseforge"),
        ("https://edge.forgecdn.net", "https://mod.mcimirror.top"),
        ("https://mediafiles.forgecdn.net", "https://mod.mcimirror.top")
    ];

    public FrontendDownloadProvider(FrontendDownloadSourcePreference preference)
    {
        Preference = preference;
    }

    public FrontendDownloadSourcePreference Preference { get; }

    public static FrontendDownloadProvider FromPreference(int sourceIndex)
    {
        var normalizedPreference = Math.Clamp(sourceIndex, 0, (int)FrontendDownloadSourcePreference.OfficialOnly);
        return new FrontendDownloadProvider((FrontendDownloadSourcePreference)normalizedPreference);
    }

    public IReadOnlyList<string> GetPreferredUrls(string officialUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officialUrl);
        return BuildOrderedUrls(
            [officialUrl],
            TryBuildMirrorUrl(officialUrl) is { } mirrorUrl ? [mirrorUrl] : []);
    }

    public IReadOnlyList<string> GetPreferredUrls(
        IReadOnlyList<string> officialUrls,
        IReadOnlyList<string> mirrorUrls)
    {
        ArgumentNullException.ThrowIfNull(officialUrls);
        ArgumentNullException.ThrowIfNull(mirrorUrls);
        return BuildOrderedUrls(officialUrls, mirrorUrls);
    }

    public IReadOnlyList<string> GetAssetObjectUrls(string assetObjectLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetObjectLocation);
        var normalizedLocation = assetObjectLocation.TrimStart('/');
        return BuildOrderedUrls(
            [$"{AssetObjectRoot}/{normalizedLocation}"],
            [$"{BmclApiRoot}/assets/{normalizedLocation}"]);
    }

    private IReadOnlyList<string> BuildOrderedUrls(
        IReadOnlyList<string> officialUrls,
        IReadOnlyList<string> mirrorUrls)
    {
        IEnumerable<string> orderedUrls = Preference switch
        {
            FrontendDownloadSourcePreference.MirrorPreferred => mirrorUrls.Concat(officialUrls),
            FrontendDownloadSourcePreference.OfficialPreferred => officialUrls.Concat(mirrorUrls),
            _ => officialUrls
        };

        return orderedUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryBuildMirrorUrl(string url)
    {
        var rewritten = TryRewrite(url, PrimaryRewrites);
        if (!string.Equals(rewritten, url, StringComparison.OrdinalIgnoreCase))
        {
            return rewritten;
        }

        rewritten = TryRewrite(url, FallbackRewrites);
        return string.Equals(rewritten, url, StringComparison.OrdinalIgnoreCase)
            ? null
            : rewritten;
    }

    private static string TryRewrite(string url, IReadOnlyList<(string MatchText, string ReplacementText)> rewrites)
    {
        foreach (var (matchText, replacementText) in rewrites)
        {
            if (url.StartsWith(matchText, StringComparison.OrdinalIgnoreCase))
            {
                return replacementText + url[matchText.Length..];
            }
        }

        return url;
    }
}
