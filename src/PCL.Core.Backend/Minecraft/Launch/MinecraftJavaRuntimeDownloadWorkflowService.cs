namespace PCL.Core.Minecraft.Launch;

public static class MinecraftJavaRuntimeDownloadWorkflowService
{
    private static readonly IReadOnlyList<string> DefaultIndexOfficialUrls =
    [
        "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
    ];

    private static readonly IReadOnlyList<string> DefaultIndexMirrorUrls =
    [
        "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
    ];

    private static readonly IReadOnlyList<MinecraftJavaRuntimeUrlRewrite> DefaultManifestUrlRewrites =
    [
        new("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
    ];

    private static readonly IReadOnlyList<MinecraftJavaRuntimeUrlRewrite> DefaultFileUrlRewrites =
    [
        new("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
    ];

    public static MinecraftJavaRuntimeRequestUrlPlan GetDefaultIndexRequestUrlPlan()
    {
        return new MinecraftJavaRuntimeRequestUrlPlan(
            DefaultIndexOfficialUrls,
            DefaultIndexMirrorUrls);
    }

    public static IReadOnlyList<MinecraftJavaRuntimeUrlRewrite> GetDefaultManifestUrlRewrites()
    {
        return DefaultManifestUrlRewrites;
    }

    public static IReadOnlyList<MinecraftJavaRuntimeUrlRewrite> GetDefaultFileUrlRewrites()
    {
        return DefaultFileUrlRewrites;
    }

    public static MinecraftJavaRuntimeManifestRequestPlan BuildManifestRequestPlan(
        MinecraftJavaRuntimeManifestRequestPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selection = MinecraftJavaRuntimeDownloadService.SelectRuntime(
            new MinecraftJavaRuntimeSelectionRequest(
                request.IndexJson,
                request.PlatformKey,
                request.RequestedComponent));

        return new MinecraftJavaRuntimeManifestRequestPlan(
            selection,
            BuildRequestUrlPlan(
                selection.ManifestUrl,
                request.ManifestUrlRewrites),
            $"Preparing to download Java {selection.VersionName} ({selection.ComponentKey}): {selection.ManifestUrl}");
    }

    public static MinecraftJavaRuntimeDownloadWorkflowPlan BuildDownloadWorkflowPlan(
        MinecraftJavaRuntimeDownloadWorkflowPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var downloadPlan = MinecraftJavaRuntimeDownloadService.BuildDownloadPlan(
            new MinecraftJavaRuntimeDownloadPlanRequest(
                request.ManifestJson,
                request.RuntimeBaseDirectory,
                request.IgnoredSha1Hashes));

        var filePlans = downloadPlan.Files
            .Select(file => new MinecraftJavaRuntimeDownloadRequestFilePlan(
                file.RelativePath,
                file.TargetPath,
                file.Size,
                file.Sha1,
                BuildRequestUrlPlan(
                    file.Url,
                    request.FileUrlRewrites)))
            .ToArray();

        return new MinecraftJavaRuntimeDownloadWorkflowPlan(
            downloadPlan,
            filePlans,
            $"[Java] Need to download {filePlans.Length} files; target folder: {downloadPlan.RuntimeBaseDirectory}");
    }

    public static MinecraftJavaRuntimeDownloadTransferPlan BuildTransferPlan(
        MinecraftJavaRuntimeDownloadTransferPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingRelativePaths = request.ExistingRelativePaths?
            .Select(NormalizeRelativePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                                    [];

        var filesToDownload = new List<MinecraftJavaRuntimeDownloadRequestFilePlan>();
        var reusedFiles = new List<MinecraftJavaRuntimeDownloadRequestFilePlan>();

        foreach (var file in request.WorkflowPlan.Files)
        {
            if (existingRelativePaths.Contains(NormalizeRelativePath(file.RelativePath)))
            {
                reusedFiles.Add(file);
            }
            else
            {
                filesToDownload.Add(file);
            }
        }

        return new MinecraftJavaRuntimeDownloadTransferPlan(
            request.WorkflowPlan,
            filesToDownload,
            reusedFiles,
            $"[Java] Need to download {filesToDownload.Count} files, reuse {reusedFiles.Count} existing files, target folder: {request.WorkflowPlan.DownloadPlan.RuntimeBaseDirectory}");
    }

    private static MinecraftJavaRuntimeRequestUrlPlan BuildRequestUrlPlan(
        string officialUrl,
        IReadOnlyList<MinecraftJavaRuntimeUrlRewrite>? rewrites)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(officialUrl);

        if (rewrites is null || rewrites.Count == 0)
        {
            return new MinecraftJavaRuntimeRequestUrlPlan(
                [officialUrl],
                []);
        }

        var mirrorUrls = rewrites
            .Select(rewrite => rewrite.Apply(officialUrl))
            .Where(url => !string.Equals(url, officialUrl, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MinecraftJavaRuntimeRequestUrlPlan(
            [officialUrl],
            mirrorUrls);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : relativePath.Replace('\\', '/').TrimStart('/');
    }
}

public sealed record MinecraftJavaRuntimeManifestRequestPlanRequest(
    string IndexJson,
    string PlatformKey,
    string RequestedComponent,
    IReadOnlyList<MinecraftJavaRuntimeUrlRewrite>? ManifestUrlRewrites = null);

public sealed record MinecraftJavaRuntimeManifestRequestPlan(
    MinecraftJavaRuntimeSelection Selection,
    MinecraftJavaRuntimeRequestUrlPlan RequestUrls,
    string LogMessage);

public sealed record MinecraftJavaRuntimeDownloadWorkflowPlanRequest(
    string ManifestJson,
    string RuntimeBaseDirectory,
    IReadOnlyList<string>? IgnoredSha1Hashes = null,
    IReadOnlyList<MinecraftJavaRuntimeUrlRewrite>? FileUrlRewrites = null);

public sealed record MinecraftJavaRuntimeDownloadWorkflowPlan(
    MinecraftJavaRuntimeDownloadPlan DownloadPlan,
    IReadOnlyList<MinecraftJavaRuntimeDownloadRequestFilePlan> Files,
    string LogMessage);

public sealed record MinecraftJavaRuntimeDownloadTransferPlanRequest(
    MinecraftJavaRuntimeDownloadWorkflowPlan WorkflowPlan,
    IReadOnlyList<string>? ExistingRelativePaths = null);

public sealed record MinecraftJavaRuntimeDownloadTransferPlan(
    MinecraftJavaRuntimeDownloadWorkflowPlan WorkflowPlan,
    IReadOnlyList<MinecraftJavaRuntimeDownloadRequestFilePlan> FilesToDownload,
    IReadOnlyList<MinecraftJavaRuntimeDownloadRequestFilePlan> ReusedFiles,
    string LogMessage)
{
    public long DownloadBytes { get; } = FilesToDownload.Sum(file => file.Size);
}

public sealed record MinecraftJavaRuntimeDownloadRequestFilePlan(
    string RelativePath,
    string TargetPath,
    long Size,
    string Sha1,
    MinecraftJavaRuntimeRequestUrlPlan RequestUrls);

public sealed record MinecraftJavaRuntimeRequestUrlPlan(
    IReadOnlyList<string> OfficialUrls,
    IReadOnlyList<string> MirrorUrls)
{
    public IReadOnlyList<string> AllUrls { get; } =
    [
        ..OfficialUrls,
        ..MirrorUrls
    ];
}

public sealed record MinecraftJavaRuntimeUrlRewrite(
    string MatchText,
    string ReplacementText)
{
    public string Apply(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(MatchText);
        ArgumentException.ThrowIfNullOrWhiteSpace(ReplacementText);

        return url.Replace(MatchText, ReplacementText, StringComparison.OrdinalIgnoreCase);
    }
}
