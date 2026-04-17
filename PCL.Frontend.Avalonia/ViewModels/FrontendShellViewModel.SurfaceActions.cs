using System.IO.Compression;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Runtime;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void ApplySidebarAccessory(string title, string actionLabel, string command)
    {
        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadInstall) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            ResetDownloadInstallSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            ResetDownloadResourceFilters();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupLaunch) && string.Equals(command, "reset", StringComparison.Ordinal))
        {
            ResetLaunchSettingsSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUpdate) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: true);
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupFeedback) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            _ = RefreshFeedbackSectionsAsync(forceRefresh: true);
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupGameManage) && string.Equals(command, "reset", StringComparison.Ordinal))
        {
            ResetGameManageSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupLauncherMisc) && string.Equals(command, "reset", StringComparison.Ordinal))
        {
            ResetLauncherMiscSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupJava) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            RefreshJavaSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUi) && string.Equals(command, "reset", StringComparison.Ordinal))
        {
            ResetUiSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.ToolsTest) && string.Equals(command, "refresh", StringComparison.Ordinal))
        {
            RefreshToolsTestSurface();
            return;
        }

        AddActivity(T("shell.surface_actions.sidebar.title", ("action", actionLabel)), T("shell.surface_actions.sidebar.body", ("title", title), ("command", command)));
    }

    private void RefreshToolsTestSurface()
    {
        ReloadActiveToolsSurface();
        RaisePropertyChanged(nameof(ToolDownloadUrl));
        RaisePropertyChanged(nameof(ToolDownloadUserAgent));
        RaisePropertyChanged(nameof(ToolDownloadFolder));
        RaisePropertyChanged(nameof(ToolDownloadName));
        RaisePropertyChanged(nameof(OfficialSkinPlayerName));
        RaisePropertyChanged(nameof(AchievementBlockId));
        RaisePropertyChanged(nameof(AchievementTitle));
        RaisePropertyChanged(nameof(AchievementFirstLine));
        RaisePropertyChanged(nameof(AchievementSecondLine));
        RaisePropertyChanged(nameof(ShowAchievementPreview));
        RaisePropertyChanged(nameof(AchievementPreviewImage));
        RaisePropertyChanged(nameof(SelectedHeadSizeIndex));
        RaisePropertyChanged(nameof(SelectedHeadSkinPath));
        RaisePropertyChanged(nameof(HasSelectedHeadSkin));
        RaisePropertyChanged(nameof(HeadPreviewSize));
        RaisePropertyChanged(nameof(HeadPreviewImage));
        RaisePropertyChanged(nameof(HasHeadPreviewImage));
        ResetMinecraftServerQuerySurface();
        AddActivity(
            LT("shell.tools.test.refresh.title"),
            LT("shell.tools.test.refresh.activity"));
    }

    private Task CreateInstanceProfileAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(T("launch.profile.instance_create.title"), T("instance.overview.messages.no_instance_selected"));
            return Task.CompletedTask;
        }

        ResetMicrosoftDeviceFlow();
        LaunchAuthlibServer = string.IsNullOrWhiteSpace(InstanceServerAuthServer)
            ? DefaultAuthlibServer
            : InstanceServerAuthServer.Trim();
        LaunchAuthlibLoginName = string.Empty;
        LaunchAuthlibPassword = string.Empty;
        LaunchAuthlibStatusText = string.IsNullOrWhiteSpace(InstanceServerAuthName)
            ? T("launch.profile.instance_create.status_default")
            : T("launch.profile.instance_create.status_named", ("auth_name", InstanceServerAuthName));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            T("launch.profile.instance_create.navigation"));
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.AuthlibEditor);
        AddActivity(T("launch.profile.instance_create.title"), T("launch.profile.instance_create.completed", ("instance_name", _instanceComposition.Selection.InstanceName)));
        return Task.CompletedTask;
    }

    private void ResetDownloadInstallSurface()
    {
        _downloadInstallIsInSelectionStage = false;
        _downloadInstallExpandedOptionTitle = null;
        _downloadInstallMinecraftChoice = null;
        _downloadInstallIsNameEditedByUser = false;
        _downloadInstallOptionChoices.Clear();
        _downloadInstallOptionLoadsInProgress.Clear();
        _downloadInstallOptionLoadErrors.Clear();
        _downloadInstallMinecraftCatalogLoaded = false;
        ReplaceItems(DownloadInstallMinecraftSections, []);
        InitializeDownloadInstallSurface();
        RaisePropertyChanged(nameof(DownloadInstallName));
        AddActivity(
            LT("download.install.refresh.title"),
            LT("download.install.refresh.activity"));
    }

    private void ResetGameManageSurface()
    {
        _shellActionService.RemoveSharedValues(GameManageResetKeys);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.game_manage.activities.reset"),
            LT("setup.game_manage.activities.reset_completed"));
    }

    private void ResetLauncherMiscSurface()
    {
        _shellActionService.RemoveLocalValues(LauncherMiscLocalResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscSharedResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscProtectedResetKeys);
        if (!_i18n.ReloadLocaleFromSettings())
        {
            ReloadSetupComposition();
        }

        AddActivity(
            LT("setup.launcher_misc.activities.reset"),
            LT("setup.launcher_misc.activities.reset_completed"));
    }

    private async Task RefreshFeedbackSectionsAsync(bool forceRefresh)
    {
        if (_isRefreshingFeedback)
        {
            return;
        }

        if (!forceRefresh
            && _feedbackSnapshot is not null
            && DateTimeOffset.UtcNow - _lastFeedbackRefreshUtc < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _isRefreshingFeedback = true;
        try
        {
            var snapshot = await FrontendSetupFeedbackService.QueryAsync(_i18n);
            _feedbackSnapshot = snapshot;
            _lastFeedbackRefreshUtc = snapshot.FetchedAtUtc;
            ApplyFeedbackSnapshot(snapshot);
            RaisePropertyChanged(nameof(HasFeedbackSections));
            AddActivity(
                LT("setup.feedback.activities.refresh"),
                LT("setup.feedback.activities.refresh_completed", ("count", snapshot.Sections.Sum(section => section.Entries.Count))));
        }
        catch (Exception ex)
        {
            if (_feedbackSnapshot is null)
            {
                ReplaceItems(FeedbackSections,
                [
                    CreateFeedbackSection(SetupText.Feedback.LoadFailedSectionTitle, true,
                    [
                        CreateSimpleEntry(SetupText.Feedback.LoadFailedEntryTitle, ex.Message)
                    ])
                ]);
                RaisePropertyChanged(nameof(HasFeedbackSections));
            }

            AddFailureActivity(LT("setup.feedback.activities.refresh_failed"), ex.Message);
        }
        finally
        {
            _isRefreshingFeedback = false;
        }
    }

    private void ResetUiSurface()
    {
        _shellActionService.RemoveLocalValues(UiLocalResetKeys);
        _shellActionService.RemoveSharedValues(UiSharedResetKeys);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.ui.activities.reset"),
            LT("setup.ui.activities.reset_completed"));
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "update" : cleaned;
    }

    private HttpClient CreateToolHttpClient()
    {
        var userAgent = string.IsNullOrWhiteSpace(ToolDownloadUserAgent)
            ? "PCL-ME-Avalonia/1.0"
            : ToolDownloadUserAgent.Trim();
        return FrontendHttpProxyService.CreateLauncherHttpClient(
            TimeSpan.FromSeconds(100),
            userAgent);
    }
}
