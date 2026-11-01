namespace PCL.Tools.I18n;

public static class Program
{
    public static int Main(string[] args)
    {
        var parseResult = I18nToolCommandParser.Parse(args);
        if (parseResult.ShowHelp)
        {
            Console.WriteLine(I18nToolCommandParser.GetUsageText());
            return 0;
        }

        if (parseResult.ErrorMessage is not null || parseResult.Options is null)
        {
            Console.Error.WriteLine(parseResult.ErrorMessage ?? "Unknown error.");
            Console.WriteLine();
            Console.WriteLine(I18nToolCommandParser.GetUsageText());
            return 1;
        }

        return I18nToolCommandRunner.Run(parseResult.Options);
    }
}
