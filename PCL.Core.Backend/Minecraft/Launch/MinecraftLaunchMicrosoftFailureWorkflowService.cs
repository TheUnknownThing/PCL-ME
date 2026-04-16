using System;
using System.Net;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchMicrosoftFailureWorkflowService
{
    private static readonly string IpBlockedMessage = "$This IP is showing unusual sign-in activity." + Environment.NewLine +
                                                      "If you are using a VPN or accelerator, turn it off or switch nodes and try again.";

    public static MinecraftLaunchMicrosoftFailureResolution ResolveOAuthRefreshFailure(string exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(exceptionMessage))
        {
            throw new ArgumentException("Exception message cannot be empty.", nameof(exceptionMessage));
        }

        if (exceptionMessage.Contains("must sign in again", StringComparison.OrdinalIgnoreCase) ||
            exceptionMessage.Contains("password expired", StringComparison.OrdinalIgnoreCase) ||
            (exceptionMessage.Contains("refresh_token", StringComparison.OrdinalIgnoreCase) &&
             exceptionMessage.Contains("is not valid", StringComparison.OrdinalIgnoreCase)))
        {
            return new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.RequireRelogin);
        }

        return GetRetryableStepFailure();
    }

    public static MinecraftLaunchMicrosoftFailureResolution GetRetryableStepFailure(string? stepLabel = null)
    {
        return new MinecraftLaunchMicrosoftFailureResolution(
            MinecraftLaunchMicrosoftFailureResolutionKind.OfferIgnoreAndContinue,
            StepLabel: stepLabel);
    }

    public static MinecraftLaunchMicrosoftFailureResolution ResolveXstsFailure(string responseBody)
    {
        ArgumentNullException.ThrowIfNull(responseBody);

        var prompt = MinecraftLaunchAccountWorkflowService.TryGetMicrosoftXstsErrorPrompt(responseBody);
        if (prompt is not null)
        {
            return new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort,
                Prompt: prompt);
        }

        return GetRetryableStepFailure("Step 3");
    }

    public static MinecraftLaunchMicrosoftFailureResolution ResolveMinecraftAccessTokenFailure(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException,
                WrappedExceptionMessage: "$Sign-in attempts are too frequent. Please wait a few minutes and try again."),
            HttpStatusCode.Forbidden => new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException,
                WrappedExceptionMessage: IpBlockedMessage),
            _ => GetRetryableStepFailure("Step 4")
        };
    }

    public static MinecraftLaunchAccountDecisionPrompt? TryGetOwnershipFailurePrompt(string responseBody)
    {
        ArgumentNullException.ThrowIfNull(responseBody);
        return MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(responseBody)
            ? null
            : MinecraftLaunchAccountWorkflowService.GetOwnershipPrompt();
    }

    public static MinecraftLaunchMicrosoftFailureResolution ResolveMinecraftProfileFailure(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.ThrowWrappedException,
                WrappedExceptionMessage: "$Sign-in attempts are too frequent. Please wait a few minutes and try again."),
            HttpStatusCode.NotFound => new MinecraftLaunchMicrosoftFailureResolution(
                MinecraftLaunchMicrosoftFailureResolutionKind.ShowPromptAndAbort,
                Prompt: MinecraftLaunchAccountWorkflowService.GetCreateProfilePrompt()),
            _ => GetRetryableStepFailure("Step 6")
        };
    }
}

public enum MinecraftLaunchMicrosoftFailureResolutionKind
{
    OfferIgnoreAndContinue = 0,
    RequireRelogin = 1,
    ShowPromptAndAbort = 2,
    ThrowWrappedException = 3
}

public sealed record MinecraftLaunchMicrosoftFailureResolution(
    MinecraftLaunchMicrosoftFailureResolutionKind Kind,
    string? StepLabel = null,
    string? WrappedExceptionMessage = null,
    MinecraftLaunchAccountDecisionPrompt? Prompt = null);
