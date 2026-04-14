using System.Threading.Tasks;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _lastClipboardCommunityLinkText = string.Empty;
    private int _clipboardCommunityLinkCheckVersion;

    private static bool ShouldProbeClipboardCommunityLink(LauncherFrontendRoute route)
    {
        return route.Page == LauncherFrontendPageKey.Download
               && route.Subpage is LauncherFrontendSubpageKey.DownloadMod
                   or LauncherFrontendSubpageKey.DownloadPack
                   or LauncherFrontendSubpageKey.DownloadDataPack
                   or LauncherFrontendSubpageKey.DownloadResourcePack
                   or LauncherFrontendSubpageKey.DownloadShader
                   or LauncherFrontendSubpageKey.DownloadWorld
                   or LauncherFrontendSubpageKey.DownloadCompFavorites;
    }

    private void QueueClipboardCommunityLinkProbe(LauncherFrontendRoute route)
    {
        var version = ++_clipboardCommunityLinkCheckVersion;
        if (!ShouldProbeClipboardCommunityLink(route))
        {
            return;
        }

        _ = ProbeClipboardCommunityLinkAsync(route, version);
    }

    private async Task ProbeClipboardCommunityLinkAsync(LauncherFrontendRoute route, int version)
    {
        if (!DetectClipboardResourceLinks || !ShouldProbeClipboardCommunityLink(route))
        {
            return;
        }

        string? clipboardText;
        try
        {
            clipboardText = await _shellActionService.ReadClipboardTextAsync();
        }
        catch
        {
            return;
        }

        if (version != _clipboardCommunityLinkCheckVersion)
        {
            return;
        }

        var normalizedClipboardText = clipboardText?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedClipboardText)
            || string.Equals(normalizedClipboardText, _lastClipboardCommunityLinkText, StringComparison.Ordinal))
        {
            return;
        }

        _lastClipboardCommunityLinkText = normalizedClipboardText;
        if (!FrontendCommunityProjectService.TryParseClipboardProjectLink(normalizedClipboardText, out var link))
        {
            return;
        }

        FrontendCommunityProjectService.FrontendClipboardCommunityProjectResolution resolution;
        try
        {
            resolution = await Task.Run(() => FrontendCommunityProjectService.ResolveClipboardProjectLink(
                link,
                SelectedCommunityDownloadSourceIndex));
        }
        catch
        {
            return;
        }

        if (version != _clipboardCommunityLinkCheckVersion
            || !DetectClipboardResourceLinks
            || (_currentRoute.Page == LauncherFrontendPageKey.CompDetail
                && string.Equals(_selectedCommunityProjectId, resolution.ProjectId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            "检测到社区资源链接",
            $"剪贴板中包含 {resolution.Source} 项目“{resolution.ProjectTitle}”的链接，是否打开详情页？",
            "打开详情");
        if (!confirmed || version != _clipboardCommunityLinkCheckVersion)
        {
            return;
        }

        OpenCommunityProjectDetail(
            resolution.ProjectId,
            resolution.ProjectTitle,
            originSubpage: resolution.Route);
    }
}
