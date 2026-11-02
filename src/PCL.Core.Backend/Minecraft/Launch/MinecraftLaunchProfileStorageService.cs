using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Launch;

public static class MinecraftLaunchProfileStorageService
{
    public static MinecraftLaunchProfileDocument ParseDocument(string? json, Func<string?, string> decryptSecret)
    {
        ArgumentNullException.ThrowIfNull(decryptSecret);

        if (string.IsNullOrWhiteSpace(json))
        {
            return MinecraftLaunchProfileDocument.Empty;
        }

        var root = JsonNode.Parse(json) as JsonObject
                   ?? throw new InvalidOperationException("The contents of profiles.json are not a JSON object.");

        var profilesNode = root["profiles"];
        if (profilesNode is not JsonArray profilesArray)
        {
            throw new InvalidOperationException("The profiles field in profiles.json is not a JSON array.");
        }

        var profiles = new List<MinecraftLaunchPersistedProfile>(profilesArray.Count);
        foreach (var entry in profilesArray)
        {
            if (entry is not JsonObject profileObject)
            {
                continue;
            }

            profiles.Add(ParseProfile(profileObject, decryptSecret));
        }

        return new MinecraftLaunchProfileDocument(
            LastUsedProfile: ReadInt32(root, "lastUsed"),
            Profiles: profiles);
    }

    public static string SerializeDocument(MinecraftLaunchProfileDocument document, Func<string?, string> encryptSecret)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(encryptSecret);

        var profiles = new JsonArray();
        foreach (var profile in document.Profiles)
        {
            profiles.Add(SerializeProfile(profile, encryptSecret));
        }

        var root = new JsonObject
        {
            ["lastUsed"] = document.LastUsedProfile,
            ["profiles"] = profiles
        };

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    public static MinecraftLaunchProfileDocument DeleteProfile(
        MinecraftLaunchProfileDocument document,
        int profileIndex)
    {
        ArgumentNullException.ThrowIfNull(document);

        var profiles = document.Profiles.ToList();
        if (profileIndex < 0 || profileIndex >= profiles.Count)
        {
            return document;
        }

        var currentSelectedIndex = document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
            ? document.LastUsedProfile
            : 0;
        profiles.RemoveAt(profileIndex);

        if (profiles.Count == 0)
        {
            return new MinecraftLaunchProfileDocument(0, profiles);
        }

        var nextSelectedIndex = currentSelectedIndex switch
        {
            _ when currentSelectedIndex > profileIndex => currentSelectedIndex - 1,
            _ when currentSelectedIndex == profileIndex => Math.Min(profileIndex, profiles.Count - 1),
            _ => currentSelectedIndex
        };
        return new MinecraftLaunchProfileDocument(nextSelectedIndex, profiles);
    }

