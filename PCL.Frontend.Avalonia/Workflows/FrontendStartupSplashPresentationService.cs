using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendStartupSplashPresentationService
{
    private static readonly Uri WindowIconAssetUri = new("avares://PCL.Frontend.Avalonia/Assets/icon.ico");
    private static readonly Uri SplashImageAssetUri = new("avares://PCL.Frontend.Avalonia/Assets/icon.png");
    private static readonly TimeSpan MinimumVisibleDuration = TimeSpan.FromMilliseconds(240);

    public static FrontendStartupSplashSession? Show(LauncherStartupVisualPlan visualPlan)
    {
        var request = CreateRequest(visualPlan);
        if (request is null)
        {
            return null;
        }

        var window = new LauncherSplashWindow(request);
        window.Show();
        return new FrontendStartupSplashSession(window, request.MinimumVisibleDuration);
    }

    internal static FrontendStartupSplashRequest? CreateRequest(LauncherStartupVisualPlan visualPlan)
    {
        ArgumentNullException.ThrowIfNull(visualPlan);

        if (!visualPlan.ShouldShowSplashScreen)
        {
            return null;
        }

        return new FrontendStartupSplashRequest(
            WindowIconAssetUri,
            SplashImageAssetUri,
            MinimumVisibleDuration);
    }
}

internal sealed record FrontendStartupSplashRequest(
    Uri WindowIconAssetUri,
    Uri SplashImageAssetUri,
    TimeSpan MinimumVisibleDuration);

internal sealed class FrontendStartupSplashSession : IDisposable
{
    private readonly Window _window;
    private readonly TimeSpan _minimumVisibleDuration;
    private readonly Stopwatch _visibleStopwatch = Stopwatch.StartNew();
    private bool _isClosed;

    public FrontendStartupSplashSession(Window window, TimeSpan minimumVisibleDuration)
    {
        _window = window;
        _minimumVisibleDuration = minimumVisibleDuration;
    }

    public async Task CloseAsync()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;

        var remainingDuration = _minimumVisibleDuration - _visibleStopwatch.Elapsed;
        if (remainingDuration > TimeSpan.Zero)
        {
            await Task.Delay(remainingDuration);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_window.IsVisible)
            {
                _window.Close();
            }
        });
    }

    public void Dispose()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;

        if (_window.IsVisible)
        {
            _window.Close();
        }
    }
}
