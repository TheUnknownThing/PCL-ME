using System;
using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupConsentService
{
    private const string LatestReleaseUrl = "https://github.com/TheUnknownThing/PCL-ME/releases/latest";
    private const string EulaUrl = "https://shimo.im/docs/rGrd8pY8xWkt6ryW";

    public static LauncherStartupConsentResult Evaluate(LauncherStartupConsentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompts = new List<LauncherStartupPrompt>();

        if (!request.IsSpecialBuildHintDisabled)
        {
            var specialBuildPrompt = CreateSpecialBuildPrompt(request.SpecialBuildKind);
            if (specialBuildPrompt is not null)
            {
                prompts.Add(specialBuildPrompt);
            }
        }

        if (!request.HasAcceptedEula)
        {
            prompts.Add(CreateEulaPrompt());
        }

        return new LauncherStartupConsentResult(prompts);
    }

    private static LauncherStartupPrompt? CreateSpecialBuildPrompt(LauncherStartupSpecialBuildKind kind)
    {
        string? hint = kind switch
        {
            LauncherStartupSpecialBuildKind.Debug => "当前运行的 PCL 跨平台版为 Debug 版本。" + Environment.NewLine +
                                                     "该版本仅适合开发者调试运行，可能会有严重的性能下降以及各种奇怪的网络问题。" + Environment.NewLine +
                                                     Environment.NewLine +
                                                     "非开发者用户使用该版本造成的一切问题均不被社区支持，相关 issue 可能会被直接关闭。" + Environment.NewLine +
                                                     "除非您是开发者，否则请立即删除该版本，并下载最新稳定版使用。",
            LauncherStartupSpecialBuildKind.Ci => "当前运行的 PCL 跨平台版为 CI 自动构建版本。" + Environment.NewLine +
                                                  "该版本包含最新的漏洞修复、优化和新特性，但性能和稳定性较差，不适合日常使用和制作整合包。" + Environment.NewLine +
                                                  Environment.NewLine +
                                                  "除非社区开发者要求或您自己想要这么做，否则请下载最新稳定版使用。",
            _ => null
        };
        if (hint is null) return null;

        return new LauncherStartupPrompt(
            $"{hint}{Environment.NewLine}{Environment.NewLine}可以添加 PCL_DISABLE_DEBUG_HINT 环境变量 (任意值) 来隐藏这个提示。",
            "特殊版本提示",
            [
                new LauncherStartupPromptButton("我清楚我在做什么", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Continue)]),
                new LauncherStartupPromptButton(
                "打开最新版下载页并退出",
                [
                    new LauncherStartupPromptAction(LauncherStartupPromptActionKind.OpenUrl, LatestReleaseUrl),
                    new LauncherStartupPromptAction(LauncherStartupPromptActionKind.ExitLauncher)
                ])
            ],
            IsWarning: true);
    }

    private static LauncherStartupPrompt CreateEulaPrompt()
    {
        return new LauncherStartupPrompt(
            "在使用 PCL 前，请同意 PCL 的用户协议与免责声明。",
            "协议授权",
            [
                new LauncherStartupPromptButton("同意", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Accept)]),
                new LauncherStartupPromptButton(
                    "拒绝",
                    [
                        new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Reject),
                        new LauncherStartupPromptAction(LauncherStartupPromptActionKind.ExitLauncher)
                    ]),
                new LauncherStartupPromptButton(
                    "查看用户协议与免责声明",
                    [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.OpenUrl, EulaUrl)],
                    ClosesPrompt: false)
            ]);
    }
}
