using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchPrecheckService
{
    private const string MinecraftPurchaseUrl = "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj";

    public static MinecraftLaunchPrecheckResult Evaluate(MinecraftLaunchPrecheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InstancePathIndie.Contains('!') || request.InstancePathIndie.Contains(';'))
        {
            return Failed($"游戏路径中不可包含 ! 或 ;（{request.InstancePathIndie}）");
        }

        if (request.InstancePath.Contains('!') || request.InstancePath.Contains(';'))
        {
            return Failed($"游戏路径中不可包含 ! 或 ;（{request.InstancePath}）");
        }

        if (!request.IsInstanceSelected)
        {
            return Failed("未选择 Minecraft 实例！");
        }

        if (request.IsInstanceError)
        {
            return Failed("Minecraft 存在问题：" + request.InstanceErrorDescription);
        }

        var profileError = GetProfileError(request);
        if (!string.IsNullOrEmpty(profileError))
        {
            return Failed(profileError);
        }

        var prompts = new List<MinecraftLaunchPrompt>();

        if (request.IsUtf8CodePage && !request.IsNonAsciiPathWarningDisabled && !request.IsInstancePathAscii)
        {
            prompts.Add(new MinecraftLaunchPrompt(
                $"欲启动实例 \"{request.InstanceName}\" 的路径中存在可能影响游戏正常运行的字符（非 ASCII 字符），是否仍旧启动游戏？{Environment.NewLine}{Environment.NewLine}如果不清楚具体作用，你可以先选择 \"继续\"，发现游戏在启动后很快出现崩溃的情况后再尝试修改游戏路径等操作",
                "游戏路径检查",
                [
                    new MinecraftLaunchPromptButton("继续", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)]),
                    new MinecraftLaunchPromptButton("返回处理", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)]),
                    new MinecraftLaunchPromptButton("不再提示",
                    [
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.PersistNonAsciiPathWarningDisabled),
                        new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                    ])
                ]));
        }

        if (!request.HasMicrosoftProfile)
        {
            prompts.Add(request.IsRestrictedFeatureAllowed
                ? CreateOptionalPurchasePrompt()
                : CreateRequiredPurchasePrompt());
        }

        return new MinecraftLaunchPrecheckResult(null, prompts);
    }

    private static string? GetProfileError(MinecraftLaunchPrecheckRequest request)
    {
        var checkResult = request.ProfileValidationMessage;
        if (request.SelectedProfileKind == MinecraftLaunchProfileKind.None)
        {
            checkResult = "请先选择一个档案再启动游戏！";
        }
        else if (request.HasLabyMod || request.LoginRequirement == MinecraftLaunchLoginRequirement.Microsoft)
        {
            if (request.SelectedProfileKind != MinecraftLaunchProfileKind.Microsoft)
            {
                checkResult = "当前实例要求使用正版验证，请使用正版验证档案启动游戏！";
            }
        }
        else if (request.LoginRequirement == MinecraftLaunchLoginRequirement.Auth)
        {
            if (request.SelectedProfileKind != MinecraftLaunchProfileKind.Auth)
            {
                checkResult = "当前实例要求使用第三方验证，请使用第三方验证档案启动游戏！";
            }
            else if (!string.Equals(request.SelectedAuthServer, request.RequiredAuthServer, StringComparison.Ordinal))
            {
                checkResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！";
            }
        }
        else if (request.LoginRequirement == MinecraftLaunchLoginRequirement.MicrosoftOrAuth)
        {
            if (request.SelectedProfileKind == MinecraftLaunchProfileKind.Legacy)
            {
                checkResult = "当前实例要求使用正版验证或第三方验证，请使用符合要求的档案启动游戏！";
            }
            else if (request.SelectedProfileKind == MinecraftLaunchProfileKind.Auth &&
                     !string.Equals(request.SelectedAuthServer, request.RequiredAuthServer, StringComparison.Ordinal))
            {
                checkResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！";
            }
        }

        return string.IsNullOrWhiteSpace(checkResult) ? null : checkResult;
    }

    private static MinecraftLaunchPrompt CreateOptionalPurchasePrompt()
    {
        return new MinecraftLaunchPrompt(
            $"看起来你似乎没买正版...{Environment.NewLine}如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易...不要一直白嫖啦。{Environment.NewLine}{Environment.NewLine}在验证一个正版账号之后，就不会出现这个提示了！",
            "考虑一下正版？",
            [
                new MinecraftLaunchPromptButton("支持正版游戏！",
                [
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, MinecraftPurchaseUrl),
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                ]),
                new MinecraftLaunchPromptButton("下次一定", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)])
            ]);
    }

    private static MinecraftLaunchPrompt CreateRequiredPurchasePrompt()
    {
        return new MinecraftLaunchPrompt(
            "你必须先登录正版账号才能启动游戏！",
            "正版验证",
            [
                new MinecraftLaunchPromptButton("购买正版",
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, MinecraftPurchaseUrl)],
                    ClosesPrompt: false),
                new MinecraftLaunchPromptButton("试玩",
                [
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.AppendLaunchArgument, "--demo"),
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                ]),
                new MinecraftLaunchPromptButton("返回", [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)])
            ]);
    }

    private static MinecraftLaunchPrecheckResult Failed(string message)
    {
        return new MinecraftLaunchPrecheckResult(message, []);
    }
}
