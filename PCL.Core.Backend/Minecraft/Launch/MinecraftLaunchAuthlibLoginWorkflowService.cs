using System;
using System.Linq;

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
                request.Password));
        return new MinecraftLaunchAuthlibRefreshWorkflowResult(session, mutationPlan);
    }

    public static MinecraftLaunchAuthlibAuthenticatePlan PlanAuthenticate(MinecraftLaunchAuthlibAuthenticatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authResponse = MinecraftLaunchAuthlibProtocolService.ParseAuthenticateResponseJson(request.AuthenticateResponseJson);
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
            selectionResult.PromptOptions);
    }

    public static MinecraftLaunchAuthlibAuthenticateWorkflowResult ResolveAuthenticate(MinecraftLaunchAuthlibAuthenticateWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Plan.Kind == MinecraftLaunchAuthProfileSelectionKind.Fail)
        {
            throw new InvalidOperationException("无法根据失败的 Authlib 登录计划解析结果。");
        }

        var selectedProfile = ResolveSelectedProfile(request);
        var serverName = MinecraftLaunchAuthlibProtocolService.ParseServerNameFromMetadataJson(request.MetadataResponseJson);
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
                request.Password));

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
                throw new ArgumentException("需要提供所选角色 ID。", nameof(request));
            }

            var selectedProfile = request.Plan.PromptOptions.FirstOrDefault(profile =>
                string.Equals(profile.Id, request.SelectedProfileId, StringComparison.Ordinal));
            if (selectedProfile is null)
            {
                throw new InvalidOperationException("所选角色不在可选列表中。");
            }

            return selectedProfile;
        }

        if (string.IsNullOrWhiteSpace(request.Plan.SelectedProfileId) ||
            string.IsNullOrWhiteSpace(request.Plan.SelectedProfileName))
        {
            throw new InvalidOperationException("Authlib 登录计划缺少选中的角色。");
        }

        return new MinecraftLaunchAuthProfileOption(request.Plan.SelectedProfileId, request.Plan.SelectedProfileName);
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
    string AuthenticateResponseJson);

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
    IReadOnlyList<MinecraftLaunchAuthProfileOption> PromptOptions);

public sealed record MinecraftLaunchAuthlibAuthenticateWorkflowRequest(
    MinecraftLaunchAuthlibAuthenticatePlan Plan,
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
