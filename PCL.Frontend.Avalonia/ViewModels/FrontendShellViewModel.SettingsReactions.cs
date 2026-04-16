using PCL.Frontend.Avalonia.ViewModels.ShellPanes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void HandleReactiveSettingChange(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(IgnoreQuiltLoader):
                RefreshShell(_i18n.T(IgnoreQuiltLoader
                    ? "setup.game_manage.reactions.quilt_hidden"
                    : "setup.game_manage.reactions.quilt_restored"));
                if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource))
                {
                    RefreshDownloadResourceSurface();
                }

                if (ShowCompDetailSurface)
                {
                    RebuildCommunityProjectSurfaceCollections();
                    RaiseCommunityProjectProperties();
                }

                break;
            case nameof(DetectClipboardResourceLinks):
                if (DetectClipboardResourceLinks)
                {
                    _lastClipboardCommunityLinkText = string.Empty;
                    QueueClipboardCommunityLinkProbe(_currentRoute);
                }
                else
                {
                    _clipboardCommunityLinkCheckVersion++;
                }

                break;
        }
    }
}
