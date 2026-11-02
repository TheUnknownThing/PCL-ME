using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchLauncherProfilesService
{
    private const string AccountId = "00000111112222233333444445555566";
    private const string ProfileId = "66666555554444433333222221111100";

    public static MinecraftLaunchLauncherProfilesUpdatePlan BuildUpdatePlan(MinecraftLaunchLauncherProfilesUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.IsMicrosoftLogin)
        {
            return MinecraftLaunchLauncherProfilesUpdatePlan.None;
        }

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            throw new ArgumentException("The Microsoft sign-in username cannot be empty.", nameof(request));
        }

        var existingRoot = ParseExistingProfiles(request.ExistingProfilesJson);
        var mergedRoot = MergeMicrosoftAccount(existingRoot, request.UserName, request.ClientToken);

        return new MinecraftLaunchLauncherProfilesUpdatePlan(
            ShouldWrite: true,
            UpdatedProfilesJson: mergedRoot.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    private static JsonObject ParseExistingProfiles(string? existingProfilesJson)
    {
        if (string.IsNullOrWhiteSpace(existingProfilesJson))
        {
            return [];
        }

        return JsonNode.Parse(existingProfilesJson) as JsonObject
               ?? throw new InvalidOperationException("The contents of launcher_profiles.json are not a JSON object.");
    }

    private static JsonObject MergeMicrosoftAccount(JsonObject existingRoot, string userName, string? clientToken)
    {
        var result = (JsonObject)existingRoot.DeepClone();
        var authenticationDatabase = GetOrCreateObject(result, "authenticationDatabase");
        var account = GetOrCreateObject(authenticationDatabase, AccountId);
        var profiles = GetOrCreateObject(account, "profiles");
        var selectedProfile = GetOrCreateObject(profiles, ProfileId);
        var selectedUser = GetOrCreateObject(result, "selectedUser");

        account["username"] = userName.Replace("\"", "-", StringComparison.Ordinal);
        selectedProfile["displayName"] = userName;
        result["clientToken"] = clientToken ?? string.Empty;
        selectedUser["account"] = AccountId;
        selectedUser["profile"] = ProfileId;

        return result;
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject child)
        {
            return child;
        }

        child = [];
        parent[propertyName] = child;
        return child;
    }
}

public sealed record MinecraftLaunchLauncherProfilesUpdateRequest(
    bool IsMicrosoftLogin,
    string? ExistingProfilesJson,
    string UserName,
    string? ClientToken);

public sealed record MinecraftLaunchLauncherProfilesUpdatePlan(
    bool ShouldWrite,
    string? UpdatedProfilesJson)
{
    public static MinecraftLaunchLauncherProfilesUpdatePlan None { get; } = new(false, null);
}
