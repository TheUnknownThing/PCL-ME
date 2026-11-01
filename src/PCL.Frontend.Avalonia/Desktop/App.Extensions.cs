using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PCL.Core.Logging;
using PCL.Frontend.Avalonia.Desktop.Extensions;
using PCL.Frontend.Avalonia.ViewModels;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.Desktop;

internal sealed partial class App
{
    private FrontendExtensionSession? _extensions;
    private bool _closingExtensions;
    private bool _extensionsClosed;

    private void InitializeExtensions(Window window, LauncherViewModel launcher, LauncherActionService actions, II18nService text)
    {
        _extensions = new FrontendExtensionSession(window, _desktopShell!, launcher, actions, text,
            _options.DisableExtensions || Environment.GetEnvironmentVariable("PCL_DISABLE_EXTENSIONS") == "1");
        window.Closing += async (_, e) =>
        {
            if (e.Cancel || _extensionsClosed) return;
            e.Cancel = true;
            if (_closingExtensions) return;
            _closingExtensions = true;
            try { await _extensions.DisposeAsync(); }
            catch (Exception ex) { LogWrapper.Warn(ex, "Extensions", "Extension shutdown failed."); }
            finally { _extensionsClosed = true; window.Close(); }
        };
        _extensions.Run(() => _extensions.StartAsync());
    }

    private async void RequestDesktopShutdown(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_closingExtensions) return;
        _closingExtensions = true;
        try { if (_extensions is not null) await _extensions.DisposeAsync(); }
        catch (Exception ex) { LogWrapper.Warn(ex, "Extensions", "Extension shutdown failed."); }
        finally { _extensionsClosed = true; desktop.Shutdown(); }
    }
}
