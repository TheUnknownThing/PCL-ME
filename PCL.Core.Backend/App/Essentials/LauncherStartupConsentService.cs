using System;
using System.Collections.Generic;
using PCL.Core.App.I18n;

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
            LauncherStartupSpecialBuildKind.Debug => "startup.prompts.special_build.debug.message",
            LauncherStartupSpecialBuildKind.Ci => "startup.prompts.special_build.ci.message",
            _ => null
        };
        if (hint is null) return null;

        return new LauncherStartupPrompt(
            I18nText.WithArgs(
                hint,
                I18nTextArgument.String("hint_env_var", "PCL_DISABLE_DEBUG_HINT"),
                I18nTextArgument.String("latest_release_url", LatestReleaseUrl)),
            I18nText.Plain("startup.prompts.special_build.title"),
            [
                new LauncherStartupPromptButton(
                    I18nText.Plain("startup.prompts.special_build.actions.continue"),
                    [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Continue)]),
                new LauncherStartupPromptButton(
                I18nText.Plain("startup.prompts.special_build.actions.open_release_and_exit"),
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
            I18nText.WithArgs(
                "startup.prompts.eula.message",
                I18nTextArgument.String("eula_url", EulaUrl)),
            I18nText.Plain("startup.prompts.eula.title"),
            [
                new LauncherStartupPromptButton(
                    I18nText.Plain("startup.prompts.eula.actions.accept"),
                    [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Accept)]),
                new LauncherStartupPromptButton(
                    I18nText.Plain("startup.prompts.eula.actions.reject"),
                    [
                        new LauncherStartupPromptAction(LauncherStartupPromptActionKind.Reject),
                        new LauncherStartupPromptAction(LauncherStartupPromptActionKind.ExitLauncher)
                    ]),
                new LauncherStartupPromptButton(
                    I18nText.Plain("startup.prompts.eula.actions.open"),
                    [new LauncherStartupPromptAction(LauncherStartupPromptActionKind.OpenUrl, EulaUrl)],
                    ClosesPrompt: false)
            ]);
    }
}
