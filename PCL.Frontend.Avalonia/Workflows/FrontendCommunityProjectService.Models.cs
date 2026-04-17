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

    internal sealed record FrontendClipboardCommunityProjectLink(
        string Source,
        string Identifier,
        LauncherFrontendSubpageKey Route,
        string Url);

    internal sealed record FrontendClipboardCommunityProjectResolution(
        string ProjectId,
        string ProjectTitle,
        LauncherFrontendSubpageKey Route,
        string Source,
        string Url);

    private sealed record CacheEntry<T>(T Value, DateTimeOffset CreatedAt);

    private sealed record RequestCandidate(string Url, bool UseCurseForgeApiKey);

}
