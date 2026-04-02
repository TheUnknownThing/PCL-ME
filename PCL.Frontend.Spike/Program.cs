using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Frontend.Spike.Cli;
using PCL.Frontend.Spike.Models;
using PCL.Frontend.Spike.Rendering;
using PCL.Frontend.Spike.Workflows;

var parseResult = SpikeCommandParser.Parse(args);
if (parseResult.ShowHelp)
{
    Console.WriteLine(SpikeCommandParser.GetUsageText());
    return;
}

if (parseResult.ErrorMessage is not null || parseResult.Options is null)
{
    Console.Error.WriteLine(parseResult.ErrorMessage ?? "Unknown error.");
    Console.WriteLine();
    Console.WriteLine(SpikeCommandParser.GetUsageText());
    Environment.ExitCode = 1;
    return;
}

var options = parseResult.Options;
var payload = BuildPayload(options);

if (options.Format == SpikeOutputFormat.Text)
{
    Console.WriteLine(SpikeTextRenderer.Render(payload));
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(payload, CreateJsonOptions()));
}

static object BuildPayload(SpikeCommandOptions options)
{
    return options.Mode switch
    {
        SpikeOutputMode.Plan => BuildPlanPayload(options),
        SpikeOutputMode.Run => BuildRunPayload(options),
        _ => throw new InvalidOperationException($"Unsupported mode '{options.Mode}'.")
    };
}

static object BuildPlanPayload(SpikeCommandOptions options)
{
    return options.Command switch
    {
        SpikeCommandKind.Startup => SpikeSampleFactory.BuildStartupPlan(),
        SpikeCommandKind.Launch => SpikeSampleFactory.BuildLaunchPlan(options.Scenario),
        SpikeCommandKind.Crash => SpikeSampleFactory.BuildCrashPlan(),
        SpikeCommandKind.All => new SpikePlanBundle(
            SpikeSampleFactory.BuildStartupPlan(),
            SpikeSampleFactory.BuildLaunchPlan(options.Scenario),
            SpikeSampleFactory.BuildCrashPlan()),
        _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
    };
}

static object BuildRunPayload(SpikeCommandOptions options)
{
    return options.Command switch
    {
        SpikeCommandKind.Startup => SpikeRunner.BuildStartupRun(SpikeSampleFactory.BuildStartupPlan()),
        SpikeCommandKind.Launch => SpikeRunner.BuildLaunchRun(
            SpikeSampleFactory.BuildLaunchPlan(options.Scenario),
            options.JavaPromptDecision),
        SpikeCommandKind.Crash => SpikeRunner.BuildCrashRun(
            SpikeSampleFactory.BuildCrashPlan(),
            options.CrashAction),
        SpikeCommandKind.All => new SpikeRunBundle(
            SpikeRunner.BuildStartupRun(SpikeSampleFactory.BuildStartupPlan()),
            SpikeRunner.BuildLaunchRun(SpikeSampleFactory.BuildLaunchPlan(options.Scenario), options.JavaPromptDecision),
            SpikeRunner.BuildCrashRun(SpikeSampleFactory.BuildCrashPlan(), options.CrashAction)),
        _ => throw new InvalidOperationException($"Unsupported command '{options.Command}'.")
    };
}

static JsonSerializerOptions CreateJsonOptions()
{
    return new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
