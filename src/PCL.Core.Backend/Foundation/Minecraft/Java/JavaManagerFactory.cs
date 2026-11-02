using System.Collections.Generic;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Core.Minecraft.Java.Scanner;

namespace PCL.Core.Minecraft;

public static class JavaManagerFactory
{
    public static JavaManager CreateDefault(IJavaStorage? storage = null)
    {
        var runtime = SystemJavaRuntimeEnvironment.Current;
        var commandRunner = new ProcessCommandRunner();
        var scanners = new List<IJavaScanner>
        {
            new DefaultPathsScanner(runtime),
            new PathEnvironmentScanner(runtime),
            new WhereCommandScanner(runtime, commandRunner)
        };

        if (runtime.IsWindows)
        {
            scanners.Insert(0, new RegistryJavaScanner());
            scanners.Insert(3, new MicrosoftStoreJavaScanner());
        }

        var parsers = runtime.IsWindows
            ? new IJavaParser[]
            {
                new CommandJavaParser(runtime, commandRunner),
                new PeHeaderParser()
            }
            : [new CommandJavaParser(runtime, commandRunner)];

        return new JavaManager(
            parser: new CompositeJavaParser(parsers),
            storage: storage ?? NullJavaStorage.Instance,
            installationEvaluator: new DefaultJavaInstallationEvaluator(runtime),
            scanners: scanners.ToArray());
    }
}
