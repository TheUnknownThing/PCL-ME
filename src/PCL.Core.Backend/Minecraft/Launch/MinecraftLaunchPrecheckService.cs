using System;
using System.Collections.Generic;
using PCL.Core.App.I18n;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchPrecheckService
{
    private const string MinecraftPurchaseUrl = "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj";

    public static MinecraftLaunchPrecheckResult Evaluate(MinecraftLaunchPrecheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InstancePathIndie.Contains('!') || request.InstancePathIndie.Contains(';'))
        {
            return Failed(new MinecraftLaunchPrecheckFailure(
                MinecraftLaunchPrecheckFailureKind.InstanceIndiePathContainsReservedCharacters,
                Path: request.InstancePathIndie));
        }

        if (request.InstancePath.Contains('!') || request.InstancePath.Contains(';'))
        {
            return Failed(new MinecraftLaunchPrecheckFailure(
                MinecraftLaunchPrecheckFailureKind.InstancePathContainsReservedCharacters,
                Path: request.InstancePath));
        }

        if (!request.IsInstanceSelected)
        {
            return Failed(new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.InstanceNotSelected));
        }

        if (request.IsInstanceError)
        {
            return Failed(new MinecraftLaunchPrecheckFailure(
                MinecraftLaunchPrecheckFailureKind.InstanceHasError,
                Detail: request.InstanceErrorDescription));
        }

        var profileError = GetProfileError(request);
        if (profileError is not null)
        {
            return Failed(profileError);
        }

        var prompts = new List<MinecraftLaunchPrompt>();

        if (request.IsUtf8CodePage && !request.IsNonAsciiPathWarningDisabled && !request.IsInstancePathAscii)
        {
            prompts.Add(new MinecraftLaunchPrompt(
                I18nText.WithArgs(
                    "launch.prompts.non_ascii_path.message",
                    I18nTextArgument.String("instance_name", request.InstanceName)),
                I18nText.Plain("launch.prompts.non_ascii_path.title"),
                [
                    new MinecraftLaunchPromptButton(
                        I18nText.Plain("launch.prompts.non_ascii_path.actions.continue"),
                        [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)]),
                    new MinecraftLaunchPromptButton(
                        I18nText.Plain("launch.prompts.non_ascii_path.actions.abort"),
                        [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)]),
                    new MinecraftLaunchPromptButton(
                        I18nText.Plain("launch.prompts.non_ascii_path.actions.disable"),
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

    private static MinecraftLaunchPrecheckFailure? GetProfileError(MinecraftLaunchPrecheckRequest request)
    {
        if (request.SelectedProfileKind == MinecraftLaunchProfileKind.None)
        {
            return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.NoProfileSelected);
        }

        if (request.HasLabyMod || request.LoginRequirement == MinecraftLaunchLoginRequirement.Microsoft)
        {
            if (request.SelectedProfileKind != MinecraftLaunchProfileKind.Microsoft)
            {
                return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.MicrosoftProfileRequired);
            }
        }
        else if (request.LoginRequirement == MinecraftLaunchLoginRequirement.Auth)
        {
            if (request.SelectedProfileKind != MinecraftLaunchProfileKind.Auth)
            {
                return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.AuthProfileRequired);
            }

            if (!string.Equals(request.SelectedAuthServer, request.RequiredAuthServer, StringComparison.Ordinal))
            {
                return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.AuthServerMismatch);
            }
        }
        else if (request.LoginRequirement == MinecraftLaunchLoginRequirement.MicrosoftOrAuth)
        {
            if (request.SelectedProfileKind == MinecraftLaunchProfileKind.Legacy)
            {
                return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.MicrosoftOrAuthProfileRequired);
            }

            if (request.SelectedProfileKind == MinecraftLaunchProfileKind.Auth &&
                !string.Equals(request.SelectedAuthServer, request.RequiredAuthServer, StringComparison.Ordinal))
            {
                return new MinecraftLaunchPrecheckFailure(MinecraftLaunchPrecheckFailureKind.AuthServerMismatch);
            }
        }

        return null;
    }

    private static MinecraftLaunchPrompt CreateOptionalPurchasePrompt()
    {
        return new MinecraftLaunchPrompt(
            I18nText.WithArgs(
                "launch.prompts.purchase.optional.message",
                I18nTextArgument.String("purchase_url", MinecraftPurchaseUrl)),
            I18nText.Plain("launch.prompts.purchase.optional.title"),
            [
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.purchase.optional.actions.buy"),
                [
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, MinecraftPurchaseUrl),
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                ]),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.purchase.optional.actions.continue"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)])
            ]);
    }

    private static MinecraftLaunchPrompt CreateRequiredPurchasePrompt()
    {
        return new MinecraftLaunchPrompt(
            I18nText.Plain("launch.prompts.purchase.required.message"),
            I18nText.Plain("launch.prompts.purchase.required.title"),
            [
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.purchase.required.actions.buy"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.OpenUrl, MinecraftPurchaseUrl)],
                    ClosesPrompt: false),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.purchase.required.actions.demo"),
                [
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.AppendLaunchArgument, "--demo"),
                    new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Continue)
                ]),
                new MinecraftLaunchPromptButton(
                    I18nText.Plain("launch.prompts.purchase.required.actions.abort"),
                    [new MinecraftLaunchPromptAction(MinecraftLaunchPromptActionKind.Abort)])
            ]);
    }

    private static MinecraftLaunchPrecheckResult Failed(MinecraftLaunchPrecheckFailure failure)
    {
        return new MinecraftLaunchPrecheckResult(failure, []);
    }
}
