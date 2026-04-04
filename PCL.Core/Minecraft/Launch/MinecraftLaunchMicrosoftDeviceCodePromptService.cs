namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftDeviceCodePromptService
{
    public const string DefaultPollUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string PromptTitle = "登录 Minecraft";

    public static MinecraftLaunchMicrosoftDeviceCodePromptPlan BuildPromptPlan(
        string responseJson,
        string pollUrl = DefaultPollUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pollUrl);

        var response = MinecraftLaunchMicrosoftProtocolService.ParseDeviceCodeResponseJson(responseJson);
        var openBrowserUrl = string.IsNullOrWhiteSpace(response.VerificationUrlComplete)
            ? response.VerificationUrl
            : response.VerificationUrlComplete;

        var message = string.IsNullOrWhiteSpace(response.VerificationUrlComplete)
            ? $"登录网页将自动开启，请在网页中输入授权码 {response.UserCode}（将自动复制）。\n\n" +
              "如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。\n" +
              $"你也可以用其他设备打开 {openBrowserUrl} 并输入上述授权码。"
            : "登录网页将自动开启，授权码将自动填充。\n\n" +
              "如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。\n" +
              $"如果没有自动填充，请在页面内粘贴此授权码 {response.UserCode} （将自动复制）\n" +
              $"你也可以用其他设备打开 {openBrowserUrl} 并输入授权码。";

        return new MinecraftLaunchMicrosoftDeviceCodePromptPlan(
            PromptTitle,
            message,
            response.UserCode,
            response.DeviceCode,
            openBrowserUrl,
            pollUrl,
            response.IntervalSeconds,
            response.ExpiresInSeconds,
            $"网页登录地址：{openBrowserUrl}");
    }
}

public sealed record MinecraftLaunchMicrosoftDeviceCodePromptPlan(
    string Title,
    string Message,
    string UserCode,
    string DeviceCode,
    string OpenBrowserUrl,
    string PollUrl,
    int PollIntervalSeconds,
    int ExpiresInSeconds,
    string LogMessage);
