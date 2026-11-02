using Avalonia;
using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;

public partial class Program
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AvaloniaDesktopHost.BuildAvaloniaApp(CreateDesignTimeOptions());
    }

    private static AvaloniaCommandOptions CreateDesignTimeOptions()
    {
        return AvaloniaCommandParser.CreateDefaultOptions();
    }
}
