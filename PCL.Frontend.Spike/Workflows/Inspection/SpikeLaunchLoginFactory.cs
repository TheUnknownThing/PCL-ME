using System.Text.Json;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows.Inspection;

internal static class SpikeLaunchLoginFactory
{
    public static LaunchLoginSpikePlan BuildPlan(LaunchLoginSpikeInputs inputs)
    {
        return inputs.Provider switch
        {
            LaunchLoginProviderKind.Microsoft when inputs.Microsoft is not null => BuildMicrosoftPlan(inputs.Microsoft),
            LaunchLoginProviderKind.Authlib when inputs.Authlib is not null => BuildAuthlibPlan(inputs.Authlib),
            _ => throw new InvalidOperationException($"Missing launch login inputs for provider '{inputs.Provider}'.")
        };
    }

    private static LaunchLoginSpikePlan BuildMicrosoftPlan(MicrosoftLaunchLoginSpikeInputs inputs)
    {
        var shouldReuseCachedSession = MinecraftLaunchLoginProfileWorkflowService.ShouldReuseMicrosoftLogin(inputs.SessionReuseRequest);
        var currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetInitialStep(
            new MinecraftLaunchMicrosoftLoginExecutionRequest(
                shouldReuseCachedSession,
                HasRefreshToken: !string.IsNullOrWhiteSpace(inputs.OAuthRefreshToken)));

        if (currentStep.Kind == MinecraftLaunchMicrosoftLoginStepKind.FinishWithCachedSession)
        {
            return new LaunchLoginSpikePlan(
                LaunchLoginProviderKind.Microsoft,
                [
                    new LaunchLoginSpikeStepPlan(
                        "Reuse cached Microsoft session",
                        currentStep.Progress,
                        Method: null,
                        Url: null,
                        ContentType: null,
                        RequestBody: null,
                        ResponseBody: null,
                        Notes:
                        [
                            "Cached Microsoft session is still inside the refresh reuse window.",
                            "No network request would be sent before launch continues."
                        ])
                ],
                MutationPlan: null);
        }

        var steps = new List<LaunchLoginSpikeStepPlan>();

        var refreshRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOAuthRefreshRequest(inputs.OAuthRefreshToken);
        var refreshResponse = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(inputs.OAuthRefreshResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Refresh OAuth tokens",
            currentStep.Progress,
            refreshRequestPlan.Method,
            refreshRequestPlan.Url,
            refreshRequestPlan.ContentType,
            refreshRequestPlan.Body,
            PrettyJson(inputs.OAuthRefreshResponseJson),
            [
                $"Refreshed OAuth access token length: {refreshResponse.AccessToken.Length}",
                $"Refreshed OAuth refresh token length: {refreshResponse.RefreshToken.Length}"
            ]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded);

        var xboxRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildXboxLiveTokenRequest(refreshResponse.AccessToken);
        var xboxLiveToken = MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(inputs.XboxLiveResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Xbox Live token",
            currentStep.Progress,
            xboxRequestPlan.Method,
            xboxRequestPlan.Url,
            xboxRequestPlan.ContentType,
            PrettyJson(xboxRequestPlan.Body!),
            PrettyJson(inputs.XboxLiveResponseJson),
            [$"Parsed XBL token length: {xboxLiveToken.Length}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxLiveToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var xstsRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildXstsTokenRequest(xboxLiveToken);
        var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(inputs.XstsResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Xbox security token",
            currentStep.Progress,
            xstsRequestPlan.Method,
            xstsRequestPlan.Url,
            xstsRequestPlan.ContentType,
            PrettyJson(xstsRequestPlan.Body!),
            PrettyJson(inputs.XstsResponseJson),
            [
                $"Parsed XSTS token length: {xstsResponse.Token.Length}",
                $"Parsed user hash: {xstsResponse.UserHash}"
            ]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxSecurityToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var minecraftAccessRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildMinecraftAccessTokenRequest(
            xstsResponse.UserHash,
            xstsResponse.Token);
        var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(
            inputs.MinecraftAccessTokenResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Minecraft access token",
            currentStep.Progress,
            minecraftAccessRequestPlan.Method,
            minecraftAccessRequestPlan.Url,
            minecraftAccessRequestPlan.ContentType,
            PrettyJson(minecraftAccessRequestPlan.Body!),
            PrettyJson(inputs.MinecraftAccessTokenResponseJson),
            [$"Parsed Minecraft access token length: {minecraftAccessToken.Length}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftAccessToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var hasOwnership = MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(inputs.OwnershipResponseJson);
        var ownershipRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildOwnershipRequest(minecraftAccessToken);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Verify Minecraft ownership",
            currentStep.Progress,
            ownershipRequestPlan.Method,
            ownershipRequestPlan.Url,
            ContentType: null,
            RequestBody: null,
            PrettyJson(inputs.OwnershipResponseJson),
            [$"Ownership detected: {hasOwnership}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterOwnershipVerification();

        var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(inputs.ProfileResponseJson);
        var profileRequestPlan = MinecraftLaunchMicrosoftRequestWorkflowService.BuildProfileRequest(minecraftAccessToken);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Fetch Minecraft profile",
            currentStep.Progress,
            profileRequestPlan.Method,
            profileRequestPlan.Url,
            ContentType: null,
            RequestBody: null,
            PrettyJson(inputs.ProfileResponseJson),
            [
                $"Profile UUID: {profileResponse.Uuid}",
                $"Profile name: {profileResponse.UserName}"
            ]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftProfile(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveMicrosoftProfileMutation(
            new MinecraftLaunchMicrosoftProfileMutationRequest(
                inputs.IsCreatingProfile,
                inputs.SelectedProfileIndex,
                inputs.Profiles,
                profileResponse.Uuid,
                profileResponse.UserName,
                minecraftAccessToken,
                refreshResponse.RefreshToken,
                profileResponse.ProfileJson));
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Apply Microsoft profile mutation",
            currentStep.Progress,
            Method: null,
            Url: null,
            ContentType: null,
            RequestBody: null,
            ResponseBody: null,
            [
                $"Mutation kind: {mutationPlan.Kind}",
                $"Select created profile: {mutationPlan.ShouldSelectCreatedProfile}",
                $"Clear creating profile flag: {mutationPlan.ShouldClearCreatingProfile}"
            ]));

        return new LaunchLoginSpikePlan(
            LaunchLoginProviderKind.Microsoft,
            steps,
            mutationPlan);
    }

    private static LaunchLoginSpikePlan BuildAuthlibPlan(AuthlibLaunchLoginSpikeInputs inputs)
    {
        var currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetInitialStep(
            new MinecraftLaunchThirdPartyLoginExecutionRequest(
                ShouldSkipCachedSessionRecovery: true));

        var steps = new List<LaunchLoginSpikeStepPlan>();

        var authenticateRequestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildAuthenticateRequest(
            inputs.ServerBaseUrl,
            inputs.LoginName,
            inputs.Password);
        var authenticatePlan = MinecraftLaunchAuthlibLoginWorkflowService.PlanAuthenticate(
            new MinecraftLaunchAuthlibAuthenticatePlanRequest(
                inputs.ForceReselectProfile,
                inputs.CachedProfileId,
                inputs.AuthenticateResponseJson));
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Authenticate with Authlib server",
            currentStep.Progress,
            authenticateRequestPlan.Method,
            authenticateRequestPlan.Url,
            authenticateRequestPlan.ContentType,
            PrettyJson(authenticateRequestPlan.Body!),
            PrettyJson(inputs.AuthenticateResponseJson),
            [
                $"Selection result: {authenticatePlan.Kind}",
                $"Needs refresh: {authenticatePlan.NeedsRefresh}"
            ]));
        var selectionNotes = new List<string>();
        if (!string.IsNullOrWhiteSpace(authenticatePlan.NoticeMessage))
        {
            selectionNotes.Add($"Notice: {authenticatePlan.NoticeMessage}");
        }
        if (!string.IsNullOrWhiteSpace(authenticatePlan.SelectedProfileName))
        {
            selectionNotes.Add($"Resolved profile: {authenticatePlan.SelectedProfileName} ({authenticatePlan.SelectedProfileId})");
        }

        string? selectedProfileId = authenticatePlan.SelectedProfileId;
        string? selectedProfileName = authenticatePlan.SelectedProfileName;
        if (authenticatePlan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection)
        {
            var selectedProfile = authenticatePlan.PromptOptions.First();
            selectedProfileId = selectedProfile.Id;
            selectedProfileName = selectedProfile.Name;
            selectionNotes.Add($"Spike auto-selected prompt option: {selectedProfile.Name}");
        }

        steps.Add(new LaunchLoginSpikeStepPlan(
            "Resolve Authlib profile selection",
            0.35,
            Method: null,
            Url: null,
            ContentType: null,
            RequestBody: null,
            ResponseBody: null,
            selectionNotes.Count == 0 ? ["Selection already resolved by backend workflow."] : selectionNotes));

        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterLoginSuccess(authenticatePlan.NeedsRefresh);
        if (currentStep.Kind == MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession)
        {
            var refreshRequestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildRefreshRequest(
                inputs.ServerBaseUrl,
                selectedProfileName!,
                selectedProfileId!,
                authenticatePlan.AccessToken);
            var refreshResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveRefresh(
                new MinecraftLaunchAuthlibRefreshWorkflowRequest(
                    inputs.RefreshResponseJson,
                    inputs.SelectedProfileIndex ?? -1,
                    inputs.ServerBaseUrl,
                    "Authlib Server",
                    inputs.LoginName,
                    inputs.Password));
            steps.Add(new LaunchLoginSpikeStepPlan(
                "Refresh selected Authlib profile",
                currentStep.Progress,
                refreshRequestPlan.Method,
                refreshRequestPlan.Url,
                refreshRequestPlan.ContentType,
                PrettyJson(refreshRequestPlan.Body!),
                PrettyJson(inputs.RefreshResponseJson),
                [
                    $"Refreshed profile id: {refreshResult.Session.ProfileId}",
                    $"Refreshed profile name: {refreshResult.Session.ProfileName}"
                ]));
            currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshSuccess(hasRetriedRefresh: true);
        }

        var metadataJson = inputs.MetadataResponseJson;
        var metadataRequestPlan = MinecraftLaunchAuthlibRequestWorkflowService.BuildMetadataRequest(inputs.ServerBaseUrl);
        var authenticateResult = MinecraftLaunchAuthlibLoginWorkflowService.ResolveAuthenticate(
            new MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
                authenticatePlan,
                metadataJson,
                inputs.IsExistingProfile,
                inputs.SelectedProfileIndex ?? -1,
                inputs.ServerBaseUrl,
                inputs.LoginName,
                inputs.Password,
                selectedProfileId));
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Fetch Authlib server metadata",
            0.85,
            metadataRequestPlan.Method,
            metadataRequestPlan.Url,
            ContentType: null,
            RequestBody: null,
            PrettyJson(metadataJson),
            [$"Parsed server name: {authenticateResult.ServerName}"]));
        var mutationPlan = authenticateResult.MutationPlan;
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Apply Authlib profile mutation",
            currentStep.Progress,
            Method: null,
            Url: null,
            ContentType: null,
            RequestBody: null,
            ResponseBody: null,
            [
                $"Mutation kind: {mutationPlan.Kind}",
                $"Select created profile: {mutationPlan.ShouldSelectCreatedProfile}",
                $"Clear creating profile flag: {mutationPlan.ShouldClearCreatingProfile}"
            ]));

        return new LaunchLoginSpikePlan(
            LaunchLoginProviderKind.Authlib,
            steps,
            mutationPlan);
    }

    private static string PrettyJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
