using System;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchThirdPartyLoginExecutionService
{
    public static MinecraftLaunchThirdPartyLoginStep GetInitialStep(MinecraftLaunchThirdPartyLoginExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.ShouldSkipCachedSessionRecovery
            ? new MinecraftLaunchThirdPartyLoginStep(
                MinecraftLaunchThirdPartyLoginStepKind.Authenticate,
                0.05,
                HasRetriedRefresh: false)
            : new MinecraftLaunchThirdPartyLoginStep(
                MinecraftLaunchThirdPartyLoginStepKind.ValidateCachedSession,
                0.05,
                HasRetriedRefresh: false);
    }

    public static MinecraftLaunchThirdPartyLoginStep GetStepAfterValidateSuccess()
    {
        return CreateFinishStep();
    }

    public static MinecraftLaunchThirdPartyLoginStep GetStepAfterValidateFailure()
    {
        return new MinecraftLaunchThirdPartyLoginStep(
            MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession,
            0.25,
            HasRetriedRefresh: false);
    }

    public static MinecraftLaunchThirdPartyLoginStep GetStepAfterRefreshSuccess(bool hasRetriedRefresh)
    {
        return CreateFinishStep(hasRetriedRefresh);
    }

    public static MinecraftLaunchThirdPartyLoginStep GetStepAfterRefreshFailure(bool hasRetriedRefresh)
    {
        return hasRetriedRefresh
            ? new MinecraftLaunchThirdPartyLoginStep(
                MinecraftLaunchThirdPartyLoginStepKind.Fail,
                0.65,
                HasRetriedRefresh: true,
                FailureMessage: "Second refresh login failed")
            : new MinecraftLaunchThirdPartyLoginStep(
                MinecraftLaunchThirdPartyLoginStepKind.Authenticate,
                0.45,
                HasRetriedRefresh: false);
    }

    public static MinecraftLaunchThirdPartyLoginStep GetStepAfterLoginSuccess(bool needsRefresh)
    {
        return needsRefresh
            ? new MinecraftLaunchThirdPartyLoginStep(
                MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession,
                0.65,
                HasRetriedRefresh: true)
            : CreateFinishStep();
    }

    private static MinecraftLaunchThirdPartyLoginStep CreateFinishStep(bool hasRetriedRefresh = false)
    {
        return new MinecraftLaunchThirdPartyLoginStep(
            MinecraftLaunchThirdPartyLoginStepKind.Finish,
            0.95,
            hasRetriedRefresh);
    }
}

public sealed record MinecraftLaunchThirdPartyLoginExecutionRequest(
    bool ShouldSkipCachedSessionRecovery);

public sealed record MinecraftLaunchThirdPartyLoginStep(
    MinecraftLaunchThirdPartyLoginStepKind Kind,
    double Progress,
    bool HasRetriedRefresh,
    string? FailureMessage = null);

public enum MinecraftLaunchThirdPartyLoginStepKind
{
    ValidateCachedSession = 0,
    RefreshCachedSession = 1,
    Authenticate = 2,
    Finish = 3,
    Fail = 4
}
