using PCL.Core.Minecraft.Launch;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private int? _editingLaunchProfileIndex;
    private MinecraftLaunchPersistedProfile? _editingLaunchProfile;

    public bool CanEditLaunchProfile => HasSelectedLaunchProfile;

    public string LaunchProfileEditText => T("launch.profile.actions.edit");

    private void EditSelectedLaunchProfile()
    {
        if (!TryGetSelectedStoredLaunchProfile(out _, out var index, out var profile))
        {
            return;
        }

        // Open the existing editors first, then bind this edit to the saved entry.
        switch (profile.Kind)
        {
            case MinecraftLaunchStoredProfileKind.Offline:
                _ = CreateOfflineLaunchProfileAsync();
                LaunchOfflineUserName = profile.Username ?? string.Empty;
                // Preserve the UUID unless the user explicitly asks to regenerate it.
                SelectedLaunchOfflineUuidModeIndex = 2;
                LaunchOfflineCustomUuid = profile.Uuid ?? string.Empty;
                SelectedLaunchOfflineSkinIndex = profile.SkinHeadId switch
                {
                    "Steve" => 1,
                    "Alex" => 2,
                    _ => 0
                };
                break;
            case MinecraftLaunchStoredProfileKind.Authlib:
                _ = LoginAuthlibLaunchProfileAsync();
                LaunchAuthlibServer = profile.Server ?? DefaultAuthlibServer;
                LaunchAuthlibLoginName = profile.LoginName ?? string.Empty;
                break;
            case MinecraftLaunchStoredProfileKind.Microsoft:
                _ = LoginMicrosoftLaunchProfileAsync();
                break;
            default:
                return;
        }

        _editingLaunchProfileIndex = index;
        _editingLaunchProfile = profile;
        RaiseLaunchProfileSurfaceProperties();
    }

    private int? GetLaunchProfileEditIndex(MinecraftLaunchProfileDocument document)
    {
        if (_editingLaunchProfileIndex is not int index)
        {
            return null;
        }

        if (index < 0 || index >= document.Profiles.Count || document.Profiles[index] != _editingLaunchProfile)
        {
            throw new InvalidOperationException(T("launch.profile.edit.list_changed"));
        }

        return index;
    }

    private void ClearLaunchProfileEdit()
    {
        _editingLaunchProfileIndex = null;
        _editingLaunchProfile = null;
        LaunchAuthlibPassword = string.Empty;
    }
}
