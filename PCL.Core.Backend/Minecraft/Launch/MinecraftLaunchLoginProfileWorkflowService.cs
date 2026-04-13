using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchLoginProfileWorkflowService
{
    private const long MicrosoftRefreshReuseWindowMilliseconds = 1000 * 60 * 10;

    public static bool ShouldReuseMicrosoftLogin(MinecraftLaunchMicrosoftSessionReuseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return !request.IsForceRestarting &&
               !string.IsNullOrEmpty(request.AccessToken) &&
               request.LastRefreshTick > 0 &&
               request.CurrentTick - request.LastRefreshTick < MicrosoftRefreshReuseWindowMilliseconds;
    }

    public static MinecraftLaunchProfileMutationPlan ResolveMicrosoftProfileMutation(MinecraftLaunchMicrosoftProfileMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var duplicateIndex = FindMicrosoftProfileIndex(request.Profiles, request.ResultUsername, request.ResultUuid);
        if (request.IsCreatingProfile && duplicateIndex >= 0)
        {
            return new MinecraftLaunchProfileMutationPlan(
                MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate,
                duplicateIndex,
                ShouldSelectCreatedProfile: false,
                ShouldClearCreatingProfile: false,
                NoticeMessage: "你已经添加了这个档案...",
                CreateProfile: null,
                UpdateProfile: new MinecraftLaunchStoredProfile(
                    MinecraftLaunchStoredProfileKind.Microsoft,
                    request.ResultUuid,
                    request.ResultUsername,
                    Server: null,
                    ServerName: null,
                    AccessToken: request.AccessToken,
                    RefreshToken: request.RefreshToken,
                    LoginName: null,
                    Password: null,
                    ClientToken: null,
                    SkinHeadId: ResolveMicrosoftSkinHeadId(request.ProfileJson, request.ResultUuid),
                    RawJson: request.ProfileJson));
        }

        if (duplicateIndex >= 0)
        {
            return new MinecraftLaunchProfileMutationPlan(
                MinecraftLaunchProfileMutationKind.UpdateSelected,
                request.SelectedProfileIndex,
                ShouldSelectCreatedProfile: false,
                ShouldClearCreatingProfile: false,
                NoticeMessage: null,
                CreateProfile: null,
                UpdateProfile: new MinecraftLaunchStoredProfile(
                    MinecraftLaunchStoredProfileKind.Microsoft,
                    request.ResultUuid,
                    request.ResultUsername,
                    Server: null,
                    ServerName: null,
                    AccessToken: request.AccessToken,
                    RefreshToken: request.RefreshToken,
                    LoginName: null,
                    Password: null,
                    ClientToken: null,
                    SkinHeadId: ResolveMicrosoftSkinHeadId(request.ProfileJson, request.ResultUuid),
                    RawJson: request.ProfileJson));
        }

        return new MinecraftLaunchProfileMutationPlan(
            MinecraftLaunchProfileMutationKind.CreateNew,
            TargetProfileIndex: null,
            ShouldSelectCreatedProfile: true,
            ShouldClearCreatingProfile: true,
            NoticeMessage: null,
            CreateProfile: new MinecraftLaunchStoredProfile(
                MinecraftLaunchStoredProfileKind.Microsoft,
                request.ResultUuid,
                request.ResultUsername,
                Server: null,
                ServerName: null,
                AccessToken: request.AccessToken,
                RefreshToken: request.RefreshToken,
                LoginName: null,
                Password: null,
                ClientToken: null,
                SkinHeadId: ResolveMicrosoftSkinHeadId(request.ProfileJson, request.ResultUuid),
                RawJson: request.ProfileJson),
            UpdateProfile: null);
    }

    public static MinecraftLaunchProfileMutationPlan ResolveAuthProfileMutation(MinecraftLaunchAuthProfileMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = new MinecraftLaunchStoredProfile(
            MinecraftLaunchStoredProfileKind.Authlib,
            request.ResultUuid,
            request.ResultUsername,
            request.ServerBaseUrl,
            request.ServerName,
            request.AccessToken,
            RefreshToken: null,
            request.LoginName,
            request.Password,
            request.ClientToken,
            SkinHeadId: request.ResultUuid,
            RawJson: null);

        return request.IsExistingProfile
            ? new MinecraftLaunchProfileMutationPlan(
                MinecraftLaunchProfileMutationKind.UpdateSelected,
                request.SelectedProfileIndex,
                ShouldSelectCreatedProfile: false,
                ShouldClearCreatingProfile: false,
                NoticeMessage: null,
                CreateProfile: null,
                UpdateProfile: profile)
            : new MinecraftLaunchProfileMutationPlan(
                MinecraftLaunchProfileMutationKind.CreateNew,
                TargetProfileIndex: null,
                ShouldSelectCreatedProfile: true,
                ShouldClearCreatingProfile: true,
                NoticeMessage: null,
                CreateProfile: profile,
                UpdateProfile: null);
    }

    private static int FindMicrosoftProfileIndex(IReadOnlyList<MinecraftLaunchStoredProfile> profiles, string username, string uuid)
    {
        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            if (profile.Kind == MinecraftLaunchStoredProfileKind.Microsoft &&
                string.Equals(profile.Username, username, StringComparison.Ordinal) &&
                string.Equals(profile.Uuid, uuid, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string ResolveMicrosoftSkinHeadId(string profileJson, string fallbackId)
    {
        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            try
            {
                if (JsonNode.Parse(profileJson) is JsonObject root &&
                    root["skins"] is JsonArray skins)
                {
                    var preferredSkinUrl = skins
                        .OfType<JsonObject>()
                        .Where(skin => !string.IsNullOrWhiteSpace(skin["url"]?.ToString()))
                        .OrderByDescending(skin => string.Equals(skin["state"]?.ToString(), "ACTIVE", StringComparison.OrdinalIgnoreCase))
                        .Select(skin => skin["url"]!.ToString())
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(preferredSkinUrl))
                    {
                        var lastSegment = Uri.TryCreate(preferredSkinUrl, UriKind.Absolute, out var uri)
                            ? uri.Segments.LastOrDefault()?.Trim('/')
                            : Path.GetFileNameWithoutExtension(preferredSkinUrl);
                        if (!string.IsNullOrWhiteSpace(lastSegment))
                        {
                            return lastSegment;
                        }
                    }
                }
            }
            catch
            {
                // Ignore malformed profile JSON and fall back to the stable identifier below.
            }
        }

        return fallbackId;
    }
}

public sealed record MinecraftLaunchMicrosoftSessionReuseRequest(
    bool IsForceRestarting,
    string? AccessToken,
    long LastRefreshTick,
    long CurrentTick);

public sealed record MinecraftLaunchMicrosoftProfileMutationRequest(
    bool IsCreatingProfile,
    int? SelectedProfileIndex,
    IReadOnlyList<MinecraftLaunchStoredProfile> Profiles,
    string ResultUuid,
    string ResultUsername,
    string AccessToken,
    string RefreshToken,
    string ProfileJson);

public sealed record MinecraftLaunchAuthProfileMutationRequest(
    bool IsExistingProfile,
    int? SelectedProfileIndex,
    string ServerBaseUrl,
    string ServerName,
    string ResultUuid,
    string ResultUsername,
    string AccessToken,
    string ClientToken,
    string LoginName,
    string Password);

public sealed record MinecraftLaunchStoredProfile(
    MinecraftLaunchStoredProfileKind Kind,
    string Uuid,
    string Username,
    string? Server,
    string? ServerName,
    string? AccessToken,
    string? RefreshToken,
    string? LoginName,
    string? Password,
    string? ClientToken,
    string? SkinHeadId,
    string? RawJson);

public enum MinecraftLaunchStoredProfileKind
{
    Offline = 0,
    Authlib = 1,
    Microsoft = 2
}

public sealed record MinecraftLaunchProfileMutationPlan(
    MinecraftLaunchProfileMutationKind Kind,
    int? TargetProfileIndex,
    bool ShouldSelectCreatedProfile,
    bool ShouldClearCreatingProfile,
    string? NoticeMessage,
    MinecraftLaunchStoredProfile? CreateProfile,
    MinecraftLaunchStoredProfile? UpdateProfile);

public enum MinecraftLaunchProfileMutationKind
{
    CreateNew = 0,
    UpdateSelected = 1,
    UpdateExistingDuplicate = 2
}
