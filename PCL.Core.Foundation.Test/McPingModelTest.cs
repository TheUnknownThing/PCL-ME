using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.McPing.Model;

namespace PCL.Core.Test;

[TestClass]
public class McPingModelTest
{
    [TestMethod]
    public void JsonSerializationRoundTrip()
    {
        var sample = new McPingResult(
            new McPingVersionResult("1.20.1", 763),
            new McPingPlayerResult(20, 3, [new McPingPlayerSampleResult("Steve", "uuid-1")]),
            "Hello world",
            null,
            42,
            new McPingModInfoResult("FML", [new McPingModInfoModResult("example", "1.0.0")]),
            true);

        var json = JsonSerializer.Serialize(sample);
        var restored = JsonSerializer.Deserialize<McPingResult>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(sample.Version.Protocol, restored.Version.Protocol);
        Assert.AreEqual(sample.Players.Online, restored.Players.Online);
        Assert.AreEqual(sample.ModInfo?.ModList[0].Id, restored.ModInfo?.ModList[0].Id);
        Assert.AreEqual(sample.PreventsChatReports, restored.PreventsChatReports);
    }
}
