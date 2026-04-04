using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft;

public static class MinecraftLauncherProfilesFileService
{
    public static string CreateDefaultProfilesJson(DateTime timestamp)
    {
        var root = new JsonObject
        {
            ["profiles"] = new JsonObject
            {
                ["PCL"] = new JsonObject
                {
                    ["icon"] = "Grass",
                    ["name"] = "PCL",
                    ["lastVersionId"] = "latest-release",
                    ["type"] = "latest-release",
                    ["lastUsed"] = timestamp.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.0000'Z'", CultureInfo.InvariantCulture)
                }
            },
            ["selectedProfile"] = "PCL",
            ["clientToken"] = "23323323323323323323323323323333"
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
