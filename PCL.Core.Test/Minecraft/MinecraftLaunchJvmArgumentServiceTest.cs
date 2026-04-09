using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchJvmArgumentServiceTest
{
    [TestMethod]
    public void BuildLegacyArgumentsComposesExpectedFixedAndOptionalEntries()
    {
        var result = MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
            new MinecraftLaunchLegacyJvmArgumentRequest(
                VariableJvmArguments: "-XX:+UseG1GC",
                YoungGenerationMegabytes: 512,
                TotalMemoryMegabytes: 4096,
                NativesDirectory: @"C:\Minecraft\natives",
                JavaMajorVersion: 21,
                AuthlibInjectorArgument: "-javaagent:\"authlib.jar\"=https://auth.example",
                DebugLog4jConfigurationFilePath: @"C:\Temp\log4j2.xml",
                RendererAgentArgument: "-javaagent:\"renderer.jar\"=llvmpipe",
                ProxyScheme: "http",
                ProxyHost: "http://proxy.example",
                ProxyPort: 8080,
                UseJavaWrapper: false,
                JavaWrapperTempDirectory: null,
                JavaWrapperPath: null,
                MainClass: "net.minecraft.client.main.Main"));

        StringAssert.Contains(result, "-XX:+UseG1GC");
        StringAssert.Contains(result, "-Dlog4j2.formatMsgNoLookups=true");
        StringAssert.Contains(result, "\"-Djava.library.path=C:\\Minecraft\\natives\"");
        StringAssert.Contains(result, "-cp ${classpath}");
        StringAssert.Contains(result, "-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT");
        StringAssert.Contains(result, "-Dhttp.proxyPort=8080");
        StringAssert.Contains(result, "net.minecraft.client.main.Main");
    }

    [TestMethod]
    public void BuildLegacyArgumentsSkipsJavaWrapperWhenJavaAlreadyContainsTheFix()
    {
        var result = MinecraftLaunchJvmArgumentService.BuildLegacyArguments(
            new MinecraftLaunchLegacyJvmArgumentRequest(
                VariableJvmArguments: "-XX:+UseG1GC",
                YoungGenerationMegabytes: 512,
                TotalMemoryMegabytes: 4096,
                NativesDirectory: @"C:\Minecraft\natives",
                JavaMajorVersion: 21,
                AuthlibInjectorArgument: null,
                DebugLog4jConfigurationFilePath: null,
                RendererAgentArgument: null,
                ProxyScheme: null,
                ProxyHost: null,
                ProxyPort: null,
                UseJavaWrapper: true,
                JavaWrapperTempDirectory: @"C:\Temp",
                JavaWrapperPath: @"C:\Temp\JavaWrapper.jar",
                MainClass: "net.minecraft.client.main.Main"));

        Assert.IsFalse(result.Contains("-jar \"C:\\Temp\\JavaWrapper.jar\"", System.StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("-Doolloo.jlw.tmpdir=", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void BuildModernArgumentsDeduplicatesAndAppendsRetroWrapper()
    {
        var result = MinecraftLaunchJvmArgumentService.BuildModernArguments(
            new MinecraftLaunchModernJvmArgumentRequest(
                BaseArguments: ["--add-exports", "module/pkg=ALL-UNNAMED", "--add-exports", "module/pkg=ALL-UNNAMED"],
                VariableJvmArguments: "-XX:+UseG1GC -XX:MaxDirectMemorySize=256M",
                PreferredIpStack: JvmPreferredIpStack.PreferV4,
                YoungGenerationMegabytes: 256,
                TotalMemoryMegabytes: 2048,
                UseRetroWrapper: true,
                JavaMajorVersion: 17,
                AuthlibInjectorArgument: null,
                DebugLog4jConfigurationFilePath: null,
                RendererAgentArgument: null,
                ProxyScheme: null,
                ProxyHost: null,
                ProxyPort: null,
                UseJavaWrapper: false,
                JavaWrapperTempDirectory: null,
                JavaWrapperPath: null,
                MainClass: "Main"));

        Assert.AreEqual(1, CountOccurrences(result, "--add-exports module/pkg=ALL-UNNAMED"));
        Assert.IsFalse(result.Contains("-XX:MaxDirectMemorySize=256M", System.StringComparison.Ordinal));
        StringAssert.Contains(result, "-Djava.net.preferIPv4Stack=true");
        StringAssert.Contains(result, "-Dretrowrapper.doUpdateCheck=false");
        StringAssert.EndsWith(result, "Main");
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
