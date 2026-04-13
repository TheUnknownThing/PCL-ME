using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchAuthlibProtocolService
{
    public static string BuildValidateRequestJson(string accessToken, string clientToken)
    {
        var payload = new JsonObject
        {
            ["accessToken"] = accessToken,
            ["clientToken"] = clientToken
        };
        return payload.ToJsonString();
    }

    public static string BuildRefreshRequestJson(string selectedProfileName, string selectedProfileId, string accessToken)
    {
        var payload = new JsonObject
        {
            ["selectedProfile"] = new JsonObject
            {
                ["name"] = selectedProfileName,
                ["id"] = selectedProfileId
            },
            ["accessToken"] = accessToken,
            ["requestUser"] = true
        };
        return payload.ToJsonString();
    }

    public static string BuildAuthenticateRequestJson(string userName, string password)
    {
        var payload = new JsonObject
        {
            ["agent"] = new JsonObject
            {
                ["name"] = "Minecraft",
                ["version"] = 1
            },
            ["username"] = userName,
            ["password"] = password,
            ["requestUser"] = true
        };
        return payload.ToJsonString();
    }

    public static MinecraftLaunchAuthlibRefreshResponse ParseRefreshResponseJson(string json)
    {
        var root = ParseObject(json);
        var selectedProfile = root["selectedProfile"]?.AsObject()
                              ?? throw new InvalidOperationException("选择的角色无效！");

        return new MinecraftLaunchAuthlibRefreshResponse(
            GetRequiredString(root, "accessToken"),
            GetRequiredString(root, "clientToken"),
            GetRequiredString(selectedProfile, "id"),
            GetRequiredString(selectedProfile, "name"));
    }

    public static MinecraftLaunchAuthlibAuthenticateResponse ParseAuthenticateResponseJson(string json)
    {
        var root = ParseObject(json);
        var availableProfilesNode = root["availableProfiles"] as JsonArray ?? [];
        var availableProfiles = availableProfilesNode
            .Select(profile => profile as JsonObject)
            .Where(profile => profile is not null)
            .Select(profile => new MinecraftLaunchAuthProfileOption(
                GetRequiredString(profile!, "id"),
                GetRequiredString(profile!, "name")))
            .ToList();

        string? selectedProfileId = null;
        if (root["selectedProfile"] is JsonObject selectedProfile)
        {
            selectedProfileId = GetRequiredString(selectedProfile, "id");
        }

        return new MinecraftLaunchAuthlibAuthenticateResponse(
            GetRequiredString(root, "accessToken"),
            GetRequiredString(root, "clientToken"),
            availableProfiles,
            selectedProfileId);
    }

    public static string ParseServerNameFromMetadataJson(string json)
    {
        var root = ParseObject(json);
        var meta = root["meta"]?.AsObject()
                   ?? throw new InvalidOperationException("服务器元数据缺少 meta 字段。");
        return GetRequiredString(meta, "serverName");
    }

    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON 内容不能为空。", nameof(json));
        }

        return JsonNode.Parse(json) as JsonObject
               ?? throw new InvalidOperationException("JSON 内容不是对象。");
    }

    private static string GetRequiredString(JsonObject obj, string propertyName)
    {
        var value = obj[propertyName]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"JSON 内容缺少 {propertyName} 字段。");
        }

        return value;
    }
}

public sealed record MinecraftLaunchAuthlibRefreshResponse(
    string AccessToken,
    string ClientToken,
    string SelectedProfileId,
    string SelectedProfileName);

public sealed record MinecraftLaunchAuthlibAuthenticateResponse(
    string AccessToken,
    string ClientToken,
    IReadOnlyList<MinecraftLaunchAuthProfileOption> AvailableProfiles,
    string? SelectedProfileId);
