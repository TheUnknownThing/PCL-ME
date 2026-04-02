using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchThirdPartyLoginWorkflowService
{
    private const string FailureTitle = "第三方验证失败";

    public static MinecraftLaunchThirdPartyLoginFailure GetValidationTimeoutFailure(string? webResponse)
    {
        var message = "$登录失败：连接登录服务器超时。" + System.Environment.NewLine +
                      "请检查你的网络状况是否良好，或尝试使用 VPN！" + System.Environment.NewLine + System.Environment.NewLine +
                      "详细信息：" + webResponse;
        return new MinecraftLaunchThirdPartyLoginFailure(FailureTitle, message, message);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetValidationFailure(string details)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "验证登录失败: " + details,
            null);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetRefreshFailure(string details)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "刷新登录失败: " + details,
            null);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetLoginHttpFailure(string exceptionDetails, string? responseText)
    {
        var wrappedMessage = TryGetLoginErrorMessage(responseText) ??
                             ("第三方验证登录失败，请检查你的网络状况是否良好。" + System.Environment.NewLine + System.Environment.NewLine +
                              "详细信息：" + responseText);
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "刷新登录失败: " + exceptionDetails,
            "$" + wrappedMessage);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetLoginFailure(string exceptionDetails)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "刷新登录失败: " + exceptionDetails,
            "$第三方验证登录失败" + System.Environment.NewLine + System.Environment.NewLine +
            "详细信息：" + exceptionDetails);
    }

    private static string? TryGetLoginErrorMessage(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            var errorMessage = JsonNode.Parse(responseText)?["errorMessage"]?.ToString();
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return "登录失败：" + errorMessage;
            }
        }
        catch
        {
            // Ignore malformed responses and fall back to the generic message.
        }

        return null;
    }
}

public sealed record MinecraftLaunchThirdPartyLoginFailure(
    string DialogTitle,
    string DialogMessage,
    string? WrappedExceptionMessage);
