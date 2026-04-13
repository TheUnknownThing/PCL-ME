using PCL.Frontend.Avalonia.Cli;
using PCL.Frontend.Avalonia.Desktop;

public partial class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            AvaloniaDesktopHost.Run(CreateDefaultAppOptions());
            return;
        }

        var parseResult = AvaloniaCommandParser.Parse(args);
        if (parseResult.ShowHelp)
        {
            Console.WriteLine(AvaloniaCommandParser.GetUsageText());
            return;
        }

        if (parseResult.ErrorMessage is not null || parseResult.Options is null)
        {
            Console.Error.WriteLine(parseResult.ErrorMessage ?? "Unknown error.");
            Console.WriteLine();
            Console.WriteLine(AvaloniaCommandParser.GetUsageText());
            Environment.ExitCode = 1;
            return;
        }

        AvaloniaDesktopHost.Run(parseResult.Options);
    }

    private static AvaloniaCommandOptions CreateDefaultAppOptions()
    {
        return AvaloniaCommandParser.CreateDefaultOptions();
    }
}
