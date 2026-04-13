using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftLoginExecutionService
{
    public static MinecraftLaunchMicrosoftLoginStep GetInitialStep(MinecraftLaunchMicrosoftLoginExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ShouldReuseCachedSession)
        {
            return new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.05);
        }

        return request.HasRefreshToken
            ? new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.RefreshOAuthTokens,
                0.05)
            : new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.RequestDeviceCodeOAuthTokens,
                0.05);
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterRefreshOAuth(MinecraftLaunchMicrosoftOAuthRefreshOutcome outcome)
    {
        return outcome switch
        {
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.GetXboxLiveToken,
                0.25),
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.RequireRelogin => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.RequestDeviceCodeOAuthTokens,
                0.05),
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.IgnoreAndContinue => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.99),
            _ => throw new InvalidOperationException("未知的微软登录 OAuth 刷新结果。")
        };
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterDeviceCodeOAuthSuccess()
    {
        return new MinecraftLaunchMicrosoftLoginStep(
            MinecraftLaunchMicrosoftLoginStepKind.GetXboxLiveToken,
            0.25);
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterXboxLiveToken(MinecraftLaunchMicrosoftStepOutcome outcome)
    {
        return outcome switch
        {
            MinecraftLaunchMicrosoftStepOutcome.Succeeded => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.GetXboxSecurityToken,
                0.4),
            MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.99),
            _ => throw new InvalidOperationException("未知的微软登录 Xbox Live Token 结果。")
        };
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterXboxSecurityToken(MinecraftLaunchMicrosoftStepOutcome outcome)
    {
        return outcome switch
        {
            MinecraftLaunchMicrosoftStepOutcome.Succeeded => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftAccessToken,
                0.55),
            MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.99),
            _ => throw new InvalidOperationException("未知的微软登录 XSTS 结果。")
        };
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterMinecraftAccessToken(MinecraftLaunchMicrosoftStepOutcome outcome)
    {
        return outcome switch
        {
            MinecraftLaunchMicrosoftStepOutcome.Succeeded => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.VerifyOwnership,
                0.7),
            MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.99),
            _ => throw new InvalidOperationException("未知的微软登录 Minecraft AccessToken 结果。")
        };
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterOwnershipVerification()
    {
        return new MinecraftLaunchMicrosoftLoginStep(
            MinecraftLaunchMicrosoftLoginStepKind.GetMinecraftProfile,
            0.85);
    }

    public static MinecraftLaunchMicrosoftLoginStep GetStepAfterMinecraftProfile(MinecraftLaunchMicrosoftStepOutcome outcome)
    {
        return outcome switch
        {
            MinecraftLaunchMicrosoftStepOutcome.Succeeded => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.ApplyProfileMutation,
                0.98),
            MinecraftLaunchMicrosoftStepOutcome.IgnoreAndContinue => new MinecraftLaunchMicrosoftLoginStep(
                MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession,
                0.99),
            _ => throw new InvalidOperationException("未知的微软登录档案结果。")
        };
    }
}

public sealed record MinecraftLaunchMicrosoftLoginExecutionRequest(
    bool ShouldReuseCachedSession,
    bool HasRefreshToken);

public sealed record MinecraftLaunchMicrosoftLoginStep(
    MinecraftLaunchMicrosoftLoginStepKind Kind,
    double Progress);

public enum MinecraftLaunchMicrosoftLoginStepKind
{
    FinishWithCachedSession = 0,
    RequestDeviceCodeOAuthTokens = 1,
    RefreshOAuthTokens = 2,
    GetXboxLiveToken = 3,
    GetXboxSecurityToken = 4,
    GetMinecraftAccessToken = 5,
    VerifyOwnership = 6,
    GetMinecraftProfile = 7,
    ApplyProfileMutation = 8
}

public enum MinecraftLaunchMicrosoftOAuthRefreshOutcome
{
    Succeeded = 0,
    RequireRelogin = 1,
    IgnoreAndContinue = 2
}

public enum MinecraftLaunchMicrosoftStepOutcome
{
    Succeeded = 0,
    IgnoreAndContinue = 1
}
