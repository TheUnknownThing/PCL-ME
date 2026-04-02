using System.Text.Json;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Spike.Models;

namespace PCL.Frontend.Spike.Workflows;

internal static class SpikeLaunchLoginFactory
{
    private const string MicrosoftClientId = "00000000402b5328";

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

        var refreshRequestBody = BuildFormBody(
            new Dictionary<string, string>
            {
                ["client_id"] = MicrosoftClientId,
                ["refresh_token"] = inputs.OAuthRefreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = "XboxLive.signin offline_access"
            });
        var refreshResponse = MinecraftLaunchMicrosoftProtocolService.ParseOAuthRefreshResponseJson(inputs.OAuthRefreshResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Refresh OAuth tokens",
            currentStep.Progress,
            "POST",
            "https://login.live.com/oauth20_token.srf",
            "application/x-www-form-urlencoded",
            refreshRequestBody,
            PrettyJson(inputs.OAuthRefreshResponseJson),
            [
                $"Refreshed OAuth access token length: {refreshResponse.AccessToken.Length}",
                $"Refreshed OAuth refresh token length: {refreshResponse.RefreshToken.Length}"
            ]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterRefreshOAuth(
            MinecraftLaunchMicrosoftOAuthRefreshOutcome.Succeeded);

        var xboxRequest = MinecraftLaunchMicrosoftProtocolService.BuildXboxLiveTokenRequest(refreshResponse.AccessToken);
        var xboxLiveToken = MinecraftLaunchMicrosoftProtocolService.ParseXboxLiveTokenResponseJson(inputs.XboxLiveResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Xbox Live token",
            currentStep.Progress,
            "POST",
            "https://user.auth.xboxlive.com/user/authenticate",
            "application/json",
            PrettyJson(JsonSerializer.Serialize(xboxRequest)),
            PrettyJson(inputs.XboxLiveResponseJson),
            [$"Parsed XBL token length: {xboxLiveToken.Length}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxLiveToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var xstsRequest = MinecraftLaunchMicrosoftProtocolService.BuildXstsTokenRequest(xboxLiveToken);
        var xstsResponse = MinecraftLaunchMicrosoftProtocolService.ParseXstsTokenResponseJson(inputs.XstsResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Xbox security token",
            currentStep.Progress,
            "POST",
            "https://xsts.auth.xboxlive.com/xsts/authorize",
            "application/json",
            PrettyJson(JsonSerializer.Serialize(xstsRequest)),
            PrettyJson(inputs.XstsResponseJson),
            [
                $"Parsed XSTS token length: {xstsResponse.Token.Length}",
                $"Parsed user hash: {xstsResponse.UserHash}"
            ]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterXboxSecurityToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var minecraftAccessRequest = MinecraftLaunchMicrosoftProtocolService.BuildMinecraftAccessTokenRequest(
            xstsResponse.UserHash,
            xstsResponse.Token);
        var minecraftAccessToken = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftAccessTokenResponseJson(
            inputs.MinecraftAccessTokenResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Request Minecraft access token",
            currentStep.Progress,
            "POST",
            "https://api.minecraftservices.com/authentication/login_with_xbox",
            "application/json",
            PrettyJson(JsonSerializer.Serialize(minecraftAccessRequest)),
            PrettyJson(inputs.MinecraftAccessTokenResponseJson),
            [$"Parsed Minecraft access token length: {minecraftAccessToken.Length}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterMinecraftAccessToken(
            MinecraftLaunchMicrosoftStepOutcome.Succeeded);

        var hasOwnership = MinecraftLaunchMicrosoftProtocolService.HasMinecraftOwnership(inputs.OwnershipResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Verify Minecraft ownership",
            currentStep.Progress,
            "GET",
            "https://api.minecraftservices.com/entitlements/mcstore",
            ContentType: null,
            RequestBody: null,
            PrettyJson(inputs.OwnershipResponseJson),
            [$"Ownership detected: {hasOwnership}"]));
        currentStep = MinecraftLaunchMicrosoftLoginExecutionService.GetStepAfterOwnershipVerification();

        var profileResponse = MinecraftLaunchMicrosoftProtocolService.ParseMinecraftProfileResponseJson(inputs.ProfileResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Fetch Minecraft profile",
            currentStep.Progress,
            "GET",
            "https://api.minecraftservices.com/minecraft/profile",
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

        var authenticateRequestJson = MinecraftLaunchAuthlibProtocolService.BuildAuthenticateRequestJson(
            inputs.LoginName,
            inputs.Password);
        var authenticateResponse = MinecraftLaunchAuthlibProtocolService.ParseAuthenticateResponseJson(
            inputs.AuthenticateResponseJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Authenticate with Authlib server",
            currentStep.Progress,
            "POST",
            $"{inputs.ServerBaseUrl}/authenticate",
            "application/json",
            PrettyJson(authenticateRequestJson),
            PrettyJson(inputs.AuthenticateResponseJson),
            [
                $"Available profiles: {authenticateResponse.AvailableProfiles.Count}",
                $"Server-selected profile id: {authenticateResponse.SelectedProfileId ?? "none"}"
            ]));

        var selection = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                inputs.ForceReselectProfile,
                inputs.CachedProfileId,
                inputs.ServerSelectedProfileId ?? authenticateResponse.SelectedProfileId,
                authenticateResponse.AvailableProfiles));

        var selectionNotes = new List<string>
        {
            $"Selection result: {selection.Kind}",
            $"Needs refresh: {selection.NeedsRefresh}"
        };
        if (!string.IsNullOrWhiteSpace(selection.NoticeMessage))
        {
            selectionNotes.Add($"Notice: {selection.NoticeMessage}");
        }

        if (!string.IsNullOrWhiteSpace(selection.SelectedProfileName))
        {
            selectionNotes.Add($"Resolved profile: {selection.SelectedProfileName} ({selection.SelectedProfileId})");
        }

        if (selection.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection)
        {
            var selectedProfile = selection.PromptOptions.First();
            selection = selection with
            {
                Kind = MinecraftLaunchAuthProfileSelectionKind.Resolved,
                SelectedProfileId = selectedProfile.Id,
                SelectedProfileName = selectedProfile.Name
            };
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
            selectionNotes));

        currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterLoginSuccess(selection.NeedsRefresh);
        if (currentStep.Kind == MinecraftLaunchThirdPartyLoginStepKind.RefreshCachedSession)
        {
            var refreshRequestJson = MinecraftLaunchAuthlibProtocolService.BuildRefreshRequestJson(
                selection.SelectedProfileName!,
                selection.SelectedProfileId!,
                authenticateResponse.AccessToken);
            var refreshResponse = MinecraftLaunchAuthlibProtocolService.ParseRefreshResponseJson(inputs.RefreshResponseJson);
            steps.Add(new LaunchLoginSpikeStepPlan(
                "Refresh selected Authlib profile",
                currentStep.Progress,
                "POST",
                $"{inputs.ServerBaseUrl}/refresh",
                "application/json",
                PrettyJson(refreshRequestJson),
                PrettyJson(inputs.RefreshResponseJson),
                [
                    $"Refreshed profile id: {refreshResponse.SelectedProfileId}",
                    $"Refreshed profile name: {refreshResponse.SelectedProfileName}"
                ]));
            authenticateResponse = authenticateResponse with
            {
                AccessToken = refreshResponse.AccessToken,
                ClientToken = refreshResponse.ClientToken
            };
            selection = selection with
            {
                SelectedProfileId = refreshResponse.SelectedProfileId,
                SelectedProfileName = refreshResponse.SelectedProfileName
            };
            currentStep = MinecraftLaunchThirdPartyLoginExecutionService.GetStepAfterRefreshSuccess(hasRetriedRefresh: true);
        }

        var metadataJson = inputs.MetadataResponseJson;
        var serverName = MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(metadataJson);
        steps.Add(new LaunchLoginSpikeStepPlan(
            "Fetch Authlib server metadata",
            0.85,
            "GET",
            inputs.ServerBaseUrl.Replace("/authserver", string.Empty, StringComparison.Ordinal),
            ContentType: null,
            RequestBody: null,
            PrettyJson(metadataJson),
            [$"Parsed server name: {serverName}"]));

        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                inputs.IsExistingProfile,
                inputs.SelectedProfileIndex,
                inputs.ServerBaseUrl,
                serverName,
                selection.SelectedProfileId!,
                selection.SelectedProfileName!,
                authenticateResponse.AccessToken,
                authenticateResponse.ClientToken,
                inputs.LoginName,
                inputs.Password));
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

    private static string BuildFormBody(IReadOnlyDictionary<string, string> parameters)
    {
        return string.Join(
            "&",
            parameters.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string PrettyJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
