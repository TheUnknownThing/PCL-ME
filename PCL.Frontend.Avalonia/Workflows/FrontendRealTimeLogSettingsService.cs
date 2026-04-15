namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendRealTimeLogSettingsService
{
    public static string FormatLineLimitLabel(double value)
    {
        return ResolveLineLimit(value)?.ToString() ?? "Unlimited";
    }

    public static int? ResolveLineLimit(double value)
    {
        var rounded = Math.Round(value);
        return rounded switch
        {
            <= 5 => (int)(rounded * 10 + 50),
            <= 13 => (int)(rounded * 50 - 150),
            <= 28 => (int)(rounded * 100 - 800),
            _ => null
        };
    }
}
