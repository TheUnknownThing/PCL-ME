using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Models;

namespace PCL.Frontend.Avalonia.Workflows;

internal static partial class FrontendCommunityProjectService
{

    private static IReadOnlyList<FrontendDownloadCatalogEntry> BuildModrinthLinkEntries(JsonObject project, string? website)
    {
        var entries = new List<FrontendDownloadCatalogEntry>();
        AddLinkEntry(entries, "homepage", website, "open_homepage");
        AddLinkEntry(entries, "issues", GetString(project, "issues_url"), "open_issues");
        AddLinkEntry(entries, "source", GetString(project, "source_url"), "open_source");
        AddLinkEntry(entries, "wiki", GetString(project, "wiki_url"), "open_wiki");
        AddLinkEntry(entries, "community", GetString(project, "discord_url"), "open_community");

        foreach (var donation in (project["donation_urls"] as JsonArray ?? [])
                     .Select(node => node as JsonObject)
                     .Where(node => node is not null))
        {
            AddLinkEntry(
                entries,
                BuildToken("support_author", GetString(donation, "platform") ?? "donation"),
                GetString(donation, "url"),
                "open_link");
        }

        return entries;
    }

    private static IReadOnlyList<FrontendDownloadCatalogEntry> BuildCurseForgeLinkEntries(JsonObject project, string? website)
    {
        var links = project["links"] as JsonObject;
        var entries = new List<FrontendDownloadCatalogEntry>();
        AddLinkEntry(entries, "homepage", website, "open_homepage");
        AddLinkEntry(entries, "issues", GetString(links, "issuesUrl"), "open_issues");
        AddLinkEntry(entries, "source", GetString(links, "sourceUrl"), "open_source");
        AddLinkEntry(entries, "wiki", GetString(links, "wikiUrl"), "open_wiki");
        return entries;
    }

    private static string BuildModrinthWebsite(JsonObject project)
    {
        var type = GetString(project, "project_type");
        var slug = GetString(project, "slug");
        return string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(slug)
            ? string.Empty
            : $"https://modrinth.com/{type}/{slug}";
    }

    private static string BuildCurseForgeWebsite(JsonObject project)
    {
        var slug = GetString(project, "slug");
        var section = TranslateCurseForgeSection(GetInt(project, "classId"));
        return string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(section)
            ? string.Empty
            : $"https://www.curseforge.com/minecraft/{section}/{slug}";
    }

    private static void AddLinkEntry(
        ICollection<FrontendDownloadCatalogEntry> entries,
        string title,
        string? target,
        string actionText)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        entries.Add(new FrontendDownloadCatalogEntry(
            title,
            target,
            string.Empty,
            actionText,
            target));
    }

}
