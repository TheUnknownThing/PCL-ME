namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftDeviceCodePromptService
{
    public const string DefaultPollUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string PromptTitle = "Sign in to Minecraft";

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
            ? $"The sign-in page will open automatically. Enter code {response.UserCode} on the page (it will be copied automatically).\n\n" +
              "If your network connection is poor, the page may fail to load; use a VPN and try again.\n" +
              $"You can also open {openBrowserUrl} on another device and enter the code there."
            : "The sign-in page will open automatically and the code will be filled in automatically.\n\n" +
              "If your network connection is poor, the page may fail to load; use a VPN and try again.\n" +
              $"If it is not filled in automatically, paste this code on the page: {response.UserCode} (it will be copied automatically).\n" +
              $"You can also open {openBrowserUrl} on another device and enter the code.";

        return new MinecraftLaunchMicrosoftDeviceCodePromptPlan(
            PromptTitle,
            message,
            response.UserCode,
            response.DeviceCode,
            openBrowserUrl,
            pollUrl,
            response.IntervalSeconds,
            response.ExpiresInSeconds,
            $"Sign-in page URL: {openBrowserUrl}");
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
