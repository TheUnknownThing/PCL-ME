using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchThirdPartyLoginWorkflowService
{
    private const string FailureTitle = "Third-party verification failed";

    public static MinecraftLaunchThirdPartyLoginFailureResolution ResolveValidationHttpFailure(string exceptionDetails, string? webResponse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionDetails);

        var isTimeout = (exceptionDetails.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                         exceptionDetails.Contains("imeout", StringComparison.Ordinal)) &&
                        !exceptionDetails.Contains("403", StringComparison.Ordinal);
        if (isTimeout)
        {
            var failure = GetValidationTimeoutFailure(webResponse);
            return new MinecraftLaunchThirdPartyLoginFailureResolution(
                MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped,
                Failure: failure,
                WrappedExceptionMessage: failure.WrappedExceptionMessage);
        }

        return new MinecraftLaunchThirdPartyLoginFailureResolution(
            MinecraftLaunchThirdPartyLoginFailureResolutionKind.AdvanceToStep,
            NextStep: MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterValidateFailure());
    }

    public static MinecraftLaunchThirdPartyLoginFailureResolution ResolveValidationFailure(string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        return new MinecraftLaunchThirdPartyLoginFailureResolution(
            MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndRethrow,
            Failure: GetValidationFailure(details));
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetValidationTimeoutFailure(string? webResponse)
    {
        var message = "$Login failed: the connection to the login server timed out." + System.Environment.NewLine +
                      "Check your network connection or try using a VPN." + System.Environment.NewLine + System.Environment.NewLine +
                      "Details: " + webResponse;
        return new MinecraftLaunchThirdPartyLoginFailure(FailureTitle, message, message);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetValidationFailure(string details)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "Validation login failed: " + details,
            null);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetRefreshFailure(string details)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "Refresh login failed: " + details,
            null);
    }

    public static MinecraftLaunchThirdPartyLoginFailureResolution ResolveRefreshFailure(string details, bool hasRetriedRefresh)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        var failure = GetRefreshFailure(details);
        var nextStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshFailure(hasRetriedRefresh);
        return nextStep.Kind == MinecraftLaunchThirdPartyLoginStepKind.Fail
            ? new MinecraftLaunchThirdPartyLoginFailureResolution(
                MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped,
                NextStep: nextStep,
                Failure: failure,
                WrappedExceptionMessage: nextStep.FailureMessage)
            : new MinecraftLaunchThirdPartyLoginFailureResolution(
                MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndAdvance,
                NextStep: nextStep,
                Failure: failure);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetLoginHttpFailure(string exceptionDetails, string? responseText)
    {
        var wrappedMessage = TryGetLoginErrorMessage(responseText) ??
                             ("Third-party verification login failed. Check your network connection." + System.Environment.NewLine + System.Environment.NewLine +
                              "Details: " + responseText);
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "Refresh login failed: " + exceptionDetails,
            "$" + wrappedMessage);
    }

    public static MinecraftLaunchThirdPartyLoginFailure GetLoginFailure(string exceptionDetails)
    {
        return new MinecraftLaunchThirdPartyLoginFailure(
            FailureTitle,
            "Refresh login failed: " + exceptionDetails,
            "$Third-party verification login failed" + System.Environment.NewLine + System.Environment.NewLine +
            "Details: " + exceptionDetails);
    }

    public static MinecraftLaunchThirdPartyLoginFailureResolution ResolveLoginHttpFailure(string exceptionDetails, string? responseText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionDetails);

        var failure = GetLoginHttpFailure(exceptionDetails, responseText);
        return new MinecraftLaunchThirdPartyLoginFailureResolution(
            MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped,
            Failure: failure,
            WrappedExceptionMessage: failure.WrappedExceptionMessage);
    }

    public static MinecraftLaunchThirdPartyLoginFailureResolution ResolveLoginFailure(string exceptionDetails)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exceptionDetails);

        var failure = GetLoginFailure(exceptionDetails);
        return new MinecraftLaunchThirdPartyLoginFailureResolution(
            MinecraftLaunchThirdPartyLoginFailureResolutionKind.ShowFailureAndThrowWrapped,
            Failure: failure,
            WrappedExceptionMessage: failure.WrappedExceptionMessage);
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
                return "Login failed: " + errorMessage;
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

public sealed record MinecraftLaunchThirdPartyLoginFailureResolution(
    MinecraftLaunchThirdPartyLoginFailureResolutionKind Kind,
    MinecraftLaunchThirdPartyLoginStep? NextStep = null,
    MinecraftLaunchThirdPartyLoginFailure? Failure = null,
    string? WrappedExceptionMessage = null);

public enum MinecraftLaunchThirdPartyLoginFailureResolutionKind
{
    AdvanceToStep = 0,
    ShowFailureAndAdvance = 1,
    ShowFailureAndThrowWrapped = 2,
    ShowFailureAndRethrow = 3
}
