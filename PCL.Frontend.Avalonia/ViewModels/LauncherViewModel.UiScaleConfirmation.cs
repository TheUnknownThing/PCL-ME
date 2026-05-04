using System.Threading;
using Avalonia.Threading;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private const int UiScaleConfirmationTimeoutSeconds = 10;

    private async Task ConfirmUiScaleFactorChangeAsync(
        int version,
        double previousScale,
        double requestedScale)
    {
        var keepChange = false;
        try
        {
            keepChange = await ShowInAppTimedConfirmationAsync(
                T("setup.ui.scale_confirmation.title"),
                remainingSeconds => T(
                    "setup.ui.scale_confirmation.message",
                    ("old_scale", FrontendStartupScalingService.FormatUiScaleFactorLabel(previousScale)),
                    ("new_scale", FrontendStartupScalingService.FormatUiScaleFactorLabel(requestedScale)),
                    ("seconds", remainingSeconds)),
                T("common.actions.confirm"),
                isDanger: false,
                timeoutSeconds: UiScaleConfirmationTimeoutSeconds).ConfigureAwait(false);
        }
        catch
        {
            keepChange = false;
        }

        if (version != Volatile.Read(ref _uiScaleConfirmationVersion))
        {
            return;
        }

        if (keepChange && AreEquivalentUiScaleFactors(_uiScaleFactor, requestedScale))
        {
            _launcherActionService.PersistLocalValue(
                FrontendStartupScalingService.UiScaleFactorConfigKey,
                requestedScale);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => ApplyUiScaleFactorState(
            FrontendStartupScalingService.ResolveClosestScaleFactorIndex(previousScale),
            previousScale,
            suppressPersistence: true));
    }
}
