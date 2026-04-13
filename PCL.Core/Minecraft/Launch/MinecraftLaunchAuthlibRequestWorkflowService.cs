namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchAuthlibRequestWorkflowService
{
    private static readonly IReadOnlyDictionary<string, string> DefaultHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept-Language"] = "zh-CN"
        };

    public static MinecraftLaunchHttpRequestPlan BuildValidateRequest(
        string baseUrl,
        string accessToken,
        string clientToken)
    {
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            CombineAuthserverUrl(baseUrl, "validate"),
            "application/json",
            MinecraftLaunchAuthlibProtocolService.BuildValidateRequestJson(accessToken, clientToken),
            DefaultHeaders);
    }

    public static MinecraftLaunchHttpRequestPlan BuildRefreshRequest(
        string baseUrl,
        string selectedProfileName,
        string selectedProfileId,
        string accessToken)
    {
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            CombineAuthserverUrl(baseUrl, "refresh"),
            "application/json",
            MinecraftLaunchAuthlibProtocolService.BuildRefreshRequestJson(
                selectedProfileName,
                selectedProfileId,
                accessToken),
            DefaultHeaders);
    }

    public static MinecraftLaunchHttpRequestPlan BuildAuthenticateRequest(
        string baseUrl,
        string userName,
        string password)
    {
        return new MinecraftLaunchHttpRequestPlan(
            "POST",
            CombineAuthserverUrl(baseUrl, "authenticate"),
            "application/json",
            MinecraftLaunchAuthlibProtocolService.BuildAuthenticateRequestJson(userName, password),
            DefaultHeaders);
    }

    public static MinecraftLaunchHttpRequestPlan BuildMetadataRequest(string baseUrl)
    {
        var metadataUrl = baseUrl.Replace("/authserver", string.Empty, StringComparison.Ordinal);
        return new MinecraftLaunchHttpRequestPlan(
            "GET",
            metadataUrl);
    }

    private static string CombineAuthserverUrl(string baseUrl, string suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        return $"{baseUrl.TrimEnd('/')}/{suffix}";
    }
}
