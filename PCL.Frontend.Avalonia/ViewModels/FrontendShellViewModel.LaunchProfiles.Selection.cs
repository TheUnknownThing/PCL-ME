using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PCL.Core.App.I18n;
using PCL.Core.Minecraft.Launch;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    public bool ShowLaunchProfileSummaryCard => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Summary;

    public bool ShowLaunchProfileChooser => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Chooser;

    public bool ShowLaunchProfileSelection => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.Selection;

    public bool ShowLaunchOfflineEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.OfflineEditor;

    public bool ShowLaunchMicrosoftEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.MicrosoftEditor;

    public bool ShowLaunchAuthlibEditor => GetEffectiveLaunchProfileSurface() == LaunchProfileSurfaceKind.AuthlibEditor;

    public bool CanRefreshLaunchProfile => _launchComposition.SelectedProfile.Kind is MinecraftLaunchProfileKind.Microsoft or MinecraftLaunchProfileKind.Auth;

    public bool IsLaunchProfileRefreshInProgress => _isLaunchProfileRefreshInProgress;

    public bool ShowLaunchProfileBackButton => GetEffectiveLaunchProfileSurface() switch
    {
        LaunchProfileSurfaceKind.Summary => false,
        LaunchProfileSurfaceKind.Chooser => HasSelectedLaunchProfile,
        LaunchProfileSurfaceKind.Selection => HasSelectedLaunchProfile,
        _ => true
    };

    public bool HasLaunchProfileEntries => LaunchProfileEntries.Count > 0;

    public bool HasNoLaunchProfileEntries => !HasLaunchProfileEntries;

    public string LaunchProfileSelectionHint => HasLaunchProfileEntries
        ? T("launch.profile.selection.hint")
        : T("launch.profile.selection.hint_empty");

    private Task SelectLaunchProfileAsync()
    {
        RefreshLaunchProfileEntries();
        if (!HasLaunchProfileEntries)
        {
            AddActivity(T("launch.profile.activities.switch"), T("launch.profile.selection.empty"));
            SetLaunchProfileSurface(LaunchProfileSurfaceKind.Selection);
            return Task.CompletedTask;
        }

        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Selection);
        return Task.CompletedTask;
    }

    private Task AddLaunchProfileAsync()
    {
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Chooser);
        return Task.CompletedTask;
    }

    private async Task RefreshSelectedLaunchProfileAsync()
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.refresh")))
        {
            return;
        }

        try
        {
            _isLaunchProfileRefreshInProgress = true;
            RefreshLaunchProfileEntries();
            RaiseLaunchProfileSurfaceProperties();
            NotifyLaunchProfileCommandsChanged();
            var result = await RefreshSelectedLaunchProfileCoreAsync(
                CancellationToken.None,
                forceRefresh: true);
            if (!result.WasChecked)
            {
                AddActivity(T("launch.profile.activities.refresh"), result.Message);
                return;
            }

            if (result.ShouldInvalidateAvatarCache)
            {
                InvalidateLaunchAvatarCache(_launchComposition.SelectedProfile);
            }

            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.refresh"), result.Message);
            AvaloniaHintBus.Show(result.Message, AvaloniaHintTheme.Success);
        }
        catch (OperationCanceledException)
        {
            AddActivity(T("launch.profile.activities.refresh"), T("launch.profile.refresh.canceled"));
        }
        catch (Exception ex)
        {
            var message = GetLaunchProfileFriendlyError(ex);
            AddFailureActivity(T("launch.profile.activities.refresh_failed"), message);
            AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error);
        }
        finally
        {
            _isLaunchProfileRefreshInProgress = false;
            RefreshLaunchProfileEntries();
            RaiseLaunchProfileSurfaceProperties();
            NotifyLaunchProfileCommandsChanged();
            EndLaunchProfileAction();
        }
    }

    private void BackLaunchProfileSurface()
    {
        if (_launchProfileSurface == LaunchProfileSurfaceKind.MicrosoftEditor)
        {
            ResetMicrosoftDeviceFlow();
        }

        LaunchOfflineStatusText = string.Empty;
        LaunchAuthlibStatusText = string.Empty;
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.Auto);
    }

    private bool TryBeginLaunchProfileAction(string actionName)
    {
        if (_isLaunchProfileActionInProgress)
        {
            AddActivity(actionName, T("launch.profile.activities.busy"));
            return false;
        }

        _isLaunchProfileActionInProgress = true;
        NotifyLaunchProfileCommandsChanged();
        return true;
    }

    private void EndLaunchProfileAction()
    {
        _isLaunchProfileActionInProgress = false;
        NotifyLaunchProfileCommandsChanged();
    }

    private void NotifyLaunchProfileCommandsChanged()
    {
        _selectLaunchProfileCommand.NotifyCanExecuteChanged();
        _addLaunchProfileCommand.NotifyCanExecuteChanged();
        _createOfflineLaunchProfileCommand.NotifyCanExecuteChanged();
        _loginMicrosoftLaunchProfileCommand.NotifyCanExecuteChanged();
        _loginAuthlibLaunchProfileCommand.NotifyCanExecuteChanged();
        _refreshLaunchProfileCommand.NotifyCanExecuteChanged();
        _backLaunchProfileCommand.NotifyCanExecuteChanged();
        _submitOfflineLaunchProfileCommand.NotifyCanExecuteChanged();
        _submitMicrosoftLaunchProfileCommand.NotifyCanExecuteChanged();
        _openMicrosoftDeviceLinkCommand.NotifyCanExecuteChanged();
        _submitAuthlibLaunchProfileCommand.NotifyCanExecuteChanged();
        _useLittleSkinLaunchProfileCommand.NotifyCanExecuteChanged();
    }

    private void SetLaunchProfileSurface(LaunchProfileSurfaceKind surface)
    {
        _launchProfileSurface = surface;
        if (surface == LaunchProfileSurfaceKind.Selection)
        {
            RefreshLaunchProfileEntries();
        }

        RaiseLaunchProfileSurfaceProperties();
    }

    private void RefreshLaunchProfileEntries()
    {
        var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
        var selectedIndex = GetSelectedProfileIndex(profileDocument);
        ReplaceItems(
            LaunchProfileEntries,
            profileDocument.Profiles.Select((profile, index) => new LaunchProfileEntryViewModel(
                string.IsNullOrWhiteSpace(profile.Username) ? T("launch.profile.entry.unnamed") : profile.Username!,
                BuildProfileChoiceSummary(profile),
                index == selectedIndex,
                new ActionCommand(() => _ = SelectLaunchProfileEntryAsync(index)),
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? FrontendIconCatalog.Refresh.Data
                    : string.Empty,
                index == selectedIndex && IsRefreshableLaunchProfile(profile) && IsLaunchProfileRefreshInProgress,
                T("launch.profile.activities.refresh"),
                index == selectedIndex && IsRefreshableLaunchProfile(profile)
                    ? _refreshLaunchProfileCommand
                    : null,
                FrontendIconCatalog.DeleteOutline.Data,
                T("launch.profile.activities.delete"),
                new ActionCommand(() => _ = DeleteLaunchProfileAsync(index)))));
        RaisePropertyChanged(nameof(HasLaunchProfileEntries));
    }

    private static bool IsRefreshableLaunchProfile(MinecraftLaunchPersistedProfile profile)
    {
        return profile.Kind is MinecraftLaunchStoredProfileKind.Microsoft or MinecraftLaunchStoredProfileKind.Authlib;
    }

    private async Task DeleteLaunchProfileAsync(int profileIndex)
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.delete")))
        {
            return;
        }

        try
        {
            var confirmed = await _shellActionService.ConfirmAsync(
                T("launch.profile.delete.confirmation.title"),
                T("launch.profile.delete.confirmation.message"),
                T("launch.profile.delete.confirmation.confirm"),
                isDanger: true);
            if (!confirmed)
            {
                AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.canceled"));
                return;
            }

            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (profileIndex < 0 || profileIndex >= profileDocument.Profiles.Count)
            {
                AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.list_changed"));
                return;
            }

            var profileName = string.IsNullOrWhiteSpace(profileDocument.Profiles[profileIndex].Username)
                ? T("launch.profile.entry.unnamed")
                : profileDocument.Profiles[profileIndex].Username!;
            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.DeleteProfile(profileDocument, profileIndex));
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(T("launch.profile.activities.delete"), T("launch.profile.delete.completed", ("profile_name", profileName)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("launch.profile.activities.delete_failed"), ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private async Task SelectLaunchProfileEntryAsync(int selectedIndex)
    {
        if (!TryBeginLaunchProfileAction(T("launch.profile.activities.switch")))
        {
            return;
        }

        try
        {
            var profileDocument = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
            if (selectedIndex < 0 || selectedIndex >= profileDocument.Profiles.Count)
            {
                return;
            }

            FrontendProfileStorageService.Save(
                _shellActionService.RuntimePaths,
                FrontendProfileStorageService.SelectProfile(profileDocument, selectedIndex));
            _launchProfileSurface = LaunchProfileSurfaceKind.Auto;
            await RefreshLaunchProfileCompositionAsync();
            AddActivity(
                T("launch.profile.activities.switch"),
                T(
                    "launch.profile.switch.completed",
                    ("profile_name", profileDocument.Profiles[selectedIndex].Username ?? T("launch.profile.entry.unnamed"))));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("launch.profile.activities.switch_failed"), ex.Message);
        }
        finally
        {
            EndLaunchProfileAction();
        }
    }

    private void NormalizeLaunchProfileSurface()
    {
        RefreshLaunchProfileEntries();
        var effectiveSurface = GetEffectiveLaunchProfileSurface();
        if (effectiveSurface == LaunchProfileSurfaceKind.Selection)
        {
            if (!HasLaunchProfileEntries)
            {
                _launchProfileSurface = HasSelectedLaunchProfile ? LaunchProfileSurfaceKind.Summary : LaunchProfileSurfaceKind.Selection;
            }
        }
        else if (!HasSelectedLaunchProfile &&
                 effectiveSurface == LaunchProfileSurfaceKind.Summary)
        {
            _launchProfileSurface = LaunchProfileSurfaceKind.Selection;
        }

        RaiseLaunchProfileSurfaceProperties();
    }

    private LaunchProfileSurfaceKind GetEffectiveLaunchProfileSurface()
    {
        if (_launchProfileSurface != LaunchProfileSurfaceKind.Auto)
        {
            return _launchProfileSurface;
        }

        return HasSelectedLaunchProfile
            ? LaunchProfileSurfaceKind.Summary
            : LaunchProfileSurfaceKind.Selection;
    }

    private void RaiseLaunchProfileSurfaceProperties()
    {
        RaisePropertyChanged(nameof(ShowLaunchProfileSummaryCard));
        RaisePropertyChanged(nameof(ShowLaunchProfileChooser));
        RaisePropertyChanged(nameof(ShowLaunchProfileSelection));
        RaisePropertyChanged(nameof(ShowLaunchOfflineEditor));
        RaisePropertyChanged(nameof(ShowLaunchMicrosoftEditor));
        RaisePropertyChanged(nameof(ShowLaunchAuthlibEditor));
        RaisePropertyChanged(nameof(ShowLaunchProfileBackButton));
        RaisePropertyChanged(nameof(HasLaunchProfileEntries));
        RaisePropertyChanged(nameof(HasNoLaunchProfileEntries));
        RaisePropertyChanged(nameof(LaunchProfileSelectionHint));
        RaisePropertyChanged(nameof(LaunchOfflineUuidModeOptions));
        RaisePropertyChanged(nameof(IsLaunchOfflineCustomUuidVisible));
        RaisePropertyChanged(nameof(HasLaunchOfflineStatus));
        RaisePropertyChanged(nameof(LaunchMicrosoftPrimaryButtonText));
        RaisePropertyChanged(nameof(HasLaunchMicrosoftDeviceCode));
        RaisePropertyChanged(nameof(HasLaunchMicrosoftVerificationUrl));
        RaisePropertyChanged(nameof(HasLaunchAuthlibStatus));
        RaisePropertyChanged(nameof(IsLaunchProfileRefreshInProgress));
    }

    private bool TryGetSelectedStoredLaunchProfile(
        out MinecraftLaunchProfileDocument document,
        out int selectedProfileIndex,
        out MinecraftLaunchPersistedProfile selectedProfile)
    {
        document = FrontendProfileStorageService.Load(_shellActionService.RuntimePaths).Document;
        if (document.Profiles.Count == 0)
        {
            selectedProfileIndex = 0;
            selectedProfile = null!;
            return false;
        }

        selectedProfileIndex = GetSelectedProfileIndex(document);
        if (selectedProfileIndex < 0 || selectedProfileIndex >= document.Profiles.Count)
        {
            selectedProfile = null!;
            return false;
        }

        selectedProfile = document.Profiles[selectedProfileIndex];
        return true;
    }

    private static int GetSelectedProfileIndex(MinecraftLaunchProfileDocument document)
    {
        if (document.Profiles.Count == 0)
        {
            return 0;
        }

        return document.LastUsedProfile >= 0 && document.LastUsedProfile < document.Profiles.Count
            ? document.LastUsedProfile
            : 0;
    }

    private static int? GetSelectedProfileIndexOrNull(MinecraftLaunchProfileDocument document)
    {
        return document.Profiles.Count == 0 ? null : GetSelectedProfileIndex(document);
    }

    private string BuildProfileChoiceSummary(MinecraftLaunchPersistedProfile profile)
    {
        var authLabel = profile.Kind switch
        {
            MinecraftLaunchStoredProfileKind.Microsoft => T("launch.profile.kinds.microsoft"),
            MinecraftLaunchStoredProfileKind.Authlib => string.IsNullOrWhiteSpace(profile.ServerName)
                ? T("launch.profile.kinds.authlib")
                : T("launch.profile.kinds.authlib_with_server", ("server_name", profile.ServerName)),
            _ => T("launch.profile.kinds.offline")
        };

        if (string.IsNullOrWhiteSpace(profile.Desc))
        {
            return authLabel;
        }

        return $"{authLabel}，{profile.Desc}";
    }

    private static MinecraftLaunchStoredProfile ToStoredProfile(MinecraftLaunchPersistedProfile profile)
    {
        return new MinecraftLaunchStoredProfile(
            profile.Kind,
            profile.Uuid ?? string.Empty,
            profile.Username ?? string.Empty,
            profile.Server,
            profile.ServerName,
            profile.AccessToken,
            profile.RefreshToken,
            profile.LoginName,
            profile.Password,
            profile.ClientToken,
            profile.SkinHeadId,
            profile.RawJson);
    }
}
