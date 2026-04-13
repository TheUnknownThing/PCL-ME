using System;
using System.Collections.Generic;

namespace PCL.Core.App.Essentials;

public static class LauncherStartupConsentService
{
    private const string LatestReleaseUrl = "https://github.com/PCL-Community/PCL2-CE/releases/latest";
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

        if (request.IsTelemetryDefault)
        {
            prompts.Add(CreateTelemetryPrompt());
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

    private static LauncherStartupPrompt CreateTelemetryPrompt()
    {
        return new LauncherStartupPrompt(
            "启用遥测数据收集后，启动器将会收集并上报错误与设备环境信息，这可以帮助开发者修复潜在的问题、更好的进行规划和开发。" + Environment.NewLine +
            "若启用此功能，我们将会收集以下信息：" + Environment.NewLine + Environment.NewLine +
            "- 启动器内出现的错误" + Environment.NewLine +
            "- 启动器版本信息与识别码" + Environment.NewLine +
            "- Windows 系统版本与架构" + Environment.NewLine +
            "- 已安装的物理内存大小" + Environment.NewLine +
            "- NAT 与 IPv6 支持情况" + Environment.NewLine +
            "- 是否使用过官方版 PCL、HMCL 或 BakaXL" + Environment.NewLine + Environment.NewLine +
            "这些数据均不与你关联，我们也绝不会向第三方出售数据。" + Environment.NewLine +
            "如果不希望启用遥测，可以选择拒绝。这不会影响其他功能的正常使用，但可能会影响开发者修复潜在 Bug。" + Environment.NewLine +
            "你可以随时在启动器设置中调整这项设置。",
            "启用遥测数据收集",
            [
                new LauncherStartupPromptButton("同意", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.SetTelemetryEnabled, bool.TrueString)]),
                new LauncherStartupPromptButton("拒绝", [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.SetTelemetryEnabled, bool.FalseString)])
            ]);
    }
}
