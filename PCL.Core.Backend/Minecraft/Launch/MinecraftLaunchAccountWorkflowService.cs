using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchAccountWorkflowService
{
    private const string MicrosoftPasswordResetUrl = "https://account.live.com/password/Change";
    private const string MicrosoftSignupUrl = "https://signup.live.com/signup";
    private const string MicrosoftBirthDateEditUrl = "https://account.live.com/editprof.aspx";
    private const string MicrosoftBirthDateHelpUrl = "https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a";
    private const string MinecraftPurchaseUrl = "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj";
    private const string MinecraftCreateProfileUrl = "https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile";

    public static MinecraftLaunchAccountDecisionPrompt GetPasswordLoginPrompt()
    {
        return new MinecraftLaunchAccountDecisionPrompt(
            $"When signing in, choose {Constants.LeftQuote}Other sign-in options{Constants.RightQuote}, then choose {Constants.LeftQuote}Use my password{Constants.RightQuote}.{Environment.NewLine}If that option is unavailable, choose {Constants.LeftQuote}Set password{Constants.RightQuote} and sign in again after setting it.",
            "Password sign-in required",
            [
                new MinecraftLaunchAccountDecisionOption("Retry sign-in", MinecraftLaunchAccountDecisionKind.Retry),
                new MinecraftLaunchAccountDecisionOption("Set password", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MicrosoftPasswordResetUrl),
                new MinecraftLaunchAccountDecisionOption("Cancel", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt GetMicrosoftRefreshNetworkErrorPrompt(string? stepLabel = null)
    {
        var stepSuffix = string.IsNullOrEmpty(stepLabel) ? string.Empty : $"({stepLabel})";
        return new MinecraftLaunchAccountDecisionPrompt(
            $"The launcher encountered a network error while refreshing account information{stepSuffix}.{Environment.NewLine}You can cancel, check your network, and try again, or ignore the error and continue, but some servers may not work.",
            "Account refresh failed",
            [
                new MinecraftLaunchAccountDecisionOption("Continue", MinecraftLaunchAccountDecisionKind.IgnoreAndContinue),
                new MinecraftLaunchAccountDecisionOption("Cancel", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt? TryGetMicrosoftXstsErrorPrompt(string responseBody)
    {
        ArgumentNullException.ThrowIfNull(responseBody);

        if (responseBody.Contains("2148916227", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                "This Microsoft account appears to be banned and cannot sign in.",
                "Sign-in failed",
                [new MinecraftLaunchAccountDecisionOption("OK", MinecraftLaunchAccountDecisionKind.Abort)],
                IsWarning: true);
        }

        if (responseBody.Contains("2148916233", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                "You have not registered an Xbox account yet. Please register before signing in.",
                "Sign-in notice",
                [
                    new MinecraftLaunchAccountDecisionOption("Register", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MicrosoftSignupUrl),
                    new MinecraftLaunchAccountDecisionOption("Cancel", MinecraftLaunchAccountDecisionKind.Abort)
                ]);
        }

        if (responseBody.Contains("2148916235", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                $"Your country or region cannot sign in to Microsoft accounts.{Environment.NewLine}Please use a VPN or accelerator and try again.",
                "Sign-in failed",
                [new MinecraftLaunchAccountDecisionOption("OK", MinecraftLaunchAccountDecisionKind.Abort)]);
        }

        if (responseBody.Contains("2148916238", StringComparison.Ordinal))
        {
            var followupMessage = "Please update the account birth date on the opened page (set it to at least 18 years old)." + Environment.NewLine +
                                  "After the change is saved, wait one minute, then return to PCL and sign in normally.";
            return new MinecraftLaunchAccountDecisionPrompt(
                "This account is too young. You need to update the birth date before signing in." + Environment.NewLine +
                "Is the age currently entered for this account over 13?",
                "Sign-in notice",
                [
                    new MinecraftLaunchAccountDecisionOption(
                        "Over 13",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateEditUrl,
                        new MinecraftLaunchAccountFollowup(followupMessage, "Sign-in notice")),
                    new MinecraftLaunchAccountDecisionOption(
                        "Under 13",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateHelpUrl,
                        new MinecraftLaunchAccountFollowup(followupMessage, "Sign-in notice")),
                    new MinecraftLaunchAccountDecisionOption(
                        "I do not know",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateHelpUrl,
                        new MinecraftLaunchAccountFollowup(
                            "Please follow the instructions on the opened page and update the account birth date (set it to at least 18 years old)." + Environment.NewLine +
                            "After the change is saved, wait one minute, then return to PCL and sign in normally.",
                            "Sign-in notice"))
                ]);
        }

        return null;
    }

    public static MinecraftLaunchAccountDecisionPrompt GetOwnershipPrompt()
    {
        return new MinecraftLaunchAccountDecisionPrompt(
            "We could not retrieve this account's information. The account may not own Minecraft Java Edition, or the Xbox Game Pass may have expired.",
            "Sign-in failed",
            [
                new MinecraftLaunchAccountDecisionOption("Buy Minecraft", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MinecraftPurchaseUrl),
                new MinecraftLaunchAccountDecisionOption("Cancel", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt GetCreateProfilePrompt()
    {
        return new MinecraftLaunchAccountDecisionPrompt(
            "Please create a Minecraft profile before signing in again.",
            "Sign-in failed",
            [
                new MinecraftLaunchAccountDecisionOption("Create profile", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MinecraftCreateProfileUrl),
                new MinecraftLaunchAccountDecisionOption("Cancel", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAuthProfileSelectionResult ResolveAuthProfileSelection(MinecraftLaunchAuthProfileSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AvailableProfiles.Count == 0)
        {
            return new MinecraftLaunchAuthProfileSelectionResult(
                MinecraftLaunchAuthProfileSelectionKind.Fail,
                NeedsRefresh: false,
                SelectedProfileId: null,
                SelectedProfileName: null,
                FailureMessage: "$You have not created a profile yet. Please create one and try again!",
                NoticeMessage: request.ForceReselectProfile ? "You have not created a profile yet, so it cannot be changed." : null,
                PromptTitle: null,
                PromptOptions: []);
        }

        if (request.ForceReselectProfile && request.AvailableProfiles.Count == 1)
        {
            var onlyProfile = request.AvailableProfiles[0];
            return new MinecraftLaunchAuthProfileSelectionResult(
                MinecraftLaunchAuthProfileSelectionKind.Resolved,
                NeedsRefresh: false,
                SelectedProfileId: onlyProfile.Id,
                SelectedProfileName: onlyProfile.Name,
                FailureMessage: null,
                NoticeMessage: "Your account only has one profile, so it cannot be changed.",
                PromptTitle: null,
                PromptOptions: []);
        }

        if ((string.IsNullOrEmpty(request.ServerSelectedProfileId) || request.ForceReselectProfile) &&
            request.AvailableProfiles.Count > 1)
        {
            var cachedProfile = request.AvailableProfiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, request.CachedProfileId, StringComparison.Ordinal));
            if (cachedProfile is not null)
            {
                return new MinecraftLaunchAuthProfileSelectionResult(
                    MinecraftLaunchAuthProfileSelectionKind.Resolved,
                    NeedsRefresh: true,
                    SelectedProfileId: cachedProfile.Id,
                    SelectedProfileName: cachedProfile.Name,
                    FailureMessage: null,
                    NoticeMessage: null,
                    PromptTitle: null,
                    PromptOptions: []);
            }

            return new MinecraftLaunchAuthProfileSelectionResult(
                MinecraftLaunchAuthProfileSelectionKind.PromptForSelection,
                NeedsRefresh: true,
                SelectedProfileId: null,
                SelectedProfileName: null,
                FailureMessage: null,
                NoticeMessage: null,
                PromptTitle: "Select a profile",
                PromptOptions: request.AvailableProfiles);
        }

        var resolvedProfile = request.AvailableProfiles.FirstOrDefault(profile =>
                                  string.Equals(profile.Id, request.ServerSelectedProfileId, StringComparison.Ordinal)) ??
                              request.AvailableProfiles[0];
        return new MinecraftLaunchAuthProfileSelectionResult(
            MinecraftLaunchAuthProfileSelectionKind.Resolved,
            NeedsRefresh: false,
            SelectedProfileId: resolvedProfile.Id,
            SelectedProfileName: resolvedProfile.Name,
            FailureMessage: null,
            NoticeMessage: null,
            PromptTitle: null,
            PromptOptions: []);
    }
}

public sealed record MinecraftLaunchAccountDecisionPrompt(
    string Message,
    string Title,
    IReadOnlyList<MinecraftLaunchAccountDecisionOption> Options,
    bool IsWarning = false);

public sealed record MinecraftLaunchAccountDecisionOption(
    string Label,
    MinecraftLaunchAccountDecisionKind Decision,
    string? Url = null,
    MinecraftLaunchAccountFollowup? Followup = null);

public sealed record MinecraftLaunchAccountFollowup(
    string Message,
    string Title,
    bool IsWarning = false);

public enum MinecraftLaunchAccountDecisionKind
{
    Retry = 0,
    Abort = 1,
    IgnoreAndContinue = 2,
    OpenUrlAndAbort = 3
}

public sealed record MinecraftLaunchAuthProfileSelectionRequest(
    bool ForceReselectProfile,
    string? CachedProfileId,
    string? ServerSelectedProfileId,
    IReadOnlyList<MinecraftLaunchAuthProfileOption> AvailableProfiles);

public sealed record MinecraftLaunchAuthProfileOption(
    string Id,
    string Name);

public sealed record MinecraftLaunchAuthProfileSelectionResult(
    MinecraftLaunchAuthProfileSelectionKind Kind,
    bool NeedsRefresh,
    string? SelectedProfileId,
    string? SelectedProfileName,
    string? FailureMessage,
    string? NoticeMessage,
    string? PromptTitle,
    IReadOnlyList<MinecraftLaunchAuthProfileOption> PromptOptions)
{
    public bool IsSuccess => Kind != MinecraftLaunchAuthProfileSelectionKind.Fail;
}

public enum MinecraftLaunchAuthProfileSelectionKind
{
    Resolved = 0,
    PromptForSelection = 1,
    Fail = 2
}

internal static class Constants
{
    public const string LeftQuote = "“";
    public const string RightQuote = "”";
}
