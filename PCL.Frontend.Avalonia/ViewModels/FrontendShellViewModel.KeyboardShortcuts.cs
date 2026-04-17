using Avalonia.Input;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    internal bool TryHandleTopLevelNavigationShortcut(Key key)
    {
        if (!IsTopLevelNavigationInteractive)
        {
            return false;
        }

        var targetPage = key switch
        {
            Key.F1 => LauncherFrontendPageKey.Launch,
            Key.F2 => LauncherFrontendPageKey.Download,
            Key.F3 => LauncherFrontendPageKey.Setup,
            Key.F4 => LauncherFrontendPageKey.Tools,
            _ => (LauncherFrontendPageKey?)null
        };
        if (targetPage is null)
        {
            return false;
        }

        var targetRoute = NormalizeRoute(new LauncherFrontendRoute(targetPage.Value));
        var title = FrontendShellLocalizationService.ResolveRouteLabel(targetRoute, _i18n);
        NavigateTo(
            targetRoute,
            FrontendShellLocalizationService.DescribeNavigationActivity(title, "keyboard_shortcut", _i18n),
            RouteNavigationBehavior.Reset);
        return true;
    }

    internal bool TryHandlePrimaryEnterShortcut()
    {
        if (HasPromptOverlayInlineDialog)
        {
            return false;
        }

        if (IsWelcomeOverlayVisible)
        {
            if (!WelcomeNextStepCommand.CanExecute(null))
            {
                return false;
            }

            WelcomeNextStepCommand.Execute(null);
            return true;
        }

        if (IsPromptOverlayVisible
            || IsLaunchDialogVisible
            || _currentRoute.Page != LauncherFrontendPageKey.Launch
            || !LaunchCommand.CanExecute(null))
        {
            return false;
        }

        LaunchCommand.Execute(null);
        return true;
    }
}
