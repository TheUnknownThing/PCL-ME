using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.UI.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private static readonly Regex OfflineUserNamePattern = new("^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);
    private static readonly Regex OfflineUuidPattern = new("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    private int _selectedLaunchOfflineSkinIndex;

    public IReadOnlyList<string> LaunchOfflineSkinOptions =>
    [
        T("launch.profile.offline.skins.automatic"),
        "Steve",
        "Alex"
    ];

    public string LaunchOfflineSkinLabel => T("launch.profile.offline.skins.label");

    public string LaunchOfflineSkinHint => T("launch.profile.offline.skins.hint");

    public int SelectedLaunchOfflineSkinIndex
    {
        get => _selectedLaunchOfflineSkinIndex;
        set
        {
            if (TryNormalizeSelectionIndex(value, LaunchOfflineSkinOptions.Count, out var index))
            {
                SetProperty(ref _selectedLaunchOfflineSkinIndex, index);
            }
        }
    }

    public IReadOnlyList<string> LaunchOfflineUuidModeOptions =>
    [
        T("launch.profile.offline.uuid_modes.standard"),
        T("launch.profile.offline.uuid_modes.legacy"),
        T("launch.profile.offline.uuid_modes.custom")
    ];

    public string LaunchOfflineUserName
    {
        get => _launchOfflineUserName;
        set => SetProperty(ref _launchOfflineUserName, value);
    }

    public int SelectedLaunchOfflineUuidModeIndex
    {
        get => _selectedLaunchOfflineUuidModeIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, LaunchOfflineUuidModeOptions.Count, out var clampedValue))
            {
                return;
            }

            if (SetProperty(ref _selectedLaunchOfflineUuidModeIndex, clampedValue))
            {
                RaisePropertyChanged(nameof(IsLaunchOfflineCustomUuidVisible));
            }
        }
    }

    public bool IsLaunchOfflineCustomUuidVisible => SelectedLaunchOfflineUuidModeIndex == 2;

    public string LaunchOfflineCustomUuid
    {
        get => _launchOfflineCustomUuid;
        set => SetProperty(ref _launchOfflineCustomUuid, value);
    }

    public string LaunchOfflineStatusText
    {
        get => _launchOfflineStatusText;
        private set => SetProperty(ref _launchOfflineStatusText, value);
    }

    public bool HasLaunchOfflineStatus => !string.IsNullOrWhiteSpace(LaunchOfflineStatusText);

    private Task CreateOfflineLaunchProfileAsync()
    {
        ClearLaunchProfileEdit();
        SelectedLaunchOfflineSkinIndex = 0;
        LaunchOfflineUserName = HasSelectedLaunchProfile && !string.Equals(LaunchUserName, T("launch.profile.none_selected"), StringComparison.Ordinal)
            ? LaunchUserName
            : string.Empty;
        SelectedLaunchOfflineUuidModeIndex = 0;
        LaunchOfflineCustomUuid = string.Empty;
        LaunchOfflineStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.OfflineEditor);
        return Task.CompletedTask;
    }

    private async Task SubmitOfflineLaunchProfileAsync()
    {
        var isEditing = _editingLaunchProfileIndex.HasValue;
        var actionName = T(isEditing ? "launch.profile.activities.edit" : "launch.profile.activities.create_offline");
        if (!TryBeginLaunchProfileAction(actionName))
        {
            return;
        }

        try
        {
            var userName = LaunchOfflineUserName.Trim();
            if (string.IsNullOrWhiteSpace(userName))
            {
                LaunchOfflineStatusText = T("launch.profile.offline.errors.empty_user_name");
                return;
            }

            var uuid = ResolveOfflineUuid(userName);
            var profileDocument = FrontendProfileStorageService.Load(_launcherActionService.RuntimePaths).Document;
            var editIndex = GetLaunchProfileEditIndex(profileDocument);
            var skinHeadId = SelectedLaunchOfflineSkinIndex switch { 1 => "Steve", 2 => "Alex", _ => null };
            var nextDocument = editIndex is int index
                ? FrontendProfileStorageService.UpdateOfflineProfile(profileDocument, index, userName, uuid, skinHeadId)
                : FrontendProfileStorageService.CreateOfflineProfile(profileDocument, userName, uuid, skinHeadId);
            FrontendProfileStorageService.Save(_launcherActionService.RuntimePaths, nextDocument);
            ClearLaunchProfileEdit();
            LaunchOfflineStatusText = string.Empty;
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(actionName, T(isEditing ? "launch.profile.edit.completed" : "launch.profile.offline.completed", ("user_name", userName)));
        }
        catch (Exception ex)
        {
            LaunchOfflineStatusText = ex.Message.Trim().TrimStart('$');
            AddFailureActivity(T(isEditing ? "launch.profile.activities.edit_failed" : "launch.profile.activities.create_offline_failed"), LaunchOfflineStatusText);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private string ResolveOfflineUuid(string userName)
    {
        return SelectedLaunchOfflineUuidModeIndex switch
        {
            2 => ResolveCustomOfflineUuid(),
            1 => CreateOfflineLegacyUuid(userName),
            _ => CreateOfflineUuid(userName)
        };
    }

    private string ResolveCustomOfflineUuid()
    {
        var uuid = LaunchOfflineCustomUuid.Trim().Replace("-", string.Empty, StringComparison.Ordinal);
        if (!OfflineUuidPattern.IsMatch(uuid))
        {
            throw new InvalidOperationException(T("launch.profile.offline.errors.invalid_uuid"));
        }

        return uuid;
    }

    private static string CreateOfflineUuid(string userName)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash).ToString("N");
    }

    private static string CreateOfflineLegacyUuid(string userName)
    {
        var fullUuid = userName.Length.ToString("X").PadLeft(16, '0') + GetLegacyHash(userName).ToString("X").PadLeft(16, '0');
        return fullUuid[..12] + "3" + fullUuid[13..16] + "9" + fullUuid[17..];
    }

    private static ulong GetLegacyHash(string value)
    {
        ulong result = 5381;
        foreach (var character in value)
        {
            result = (result << 5) ^ result ^ character;
        }

        return result ^ 0xA98F501BC684032FUL;
    }
}
