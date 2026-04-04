using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.ViewModels;
using PCL.Frontend.Spike.Workflows;

namespace PCL.Frontend.Spike.Desktop;

internal sealed class App(SpikeCommandOptions options) : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shellActionService = new FrontendShellActionService(
                FrontendRuntimePaths.Resolve(),
                () => desktop.Shutdown());
            desktop.MainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(options, shellActionService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
