using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchAuthlibLoginWorkflowService
{
    public static MinecraftLaunchAuthlibRefreshWorkflowResult ResolveRefresh(MinecraftLaunchAuthlibRefreshWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var refreshResponse = MinecraftLaunchAuthlibProtocolService.ParseRefreshResponseJson(request.RefreshResponseJson);
        var session = new MinecraftLaunchAuthlibSession(
            refreshResponse.AccessToken,
            refreshResponse.ClientToken,
            refreshResponse.SelectedProfileId,
            refreshResponse.SelectedProfileName);
        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                true,
                request.SelectedProfileIndex,
                request.ServerBaseUrl,
                request.ServerName,
                session.ProfileId,
                session.ProfileName,
                session.AccessToken,
                session.ClientToken,
                request.LoginName,
                request.Password,
                SkinHeadId: null,
                RawJson: request.RefreshResponseJson));
        return new MinecraftLaunchAuthlibRefreshWorkflowResult(session, mutationPlan);
    }

    public static MinecraftLaunchAuthlibAuthenticatePlan PlanAuthenticate(MinecraftLaunchAuthlibAuthenticatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authResponse = MinecraftLaunchAuthlibProtocolService.ParseAuthenticateResponseJson(
            request.AuthenticateResponseJson,
            request.RequestedClientToken);
        var selectionResult = MinecraftLaunchAccountWorkflowService.ResolveAuthProfileSelection(
            new MinecraftLaunchAuthProfileSelectionRequest(
                request.ForceReselectProfile,
                request.CachedProfileId,
                authResponse.SelectedProfileId,
                authResponse.AvailableProfiles));

        return new MinecraftLaunchAuthlibAuthenticatePlan(
            authResponse.AccessToken,
            authResponse.ClientToken,
            selectionResult.Kind,
            selectionResult.NeedsRefresh,
            selectionResult.SelectedProfileId,
            selectionResult.SelectedProfileName,
            selectionResult.FailureMessage,
            selectionResult.NoticeMessage,
            selectionResult.PromptTitle,
            selectionResult.PromptTitleText,
            selectionResult.PromptOptions);
    }

    public static MinecraftLaunchAuthlibAuthenticateWorkflowResult ResolveAuthenticate(MinecraftLaunchAuthlibAuthenticateWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Plan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
        {
            throw new InvalidOperationException("Cannot resolve a failed Authlib sign-in plan.");
        }

        var selectedProfile = ResolveSelectedProfile(request);
        var serverName = ResolveServerName(request.ServerBaseUrl, request.MetadataResponseJson);
        var session = new MinecraftLaunchAuthlibSession(
            request.Plan.AccessToken,
            request.Plan.ClientToken,
            selectedProfile.Id,
            selectedProfile.Name);
        var mutationPlan = MinecraftLaunchLoginProfileWorkflowService.ResolveAuthProfileMutation(
            new MinecraftLaunchAuthProfileMutationRequest(
                request.IsExistingProfile,
                request.SelectedProfileIndex,
                request.ServerBaseUrl,
                serverName,
                session.ProfileId,
                session.ProfileName,
                session.AccessToken,
                session.ClientToken,
                request.LoginName,
                request.Password,
                SkinHeadId: null,
                RawJson: request.AuthenticateResponseJson));

        return new MinecraftLaunchAuthlibAuthenticateWorkflowResult(
            session,
            request.Plan.NeedsRefresh,
            serverName,
            mutationPlan);
    }

    private static MinecraftLaunchAuthProfileOption ResolveSelectedProfile(MinecraftLaunchAuthlibAuthenticateWorkflowRequest request)
    {
        if (request.Plan.Kind == MinecraftLaunchAuthProfileSelectionKind.PromptForSelection)
        {
            if (string.IsNullOrWhiteSpace(request.SelectedProfileId))
            {
                throw new ArgumentException("The selected profile ID is required.", nameof(request));
            }

            var selectedProfile = request.Plan.PromptOptions.FirstOrDefault(profile =>
                string.Equals(profile.Id, request.SelectedProfileId, StringComparison.Ordinal));
            if (selectedProfile is null)
            {
                throw new InvalidOperationException("The selected profile is not in the available list.");
            }

            return selectedProfile;
        }

        if (string.IsNullOrWhiteSpace(request.Plan.SelectedProfileId) ||
            string.IsNullOrWhiteSpace(request.Plan.SelectedProfileName))
        {
            throw new InvalidOperationException("The Authlib sign-in plan is missing the selected profile.");
        }

        return new MinecraftLaunchAuthProfileOption(request.Plan.SelectedProfileId, request.Plan.SelectedProfileName);
    }

    private static string ResolveServerName(string serverBaseUrl, string? metadataResponseJson)
    {
        if (!string.IsNullOrWhiteSpace(metadataResponseJson))
        {
            try
            {
                return MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(metadataResponseJson);
            }
            catch
            {
                try
                {
                    if (JsonNode.Parse(metadataResponseJson) is JsonObject root)
                    {
                        var topLevelName = root["serverName"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(topLevelName))
                        {
                            return topLevelName;
                        }

                        var metaName = root["meta"]?["name"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(metaName))
                        {
                            return metaName;
                        }
                    }
                }
                catch
                {
                    // Ignore malformed metadata and fallback to host below.
                }
            }
        }

        return Uri.TryCreate(serverBaseUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : "Authlib Server";
    }
}

public sealed record MinecraftLaunchAuthlibSession(
    string AccessToken,
    string ClientToken,
    string ProfileId,
    string ProfileName);

public sealed record MinecraftLaunchAuthlibRefreshWorkflowRequest(
    string RefreshResponseJson,
    int SelectedProfileIndex,
    string ServerBaseUrl,
    string ServerName,
    string LoginName,
    string Password);

public sealed record MinecraftLaunchAuthlibRefreshWorkflowResult(
    MinecraftLaunchAuthlibSession Session,
    MinecraftLaunchProfileMutationPlan MutationPlan);

public sealed record MinecraftLaunchAuthlibAuthenticatePlanRequest(
    bool ForceReselectProfile,
    string? CachedProfileId,
    string AuthenticateResponseJson,
    string RequestedClientToken);

public sealed record MinecraftLaunchAuthlibAuthenticatePlan(
    string AccessToken,
    string ClientToken,
    MinecraftLaunchAuthProfileSelectionKind Kind,
    bool NeedsRefresh,
    string? SelectedProfileId,
    string? SelectedProfileName,
    string? FailureMessage,
    string? NoticeMessage,
    string? PromptTitle,
    PCL.Core.App.I18n.I18nText? PromptTitleText,
    IReadOnlyList<MinecraftLaunchAuthProfileOption> PromptOptions);

public sealed record MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
    MinecraftLaunchAuthlibAuthenticatePlan Plan,
    string AuthenticateResponseJson,
    string MetadataResponseJson,
    bool IsExistingProfile,
    int SelectedProfileIndex,
    string ServerBaseUrl,
    string LoginName,
    string Password,
    string? SelectedProfileId = null);

public sealed record MinecraftLaunchAuthlibAuthenticateWorkflowResult(
    MinecraftLaunchAuthlibSession Session,
    bool NeedsRefresh,
    string ServerName,
    MinecraftLaunchProfileMutationPlan MutationPlan);