    private static MinecraftLaunchPersistedProfile ParseProfile(JsonObject profile, Func<string?, string> decryptSecret)
    {
        var kind = ParseProfileKind(ReadString(profile, "type"));

        return kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => new MinecraftLaunchPersistedProfile(
                kind,
                Uuid: ReadString(profile, "uuid"),
                Username: ReadString(profile, "username"),
                Desc: ReadString(profile, "desc"),
                SkinHeadId: ReadString(profile, "skinHeadId"),
                Expires: ReadInt64(profile, "expires"),
                Server: null,
                ServerName: null,
                AccessToken: decryptSecret(ReadString(profile, "accessToken")),
                RefreshToken: decryptSecret(ReadString(profile, "refreshToken")),
                LoginName: null,
                Password: null,
                ClientToken: null,
                RawJson: decryptSecret(ReadString(profile, "rawJson"))),
            MinecraftLaunchStoredProfileKind.Authlib => new MinecraftLaunchPersistedProfile(
                kind,
                Uuid: ReadString(profile, "uuid"),
                Username: ReadString(profile, "username"),
                Desc: ReadString(profile, "desc"),
                SkinHeadId: ReadString(profile, "skinHeadId"),
                Expires: ReadInt64(profile, "expires"),
                Server: ReadString(profile, "server"),
                ServerName: ReadString(profile, "serverName"),
                AccessToken: decryptSecret(ReadString(profile, "accessToken")),
                RefreshToken: decryptSecret(ReadString(profile, "refreshToken")),
                LoginName: decryptSecret(ReadString(profile, "name")),
                Password: decryptSecret(ReadString(profile, "password")),
                ClientToken: decryptSecret(ReadString(profile, "clientToken")),
                RawJson: decryptSecret(ReadString(profile, "rawJson"))),
            _ => new MinecraftLaunchPersistedProfile(
                MinecraftLaunchStoredProfileKind.Offline,
                Uuid: ReadString(profile, "uuid"),
                Username: ReadString(profile, "username"),
                Desc: ReadString(profile, "desc"),
                SkinHeadId: ReadString(profile, "skinHeadId"),
                Expires: ReadInt64(profile, "expires"),
                Server: null,
                ServerName: null,
                AccessToken: null,
                RefreshToken: null,
                LoginName: null,
                Password: null,
                ClientToken: null,
                RawJson: null)
        };
    }

    private static JsonObject SerializeProfile(MinecraftLaunchPersistedProfile profile, Func<string?, string> encryptSecret)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return profile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => new JsonObject
            {
                ["type"] = "microsoft",
                ["uuid"] = profile.Uuid,
                ["username"] = profile.Username,
                ["accessToken"] = encryptSecret(profile.AccessToken),
                ["refreshToken"] = encryptSecret(profile.RefreshToken),
                ["expires"] = profile.Expires,
                ["desc"] = profile.Desc,
                ["rawJson"] = encryptSecret(profile.RawJson),
                ["skinHeadId"] = profile.SkinHeadId
            },
            MinecraftLaunchStoredProfileKind.Authlib => new JsonObject
            {
                ["type"] = "authlib",
                ["uuid"] = profile.Uuid,
                ["username"] = profile.Username,
                ["accessToken"] = encryptSecret(profile.AccessToken),
                ["refreshToken"] = encryptSecret(profile.RefreshToken),
                ["expires"] = profile.Expires,
                ["server"] = profile.Server,
                ["serverName"] = profile.ServerName,
                ["name"] = encryptSecret(profile.LoginName),
                ["password"] = encryptSecret(profile.Password),
                ["clientToken"] = encryptSecret(profile.ClientToken),
                ["rawJson"] = encryptSecret(profile.RawJson),
                ["desc"] = profile.Desc,
                ["skinHeadId"] = profile.SkinHeadId
            },
            _ => new JsonObject
            {
                ["type"] = "offline",
                ["uuid"] = profile.Uuid,
                ["username"] = profile.Username,
                ["desc"] = profile.Desc,
                ["skinHeadId"] = profile.SkinHeadId
            }
        };
    }

    private static MinecraftLaunchStoredProfileKind ParseProfileKind(string? profileType)
    {
        return profileType?.Trim().ToLowerInvariant() switch
        {
            "microsoft" => MinecraftLaunchStoredProfileKind.Microsoft,
            "authlib" => MinecraftLaunchStoredProfileKind.Authlib,
            _ => MinecraftLaunchStoredProfileKind.Offline
        };
    }

    private static string? ReadString(JsonObject source, string propertyName)
    {
        if (source[propertyName] is null)
        {
            return null;
        }

        return source[propertyName]?.GetValue<string>();
    }

    private static int ReadInt32(JsonObject source, string propertyName)
    {
        if (source[propertyName] is null)
        {
            return 0;
        }

        return source[propertyName]?.GetValue<int>() ?? 0;
    }

    private static long ReadInt64(JsonObject source, string propertyName)
    {
        if (source[propertyName] is null)
        {
            return 0L;
        }

        return source[propertyName]?.GetValue<long>() ?? 0L;
    }
}

public sealed record MinecraftLaunchProfileDocument(
    int LastUsedProfile,
    IReadOnlyList<MinecraftLaunchPersistedProfile> Profiles)
{
    public static MinecraftLaunchProfileDocument Empty { get; } = new(0, Array.Empty<MinecraftLaunchPersistedProfile>());
}

public sealed record MinecraftLaunchPersistedProfile(
    MinecraftLaunchStoredProfileKind Kind,
    string? Uuid,
    string? Username,
    string? Desc,
    string? SkinHeadId,
    long Expires,
    string? Server,
    string? ServerName,
    string? AccessToken,
    string? RefreshToken,
    string? LoginName,
    string? Password,
    string? ClientToken,
    string? RawJson);
