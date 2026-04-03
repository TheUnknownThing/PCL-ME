using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.ViewModels;

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
            desktop.MainWindow = new MainWindow
            {
                DataContext = FrontendShellViewModel.CreateBootstrap(options)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
