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

    private async Task CheckForLauncherUpdatesAsync(bool forceRefresh)
    {
        var signature = $"{SelectedUpdateChannelIndex}";
        if (_isCheckingUpdate)
        {
            return;
        }

        if (!forceRefresh
            && string.Equals(_lastUpdateCheckSignature, signature, StringComparison.Ordinal)
            && _updateStatus.SurfaceState is not UpdateSurfaceState.Checking)
        {
            return;
        }

        _isCheckingUpdate = true;
        _updateStatus = FrontendSetupUpdateStatusService.CreateChecking(_i18n);
        RaiseUpdateSurfaceProperties();

        try
        {
            _updateStatus = await FrontendSetupUpdateStatusService.QueryAsync(SelectedUpdateChannelIndex, _i18n);
            _lastUpdateCheckSignature = signature;
            RaiseUpdateSurfaceProperties();

            AddActivity(
                LT("setup.update.activities.refresh"),
                _updateStatus.SurfaceState switch
                {
                    UpdateSurfaceState.Available => LT("setup.update.activities.available", ("version", _updateStatus.AvailableUpdateName)),
                    UpdateSurfaceState.Latest => LT("setup.update.activities.latest", ("version", _updateStatus.CurrentVersionName)),
                    UpdateSurfaceState.Error => _updateStatus.CurrentVersionDescription,
                    _ => LT("setup.update.activities.checking")
                });
        }
        finally
        {
            _isCheckingUpdate = false;
        }
    }

    private void DownloadAvailableUpdate()
    {
        _ = DownloadAvailableUpdateAsync();
    }

    private async Task DownloadAvailableUpdateAsync()
    {
        if (_updateStatus.SurfaceState != UpdateSurfaceState.Available)
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.no_pending_update"));
            return;
        }

        var target = _updateStatus.AvailableUpdateDownloadUrl ?? _updateStatus.AvailableUpdateReleaseUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.no_download_url"));
            return;
        }

        try
        {
            var preparedInstall = await FrontendUpdateInstallWorkflowService.PrepareAsync(
                new FrontendUpdateInstallRequest(
                    DownloadUrl: target,
                    ReleaseFileStem: SanitizeFileSegment(_updateStatus.AvailableUpdateName),
                    ExpectedSha256: string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateSha256)
                        ? null
                        : _updateStatus.AvailableUpdateSha256,
                    ArtifactDirectory: _shellActionService.RuntimePaths.FrontendArtifactDirectory,
                    TempDirectory: _shellActionService.RuntimePaths.FrontendTempDirectory,
                    ExecutableDirectory: _shellActionService.RuntimePaths.ExecutableDirectory,
                    ProcessPath: Environment.ProcessPath,
                    ProcessId: Environment.ProcessId,
                    PlatformAdapter: _shellActionService.PlatformAdapter));
            var outputPath = Path.Combine(
                _shellActionService.RuntimePaths.FrontendArtifactDirectory,
                "update-downloads",
                $"{SanitizeFileSegment(_updateStatus.AvailableUpdateName)}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, $"""
                Update: {_updateStatus.AvailableUpdateName}
                Source: {_updateStatus.AvailableUpdateSource}
                SHA256: {_updateStatus.AvailableUpdateSha256}
                Download: {target}
                Release: {_updateStatus.AvailableUpdateReleaseUrl}
                Archive: {preparedInstall.ArchivePath}
                Extracted: {preparedInstall.ExtractedPackagePath}
                Script: {preparedInstall.InstallerScriptPath}
                """, new UTF8Encoding(false));

            if (!_shellActionService.TryStartDetachedScript(preparedInstall.InstallerScriptPath, out var error))
            {
                AddFailureActivity(
                    LT("setup.update.activities.download_install_failed"),
                    error ?? preparedInstall.InstallerScriptPath);
                return;
            }

            AddActivity(
                LT("setup.update.activities.download_install"),
                LT("setup.update.activities.package_ready", ("version", _updateStatus.AvailableUpdateName)));
            AvaloniaHintBus.Show(LT("setup.update.activities.package_ready_hint"), AvaloniaHintTheme.Success);
            await Task.Delay(400);
            _shellActionService.ExitLauncher();
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.update.activities.download_install_failed"), ex.Message);
        }
    }

    private void ShowAvailableUpdateDetail() => _ = ShowAvailableUpdateDetailAsync();

    private async Task ShowAvailableUpdateDetailAsync()
    {
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateChangelog))
        {
            var result = await ShowToolboxConfirmationAsync(
                LT("setup.update.activities.view_detail_title", ("version", _updateStatus.AvailableUpdateName)),
                _updateStatus.AvailableUpdateChangelog);
            if (result is null)
            {
                return;
            }

            AddActivity(LT("setup.update.activities.view_detail"), _updateStatus.AvailableUpdateName);

            return;
        }

        string? openError = null;
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateReleaseUrl)
            && _shellActionService.TryOpenExternalTarget(_updateStatus.AvailableUpdateReleaseUrl, out openError))
        {
            AddActivity(LT("setup.update.activities.view_detail"), _updateStatus.AvailableUpdateReleaseUrl);
            return;
        }

        AddFailureActivity(
            LT("setup.update.activities.view_detail_failed"),
            openError ?? LT("setup.update.activities.detail_unavailable"));
    }

    private void ResetLaunchSettingsSurface()
    {
        _shellActionService.RemoveLocalValues(LaunchLocalResetKeys);
        _shellActionService.RemoveSharedValues(LaunchSharedResetKeys);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.launch.activities.reset"),
            LT("setup.launch.activities.reset_completed"));
    }

    private void RefreshToolsTestSurface()
    {
        ReloadToolsComposition();
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

    private async Task ManageDownloadFavoriteTargetsAsync()
    {
        var root = LoadDownloadFavoriteTargetRoot();
        var targets = root.OfType<JsonObject>().ToArray();
        var selectedIndex = Math.Clamp(SelectedDownloadFavoriteTargetIndex, 0, Math.Max(targets.Length - 1, 0));
        var currentTarget = targets.Length == 0
            ? EnsureCommunityProjectFavoriteTarget(root)
            : targets[selectedIndex];
        var currentTargetName = GetDownloadFavoriteTargetName(currentTarget);

        string? actionId;
        try
        {
            actionId = await _shellActionService.PromptForChoiceAsync(
                T("download.favorites.targets.manage.title"),
                T("download.favorites.targets.manage.current_target", ("target_name", currentTargetName)),
                [
                    new PclChoiceDialogOption("share", T("download.favorites.targets.manage.options.share.label"), T("download.favorites.targets.manage.options.share.description")),
                    new PclChoiceDialogOption("import", T("download.favorites.targets.manage.options.import.label"), T("download.favorites.targets.manage.options.import.description")),
                    new PclChoiceDialogOption("create", T("download.favorites.targets.manage.options.create.label"), T("download.favorites.targets.manage.options.create.description")),
                    new PclChoiceDialogOption("rename", T("download.favorites.targets.manage.options.rename.label"), T("download.favorites.targets.manage.options.rename.description")),
                    new PclChoiceDialogOption("delete", T("download.favorites.targets.manage.options.delete.label"), T("download.favorites.targets.manage.options.delete.description"))
                ],
                "share",
                T("common.actions.continue"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.manage.failed"), ex.Message);
            return;
        }

        if (actionId is null)
        {
            return;
        }

        switch (actionId)
        {
            case "share":
                await ShareDownloadFavoriteTargetAsync(currentTarget);
                return;
            case "import":
                await ImportDownloadFavoriteTargetAsync(root, currentTarget);
                return;
            case "create":
                await CreateDownloadFavoriteTargetAsync(root);
                return;
            case "rename":
                await RenameDownloadFavoriteTargetAsync(root, currentTarget, selectedIndex);
                return;
            case "delete":
                await DeleteDownloadFavoriteTargetAsync(root, currentTarget);
                return;
            default:
                AddFailureActivity(T("download.favorites.targets.manage.failed"), T("download.favorites.targets.manage.unknown_action", ("action_id", actionId)));
                return;
        }
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

    private static string SafeReadFavoriteJson(JsonFileProvider provider)
    {
        try
        {
            return provider.Get<string>("CompFavorites");
        }
        catch
        {
            return "[]";
        }
    }

    private JsonArray LoadDownloadFavoriteTargetRoot()
    {
        var provider = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
        var rawFavorites = provider.Exists("CompFavorites")
            ? SafeReadFavoriteJson(provider)
            : "[]";
        var root = ParseCommunityProjectFavoriteTargets(rawFavorites);
        EnsureCommunityProjectFavoriteTarget(root);
        return root;
    }

    private void PersistDownloadFavoriteTargetRoot(JsonArray root, int selectedIndex)
    {
        _shellActionService.PersistSharedValue("CompFavorites", root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        }));
        ReloadDownloadComposition();
        SelectedDownloadFavoriteTargetIndex = Math.Clamp(selectedIndex, 0, Math.Max(DownloadFavoriteTargetOptions.Count - 1, 0));
        RefreshDownloadFavoriteSurface();
    }

    private async Task ShareDownloadFavoriteTargetAsync(JsonObject target)
    {
        var favoriteIds = EnsureCommunityProjectFavoriteArray(target)
            .Select(GetCommunityProjectFavoriteId)
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (favoriteIds.Length == 0)
        {
            AddActivity(T("download.favorites.targets.share.activity"), T("download.favorites.targets.empty_share_code"));
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(JsonSerializer.Serialize(favoriteIds));
            AddActivity(T("download.favorites.targets.share.activity"), T("download.favorites.targets.share.completed", ("target_name", GetDownloadFavoriteTargetName(target))));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.share.failed"), ex.Message);
        }
    }

    private async Task ImportDownloadFavoriteTargetAsync(JsonArray root, JsonObject currentTarget)
    {
        string? shareCode;
        try
        {
            shareCode = await _shellActionService.PromptForTextAsync(
                T("download.favorites.targets.import.title"),
                T("download.favorites.targets.import.prompt"),
                string.Empty,
                T("common.actions.continue"),
                T("download.favorites.targets.import.watermark"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.import.failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(shareCode))
        {
            return;
        }

        var importedIds = ParseDownloadFavoriteShareCode(shareCode);
        if (importedIds.Count == 0)
        {
            AddActivity(T("download.favorites.targets.import.activity"), T("download.favorites.targets.empty_share_code"));
            return;
        }

        string? destinationId;
        try
        {
            destinationId = await _shellActionService.PromptForChoiceAsync(
                T("download.favorites.targets.import.title"),
                T("download.favorites.targets.import.destination_prompt", ("count", importedIds.Count)),
                [
                    new PclChoiceDialogOption("new", T("download.favorites.targets.import.options.new_target.label"), T("download.favorites.targets.import.options.new_target.description")),
                    new PclChoiceDialogOption("current", T("download.favorites.targets.import.options.current_target.label"), T("download.favorites.targets.import.options.current_target.description"))
                ],
                "current",
                T("common.actions.continue"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.import.failed"), ex.Message);
            return;
        }

        if (destinationId is null)
        {
            return;
        }

        if (string.Equals(destinationId, "new", StringComparison.Ordinal))
        {
            string? newTargetName;
            try
            {
                newTargetName = await _shellActionService.PromptForTextAsync(
                    T("download.favorites.targets.create.title"),
                    T("download.favorites.targets.create.prompt"));
            }
            catch (Exception ex)
            {
                AddFailureActivity(T("download.favorites.targets.import.failed"), ex.Message);
                return;
            }

            newTargetName = newTargetName?.Trim();
            if (string.IsNullOrWhiteSpace(newTargetName))
            {
                return;
            }

            root.Add(CreateDownloadFavoriteTargetNode(newTargetName, importedIds));
            PersistDownloadFavoriteTargetRoot(root, root.OfType<JsonObject>().Count() - 1);
            AddActivity(T("download.favorites.targets.import.activity"), T("download.favorites.targets.import.completed_new", ("target_name", newTargetName)));
            return;
        }

        var favorites = EnsureCommunityProjectFavoriteArray(currentTarget);
        var mergedIds = new HashSet<string>(
            favorites.Select(GetCommunityProjectFavoriteId).OfType<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var id in importedIds)
        {
            if (mergedIds.Add(id))
            {
                favorites.Add(id);
            }
        }

        PersistDownloadFavoriteTargetRoot(root, SelectedDownloadFavoriteTargetIndex);
        AddActivity(T("download.favorites.targets.import.activity"), T("download.favorites.targets.import.completed_current", ("target_name", GetDownloadFavoriteTargetName(currentTarget))));
    }

    private async Task CreateDownloadFavoriteTargetAsync(JsonArray root)
    {
        string? newTargetName;
        try
        {
            newTargetName = await _shellActionService.PromptForTextAsync(
                T("download.favorites.targets.create.title"),
                T("download.favorites.targets.create.prompt"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.create.failed"), ex.Message);
            return;
        }

        newTargetName = newTargetName?.Trim();
        if (string.IsNullOrWhiteSpace(newTargetName))
        {
            return;
        }

        root.Add(CreateDownloadFavoriteTargetNode(newTargetName));
        PersistDownloadFavoriteTargetRoot(root, root.OfType<JsonObject>().Count() - 1);
        AddActivity(T("download.favorites.targets.create.activity"), newTargetName);
    }

    private async Task RenameDownloadFavoriteTargetAsync(JsonArray root, JsonObject target, int selectedIndex)
    {
        string? nextName;
        try
        {
            nextName = await _shellActionService.PromptForTextAsync(
                T("download.favorites.targets.rename.title"),
                T("download.favorites.targets.rename.prompt"),
                GetDownloadFavoriteTargetName(target));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.rename.failed"), ex.Message);
            return;
        }

        nextName = nextName?.Trim();
        if (string.IsNullOrWhiteSpace(nextName) || string.Equals(nextName, GetDownloadFavoriteTargetName(target), StringComparison.Ordinal))
        {
            return;
        }

        target["Name"] = nextName;
        PersistDownloadFavoriteTargetRoot(root, selectedIndex);
        AddActivity(T("download.favorites.targets.rename.activity"), nextName);
    }

    private async Task DeleteDownloadFavoriteTargetAsync(JsonArray root, JsonObject target)
    {
        var targets = root.OfType<JsonObject>().ToArray();
        if (targets.Length <= 1)
        {
            AddActivity(T("download.favorites.targets.delete.activity"), T("download.favorites.targets.delete.blocked_last"));
            return;
        }

        var favoriteCount = EnsureCommunityProjectFavoriteArray(target)
            .Select(GetCommunityProjectFavoriteId)
            .OfType<string>()
            .Count();
        var content = T(
            "download.favorites.targets.delete.confirmation_message",
            ("target_name", GetDownloadFavoriteTargetName(target)),
            ("count", favoriteCount),
            ("target_id", GetDownloadFavoriteTargetId(target)));
        bool confirmed;
        try
        {
            confirmed = await _shellActionService.ConfirmAsync(T("download.favorites.targets.delete.confirmation_title"), content, T("download.favorites.targets.delete.confirm"), isDanger: true);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("download.favorites.targets.delete.failed"), ex.Message);
            return;
        }

        if (!confirmed)
        {
            return;
        }

        root.Remove(target);
        PersistDownloadFavoriteTargetRoot(root, 0);
        AddActivity(T("download.favorites.targets.delete.activity"), T("download.favorites.targets.delete.completed", ("target_name", GetDownloadFavoriteTargetName(target))));
    }

    private static HashSet<string> ParseDownloadFavoriteShareCode(string code)
    {
        try
        {
            return JsonSerializer.Deserialize<HashSet<string>>(code) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static JsonObject CreateDownloadFavoriteTargetNode(string name, IEnumerable<string>? favoriteIds = null)
    {
        var favorites = new JsonArray();
        if (favoriteIds is not null)
        {
            foreach (var favoriteId in favoriteIds
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                favorites.Add(favoriteId);
            }
        }

        return new JsonObject
        {
            ["Name"] = name,
            ["Id"] = Guid.NewGuid().ToString("N"),
            ["Favs"] = favorites,
            ["Notes"] = new JsonObject()
        };
    }

    private string GetDownloadFavoriteTargetName(JsonObject target)
    {
        return target["Name"]?.GetValue<string>()?.Trim() switch
        {
            { Length: > 0 } value => value,
            _ => T("download.favorites.targets.default_name")
        };
    }

    private static string GetDownloadFavoriteTargetId(JsonObject target)
    {
        return target["Id"]?.GetValue<string>()?.Trim() switch
        {
            { Length: > 0 } value => value,
            _ => "default"
        };
    }

    private async Task SelectDownloadFolderAsync()
    {
        string? selectedFolder;
        try
        {
            selectedFolder = await _shellActionService.PickFolderAsync(LT("shell.tools.test.custom_download.pick_folder_title"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.pick_folder_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            AddActivity(
                LT("shell.tools.test.custom_download.pick_folder_activity"),
                LT("shell.tools.test.custom_download.pick_folder_cancelled"));
            return;
        }

        ToolDownloadFolder = selectedFolder;
        AddActivity(LT("shell.tools.test.custom_download.pick_folder_activity"), ToolDownloadFolder);
    }

    private Task StartCustomDownloadAsync()
    {
        if (!Uri.TryCreate(ToolDownloadUrl, UriKind.Absolute, out var uri))
        {
            AddFailureActivity(
                LT("shell.tools.test.custom_download.start_failure"),
                LT("shell.tools.test.custom_download.invalid_address"));
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(ToolDownloadFolder))
        {
            AddFailureActivity(
                LT("shell.tools.test.custom_download.start_failure"),
                LT("shell.tools.test.custom_download.missing_folder"));
            return Task.CompletedTask;
        }

        var fileName = string.IsNullOrWhiteSpace(ToolDownloadName)
            ? Path.GetFileName(uri.LocalPath)
            : ToolDownloadName.Trim();
        fileName = string.IsNullOrWhiteSpace(fileName) ? "download.bin" : SanitizeFileSegment(fileName);

        var targetDirectory = Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(targetDirectory);
        var targetPath = Path.Combine(targetDirectory, fileName);

        try
        {
            var userAgent = string.IsNullOrWhiteSpace(ToolDownloadUserAgent)
                ? null
                : ToolDownloadUserAgent.Trim();
            TaskCenter.Register(new FrontendManagedFileDownloadTask(
                $"自定义下载：{fileName}",
                uri.ToString(),
                targetPath,
                ResolveDownloadRequestTimeout(),
                _shellActionService.GetDownloadTransferOptions(),
                onStarted: filePath => AvaloniaHintBus.Show($"开始下载 {Path.GetFileName(filePath)}", AvaloniaHintTheme.Info),
                onCompleted: filePath => AvaloniaHintBus.Show($"{Path.GetFileName(filePath)} 下载完成", AvaloniaHintTheme.Success),
                onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error),
                userAgent: userAgent));

            AddActivity(LT("shell.tools.test.custom_download.start_activity"), $"{uri} -> {targetPath}");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.start_failure"), ex.Message);
        }

        return Task.CompletedTask;
    }

    private void SaveOfficialSkin()
    {
        _ = SaveOfficialSkinAsync();
    }

    private void ExportLauncherLogs(bool includeAllLogs)
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity(LT("shell.game_log.actions.export"), LT("shell.game_log.export.missing_directory"));
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length == 0)
        {
            AddActivity(LT("shell.game_log.actions.export"), LT("shell.game_log.export.empty_directory"));
            return;
        }

        var selectedFiles = includeAllLogs ? logFiles : logFiles.Take(1).ToArray();
        var exportDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "log-exports");
        Directory.CreateDirectory(exportDirectory);

        var archiveName = includeAllLogs ? "launcher-logs-all.zip" : "launcher-log-latest.zip";
        var archivePath = GetUniqueArchivePath(Path.Combine(exportDirectory, archiveName));

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            foreach (var file in selectedFiles)
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        AddActivity(
            includeAllLogs ? LT("shell.game_log.export.export_all_activity") : LT("shell.game_log.actions.export"),
            archivePath);
    }

    private void OpenLauncherLogDirectory()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        Directory.CreateDirectory(logDirectory);
        if (_shellActionService.TryOpenExternalTarget(logDirectory, out var error))
        {
            AddActivity(LT("shell.game_log.actions.open_directory"), logDirectory);
        }
        else
        {
            AddFailureActivity(LT("shell.game_log.open_directory_failure"), error ?? logDirectory);
        }
    }

    private void CleanLauncherLogs()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity(
                LT("setup.log.activities.clean_history"),
                LT("setup.log.activities.directory_missing"));
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length <= 1)
        {
            AddActivity(
                LT("setup.log.activities.clean_history"),
                LT("setup.log.activities.nothing_to_clean"));
            return;
        }

        var removedCount = 0;
        foreach (var file in logFiles.Skip(1))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore individual failures and continue clearing other archived logs.
            }
        }

        ReloadSetupComposition();
        AddActivity(
            LT("setup.log.activities.clean_history"),
            removedCount == 0
                ? LT("setup.log.activities.clean_failed")
                : LT("setup.log.activities.cleaned_count", ("count", removedCount)));
    }

    private void ExportSettingsSnapshot()
    {
        var exportDirectory = Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "config-exports",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(exportDirectory);

        var sharedTarget = Path.Combine(exportDirectory, Path.GetFileName(_shellActionService.RuntimePaths.SharedConfigPath));
        var localTarget = Path.Combine(exportDirectory, Path.GetFileName(_shellActionService.RuntimePaths.LocalConfigPath));
        File.Copy(_shellActionService.RuntimePaths.SharedConfigPath, sharedTarget, true);
        File.Copy(_shellActionService.RuntimePaths.LocalConfigPath, localTarget, true);

        AddActivity(LT("setup.launcher_misc.activities.export_settings"), exportDirectory);
    }

    private async Task ImportSettingsAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("setup.launcher_misc.activities.import_settings_pick_title"),
                LT("setup.launcher_misc.activities.import_settings_pick_filter"),
                "*.json");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.launcher_misc.activities.import_settings_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(
                LT("setup.launcher_misc.activities.import_settings"),
                LT("setup.launcher_misc.activities.import_settings_cancelled"));
            return;
        }

        Directory.CreateDirectory(_shellActionService.RuntimePaths.SharedConfigDirectory);
        File.Copy(sourcePath, _shellActionService.RuntimePaths.SharedConfigPath, true);
        if (!_i18n.ReloadLocaleFromSettings())
        {
            ReloadSetupComposition();
        }

        AddActivity(
            LT("setup.launcher_misc.activities.import_settings"),
            LT("setup.launcher_misc.activities.import_settings_completed", ("path", sourcePath)));
    }

    private void ApplyProxySettings()
    {
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxy", HttpProxyAddress);
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomUsername", HttpProxyUsername);
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomPassword", HttpProxyPassword);
        ReloadSetupComposition();
        AddActivity(
            LT("setup.launcher_misc.activities.apply_proxy"),
            string.IsNullOrWhiteSpace(HttpProxyAddress)
                ? LT("setup.launcher_misc.activities.proxy_cleared")
                : HttpProxyAddress);
    }

    private async Task TestProxyConnectionAsync()
    {
        if (_isTestingProxyConnection)
        {
            return;
        }

        var configuration = FrontendHttpProxyService.BuildConfiguration(
            SelectedHttpProxyTypeIndex,
            HttpProxyAddress,
            HttpProxyUsername,
            HttpProxyPassword);
        if (SelectedHttpProxyTypeIndex == 2 && configuration.CustomProxyAddress is null)
        {
            var invalidAddressMessage = LT("setup.launcher_misc.messages.proxy_invalid_address");
            SetProxyTestFeedback(invalidAddressMessage, isSuccess: false);
            AddFailureActivity(LT("setup.launcher_misc.activities.test_proxy_failed"), invalidAddressMessage);
            return;
        }

        _isTestingProxyConnection = true;
        _testProxyConnectionCommand.NotifyCanExecuteChanged();
        AddActivity(
            LT("setup.launcher_misc.activities.test_proxy"),
            LT(
                "setup.launcher_misc.messages.proxy_test_started",
                ("mode", DescribeProxyMode(configuration)),
                ("host", FrontendHttpProxyService.ProxyConnectivityProbeUri.Host)));

        try
        {
            using var client = FrontendHttpProxyService.CreateHttpClient(
                configuration,
                TimeSpan.FromSeconds(12),
                "PCL-ME-Avalonia/1.0");
            using var request = new HttpRequestMessage(HttpMethod.Get, FrontendHttpProxyService.ProxyConnectivityProbeUri);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var successMessage = LT(
                "setup.launcher_misc.messages.proxy_test_succeeded",
                ("mode", DescribeProxyMode(configuration)),
                ("status_code", (int)response.StatusCode),
                ("reason_phrase", response.ReasonPhrase ?? string.Empty));
            AddActivity(LT("setup.launcher_misc.activities.test_proxy"), successMessage);
            SetProxyTestFeedback(successMessage, isSuccess: true);
            AvaloniaHintBus.Show(LT("setup.launcher_misc.messages.proxy_test_succeeded_hint"), AvaloniaHintTheme.Success);
        }
        catch (Exception ex)
        {
            SetProxyTestFeedback(ex.Message, isSuccess: false);
            AddFailureActivity(LT("setup.launcher_misc.activities.test_proxy_failed"), ex.Message);
        }
        finally
        {
            _isTestingProxyConnection = false;
            _testProxyConnectionCommand.NotifyCanExecuteChanged();
        }
    }

    private void ClearProxyTestFeedback()
    {
        _isProxyTestFeedbackSuccess = false;
        ProxyTestFeedbackText = string.Empty;
    }

    private void SetProxyTestFeedback(string text, bool isSuccess)
    {
        _isProxyTestFeedbackSuccess = isSuccess;
        ProxyTestFeedbackText = text;
    }

    private string DescribeProxyMode(FrontendResolvedProxyConfiguration configuration)
    {
        return configuration.Mode switch
        {
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.CustomProxy => configuration.CustomProxyAddress?.ToString() ?? SetupText.LauncherMisc.CustomProxyLabel,
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.SystemProxy => SetupText.LauncherMisc.SystemProxyLabel,
            _ => SetupText.LauncherMisc.NoProxyLabel
        };
    }

    private void OpenBackgroundFolder()
    {
        var folder = GetBackgroundFolderPath();
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("setup.ui.background.activities.open_folder"), folder);
        }
        else
        {
            AddFailureActivity(LT("setup.ui.background.activities.open_folder_failed"), error ?? folder);
        }
    }

    private void RefreshBackgroundAssets()
    {
        RefreshBackgroundContentState(selectNewAsset: true, addActivity: true);
    }

    private void ClearBackgroundAssets()
    {
        var folder = GetBackgroundFolderPath();
        var removedCount = DeleteDirectoryContents(folder, BackgroundCleanupExtensions);
        RefreshBackgroundContentState(selectNewAsset: false, addActivity: false);
        AddActivity(
            LT("setup.ui.background.activities.clear"),
            removedCount == 0
                ? LT("setup.ui.background.activities.clear_empty")
                : LT("setup.ui.background.activities.clear_count", ("count", removedCount)));
    }

    private void OpenMusicFolder()
    {
        var folder = GetMusicFolderPath();
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("setup.ui.music.activities.open_folder"), folder);
        }
        else
        {
            AddFailureActivity(LT("setup.ui.music.activities.open_folder_failed"), error ?? folder);
        }
    }

    private void RefreshMusicAssets()
    {
        var assets = EnumerateMediaFiles(GetMusicFolderPath(), MusicMediaExtensions).ToArray();
        AddActivity(
            LT("setup.ui.music.activities.refresh"),
            assets.Length == 0
                ? LT("setup.ui.music.activities.empty")
                : LT("setup.ui.music.activities.refreshed_count", ("count", assets.Length)));
    }

    private void ClearMusicAssets()
    {
        var folder = GetMusicFolderPath();
        var removedCount = DeleteDirectoryContents(folder, MusicMediaExtensions);
        AddActivity(
            LT("setup.ui.music.activities.clear"),
            removedCount == 0
                ? LT("setup.ui.music.activities.clear_empty")
                : LT("setup.ui.music.activities.clear_count", ("count", removedCount)));
    }

    private async Task ChangeLogoImageAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("setup.ui.title_bar.activities.change_image_pick_title"),
                LT("setup.ui.title_bar.activities.change_image_pick_filter"),
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.gif",
                "*.webp");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.ui.title_bar.activities.change_image_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(
                LT("setup.ui.title_bar.activities.change_image"),
                LT("setup.ui.title_bar.activities.change_image_cancelled"));
            return;
        }

        var targetPath = GetLogoImagePath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        if (SelectedLogoTypeIndex != 3)
        {
            SelectedLogoTypeIndex = 3;
        }

        RefreshTitleBarLogoImage();
        AddActivity(LT("setup.ui.title_bar.activities.change_image"), $"{sourcePath} -> {targetPath}");
    }

    private void DeleteLogoImage()
    {
        var targetPath = GetLogoImagePath();
        if (!File.Exists(targetPath))
        {
            AddActivity(
                LT("setup.ui.title_bar.activities.clear_image"),
                LT("setup.ui.title_bar.activities.clear_image_empty"));
            return;
        }

        File.Delete(targetPath);
        if (SelectedLogoTypeIndex == 3)
        {
            SelectedLogoTypeIndex = 1;
        }

        RefreshTitleBarLogoImage();
        AddActivity(LT("setup.ui.title_bar.activities.clear_image"), targetPath);
    }

    private void RefreshHomepageContent()
    {
        RefreshLaunchHomepage(forceRefresh: true, addActivity: true);
    }

    private void GenerateHomepageTutorialFile()
    {
        var sourcePath = Path.Combine(LauncherRootDirectory, "Resources", "Custom.xml");
        if (!File.Exists(sourcePath))
        {
            AddFailureActivity(
                LT("setup.ui.homepage.activities.generate_tutorial_failed"),
                LT("setup.ui.homepage.activities.tutorial_template_missing", ("path", sourcePath)));
            return;
        }

        var targetPath = GetHomepageTutorialPath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        AddActivity(LT("setup.ui.homepage.activities.generate_tutorial"), targetPath);
    }

    private void ViewHomepageTutorial() => _ = ViewHomepageTutorialAsync();

    private async Task ViewHomepageTutorialAsync()
    {
        var result = await ShowToolboxConfirmationAsync(
            LT("setup.ui.homepage.activities.view_tutorial"),
            LT("setup.ui.homepage.activities.tutorial_content"));
        if (result is null)
        {
            return;
        }

        AddActivity(
            LT("setup.ui.homepage.activities.view_tutorial"),
            LT("setup.ui.homepage.activities.tutorial_shown"));
    }

    private async Task PreviewAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddFailureActivity("预览成就图片失败", "请先填写有效的成就内容。");
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            AchievementPreviewImage = new Bitmap(stream);
            ShowAchievementPreview = true;
            AddActivity("预览成就图片", $"已加载 {AchievementTitle.Trim()} 的成就图像。\n{url}");
        }
        catch (Exception ex)
        {
            ShowAchievementPreview = false;
            AchievementPreviewImage = null;
            AddFailureActivity("预览成就图片失败", ex.Message);
        }
    }

    private async Task SelectHeadSkinAsync()
    {
        string? sourcePath;
        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("shell.tools.test.head.pick_skin_title"),
                LT("shell.tools.test.head.pick_skin_filter"),
                "*.png",
                "*.jpg",
                "*.jpeg");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.head.pick_skin_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity(
                LT("shell.tools.test.head.pick_skin_activity"),
                LT("shell.tools.test.head.pick_skin_cancelled"));
            return;
        }

        SelectedHeadSkinPath = sourcePath;
        AddActivity(LT("shell.tools.test.head.pick_skin_activity"), SelectedHeadSkinPath);
    }

    private void RefreshHeadPreviewFromSelection(bool addActivity)
    {
        if (!HasSelectedHeadSkin || !File.Exists(SelectedHeadSkinPath))
        {
            HeadPreviewImage = null;
            return;
        }

        try
        {
            HeadPreviewImage = GenerateHeadPreviewBitmap(SelectedHeadSkinPath, SelectedHeadSizeIndex);
            if (addActivity)
            {
                AddActivity("头像预览", $"已生成 {HeadSizeOptions[SelectedHeadSizeIndex]} 头像预览。");
            }
        }
        catch (Exception ex)
        {
            HeadPreviewImage = null;
            if (addActivity)
            {
                AddFailureActivity("头像预览失败", ex.Message);
            }
        }
    }

    private Task SaveHeadAsync()
    {
        if (!HasSelectedHeadSkin || !File.Exists(SelectedHeadSkinPath))
        {
            AddActivity(
                LT("shell.tools.test.head.save_activity"),
                LT("shell.tools.test.head.no_skin_selected"));
            return Task.CompletedTask;
        }

        var previewImage = HeadPreviewImage;
        if (previewImage is null)
        {
            AddActivity(
                LT("shell.tools.test.head.save_activity"),
                LT("shell.tools.test.head.output_missing"));
            return Task.CompletedTask;
        }

        try
        {
            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "heads");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(
                outputDirectory,
                $"{SanitizeFileSegment(Path.GetFileNameWithoutExtension(SelectedHeadSkinPath))}-{HeadSizeOptions[SelectedHeadSizeIndex]}.png");
            previewImage.Save(outputPath);
            OpenInstanceTarget(
                LT("shell.tools.test.head.save_activity"),
                outputPath,
                LT("shell.tools.test.head.output_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.head.save_failure"), ex.Message);
        }

        return Task.CompletedTask;
    }

    private static RenderTargetBitmap GenerateHeadPreviewBitmap(string skinPath, int selectedHeadSizeIndex)
    {
        using var skinBitmap = new Bitmap(skinPath);
        var width = skinBitmap.PixelSize.Width;
        var height = skinBitmap.PixelSize.Height;
        if (width < 64 || height < 32 || width % 64 != 0)
        {
            throw new InvalidOperationException("皮肤尺寸无效，需满足宽度为 64 的整数倍且高度至少为 32 像素。");
        }

        var scale = Math.Max(1, width / 64);
        var headSize = selectedHeadSizeIndex switch
        {
            0 => 64,
            1 => 96,
            _ => 128
        };
        var headBitmap = new RenderTargetBitmap(new PixelSize(headSize, headSize));
        using var context = headBitmap.CreateDrawingContext();
        using (context.PushRenderOptions(new RenderOptions
               {
                   BitmapInterpolationMode = BitmapInterpolationMode.None
               }))
        {
            context.DrawImage(
                skinBitmap,
                new Rect(scale * 8, scale * 8, scale * 8, scale * 8),
                new Rect(0, 0, headSize, headSize));
            if (width >= 64 && height >= 32)
            {
                context.DrawImage(
                    skinBitmap,
                    new Rect(scale * 40, scale * 8, scale * 8, scale * 8),
                    new Rect(0, 0, headSize, headSize));
            }
        }

        return headBitmap;
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

    private void RefreshJavaSurface()
    {
        _ = RefreshJavaSurfaceAsync();
    }

    private async Task RefreshJavaSurfaceAsync()
    {
        AddActivity(
            LT("setup.java.activities.refresh"),
            LT("setup.java.activities.refresh_scanning"));

        try
        {
            await FrontendJavaInventoryService.RefreshPortableJavaScanCacheAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                RefreshLaunchState();
                AddActivity(
                    LT("setup.java.activities.refresh"),
                    LT("setup.java.activities.refresh_completed"));
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                AddFailureActivity(LT("setup.java.activities.refresh_failed"), ex.Message);
            });
        }
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

    private async Task AddJavaRuntimeAsync()
    {
        string? selectedPath;
        try
        {
            selectedPath = await _shellActionService.PickOpenFileAsync(
                LT("setup.java.activities.add_pick_title"),
                LT(OperatingSystem.IsWindows()
                    ? "setup.java.activities.add_pick_filter_windows"
                    : "setup.java.activities.add_pick_filter_unix"),
                OperatingSystem.IsWindows() ? "*.exe" : "java",
                OperatingSystem.IsWindows() ? "java.exe" : "java.exe",
                OperatingSystem.IsWindows() ? "javaw.exe" : "javaw");
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("setup.java.activities.add_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_cancelled"));
            return;
        }

        var installation = ParseJavaInstallation(selectedPath);
        if (installation is null)
        {
            AddFailureActivity(
                LT("setup.java.activities.add_failed"),
                LT("setup.java.activities.add_unrecognized", ("path", selectedPath)));
            return;
        }

        var javaPath = Path.GetFullPath(installation.JavaExePath);
        var items = LoadStoredJavaItems();
        if (items.Any(item => string.Equals(item.Path, javaPath, StringComparison.OrdinalIgnoreCase)))
        {
            AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_exists", ("path", javaPath)));
            SelectJavaRuntime(javaPath);
            ReloadSetupComposition();
            return;
        }

        items.Add(new JavaStorageItem
        {
            Path = javaPath,
            IsEnable = true,
            Source = JavaSource.ManualAdded
        });
        SaveStoredJavaItems(items);
        SelectJavaRuntime(javaPath);
        ReloadSetupComposition();
        AddActivity(LT("setup.java.activities.add"), LT("setup.java.activities.add_completed", ("path", javaPath)));
    }

    private JavaRuntimeEntryViewModel CreateJavaRuntimeEntry(
        string key,
        string title,
        string folder,
        IReadOnlyList<string> tags,
        bool isEnabled)
    {
        return new JavaRuntimeEntryViewModel(
            key,
            title,
            folder,
            tags,
            isEnabled,
            new ActionCommand(() => SelectJavaRuntime(key)),
            new ActionCommand(() => OpenJavaRuntimeFolder(title, folder)),
            new ActionCommand(() => OpenJavaRuntimeDetail(key, title, folder, tags)),
            new ActionCommand(() => ToggleJavaEnabled(key)),
            _i18n.T("setup.java.actions.enable"),
            _i18n.T("setup.java.actions.disable"));
    }

    private void OpenJavaRuntimeFolder(string title, string folder)
    {
        OpenInstanceTarget(
            LT("setup.java.activities.open_folder", ("title", title)),
            folder,
            LT("setup.java.activities.folder_missing"));
    }

    private void OpenJavaRuntimeDetail(string key, string title, string folder, IReadOnlyList<string> tags)
        => _ = OpenJavaRuntimeDetailAsync(key, title, folder, tags);

    private async Task OpenJavaRuntimeDetailAsync(string key, string title, string folder, IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key) || !File.Exists(key))
        {
            AddActivity(
                LT("setup.java.activities.view_detail", ("title", title)),
                LT("setup.java.activities.unavailable"));
            return;
        }

        var installation = ParseJavaInstallation(key);
        var sourceLabel = ResolveJavaSourceLabel(key);
        var detail = installation is null
            ? string.Join(Environment.NewLine,
            [
                LT("setup.java.details.path", ("value", key)),
                LT("setup.java.details.folder", ("value", folder)),
                LT("setup.java.details.source", ("value", sourceLabel)),
                LT("setup.java.details.tags", ("value", string.Join(" / ", tags))),
                LT("setup.java.details.default_java", ("value", _selectedJavaRuntimeKey == key ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                string.Empty,
                LT("setup.java.details.metadata_unavailable")
            ])
            : string.Join(Environment.NewLine,
            [
                LT("setup.java.details.type", ("value", installation.IsJre ? "JRE" : "JDK")),
                LT("setup.java.details.version", ("value", installation.Version)),
                LT("setup.java.details.major_version", ("value", installation.MajorVersion)),
                LT("setup.java.details.architecture", ("value", $"{installation.Architecture} ({(installation.Is64Bit ? "64 Bit" : "32 Bit")})")),
                LT("setup.java.details.brand", ("value", installation.Brand)),
                LT("setup.java.details.source", ("value", sourceLabel)),
                LT("setup.java.details.default_java", ("value", _selectedJavaRuntimeKey == key ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                LT("setup.java.details.enabled", ("value", JavaRuntimeEntries.FirstOrDefault(item => item.Key == key)?.IsEnabled == true ? LT("setup.java.details.yes") : LT("setup.java.details.no"))),
                LT("setup.java.details.availability", ("value", installation.IsStillAvailable ? LT("setup.java.details.available") : LT("setup.java.details.unavailable"))),
                LT("setup.java.details.executable", ("value", installation.JavaExePath)),
                LT("setup.java.details.folder", ("value", installation.JavaFolder))
            ]);
        var result = await ShowToolboxConfirmationAsync(LT("setup.java.activities.view_detail", ("title", title)), detail);
        if (result is null)
        {
            return;
        }

        AddActivity(
            LT("setup.java.activities.view_detail", ("title", title)),
            LT("setup.java.activities.detail_shown"));
    }

    private static JavaInstallation? ParseJavaInstallation(string javaExecutablePath)
    {
        var parsers = new List<IJavaParser>
        {
            new CommandJavaParser(SystemJavaRuntimeEnvironment.Current, new ProcessCommandRunner())
        };
        if (TryCreatePeHeaderParser() is { } peHeaderParser)
        {
            parsers.Add(peHeaderParser);
        }

        var parser = new CompositeJavaParser([.. parsers]);
        return parser.Parse(javaExecutablePath);
    }

    private static IJavaParser? TryCreatePeHeaderParser()
    {
        const string typeName = "PCL.Core.Minecraft.Java.Parser.PeHeaderParser";

        try
        {
            var parserType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(candidate => candidate.GetType(typeName, throwOnError: false))
                .FirstOrDefault(candidate => candidate is not null);

            if (parserType is null)
            {
                parserType = TryLoadAssembly("PCL.Core.Backend")?.GetType(typeName, throwOnError: false)
                             ?? TryLoadAssembly("PCL.Core.Foundation")?.GetType(typeName, throwOnError: false)
                             ?? TryLoadAssembly("PCL.Core")?.GetType(typeName, throwOnError: false);
            }

            if (parserType is null || !typeof(IJavaParser).IsAssignableFrom(parserType))
            {
                return null;
            }

            return Activator.CreateInstance(parserType) as IJavaParser;
        }
        catch
        {
            return null;
        }
    }

    private static Assembly? TryLoadAssembly(string assemblyName)
    {
        var candidateRoots = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in candidateRoots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            foreach (var directory in EnumerateParentDirectories(root))
            {
                var assemblyFileName = $"{assemblyName}.dll";
                var directPath = Path.Combine(directory, assemblyFileName);
                if (seenPaths.Add(directPath) && File.Exists(directPath))
                {
                    try
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(directPath);
                    }
                    catch
                    {
                    }
                }

                var binDirectory = Path.Combine(directory, assemblyName, "bin");
                if (!Directory.Exists(binDirectory))
                {
                    continue;
                }

                foreach (var buildPath in Directory.EnumerateFiles(binDirectory, assemblyFileName, SearchOption.AllDirectories)
                             .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
                             .Take(8))
                {
                    if (!seenPaths.Add(buildPath))
                    {
                        continue;
                    }

                    try
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(buildPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateParentDirectories(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        while (current is not null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

    private string ResolveJavaSourceLabel(string key)
    {
        var item = LoadStoredJavaItems().FirstOrDefault(candidate =>
            string.Equals(candidate.Path, key, StringComparison.OrdinalIgnoreCase));
        return item?.Source switch
        {
            JavaSource.AutoInstalled => LT("setup.java.tags.auto_installed"),
            JavaSource.ManualAdded => LT("setup.java.tags.manual_added"),
            _ => LT("setup.java.tags.auto_scanned")
        };
    }

    private void SelectJavaRuntime(string key)
    {
        _selectedJavaRuntimeKey = key;
        _shellActionService.PersistSharedValue("LaunchArgumentJavaSelect", key == "auto" ? string.Empty : key);
        SyncJavaSelection();
        _ = RefreshLaunchProfileCompositionAsync();
        RaisePropertyChanged(nameof(IsAutoJavaSelected));
        AddActivity(
            LT("setup.java.activities.select_default"),
            key == "auto" ? LT("setup.java.activities.auto_select") : key);
    }

    private void ToggleJavaEnabled(string key)
    {
        var entry = JavaRuntimeEntries.FirstOrDefault(item => item.Key == key);
        if (entry is null)
        {
            return;
        }

        if (_selectedJavaRuntimeKey == key && entry.IsEnabled)
        {
            AddActivity(
                LT("setup.java.activities.disable_blocked"),
                LT("setup.java.activities.disable_blocked_reason"));
            return;
        }

        entry.IsEnabled = !entry.IsEnabled;
        var items = LoadStoredJavaItems();
        var updated = items.FindIndex(item => string.Equals(item.Path, key, StringComparison.OrdinalIgnoreCase));
        if (updated >= 0)
        {
            items[updated] = new JavaStorageItem
            {
                Path = items[updated].Path,
                IsEnable = entry.IsEnabled,
                Source = items[updated].Source,
                Installation = items[updated].Installation
            };
        }
        else
        {
            items.Add(new JavaStorageItem
            {
                Path = key,
                IsEnable = entry.IsEnabled,
                Source = JavaSource.AutoScanned
            });
        }

        SaveStoredJavaItems(items);
        ReloadSetupComposition(initializeAllSurfaces: false);
        AddActivity(
            entry.IsEnabled ? LT("setup.java.activities.enable") : LT("setup.java.activities.disable"),
            LT(
                "setup.java.activities.toggle_result",
                ("title", entry.Title),
                ("state", entry.IsEnabled ? LT("setup.java.activities.enabled_state") : LT("setup.java.activities.disabled_state"))));
    }

    private void SyncJavaSelection()
    {
        foreach (var entry in JavaRuntimeEntries)
        {
            entry.IsSelected = _selectedJavaRuntimeKey == entry.Key;
        }
    }

    private List<JavaStorageItem> LoadStoredJavaItems()
    {
        try
        {
            var provider = _shellActionService.RuntimePaths.OpenLocalConfigProvider();
            var rawJson = provider.Exists("LaunchArgumentJavaUser")
                ? provider.Get<string>("LaunchArgumentJavaUser")
                : "[]";
            return FrontendJavaInventoryService.ParseStorageItems(rawJson).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void SaveStoredJavaItems(IReadOnlyList<JavaStorageItem> items)
    {
        _shellActionService.PersistLocalValue("LaunchArgumentJavaUser", JsonSerializer.Serialize(items));
    }

    private static string GetUniqueArchivePath(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return filePath;
        }

        var directory = Path.GetDirectoryName(filePath)!;
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        for (var suffix = 1; ; suffix++)
        {
            var candidate = Path.Combine(directory, $"{fileName}-{suffix}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
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

    private static readonly string[] BackgroundCleanupExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".bmp",
        ".mp4",
        ".webm",
        ".avi",
        ".mkv",
        ".mov"
    ];

    private static readonly string[] MusicMediaExtensions =
    [
        ".mp3",
        ".flac",
        ".wav",
        ".ogg",
        ".m4a",
        ".aac"
    ];

    private string GetBackgroundFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Pictures");
    }

    private string GetMusicFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Musics");
    }

    private string GetLogoImagePath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Logo.png");
    }

    private string GetHomepageTutorialPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.DataDirectory, "Custom.xaml");
    }

    private static IEnumerable<string> EnumerateMediaFiles(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var extensionSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);
        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => extensionSet.Contains(Path.GetExtension(path)));
    }

    private static int DeleteDirectoryContents(string folder, IEnumerable<string> allowedExtensions)
    {
        Directory.CreateDirectory(folder);
        var removedCount = 0;
        foreach (var file in EnumerateMediaFiles(folder, allowedExtensions))
        {
            try
            {
                File.Delete(file);
                removedCount++;
            }
            catch
            {
                // Ignore deletion failures for individual files and keep going.
            }
        }

        return removedCount;
    }

    private static string SanitizeFileSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalidCharacters.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "update" : cleaned;
    }

    private void OpenCustomDownloadFolder()
    {
        var folder = string.IsNullOrWhiteSpace(ToolDownloadFolder)
            ? Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "tool-downloads")
            : Path.GetFullPath(ToolDownloadFolder);
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity(LT("shell.tools.test.custom_download.open_folder_activity"), folder);
        }
        else
        {
            AddFailureActivity(LT("shell.tools.test.custom_download.open_folder_failure"), error ?? folder);
        }
    }

    private async Task SaveOfficialSkinAsync()
    {
        if (string.IsNullOrWhiteSpace(OfficialSkinPlayerName))
        {
            AddFailureActivity(
                LT("shell.tools.test.official_skin.save_failure"),
                LT("shell.tools.test.official_skin.missing_player_name"));
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var profileJson = await client.GetStringAsync($"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(OfficialSkinPlayerName.Trim())}");
            using var profileDocument = JsonDocument.Parse(profileJson);
            var uuid = profileDocument.RootElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(uuid))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.player_not_found"));
                return;
            }

            var sessionJson = await client.GetStringAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}");
            using var sessionDocument = JsonDocument.Parse(sessionJson);
            var texturePayload = sessionDocument.RootElement
                .GetProperty("properties")
                .EnumerateArray()
                .FirstOrDefault(item => item.TryGetProperty("name", out var nameElement)
                    && string.Equals(nameElement.GetString(), "textures", StringComparison.Ordinal));
            if (!texturePayload.TryGetProperty("value", out var valueElement))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.missing_skin_payload"));
                return;
            }

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(valueElement.GetString() ?? string.Empty));
            using var textureDocument = JsonDocument.Parse(decoded);
            var textureUrl = textureDocument.RootElement
                .GetProperty("textures")
                .GetProperty("SKIN")
                .GetProperty("url")
                .GetString();
            if (string.IsNullOrWhiteSpace(textureUrl))
            {
                AddFailureActivity(
                    LT("shell.tools.test.official_skin.save_failure"),
                    LT("shell.tools.test.official_skin.missing_skin_url"));
                return;
            }

            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "skins");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(OfficialSkinPlayerName.Trim())}.png");
            var bytes = await client.GetByteArrayAsync(textureUrl);
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget(
                LT("shell.tools.test.official_skin.save_activity"),
                outputPath,
                LT("shell.tools.test.official_skin.output_missing"));
        }
        catch (HttpRequestException ex)
        {
            AddFailureActivity(LT("shell.tools.test.official_skin.save_failure"), ex.Message);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.official_skin.save_failure"), ex.Message);
        }
    }

    private async Task SaveAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddFailureActivity(
                LT("shell.tools.test.achievement.save_failure"),
                LT("shell.tools.test.achievement.invalid_content"));
            return;
        }

        try
        {
            using var client = CreateToolHttpClient();
            var bytes = await client.GetByteArrayAsync(url);
            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "achievements");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(AchievementTitle)}.png");
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget(
                LT("shell.tools.test.achievement.save_activity"),
                outputPath,
                LT("shell.tools.test.achievement.output_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.tools.test.achievement.save_failure"), ex.Message);
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockId.Trim();
        var title = AchievementTitle.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var firstLine = AchievementFirstLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        var secondLine = AchievementSecondLine.Trim().Replace(" ", "..", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(block) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var url = $"https://minecraft-api.com/api/achivements/{Uri.EscapeDataString(block)}/{Uri.EscapeDataString(title)}/{Uri.EscapeDataString(firstLine)}";
        if (!string.IsNullOrWhiteSpace(secondLine))
        {
            url += $"/{Uri.EscapeDataString(secondLine)}";
        }

        return url;
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
