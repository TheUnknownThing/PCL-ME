using System.Globalization;
using PCL.Core.App.Configuration.Storage;

namespace PCL.Frontend.Avalonia.Workflows;

internal sealed record FrontendStartupScalingConfiguration(
    bool HasStoredScaleFactor,
    double ScaleFactor);

internal static class FrontendStartupScalingService
{
    public const string UiScaleFactorConfigKey = "UiScaleFactor";
    public const string GlobalScaleFactorEnvironmentVariable = "AVALONIA_GLOBAL_SCALE_FACTOR";
    public const double DefaultUiScaleFactor = 1d;
    public const double MinimumUiScaleFactor = 0.5d;
    public const double MaximumUiScaleFactor = 3d;

    public static FrontendStartupScalingConfiguration Resolve(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        if (!File.Exists(runtimePaths.LocalConfigPath))
        {
            return new FrontendStartupScalingConfiguration(false, DefaultUiScaleFactor);
        }

        var localConfig = runtimePaths.OpenLocalConfigProvider();
        if (!localConfig.Exists(UiScaleFactorConfigKey))
        {
            return new FrontendStartupScalingConfiguration(false, DefaultUiScaleFactor);
        }

        return new FrontendStartupScalingConfiguration(
            true,
            ReadStoredUiScaleFactor(localConfig));
    }

    public static void ApplyStoredScale(FrontendRuntimePaths runtimePaths)
    {
        var configuration = Resolve(runtimePaths);
        if (!configuration.HasStoredScaleFactor)
        {
            return;
        }

        Environment.SetEnvironmentVariable(
            GlobalScaleFactorEnvironmentVariable,
            FormatEnvironmentScaleFactor(configuration.ScaleFactor));
    }

    public static double NormalizeUiScaleFactor(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultUiScaleFactor;
        }

        return Math.Round(
            Math.Clamp(value, MinimumUiScaleFactor, MaximumUiScaleFactor),
            2,
            MidpointRounding.AwayFromZero);
    }

    public static string FormatUiScaleFactorLabel(double value)
    {
        return $"{Math.Round(NormalizeUiScaleFactor(value) * 100d)}%";
    }

    public static double ReadStoredUiScaleFactor(IKeyValueFileProvider localConfig)
    {
        ArgumentNullException.ThrowIfNull(localConfig);

        if (!localConfig.Exists(UiScaleFactorConfigKey))
        {
            return DefaultUiScaleFactor;
        }

        try
        {
            return NormalizeUiScaleFactor(localConfig.Get<double>(UiScaleFactorConfigKey));
        }
        catch
        {
            // Older hand-edited files may store the value as text.
        }

        try
        {
            var rawValue = localConfig.Get<string>(UiScaleFactorConfigKey);
            if (double.TryParse(
                    rawValue,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return NormalizeUiScaleFactor(parsed);
            }
        }
        catch
        {
            // Fall through to the default below.
        }

        return DefaultUiScaleFactor;
    }

    private static string FormatEnvironmentScaleFactor(double value)
    {
        return NormalizeUiScaleFactor(value).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
