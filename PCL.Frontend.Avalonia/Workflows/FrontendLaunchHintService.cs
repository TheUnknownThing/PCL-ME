using PCL.Core.App.I18n;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendLaunchHintService
{
    private static readonly string[] FallbackHintKeys =
    [
        "launch.dialog.hints.create_hints_file",
        "launch.dialog.hints.time_tick",
        "launch.dialog.hints.jeb_sheep",
        "launch.dialog.hints.lodestone_compass",
        "launch.dialog.hints.parrots_dance",
        "launch.dialog.hints.map_lock",
        "launch.dialog.hints.baby_turtle_scute",
        "launch.dialog.hints.potion_damage",
        "launch.dialog.hints.dinnerbone",
        "launch.dialog.hints.best_selling"
    ];

    public static string GetRandomHint(FrontendRuntimePaths runtimePaths, II18nService i18n)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(i18n);

        var candidates = new[]
            {
                Path.Combine(runtimePaths.DataDirectory, "hints.txt"),
                FrontendLauncherAssetLocator.GetPath("Resources", "hints.txt")
            }
            .Where(File.Exists)
            .ToArray();

        foreach (var path in candidates)
        {
            try
            {
                var hints = File.ReadAllLines(path)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray();
                if (hints.Length > 0)
                {
                    return hints[Random.Shared.Next(hints.Length)];
                }
            }
            catch
            {
                // Ignore malformed or temporarily inaccessible hint sources and keep falling back.
            }
        }

        var key = FallbackHintKeys[Random.Shared.Next(FallbackHintKeys.Length)];
        return i18n.T(key);
    }
}
