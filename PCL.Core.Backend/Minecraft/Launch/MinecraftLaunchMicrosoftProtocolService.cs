using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftProtocolService
{
    public static MinecraftLaunchMicrosoftDeviceCodeResponse ParseDeviceCodeResponseJson(string json)
    {
        var root = ParseObject(json);
        return new MinecraftLaunchMicrosoftDeviceCodeResponse(
            GetRequiredString(root, "device_code"),
            GetRequiredString(root, "user_code"),
            GetRequiredString(root, "verification_uri"),
            root["verification_uri_complete"]?.ToString(),
            GetRequiredInt(root, "expires_in"),
            GetRequiredInt(root, "interval"));
    }

    public static MinecraftLaunchXboxLiveTokenRequest BuildXboxLiveTokenRequest(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("The OAuth access token cannot be empty.", nameof(accessToken));
        }

        return new MinecraftLaunchXboxLiveTokenRequest(
            new MinecraftLaunchXboxLiveTokenRequestProperties(
                "RPS",
                "user.auth.xboxlive.com",
                $"d={accessToken}"),
            "http://auth.xboxlive.com",
            "JWT");
    }

    public static string ParseXboxLiveTokenResponseJson(string json)
    {
        return GetRequiredString(ParseObject(json), "Token");
    }

    public static MinecraftLaunchXstsTokenRequest BuildXstsTokenRequest(string xblToken)
    {
        if (string.IsNullOrWhiteSpace(xblToken))
        {
            throw new ArgumentException("The XBL token cannot be empty.", nameof(xblToken));
        }

        return new MinecraftLaunchXstsTokenRequest(
            new MinecraftLaunchXstsTokenRequestProperties(
                "RETAIL",
                [xblToken]),
            "rp://api.minecraftservices.com/",
            "JWT");
    }

    public static MinecraftLaunchXstsTokenResponse ParseXstsTokenResponseJson(string json)
    {
        var root = ParseObject(json);
        var displayClaims = root["DisplayClaims"]?.AsObject()
                            ?? throw new InvalidOperationException("The XSTS response is missing DisplayClaims.");
        var xui = displayClaims["xui"] as JsonArray
                  ?? throw new InvalidOperationException("The XSTS response is missing xui.");
        var firstClaim = xui.FirstOrDefault() as JsonObject
                         ?? throw new InvalidOperationException("The XSTS response is missing a user claim.");

        return new MinecraftLaunchXstsTokenResponse(
            GetRequiredString(root, "Token"),
            GetRequiredString(firstClaim, "uhs"));
    }

    public static Dictionary<string, string> BuildMinecraftAccessTokenRequest(string userHash, string xstsToken)
    {
        if (string.IsNullOrWhiteSpace(userHash))
        {
            throw new ArgumentException("The UHS cannot be empty.", nameof(userHash));
        }

        if (string.IsNullOrWhiteSpace(xstsToken))
        {
            throw new ArgumentException("The XSTS token cannot be empty.", nameof(xstsToken));
        }

        return new Dictionary<string, string>
        {
            ["identityToken"] = $"XBL3.0 x={userHash};{xstsToken}"
        };
    }

    public static string ParseMinecraftAccessTokenResponseJson(string json)
    {
        return GetRequiredString(ParseObject(json), "access_token");
    }

    public static bool HasMinecraftOwnership(string json)
    {
        var root = ParseObject(json);
        var items = root["items"] as JsonArray;
        if (items is null)
        {
            return false;
        }

        return items
            .Select(item => item as JsonObject)
            .Where(item => item is not null)
            .Select(item => item!["name"]?.ToString())
            .Any(name => name is "product_minecraft" or "game_minecraft");
    }

    public static MinecraftLaunchMicrosoftProfileResponse ParseMinecraftProfileResponseJson(string json)
    {
        var root = ParseObject(json);
        return new MinecraftLaunchMicrosoftProfileResponse(
            GetRequiredString(root, "id"),
            GetRequiredString(root, "name"),
            json);
    }

    public static MinecraftLaunchOAuthRefreshResponse ParseOAuthRefreshResponseJson(string json)
    {
        var root = ParseObject(json);
        return new MinecraftLaunchOAuthRefreshResponse(
            GetRequiredString(root, "access_token"),
            GetRequiredString(root, "refresh_token"));
    }

    private static JsonObject ParseObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be empty.", nameof(json));
        }

        return JsonNode.Parse(json) as JsonObject
               ?? throw new InvalidOperationException("The JSON content is not an object.");
    }

    private static string GetRequiredString(JsonObject obj, string propertyName)
    {
        var value = obj[propertyName]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The JSON content is missing the {propertyName} field.");
        }

        return value;
    }

    private static int GetRequiredInt(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is null)
        {
            throw new InvalidOperationException($"The JSON content is missing the {propertyName} field.");
        }

        if (obj[propertyName] is JsonValue value &&
            value.TryGetValue<int>(out var intValue))
        {
            return intValue;
        }

        if (int.TryParse(obj[propertyName]!.ToString(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"The {propertyName} field in the JSON is not a number.");
    }
}

public sealed record MinecraftLaunchXboxLiveTokenRequest(
    MinecraftLaunchXboxLiveTokenRequestProperties Properties,
    string RelyingParty,
    string TokenType);

public sealed record MinecraftLaunchMicrosoftDeviceCodeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUrl,
    string? VerificationUrlComplete,
    int ExpiresInSeconds,
    int IntervalSeconds);

public sealed record MinecraftLaunchXboxLiveTokenRequestProperties(
    string AuthMethod,
    string SiteName,
    string RpsTicket);

public sealed record MinecraftLaunchXstsTokenRequest(
    MinecraftLaunchXstsTokenRequestProperties Properties,
    string RelyingParty,
    string TokenType);

public sealed record MinecraftLaunchXstsTokenRequestProperties(
    string SandboxId,
    IReadOnlyList<string> UserTokens);

public sealed record MinecraftLaunchXstsTokenResponse(
    string Token,
    string UserHash);

public sealed record MinecraftLaunchMicrosoftProfileResponse(
    string Uuid,
    string UserName,
    string ProfileJson);

public sealed record MinecraftLaunchOAuthRefreshResponse(
    string AccessToken,
    string RefreshToken);
