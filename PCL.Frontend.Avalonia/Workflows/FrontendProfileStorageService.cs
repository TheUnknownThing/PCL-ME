using System.Text;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.Workflows;

internal static class FrontendProfileStorageService
{
    private const string DemoOfflineUserName = "DemoOffline";
    private const string DemoOfflineDescription = "local test";

    public static string GetProfilesPath(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        return Path.Combine(runtimePaths.LauncherProfileDirectory, "profiles.json");
    }

    public static FrontendProfileDocument Load(FrontendRuntimePaths runtimePaths)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);

        var profilesPath = GetProfilesPath(runtimePaths);
        if (!File.Exists(profilesPath))
        {
            TryMigrateLegacyProfiles(runtimePaths, profilesPath);
        }

        if (!File.Exists(profilesPath))
        {
            return FrontendProfileDocument.Empty;
        }

        var document = MinecraftLaunchProfileStorageService.ParseDocument(
            File.ReadAllText(profilesPath),
            value => LauncherFrontendRuntimeStateService.TryUnprotectString(
                         runtimePaths.SharedConfigDirectory,
                         value)
                     ?? value
                     ?? string.Empty);
        return new FrontendProfileDocument(profilesPath, document);
    }

    public static void Save(FrontendRuntimePaths runtimePaths, MinecraftLaunchProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(runtimePaths);
        ArgumentNullException.ThrowIfNull(document);

        var profilesPath = GetProfilesPath(runtimePaths);
        Directory.CreateDirectory(Path.GetDirectoryName(profilesPath)!);

        var json = MinecraftLaunchProfileStorageService.SerializeDocument(
            document,
            value => ProtectValue(runtimePaths, value));

        var tempPath = profilesPath + ".tmp";
        var backupPath = profilesPath + ".bak";
        File.WriteAllText(tempPath, json, new UTF8Encoding(false));
        if (File.Exists(profilesPath))
        {
            File.Replace(tempPath, profilesPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, profilesPath);
        }
    }

    public static MinecraftLaunchProfileDocument ApplyMutation(
        MinecraftLaunchProfileDocument document,
        MinecraftLaunchProfileMutationPlan mutationPlan,
        out int? selectedProfileIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mutationPlan);

        var profiles = document.Profiles.ToList();
        selectedProfileIndex = document.LastUsedProfile;

        switch (mutationPlan.Kind)
        {
            case MinecraftLaunchProfileMutationKind.CreateNew when mutationPlan.CreateProfile is not null:
                profiles.Add(ConvertToPersistedProfile(mutationPlan.CreateProfile));
                if (mutationPlan.ShouldSelectCreatedProfile)
                {
                    selectedProfileIndex = profiles.Count - 1;
                }

                break;
            case MinecraftLaunchProfileMutationKind.UpdateSelected:
            case MinecraftLaunchProfileMutationKind.UpdateExistingDuplicate:
                if (mutationPlan.TargetProfileIndex is int targetIndex &&
                    targetIndex >= 0 &&
                    targetIndex < profiles.Count &&
                    mutationPlan.UpdateProfile is not null)
                {
                    profiles[targetIndex] = ConvertToPersistedProfile(mutationPlan.UpdateProfile);
                    selectedProfileIndex = targetIndex;
                }

                break;
        }

        return new MinecraftLaunchProfileDocument(
            selectedProfileIndex ?? document.LastUsedProfile,
            profiles);
    }

    public static MinecraftLaunchProfileDocument CreateOfflineProfile(
        MinecraftLaunchProfileDocument document,
        string userName,
        string uuid)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uuid);

        var profiles = document.Profiles.ToList();
        profiles.Add(new MinecraftLaunchPersistedProfile(
            MinecraftLaunchStoredProfileKind.Offline,
            uuid,
            userName,
            Desc: string.Empty,
            SkinHeadId: ResolveOfflineDefaultSkinHeadId(uuid),
            Expires: 0,
            Server: null,
            ServerName: null,
            AccessToken: null,
            RefreshToken: null,
            LoginName: null,
            Password: null,
            ClientToken: null,
            RawJson: null));
        return new MinecraftLaunchProfileDocument(profiles.Count - 1, profiles);
    }

    public static MinecraftLaunchProfileDocument SelectProfile(
        MinecraftLaunchProfileDocument document,
        int profileIndex)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Profiles.Count == 0)
        {
            return document;
        }

        var selectedIndex = Math.Clamp(profileIndex, 0, document.Profiles.Count - 1);
        return new MinecraftLaunchProfileDocument(selectedIndex, document.Profiles);
    }

    public static MinecraftLaunchProfileDocument DeleteProfile(
        MinecraftLaunchProfileDocument document,
        int profileIndex)
    {
        return MinecraftLaunchProfileStorageService.DeleteProfile(document, profileIndex);
    }

    private static void TryMigrateLegacyProfiles(FrontendRuntimePaths runtimePaths, string targetProfilesPath)
    {
        var legacyProfilesPath = Path.Combine(runtimePaths.LauncherAppDataDirectory, "profiles.json");
        if (!File.Exists(legacyProfilesPath))
        {
            return;
        }

        try
        {
            var legacyDocument = MinecraftLaunchProfileStorageService.ParseDocument(
                File.ReadAllText(legacyProfilesPath),
                value => LauncherFrontendRuntimeStateService.TryUnprotectString(
                             runtimePaths.SharedConfigDirectory,
                             value)
                         ?? value
                         ?? string.Empty);
            if (!ShouldMigrate(legacyDocument))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetProfilesPath)!);
            File.WriteAllText(
                targetProfilesPath,
                MinecraftLaunchProfileStorageService.SerializeDocument(
                    legacyDocument,
                    value => ProtectValue(runtimePaths, value)),
                new UTF8Encoding(false));
        }
        catch
        {
            // Best effort migration only. The launch surface will fall back to no profile.
        }
    }

    private static bool ShouldMigrate(MinecraftLaunchProfileDocument document)
    {
        if (document.Profiles.Count == 0)
        {
            return false;
        }

        if (document.Profiles.Count != 1)
        {
            return true;
        }

        var profile = document.Profiles[0];
        return profile.Kind != MinecraftLaunchStoredProfileKind.Offline ||
               !string.Equals(profile.Username, DemoOfflineUserName, StringComparison.Ordinal) ||
               !string.Equals(profile.Desc, DemoOfflineDescription, StringComparison.Ordinal);
    }

    private static string ProtectValue(FrontendRuntimePaths runtimePaths, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var encryptionKey = LauncherSharedEncryptionKeyService.ResolveOrCreate(
            runtimePaths.SharedConfigDirectory,
            Environment.GetEnvironmentVariable("PCL_ENCRYPTION_KEY"));
        return LauncherDataProtectionService.Protect(value, encryptionKey);
    }

    private static MinecraftLaunchPersistedProfile ConvertToPersistedProfile(MinecraftLaunchStoredProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new MinecraftLaunchPersistedProfile(
            profile.Kind,
            profile.Uuid,
            profile.Username,
            Desc: null,
            profile.SkinHeadId,
            Expires: 0,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.LoginName,
            profile.Password,
            profile.ClientToken,
            profile.RawJson);
    }

    private static string ResolveOfflineDefaultSkinHeadId(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid) || uuid.Length != 32)
        {
            return "Steve";
        }

        var a = ParseHexNibble(uuid[7]);
        var b = ParseHexNibble(uuid[15]);
        var c = ParseHexNibble(uuid[23]);
        var d = ParseHexNibble(uuid[31]);
        return ((a ^ b ^ c ^ d) & 1) == 1 ? "Alex" : "Steve";
    }

    private static int ParseHexNibble(char value)
    {
        return value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => 0
        };
    }
}

internal sealed record FrontendProfileDocument(
    string Path,
    MinecraftLaunchProfileDocument Document)
{
    public static FrontendProfileDocument Empty { get; } = new(string.Empty, MinecraftLaunchProfileDocument.Empty);
}
