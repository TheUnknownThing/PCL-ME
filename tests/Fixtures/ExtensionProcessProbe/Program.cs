using System.Text.Json;

if (args.Contains("-version") || args.Contains("--version"))
{
    Console.Error.WriteLine("openjdk version \"17.0.1\"");
    Console.Error.WriteLine("OpenJDK Runtime Environment (build 17.0.1+12)");
    Console.Error.WriteLine("OpenJDK 64-Bit Server VM (build 17.0.1+12, mixed mode)");
    Console.Error.WriteLine("    java.version = 17.0.1");
    Console.Error.WriteLine("    java.home = " + AppContext.BaseDirectory);
    Console.Error.WriteLine("    os.arch = " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
    return;
}
var output = Environment.GetEnvironmentVariable("PCL_EXTENSION_PROBE_RESULT");
if (output is not null)
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
    {
        Arguments = args,
        Value = Environment.GetEnvironmentVariable("PCL_EXTENSION_VALUE"),
        ProcessId = Environment.ProcessId
    }));
var release = Environment.GetEnvironmentVariable("PCL_EXTENSION_PROBE_RELEASE");
if (release is not null)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    while (!File.Exists(release)) await Task.Delay(25, timeout.Token);
}
