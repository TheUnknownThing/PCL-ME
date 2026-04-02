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
            $"请在登录时选择 {Constants.LeftQuote}其他登录方法{Constants.RightQuote}，然后选择 {Constants.LeftQuote}使用我的密码{Constants.RightQuote}。{Environment.NewLine}如果没有该选项，请选择 {Constants.LeftQuote}设置密码{Constants.RightQuote}，设置完毕后再登录。",
            "需要使用密码登录",
            [
                new MinecraftLaunchAccountDecisionOption("重新登录", MinecraftLaunchAccountDecisionKind.Retry),
                new MinecraftLaunchAccountDecisionOption("设置密码", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MicrosoftPasswordResetUrl),
                new MinecraftLaunchAccountDecisionOption("取消", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt GetMicrosoftRefreshNetworkErrorPrompt(string? stepLabel = null)
    {
        var stepSuffix = string.IsNullOrEmpty(stepLabel) ? string.Empty : $"({stepLabel})";
        return new MinecraftLaunchAccountDecisionPrompt(
            $"启动器在尝试刷新账号信息时{stepSuffix}遇到了网络错误。{Environment.NewLine}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
            "账号信息获取失败",
            [
                new MinecraftLaunchAccountDecisionOption("继续", MinecraftLaunchAccountDecisionKind.IgnoreAndContinue),
                new MinecraftLaunchAccountDecisionOption("取消", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt? TryGetMicrosoftXstsErrorPrompt(string responseBody)
    {
        ArgumentNullException.ThrowIfNull(responseBody);

        if (responseBody.Contains("2148916227", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                "该账号似乎已被微软封禁，无法登录。",
                "登录失败",
                [new MinecraftLaunchAccountDecisionOption("我知道了", MinecraftLaunchAccountDecisionKind.Abort)],
                IsWarning: true);
        }

        if (responseBody.Contains("2148916233", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                "你尚未注册 Xbox 账户，请在注册后再登录。",
                "登录提示",
                [
                    new MinecraftLaunchAccountDecisionOption("注册", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MicrosoftSignupUrl),
                    new MinecraftLaunchAccountDecisionOption("取消", MinecraftLaunchAccountDecisionKind.Abort)
                ]);
        }

        if (responseBody.Contains("2148916235", StringComparison.Ordinal))
        {
            return new MinecraftLaunchAccountDecisionPrompt(
                $"你的网络所在的国家或地区无法登录微软账号。{Environment.NewLine}请使用加速器或 VPN。",
                "登录失败",
                [new MinecraftLaunchAccountDecisionOption("我知道了", MinecraftLaunchAccountDecisionKind.Abort)]);
        }

        if (responseBody.Contains("2148916238", StringComparison.Ordinal))
        {
            var followupMessage = "请在打开的网页中修改账号的出生日期（至少改为 18 岁以上）。" + Environment.NewLine +
                                  "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！";
            return new MinecraftLaunchAccountDecisionPrompt(
                "该账号年龄不足，你需要先修改出生日期，然后才能登录。" + Environment.NewLine +
                "该账号目前填写的年龄是否在 13 岁以上？",
                "登录提示",
                [
                    new MinecraftLaunchAccountDecisionOption(
                        "13 岁以上",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateEditUrl,
                        new MinecraftLaunchAccountFollowup(followupMessage, "登录提示")),
                    new MinecraftLaunchAccountDecisionOption(
                        "12 岁以下",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateHelpUrl,
                        new MinecraftLaunchAccountFollowup(followupMessage, "登录提示")),
                    new MinecraftLaunchAccountDecisionOption(
                        "我不知道",
                        MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort,
                        MicrosoftBirthDateHelpUrl,
                        new MinecraftLaunchAccountFollowup(
                            "请根据打开的网页的说明，修改账号的出生日期（至少改为 18 岁以上）。" + Environment.NewLine +
                            "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！",
                            "登录提示"))
                ]);
        }

        return null;
    }

    public static MinecraftLaunchAccountDecisionPrompt GetOwnershipPrompt()
    {
        return new MinecraftLaunchAccountDecisionPrompt(
            "暂时无法获取到此账户信息，此账户可能没有购买 Minecraft Java Edition 或者账户的 Xbox Game Pass 已过期",
            "登录失败",
            [
                new MinecraftLaunchAccountDecisionOption("购买 Minecraft", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MinecraftPurchaseUrl),
                new MinecraftLaunchAccountDecisionOption("取消", MinecraftLaunchAccountDecisionKind.Abort)
            ]);
    }

    public static MinecraftLaunchAccountDecisionPrompt GetCreateProfilePrompt()
    {
        return new MinecraftLaunchAccountDecisionPrompt(
            "请先创建 Minecraft 玩家档案，然后再重新登录。",
            "登录失败",
            [
                new MinecraftLaunchAccountDecisionOption("创建档案", MinecraftLaunchAccountDecisionKind.OpenUrlAndAbort, MinecraftCreateProfileUrl),
                new MinecraftLaunchAccountDecisionOption("取消", MinecraftLaunchAccountDecisionKind.Abort)
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
                FailureMessage: "$你还没有创建角色，请在创建角色后再试！",
                NoticeMessage: request.ForceReselectProfile ? "你还没有创建角色，无法更换！" : null,
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
                NoticeMessage: "你的账户中只有一个角色，无法更换！",
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
                PromptTitle: "选择使用的角色",
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
