using System.Text.Json;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftRequestWorkflowService
{
    public const string DefaultMicrosoftClientId = "00000000402b5328";

    public static MinecraftLaunchHttpRequestPlan BuildDeviceCodeRequest(string clientId = DefaultMicrosoftClientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var body = BuildFormBody(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["tenant"] = "/consumers",
                ["scope"] = "XboxLive.signin offline_access"
            });

        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode",
            "application/x-www-form-urlencoded",
            body);
    }

    public static MinecraftLaunchHttpRequestPlan BuildOAuthRefreshRequest(
        string refreshToken,
        string clientId = DefaultMicrosoftClientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var body = BuildFormBody(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = "XboxLive.signin offline_access"
            });

        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            "https://login.live.com/oauth20_token.srf",
            "application/x-www-form-urlencoded",
            body);
    }

    public static MinecraftLaunchHttpRequestPlan BuildXboxLiveTokenRequest(string accessToken)
    {
        var payload = MinecraftLaunchMicrosoftProtocolService.BuildXboxLiveTokenRequest(accessToken);
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            "https://user.auth.xboxlive.com/user/authenticate",
            "application/json",
            JsonSerializer.Serialize(payload));
    }

    public static MinecraftLaunchHttpRequestPlan BuildXstsTokenRequest(string xblToken)
    {
        var payload = MinecraftLaunchMicrosoftProtocolService.BuildXstsTokenRequest(xblToken);
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            "https://xsts.auth.xboxlive.com/xsts/authorize",
            "application/json",
            JsonSerializer.Serialize(payload));
    }

    public static MinecraftLaunchHttpRequestPlan BuildMinecraftAccessTokenRequest(string userHash, string xstsToken)
    {
        var payload = MinecraftLaunchMicrosoftProtocolService.BuildMinecraftAccessTokenRequest(userHash, xstsToken);
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            "https://api.minecraftservices.com/authentication/login_with_xbox",
            "application/json",
            JsonSerializer.Serialize(payload));
    }

    public static MinecraftLaunchHttpRequestPlan BuildOwnershipRequest(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return new MinecraftLaunchHttpRequestPlan(
            "GET",
            "https://api.minecraftservices.com/entitlements/mcstore",
            BearerToken: accessToken);
    }

    public static MinecraftLaunchHttpRequestPlan BuildProfileRequest(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return new MinecraftLaunchHttpRequestPlan(
            "GET",
            "https://api.minecraftservices.com/minecraft/profile",
            BearerToken: accessToken);
    }

    private static string BuildFormBody(IReadOnlyDictionary<string, string> values)
    {
        return string.Join(
            "&",
            values.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }
}
