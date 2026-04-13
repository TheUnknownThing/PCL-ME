using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Launch;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MinecraftLaunchJsonArgumentServiceTest
{
    [TestMethod]
    public void ExtractValuesRespectsRulesAndInheritanceOrder()
    {
        var result = MinecraftLaunchJsonArgumentService.ExtractValues(
            new MinecraftLaunchJsonArgumentRequest(
                [
                    """
                    [
                      "--username",
                      {"rules":[{"action":"allow","os":{"name":"windows"}}],"value":["player","--demo"]},
                      {"rules":[{"action":"allow","features":{"quick_play_multiplayer":true}}],"value":"--skip"},
                      {"rules":[{"action":"allow","os":{"name":"linux"}}],"value":"--linux"}
                    ]
                    """,
                    """
                    [
                      "--version",
                      {"value":"1.20.5"}
                    ]
                    """
                ],
                OperatingSystemVersion: "10.0.22631",
                Is32BitOperatingSystem: false));

        CollectionAssert.AreEqual(
            new[] { "--username", "player", "--demo", "--version", "1.20.5" },
            (System.Collections.ICollection)result);
    }
}
