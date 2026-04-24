using PCL.Frontend.Avalonia.ViewModels.Panes;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
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
                RefreshLauncherState(_i18n.T(IgnoreQuiltLoader
                    ? "setup.game_manage.reactions.quilt_hidden"
                    : "setup.game_manage.reactions.quilt_restored"));
                if (IsCurrentStandardRightPane(StandardRightPaneKind.DownloadResource))
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
