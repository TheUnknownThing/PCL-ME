using System.Net.Http;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void ApplyProxySettings()
    {
        _launcherActionService.PersistProtectedSharedValue("SystemHttpProxy", HttpProxyAddress);
        _launcherActionService.PersistProtectedSharedValue("SystemHttpProxyCustomUsername", HttpProxyUsername);
        _launcherActionService.PersistProtectedSharedValue("SystemHttpProxyCustomPassword", HttpProxyPassword);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.launcher_misc.activities.apply_proxy"),
            string.IsNullOrWhiteSpace(HttpProxyAddress)
                ? LT("setup.launcher_misc.activities.proxy_cleared")
                : HttpProxyAddress);
    }

    private async Task TestProxyConnectionAsync()
    {
        if (_isTestingProxyConnection)
        {
            return;
        }

        var configuration = FrontendHttpProxyService.BuildConfiguration(
            SelectedHttpProxyTypeIndex,
            HttpProxyAddress,
            HttpProxyUsername,
            HttpProxyPassword);
        if (SelectedHttpProxyTypeIndex == 2 && configuration.CustomProxyAddress is null)
        {
            var invalidAddressMessage = LT("setup.launcher_misc.messages.proxy_invalid_address");
            SetProxyTestFeedback(invalidAddressMessage, isSuccess: false);
            AddFailureActivity(LT("setup.launcher_misc.activities.test_proxy_failed"), invalidAddressMessage);
            return;
        }

        _isTestingProxyConnection = true;
        _testProxyConnectionCommand.NotifyCanExecuteChanged();
        AddActivity(
            LT("setup.launcher_misc.activities.test_proxy"),
            LT(
                "setup.launcher_misc.messages.proxy_test_started",
                ("mode", DescribeProxyMode(configuration)),
                ("host", FrontendHttpProxyService.ProxyConnectivityProbeUri.Host)));

        try
        {
            using var client = FrontendHttpProxyService.CreateHttpClient(
                configuration,
                TimeSpan.FromSeconds(12),
                "PCL-ME-Avalonia/1.0");
            using var request = new HttpRequestMessage(HttpMethod.Get, FrontendHttpProxyService.ProxyConnectivityProbeUri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var successMessage = LT(
                "setup.launcher_misc.messages.proxy_test_succeeded",
                ("mode", DescribeProxyMode(configuration)),
                ("status_code", (int)response.StatusCode),
                ("reason_phrase", response.ReasonPhrase ?? string.Empty));
            AddActivity(LT("setup.launcher_misc.activities.test_proxy"), successMessage);
            SetProxyTestFeedback(successMessage, isSuccess: true);
            AvaloniaHintBus.Show(LT("setup.launcher_misc.messages.proxy_test_succeeded_hint"), AvaloniaHintTheme.Success);
        }
        catch (Exception ex)
        {
            SetProxyTestFeedback(ex.Message, isSuccess: false);
            AddFailureActivity(LT("setup.launcher_misc.activities.test_proxy_failed"), ex.Message);
        }
        finally
        {
            _isTestingProxyConnection = false;
            _testProxyConnectionCommand.NotifyCanExecuteChanged();
        }
    }

    private void ClearProxyTestFeedback()
    {
        _isProxyTestFeedbackSuccess = false;
        ProxyTestFeedbackText = string.Empty;
    }

    private void SetProxyTestFeedback(string text, bool isSuccess)
    {
        _isProxyTestFeedbackSuccess = isSuccess;
        ProxyTestFeedbackText = text;
    }

    private string DescribeProxyMode(FrontendResolvedProxyConfiguration configuration)
    {
        return configuration.Mode switch
        {
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.CustomProxy => configuration.CustomProxyAddress?.ToString() ?? SetupText.LauncherMisc.CustomProxyLabel,
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.SystemProxy => SetupText.LauncherMisc.SystemProxyLabel,
            _ => SetupText.LauncherMisc.NoProxyLabel
        };
    }
}
