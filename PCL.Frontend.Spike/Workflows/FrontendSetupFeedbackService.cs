using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class FrontendSetupFeedbackService
{
    private const string IssuesEndpoint = "https://api.github.com/repos/PCL-Community/PCL2-CE/issues";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static readonly FeedbackSectionDefinition[] SectionDefinitions =
    [
        new("processing", "正在处理", false, 6820804544),
        new("waiting-process", "等待处理", false, 6820804546),
        new("wait", "等待", false, 8743070786),
        new("pause", "暂停", false, 8558220235),
        new("upnext", "在即", false, 8550609020),
        new("completed", "已完成", true, 6820804547),
        new("decline", "已拒绝", true, 6820804539),
        new("ignored", "已忽略", true, 8064650117),
        new("duplicate", "重复", true, 6820804541)
    ];

    public static async Task<FrontendSetupFeedbackSnapshot> QueryAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<GitHubIssueDto>();
        for (var page = 1; page <= 2; page++)
        {
            var requestUri = $"{IssuesEndpoint}?state=all&sort=created&direction=desc&per_page=100&page={page}";
            using var response = await HttpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var pageItems = await JsonSerializer.DeserializeAsync<List<GitHubIssueDto>>(stream, cancellationToken: cancellationToken)
                ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            results.AddRange(pageItems);
            if (pageItems.Count < 100)
            {
                break;
            }
        }

        return new FrontendSetupFeedbackSnapshot(
            BuildSections(results),
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<FrontendSetupFeedbackSectionSnapshot> BuildSections(IReadOnlyList<GitHubIssueDto> issues)
    {
        var buckets = SectionDefinitions.ToDictionary(
            section => section.Key,
            _ => new List<FrontendSetupFeedbackEntrySnapshot>(capacity: 8),
            StringComparer.Ordinal);
        var uncategorized = new List<FrontendSetupFeedbackEntrySnapshot>();

        foreach (var issue in issues)
        {
            if (issue.PullRequest is not null)
            {
                continue;
            }

            var entry = new FrontendSetupFeedbackEntrySnapshot(
                issue.Number,
                issue.Title?.Trim() ?? $"Issue #{issue.Number}",
                BuildSummary(issue),
                issue.HtmlUrl?.Trim() ?? $"{IssuesEndpoint}/{issue.Number}");

            var labelIds = issue.Labels?
                .Select(label => label.Id)
                .ToHashSet() ?? [];
            var matchedSection = false;
            foreach (var section in SectionDefinitions)
            {
                if (labelIds.Contains(section.LabelId))
                {
                    buckets[section.Key].Add(entry);
                    matchedSection = true;
                }
            }

            if (!matchedSection)
            {
                uncategorized.Add(entry);
            }
        }

        var sections = new List<FrontendSetupFeedbackSectionSnapshot>(SectionDefinitions.Length + 1);
        foreach (var section in SectionDefinitions)
        {
            if (buckets[section.Key].Count == 0)
            {
                continue;
            }

            sections.Add(new FrontendSetupFeedbackSectionSnapshot(
                section.Key,
                section.Title,
                section.DefaultExpanded,
                buckets[section.Key]));
        }

        if (uncategorized.Count > 0)
        {
            sections.Add(new FrontendSetupFeedbackSectionSnapshot(
                "other",
                "其他",
                false,
                uncategorized));
        }

        return sections;
    }

    private static string BuildSummary(GitHubIssueDto issue)
    {
        var author = string.IsNullOrWhiteSpace(issue.User?.Login) ? "unknown" : issue.User.Login.Trim();
        var createdAt = issue.CreatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "unknown";
        var type = string.IsNullOrWhiteSpace(issue.Type?.Name) ? "未分类" : issue.Type!.Name!.Trim().ToLowerInvariant();
        return $"{author} | {createdAt} | {type}";
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-Frontend-Spike/1.0");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed record FeedbackSectionDefinition(
        string Key,
        string Title,
        bool DefaultExpanded,
        long LabelId);

    private sealed record GitHubIssueDto(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("user")] GitHubUserDto? User,
        [property: JsonPropertyName("labels")] IReadOnlyList<GitHubLabelDto>? Labels,
        [property: JsonPropertyName("type")] GitHubIssueTypeDto? Type,
        [property: JsonPropertyName("pull_request")] JsonElement? PullRequest);

    private sealed record GitHubUserDto(
        [property: JsonPropertyName("login")] string? Login);

    private sealed record GitHubLabelDto(
        [property: JsonPropertyName("id")] long Id);

    private sealed record GitHubIssueTypeDto(
        [property: JsonPropertyName("name")] string? Name);
}
