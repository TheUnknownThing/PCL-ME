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
        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadInstall) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            ResetDownloadInstallSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.DownloadResource) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            ResetDownloadResourceFilters();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupLaunch) && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetLaunchSettingsSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUpdate) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            _ = CheckForLauncherUpdatesAsync(forceRefresh: true);
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupFeedback) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            _ = RefreshFeedbackSectionsAsync(forceRefresh: true);
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupGameManage) && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetGameManageSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupLauncherMisc) && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetLauncherMiscSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupJava) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            RefreshJavaSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.SetupUi) && string.Equals(actionLabel, "重置", StringComparison.Ordinal))
        {
            ResetUiSurface();
            return;
        }

        if (IsCurrentStandardRightPane(StandardShellRightPaneKind.ToolsTest) && string.Equals(actionLabel, "刷新", StringComparison.Ordinal))
        {
            RefreshToolsTestSurface();
            return;
        }

        AddActivity($"左侧操作: {actionLabel}", $"{title} • {command}");
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
        _updateStatus = FrontendSetupUpdateStatusService.CreateChecking();
        RaiseUpdateSurfaceProperties();

        try
        {
            _updateStatus = await FrontendSetupUpdateStatusService.QueryAsync(SelectedUpdateChannelIndex);
            _lastUpdateCheckSignature = signature;
            RaiseUpdateSurfaceProperties();

            AddActivity(
                "刷新更新页",
                _updateStatus.SurfaceState switch
                {
                    UpdateSurfaceState.Available => $"检测到可用更新：{_updateStatus.AvailableUpdateName}",
                    UpdateSurfaceState.Latest => $"{_updateStatus.CurrentVersionName} 已是最新版本",
                    UpdateSurfaceState.Error => _updateStatus.CurrentVersionDescription,
                    _ => "正在检查更新..."
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
            AddActivity("下载并安装更新", "当前没有待下载的更新。");
            return;
        }

        var target = _updateStatus.AvailableUpdateDownloadUrl ?? _updateStatus.AvailableUpdateReleaseUrl;
        if (string.IsNullOrWhiteSpace(target))
        {
            AddActivity("下载并安装更新", "当前更新源没有提供可用下载地址。");
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
                AddFailureActivity("下载并安装更新失败", error ?? preparedInstall.InstallerScriptPath);
                return;
            }

            AddActivity("下载并安装更新", $"{_updateStatus.AvailableUpdateName} • 更新包已准备完成，启动器即将退出并应用更新。");
            AvaloniaHintBus.Show("更新包已准备完成，启动器即将关闭并自动安装。", AvaloniaHintTheme.Success);
            await Task.Delay(400);
            _shellActionService.ExitLauncher();
        }
        catch (Exception ex)
        {
            AddFailureActivity("下载并安装更新失败", ex.Message);
        }
    }

    private void ShowAvailableUpdateDetail() => _ = ShowAvailableUpdateDetailAsync();

    private async Task ShowAvailableUpdateDetailAsync()
    {
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateChangelog))
        {
            var result = await ShowToolboxConfirmationAsync(
                $"更新详情: {_updateStatus.AvailableUpdateName}",
                _updateStatus.AvailableUpdateChangelog);
            if (result is null)
            {
                return;
            }

            AddActivity("查看更新详情", _updateStatus.AvailableUpdateName);

            return;
        }

        string? openError = null;
        if (!string.IsNullOrWhiteSpace(_updateStatus.AvailableUpdateReleaseUrl)
            && _shellActionService.TryOpenExternalTarget(_updateStatus.AvailableUpdateReleaseUrl, out openError))
        {
            AddActivity("查看更新详情", _updateStatus.AvailableUpdateReleaseUrl);
            return;
        }

        AddFailureActivity("查看更新详情失败", openError ?? "当前没有可用的更新详情。");
    }

    private void ResetLaunchSettingsSurface()
    {
        _shellActionService.RemoveLocalValues(LaunchLocalResetKeys);
        _shellActionService.RemoveSharedValues(LaunchSharedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置启动设置", "启动选项、内存与高级启动参数已恢复到当前启动器的默认配置。");
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
        AddActivity("刷新测试工具页", "测试页表单与工具按钮已从当前启动器配置重新加载。");
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
                "管理收藏夹",
                $"当前收藏夹：{currentTargetName}",
                [
                    new PclChoiceDialogOption("share", "分享当前收藏夹", "复制当前收藏夹的分享代码。"),
                    new PclChoiceDialogOption("import", "导入收藏", "把分享代码导入到当前或新的收藏夹。"),
                    new PclChoiceDialogOption("create", "新建收藏夹", "创建一个新的收藏夹目标。"),
                    new PclChoiceDialogOption("rename", "重命名收藏夹名称", "修改当前收藏夹的名称。"),
                    new PclChoiceDialogOption("delete", "删除当前收藏夹", "删除当前收藏夹。")
                ],
                "share",
                "继续");
        }
        catch (Exception ex)
        {
            AddFailureActivity("管理收藏夹失败", ex.Message);
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
                AddFailureActivity("管理收藏夹失败", $"未识别的收藏夹操作：{actionId}");
                return;
        }
    }

    private Task CreateInstanceProfileAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("新建档案", "当前未选择实例。");
            return Task.CompletedTask;
        }

        ResetMicrosoftDeviceFlow();
        LaunchAuthlibServer = string.IsNullOrWhiteSpace(InstanceServerAuthServer)
            ? DefaultAuthlibServer
            : InstanceServerAuthServer.Trim();
        LaunchAuthlibLoginName = string.Empty;
        LaunchAuthlibPassword = string.Empty;
        LaunchAuthlibStatusText = string.IsNullOrWhiteSpace(InstanceServerAuthName)
            ? "已从实例设置带入第三方验证服务器。"
            : $"已从实例设置带入 {InstanceServerAuthName}。";
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            "Opened the launch route from instance settings for auth profile creation.");
        SetLaunchProfileSurface(LaunchProfileSurfaceKind.AuthlibEditor);
        AddActivity("新建档案", $"已跳转到启动页，为 {_instanceComposition.Selection.InstanceName} 创建第三方验证档案。");
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
            AddActivity("分享当前收藏夹", "分享了个寂寞啊！");
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(JsonSerializer.Serialize(favoriteIds));
            AddActivity("分享当前收藏夹", $"已复制 {GetDownloadFavoriteTargetName(target)} 的分享代码。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("分享当前收藏夹失败", ex.Message);
        }
    }

    private async Task ImportDownloadFavoriteTargetAsync(JsonArray root, JsonObject currentTarget)
    {
        string? shareCode;
        try
        {
            shareCode = await _shellActionService.PromptForTextAsync(
                "导入收藏",
                "输入分享的收藏。",
                string.Empty,
                "继续",
                "例如：[\"23333\"]");
        }
        catch (Exception ex)
        {
            AddFailureActivity("导入收藏失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(shareCode))
        {
            return;
        }

        var importedIds = ParseDownloadFavoriteShareCode(shareCode);
        if (importedIds.Count == 0)
        {
            AddActivity("导入收藏", "分享了个寂寞啊！");
            return;
        }

        string? destinationId;
        try
        {
            destinationId = await _shellActionService.PromptForChoiceAsync(
                "导入收藏",
                $"识别到 {importedIds.Count} 个收藏项目，要加入到哪里？",
                [
                    new PclChoiceDialogOption("new", "新的收藏夹", "新建一个收藏夹并导入这些收藏。"),
                    new PclChoiceDialogOption("current", "当前收藏夹", "把这些收藏加入当前收藏夹。")
                ],
                "current",
                "继续");
        }
        catch (Exception ex)
        {
            AddFailureActivity("导入收藏失败", ex.Message);
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
                    "新收藏夹名称",
                    "请输入新收藏夹名称");
            }
            catch (Exception ex)
            {
                AddFailureActivity("导入收藏失败", ex.Message);
                return;
            }

            newTargetName = newTargetName?.Trim();
            if (string.IsNullOrWhiteSpace(newTargetName))
            {
                return;
            }

            root.Add(CreateDownloadFavoriteTargetNode(newTargetName, importedIds));
            PersistDownloadFavoriteTargetRoot(root, root.OfType<JsonObject>().Count() - 1);
            AddActivity("导入收藏", $"已导入到新收藏夹：{newTargetName}");
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
        AddActivity("导入收藏", $"已导入到当前收藏夹：{GetDownloadFavoriteTargetName(currentTarget)}");
    }

    private async Task CreateDownloadFavoriteTargetAsync(JsonArray root)
    {
        string? newTargetName;
        try
        {
            newTargetName = await _shellActionService.PromptForTextAsync(
                "新建收藏夹",
                "请输入新收藏夹名称");
        }
        catch (Exception ex)
        {
            AddFailureActivity("新建收藏夹失败", ex.Message);
            return;
        }

        newTargetName = newTargetName?.Trim();
        if (string.IsNullOrWhiteSpace(newTargetName))
        {
            return;
        }

        root.Add(CreateDownloadFavoriteTargetNode(newTargetName));
        PersistDownloadFavoriteTargetRoot(root, root.OfType<JsonObject>().Count() - 1);
        AddActivity("新建收藏夹", newTargetName);
    }

    private async Task RenameDownloadFavoriteTargetAsync(JsonArray root, JsonObject target, int selectedIndex)
    {
        string? nextName;
        try
        {
            nextName = await _shellActionService.PromptForTextAsync(
                "输入新名称",
                "请输入收藏夹名称。",
                GetDownloadFavoriteTargetName(target));
        }
        catch (Exception ex)
        {
            AddFailureActivity("重命名收藏夹失败", ex.Message);
            return;
        }

        nextName = nextName?.Trim();
        if (string.IsNullOrWhiteSpace(nextName) || string.Equals(nextName, GetDownloadFavoriteTargetName(target), StringComparison.Ordinal))
        {
            return;
        }

        target["Name"] = nextName;
        PersistDownloadFavoriteTargetRoot(root, selectedIndex);
        AddActivity("重命名收藏夹名称", nextName);
    }

    private async Task DeleteDownloadFavoriteTargetAsync(JsonArray root, JsonObject target)
    {
        var targets = root.OfType<JsonObject>().ToArray();
        if (targets.Length <= 1)
        {
            AddActivity("删除当前收藏夹", "您不能删除最后一个收藏夹。");
            return;
        }

        var favoriteCount = EnsureCommunityProjectFavoriteArray(target)
            .Select(GetCommunityProjectFavoriteId)
            .OfType<string>()
            .Count();
        var content = $"确认删除 {GetDownloadFavoriteTargetName(target)} 收藏夹？{Environment.NewLine}{Environment.NewLine}" +
                      $"此收藏夹有 {favoriteCount} 个收藏项目{Environment.NewLine}" +
                      $"收藏夹 ID 为 {GetDownloadFavoriteTargetId(target)}{Environment.NewLine}" +
                      "此操作不可逆！";
        bool confirmed;
        try
        {
            confirmed = await _shellActionService.ConfirmAsync("删除确认", content, "删除", isDanger: true);
        }
        catch (Exception ex)
        {
            AddFailureActivity("删除当前收藏夹失败", ex.Message);
            return;
        }

        if (!confirmed)
        {
            return;
        }

        root.Remove(target);
        PersistDownloadFavoriteTargetRoot(root, 0);
        AddActivity("删除当前收藏夹", $"已删除 {GetDownloadFavoriteTargetName(target)}。");
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

    private static string GetDownloadFavoriteTargetName(JsonObject target)
    {
        return target["Name"]?.GetValue<string>()?.Trim() switch
        {
            { Length: > 0 } value => value,
            _ => "默认收藏夹"
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
            selectedFolder = await _shellActionService.PickFolderAsync("选择下载目录");
        }
        catch (Exception ex)
        {
            AddFailureActivity("选择下载目录失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            AddActivity("选择下载目录", "已取消选择下载目录。");
            return;
        }

        ToolDownloadFolder = selectedFolder;
        AddActivity("选择下载目录", ToolDownloadFolder);
    }

    private Task StartCustomDownloadAsync()
    {
        if (!Uri.TryCreate(ToolDownloadUrl, UriKind.Absolute, out var uri))
        {
            AddFailureActivity("开始下载自定义文件失败", "下载地址无效。");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(ToolDownloadFolder))
        {
            AddFailureActivity("开始下载自定义文件失败", "请先选择保存目录。");
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

            AddActivity("开始下载自定义文件", $"{uri} -> {targetPath}（可在任务中心查看进度）");
        }
        catch (Exception ex)
        {
            AddFailureActivity("开始下载自定义文件失败", ex.Message);
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
            AddActivity("导出日志", "当前日志目录不存在，暂无可导出的日志。");
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length == 0)
        {
            AddActivity("导出日志", "日志目录为空，暂无可导出的日志。");
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

        AddActivity(includeAllLogs ? "导出全部日志" : "导出日志", archivePath);
    }

    private void OpenLauncherLogDirectory()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        Directory.CreateDirectory(logDirectory);
        if (_shellActionService.TryOpenExternalTarget(logDirectory, out var error))
        {
            AddActivity("打开日志目录", logDirectory);
        }
        else
        {
            AddFailureActivity("打开日志目录失败", error ?? logDirectory);
        }
    }

    private void CleanLauncherLogs()
    {
        var logDirectory = Path.Combine(_shellActionService.RuntimePaths.LauncherAppDataDirectory, "Log");
        if (!Directory.Exists(logDirectory))
        {
            AddActivity("清理历史日志", "当前日志目录不存在，无需清理。");
            return;
        }

        var logFiles = Directory.EnumerateFiles(logDirectory, "*", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        if (logFiles.Length <= 1)
        {
            AddActivity("清理历史日志", "没有可清理的历史日志，已保留当前日志文件。");
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
        AddActivity("清理历史日志", removedCount == 0 ? "未能删除任何历史日志文件。" : $"已清理 {removedCount} 个历史日志文件。");
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

        AddActivity("导出设置", exportDirectory);
    }

    private async Task ImportSettingsAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync("选择配置文件", "PCL 配置文件", "*.json");
        }
        catch (Exception ex)
        {
            AddFailureActivity("导入设置失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("导入设置", "已取消选择配置文件。");
            return;
        }

        Directory.CreateDirectory(_shellActionService.RuntimePaths.SharedConfigDirectory);
        File.Copy(sourcePath, _shellActionService.RuntimePaths.SharedConfigPath, true);
        ReloadSetupComposition();
        AddActivity("导入设置", $"已导入共享配置：{sourcePath}。部分系统项仍建议重启后再验证。");
    }

    private void ApplyProxySettings()
    {
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxy", HttpProxyAddress);
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomUsername", HttpProxyUsername);
        _shellActionService.PersistProtectedSharedValue("SystemHttpProxyCustomPassword", HttpProxyPassword);
        ReloadSetupComposition();
        AddActivity("应用代理信息", string.IsNullOrWhiteSpace(HttpProxyAddress) ? "已清空自定义 HTTP 代理。" : HttpProxyAddress);
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
            SetProxyTestFeedback("自定义代理地址无效。", isSuccess: false);
            AddFailureActivity("测试代理连接失败", "自定义代理地址无效。");
            return;
        }

        _isTestingProxyConnection = true;
        _testProxyConnectionCommand.NotifyCanExecuteChanged();
        AddActivity("测试代理连接", $"正在通过{DescribeProxyMode(configuration)}访问 {FrontendHttpProxyService.ProxyConnectivityProbeUri.Host}。");

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

            AddActivity(
                "测试代理连接",
                $"{DescribeProxyMode(configuration)}连接成功，HTTP {(int)response.StatusCode} {response.ReasonPhrase}。");
            SetProxyTestFeedback(
                $"{DescribeProxyMode(configuration)}连接成功，HTTP {(int)response.StatusCode} {response.ReasonPhrase}。",
                isSuccess: true);
            AvaloniaHintBus.Show("代理连接测试成功。", AvaloniaHintTheme.Success);
        }
        catch (Exception ex)
        {
            SetProxyTestFeedback(ex.Message, isSuccess: false);
            AddFailureActivity("测试代理连接失败", ex.Message);
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

    private static string DescribeProxyMode(FrontendResolvedProxyConfiguration configuration)
    {
        return configuration.Mode switch
        {
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.CustomProxy => configuration.CustomProxyAddress?.ToString() ?? "自定义代理",
            PCL.Core.IO.Net.Http.Proxying.ProxyMode.SystemProxy => "系统代理",
            _ => "直连"
        };
    }

    private void OpenBackgroundFolder()
    {
        var folder = Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Pictures");
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity("打开背景文件夹", folder);
        }
        else
        {
            AddFailureActivity("打开背景文件夹失败", error ?? folder);
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
        AddActivity("清空背景内容", removedCount == 0 ? "背景目录中没有可删除的背景内容。" : $"已清空 {removedCount} 个背景内容文件。");
    }

    private void OpenMusicFolder()
    {
        var folder = Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Musics");
        Directory.CreateDirectory(folder);
        if (_shellActionService.TryOpenExternalTarget(folder, out var error))
        {
            AddActivity("打开音乐文件夹", folder);
        }
        else
        {
            AddFailureActivity("打开音乐文件夹失败", error ?? folder);
        }
    }

    private void RefreshMusicAssets()
    {
        var assets = EnumerateMediaFiles(GetMusicFolderPath(), MusicMediaExtensions).ToArray();
        AddActivity(
            "刷新背景音乐",
            assets.Length == 0
                ? "未检测到可用背景音乐。"
                : $"已重新扫描背景音乐目录，共找到 {assets.Length} 个文件。");
    }

    private void ClearMusicAssets()
    {
        var folder = GetMusicFolderPath();
        var removedCount = DeleteDirectoryContents(folder, MusicMediaExtensions);
        AddActivity("清空背景音乐", removedCount == 0 ? "音乐目录中没有可删除的背景音乐文件。" : $"已清空 {removedCount} 个背景音乐文件。");
    }

    private async Task ChangeLogoImageAsync()
    {
        string? sourcePath;

        try
        {
            sourcePath = await _shellActionService.PickOpenFileAsync(
                "选择标题栏图片",
                "常用图片文件",
                "*.png",
                "*.jpg",
                "*.jpeg",
                "*.gif",
                "*.webp");
        }
        catch (Exception ex)
        {
            AddFailureActivity("更改标题栏图片失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("更改标题栏图片", "已取消选择标题栏图片。");
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
        AddActivity("更改标题栏图片", $"{sourcePath} -> {targetPath}");
    }

    private void DeleteLogoImage()
    {
        var targetPath = GetLogoImagePath();
        if (!File.Exists(targetPath))
        {
            AddActivity("清空标题栏图片", "当前没有自定义标题栏图片。");
            return;
        }

        File.Delete(targetPath);
        if (SelectedLogoTypeIndex == 3)
        {
            SelectedLogoTypeIndex = 1;
        }

        RefreshTitleBarLogoImage();
        AddActivity("清空标题栏图片", targetPath);
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
            AddFailureActivity("生成教学文件失败", $"未找到主页教学模板：{sourcePath}");
            return;
        }

        var targetPath = GetHomepageTutorialPath();
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, true);
        AddActivity("生成教学文件", targetPath);
    }

    private void ViewHomepageTutorial() => _ = ViewHomepageTutorialAsync();

    private async Task ViewHomepageTutorialAsync()
    {
        const string tutorialText = """
1. 点击“生成教学文件”，会在运行目录下生成 PCL/Custom.xaml。
2. 使用文本编辑器修改这个文件，保存后可再次点击“刷新主页”。
3. 若使用联网主页，请确认下载地址指向可信的主页内容。
""";
        var result = await ShowToolboxConfirmationAsync("查看主页教程", tutorialText);
        if (result is null)
        {
            return;
        }

        AddActivity("查看主页教程", "已显示主页自定义教程。");
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
                "选择皮肤文件",
                "图像文件",
                "*.png",
                "*.jpg",
                "*.jpeg");
        }
        catch (Exception ex)
        {
            AddFailureActivity("选择皮肤失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            AddActivity("选择皮肤", "已取消选择皮肤文件。");
            return;
        }

        SelectedHeadSkinPath = sourcePath;
        AddActivity("选择皮肤", SelectedHeadSkinPath);
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
            AddActivity("保存头像", "请先选择一个可用的皮肤文件。");
            return Task.CompletedTask;
        }

        if (HeadPreviewImage is null)
        {
            RefreshHeadPreviewFromSelection(addActivity: false);
        }

        if (HeadPreviewImage is null)
        {
            AddFailureActivity("保存头像失败", "无法从当前皮肤生成头像预览，请尝试选择标准皮肤文件。");
            return Task.CompletedTask;
        }

        try
        {
            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "heads");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(
                outputDirectory,
                $"{SanitizeFileSegment(Path.GetFileNameWithoutExtension(SelectedHeadSkinPath))}-{HeadSizeOptions[SelectedHeadSizeIndex]}.png");
            HeadPreviewImage.Save(outputPath);
            OpenInstanceTarget("保存头像", outputPath, "导出的头像文件不存在。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("保存头像失败", ex.Message);
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
        AddActivity("刷新自动安装页", "自动安装选项已重新载入。");
    }

    private void ResetGameManageSurface()
    {
        _shellActionService.RemoveSharedValues(GameManageResetKeys);
        ReloadSetupComposition();
        AddActivity("重置游戏管理设置", "下载、社区资源和辅助功能设置已恢复到当前启动器的默认配置。");
    }

    private void ResetLauncherMiscSurface()
    {
        _shellActionService.RemoveLocalValues(LauncherMiscLocalResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscSharedResetKeys);
        _shellActionService.RemoveSharedValues(LauncherMiscProtectedResetKeys);
        ReloadSetupComposition();
        AddActivity("重置启动器杂项设置", "系统、网络和调试选项已恢复到当前启动器的默认配置。");
    }

    private void RefreshJavaSurface()
    {
        _ = RefreshJavaSurfaceAsync();
    }

    private async Task RefreshJavaSurfaceAsync()
    {
        AddActivity("刷新 Java 列表", "正在重新扫描 Java 运行时。");

        try
        {
            await FrontendJavaInventoryService.RefreshPortableJavaScanCacheAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                RefreshLaunchState();
                AddActivity("刷新 Java 列表", "Java 列表已按当前扫描结果重新载入。");
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ReloadSetupComposition(initializeAllSurfaces: false);
                AddFailureActivity("刷新 Java 列表失败", ex.Message);
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
            var snapshot = await FrontendSetupFeedbackService.QueryAsync();
            _feedbackSnapshot = snapshot;
            _lastFeedbackRefreshUtc = snapshot.FetchedAtUtc;
            ApplyFeedbackSnapshot(snapshot);
            RaisePropertyChanged(nameof(HasFeedbackSections));
            AddActivity("刷新反馈页", $"已同步 {snapshot.Sections.Sum(section => section.Entries.Count)} 条 GitHub 反馈。");
        }
        catch (Exception ex)
        {
            if (_feedbackSnapshot is null)
            {
                ReplaceItems(FeedbackSections,
                [
                    CreateFeedbackSection("加载失败", true,
                    [
                        CreateSimpleEntry("无法获取反馈列表", ex.Message)
                    ])
                ]);
                RaisePropertyChanged(nameof(HasFeedbackSections));
            }

            AddFailureActivity("刷新反馈页失败", ex.Message);
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
                "选择 Java 程序",
                OperatingSystem.IsWindows() ? "Java 程序" : "Java 可执行文件",
                OperatingSystem.IsWindows() ? "*.exe" : "java",
                OperatingSystem.IsWindows() ? "java.exe" : "java.exe",
                OperatingSystem.IsWindows() ? "javaw.exe" : "javaw");
        }
        catch (Exception ex)
        {
            AddFailureActivity("添加 Java 失败", ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AddActivity("添加 Java", "已取消选择 Java 程序。");
            return;
        }

        var installation = ParseJavaInstallation(selectedPath);
        if (installation is null)
        {
            AddFailureActivity("添加 Java 失败", $"无法识别所选文件为可用的 Java：{selectedPath}");
            return;
        }

        var javaPath = Path.GetFullPath(installation.JavaExePath);
        var items = LoadStoredJavaItems();
        if (items.Any(item => string.Equals(item.Path, javaPath, StringComparison.OrdinalIgnoreCase)))
        {
            AddActivity("添加 Java", $"Java 已存在于配置列表中：{javaPath}");
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
        AddActivity("添加 Java", $"已添加并选中 {javaPath}");
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
            new ActionCommand(() => ToggleJavaEnabled(key)));
    }

    private void OpenJavaRuntimeFolder(string title, string folder)
    {
        OpenInstanceTarget($"打开 Java 文件夹: {title}", folder, "当前 Java 目录不存在。");
    }

    private void OpenJavaRuntimeDetail(string key, string title, string folder, IReadOnlyList<string> tags)
        => _ = OpenJavaRuntimeDetailAsync(key, title, folder, tags);

    private async Task OpenJavaRuntimeDetailAsync(string key, string title, string folder, IReadOnlyList<string> tags)
    {
        if (string.IsNullOrWhiteSpace(key) || !File.Exists(key))
        {
            AddActivity($"查看 Java 详情: {title}", "此 Java 不可用，请刷新列表。");
            return;
        }

        var installation = ParseJavaInstallation(key);
        var sourceLabel = ResolveJavaSourceLabel(key);
        var detail = installation is null
            ? string.Join(Environment.NewLine,
            [
                $"路径: {key}",
                $"目录: {folder}",
                $"来源: {sourceLabel}",
                $"当前标签: {string.Join(" / ", tags)}",
                $"默认 Java: {(_selectedJavaRuntimeKey == key ? "是" : "否")}",
                string.Empty,
                "无法进一步解析该 Java 的详细元数据，请确认文件仍然可执行。"
            ])
            : string.Join(Environment.NewLine,
            [
                $"类型: {(installation.IsJre ? "JRE" : "JDK")}",
                $"版本: {installation.Version}",
                $"主版本: {installation.MajorVersion}",
                $"架构: {installation.Architecture} ({(installation.Is64Bit ? "64 Bit" : "32 Bit")})",
                $"品牌: {installation.Brand}",
                $"来源: {sourceLabel}",
                $"默认 Java: {(_selectedJavaRuntimeKey == key ? "是" : "否")}",
                $"已启用: {(JavaRuntimeEntries.FirstOrDefault(item => item.Key == key)?.IsEnabled == true ? "是" : "否")}",
                $"可用性: {(installation.IsStillAvailable ? "可用" : "不可用")}",
                $"可执行文件: {installation.JavaExePath}",
                $"目录: {installation.JavaFolder}"
            ]);
        var result = await ShowToolboxConfirmationAsync($"查看 Java 详情: {title}", detail);
        if (result is null)
        {
            return;
        }

        AddActivity($"查看 Java 详情: {title}", "已显示 Java 运行时详情。");
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
            JavaSource.AutoInstalled => "自动安装",
            JavaSource.ManualAdded => "手动添加",
            _ => "自动扫描"
        };
    }

    private void SelectJavaRuntime(string key)
    {
        _selectedJavaRuntimeKey = key;
        _shellActionService.PersistSharedValue("LaunchArgumentJavaSelect", key == "auto" ? string.Empty : key);
        SyncJavaSelection();
        _ = RefreshLaunchProfileCompositionAsync();
        RaisePropertyChanged(nameof(IsAutoJavaSelected));
        AddActivity("切换默认 Java", key == "auto" ? "自动选择" : key);
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
            AddActivity("Java 禁用被阻止", "请先取消默认选择后再禁用当前 Java。");
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
                Source = items[updated].Source
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
            entry.IsEnabled ? "启用 Java" : "禁用 Java",
            $"{entry.Title} • {(entry.IsEnabled ? "已启用" : "已禁用")}");
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
        AddActivity("重置界面设置", "个性化界面页已恢复到当前启动器的默认配置。");
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
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Pictures");
    }

    private string GetMusicFolderPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Musics");
    }

    private string GetLogoImagePath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Logo.png");
    }

    private string GetHomepageTutorialPath()
    {
        return Path.Combine(_shellActionService.RuntimePaths.ExecutableDirectory, "PCL", "Custom.xaml");
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
            AddActivity("打开下载文件夹", folder);
        }
        else
        {
            AddFailureActivity("打开下载文件夹失败", error ?? folder);
        }
    }

    private async Task SaveOfficialSkinAsync()
    {
        if (string.IsNullOrWhiteSpace(OfficialSkinPlayerName))
        {
            AddFailureActivity("保存正版皮肤失败", "请先填写正版玩家名。");
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
                AddFailureActivity("保存正版皮肤失败", "未找到对应的正版玩家。");
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
                AddFailureActivity("保存正版皮肤失败", "正版档案中未包含皮肤信息。");
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
                AddFailureActivity("保存正版皮肤失败", "正版档案中未包含皮肤下载地址。");
                return;
            }

            var outputDirectory = Path.Combine(_shellActionService.RuntimePaths.FrontendArtifactDirectory, "skins");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"{SanitizeFileSegment(OfficialSkinPlayerName.Trim())}.png");
            var bytes = await client.GetByteArrayAsync(textureUrl);
            await File.WriteAllBytesAsync(outputPath, bytes);
            OpenInstanceTarget("保存正版皮肤", outputPath, "导出的皮肤文件不存在。");
        }
        catch (HttpRequestException ex)
        {
            AddFailureActivity("保存正版皮肤失败", ex.Message);
        }
        catch (Exception ex)
        {
            AddFailureActivity("保存正版皮肤失败", ex.Message);
        }
    }

    private async Task SaveAchievementAsync()
    {
        var url = GetAchievementUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            AddFailureActivity("保存成就图片失败", "请先填写有效的成就内容。");
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
            OpenInstanceTarget("保存成就图片", outputPath, "导出的成就图片不存在。");
        }
        catch (Exception ex)
        {
            AddFailureActivity("保存成就图片失败", ex.Message);
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
