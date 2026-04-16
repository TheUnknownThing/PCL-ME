using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Media.Imaging;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceOverviewName = string.Empty;
    private string _instanceOverviewSubtitle = string.Empty;
    private Bitmap? _instanceOverviewSelectedIcon;
    private int _selectedInstanceOverviewIconIndex;
    private int _selectedInstanceOverviewCategoryIndex;
    private bool _isInstanceOverviewStarred = true;
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewIconOptions =
    [
        new DownloadResourceFilterOptionViewModel("auto", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("cobblestone", "CobbleStone.png"),
        new DownloadResourceFilterOptionViewModel("command_block", "CommandBlock.png"),
        new DownloadResourceFilterOptionViewModel("gold_block", "GoldBlock.png"),
        new DownloadResourceFilterOptionViewModel("grass_block", "Grass.png"),
        new DownloadResourceFilterOptionViewModel("grass_path", "GrassPath.png"),
        new DownloadResourceFilterOptionViewModel("anvil", "Anvil.png"),
        new DownloadResourceFilterOptionViewModel("redstone_block", "RedstoneBlock.png"),
        new DownloadResourceFilterOptionViewModel("redstone_lamp_on", "RedstoneLampOn.png"),
        new DownloadResourceFilterOptionViewModel("redstone_lamp_off", "RedstoneLampOff.png"),
        new DownloadResourceFilterOptionViewModel("egg", "Egg.png"),
        new DownloadResourceFilterOptionViewModel("fabric", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("quilt", "Quilt.png"),
        new DownloadResourceFilterOptionViewModel("neoforge", "NeoForge.png"),
        new DownloadResourceFilterOptionViewModel("cleanroom", "Cleanroom.png")
    ];
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewCategoryOptions =
    [
        new DownloadResourceFilterOptionViewModel("auto", "auto"),
        new DownloadResourceFilterOptionViewModel("hidden", "hidden"),
        new DownloadResourceFilterOptionViewModel("modable_long", "modable_long"),
        new DownloadResourceFilterOptionViewModel("regular", "regular"),
        new DownloadResourceFilterOptionViewModel("rare", "rare"),
        new DownloadResourceFilterOptionViewModel("april_fools", "april_fools")
    ];

    public ObservableCollection<string> InstanceOverviewDisplayTags { get; } = [];

    public ObservableCollection<KeyValueEntryViewModel> InstanceOverviewInfoEntries { get; } = [];

    public string InstanceOverviewName
    {
        get => _instanceOverviewName;
        private set => SetProperty(ref _instanceOverviewName, value);
    }

    public string InstanceOverviewSubtitle
    {
        get => _instanceOverviewSubtitle;
        private set => SetProperty(ref _instanceOverviewSubtitle, value);
    }

    public Bitmap? InstanceOverviewSelectedIcon
    {
        get => _instanceOverviewSelectedIcon;
        private set => SetProperty(ref _instanceOverviewSelectedIcon, value);
    }

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewIconOptions => _instanceOverviewIconOptions
        .Select(option => new DownloadResourceFilterOptionViewModel(
            TranslateOverviewIconOption(option.Label),
            option.FilterValue))
        .ToArray();

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewCategoryOptions => _instanceOverviewCategoryOptions
        .Select(option => new DownloadResourceFilterOptionViewModel(
            TranslateOverviewCategoryOption(option.Label),
            option.FilterValue))
        .ToArray();

    public int SelectedInstanceOverviewIconIndex
    {
        get => _selectedInstanceOverviewIconIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, InstanceOverviewIconOptions.Count, out var nextValue))
            {
                return;
            }

            if (SetProperty(ref _selectedInstanceOverviewIconIndex, nextValue))
            {
                RefreshInstanceOverviewSelectionState();
            }
        }
    }

    public int SelectedInstanceOverviewCategoryIndex
    {
        get => _selectedInstanceOverviewCategoryIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, InstanceOverviewCategoryOptions.Count, out var nextValue))
            {
                return;
            }

            if (SetProperty(ref _selectedInstanceOverviewCategoryIndex, nextValue))
            {
                RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
            }
        }
    }

    public string InstanceOverviewCategoryLabel => InstanceOverviewCategoryOptions[SelectedInstanceOverviewCategoryIndex].Label;

    public string InstanceOverviewFavoriteButtonText => _isInstanceOverviewStarred
        ? SD("instance.overview.actions.remove_favorite")
        : SD("instance.overview.actions.add_favorite");

    public bool HasInstanceOverviewInfoEntries => InstanceOverviewInfoEntries.Count > 0;

    public ActionCommand RenameInstanceCommand => new(() => _ = RenameInstanceAsync());

    public ActionCommand EditInstanceDescriptionCommand => new(() => _ = EditInstanceDescriptionAsync());

    public ActionCommand ToggleInstanceFavoriteCommand => new(ToggleInstanceFavorite);

    public ActionCommand OpenInstanceFolderCommand => new(() =>
        OpenInstanceTarget(
            SD("instance.overview.shortcuts.instance_folder"),
            _instanceComposition.Selection.InstanceDirectory,
            SD("instance.overview.messages.no_instance_selected")));

    public ActionCommand OpenInstanceSavesFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            SD("instance.overview.shortcuts.saves_folder"),
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            SD("instance.overview.messages.no_saves_directory")));

    public ActionCommand OpenInstanceModsFolderCommand => new(() =>
        OpenInstanceDirectoryTarget(
            SD("instance.overview.shortcuts.mods_folder"),
            ResolveCurrentInstanceResourceDirectory("mods"),
            SD("instance.overview.messages.no_mods_directory")));

    public ActionCommand ExportInstanceScriptCommand => new(ExportInstanceScript);

    public ActionCommand TestInstanceCommand => new(() => _ = TestInstanceAsync());

    public ActionCommand CheckInstanceFilesCommand => new(() => _ = CheckInstanceFilesAsync());

    public ActionCommand RestoreInstanceCommand => new(() => _ = ResetInstanceAsync());

    public ActionCommand DeleteInstanceCommand => new(() => _ = DeleteInstanceAsync());

    public ActionCommand PatchInstanceCoreCommand => new(() => _ = PatchInstanceCoreAsync());

    private void InitializeInstanceOverviewSurface()
    {
        var overview = _instanceComposition.Overview;
        InstanceOverviewName = overview.Name;
        InstanceOverviewSubtitle = LocalizeRawInstanceSubtitle(overview.Subtitle);
        _selectedInstanceOverviewIconIndex = Math.Clamp(overview.IconIndex, 0, InstanceOverviewIconOptions.Count - 1);
        _selectedInstanceOverviewCategoryIndex = Math.Clamp(overview.CategoryIndex, 0, InstanceOverviewCategoryOptions.Count - 1);
        _isInstanceOverviewStarred = overview.IsStarred;

        ReplaceItems(InstanceOverviewDisplayTags, overview.DisplayTags.Select(LocalizeOverviewTag).ToArray());
        ReplaceItems(
            InstanceOverviewInfoEntries,
            overview.InfoEntries.Select(entry => new KeyValueEntryViewModel(
                LocalizeOverviewInfoLabel(entry.Label),
                LocalizeOverviewInfoValue(entry.Label, entry.Value))));

        InstanceOverviewSelectedIcon = LoadInstanceBitmap(
            overview.IconPath,
            "Images",
            "Blocks",
            InstanceOverviewIconOptions[_selectedInstanceOverviewIconIndex].FilterValue);
    }

    private void RefreshInstanceOverviewSurface()
    {
        if (!IsCurrentStandardRightPane(StandardShellRightPaneKind.InstanceOverview))
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceOverviewName));
        RaisePropertyChanged(nameof(InstanceOverviewSubtitle));
        RaisePropertyChanged(nameof(InstanceOverviewSelectedIcon));
        RaisePropertyChanged(nameof(InstanceOverviewIconOptions));
        RaisePropertyChanged(nameof(InstanceOverviewCategoryOptions));
        RaisePropertyChanged(nameof(SelectedInstanceOverviewIconIndex));
        RaisePropertyChanged(nameof(SelectedInstanceOverviewCategoryIndex));
        RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
        RaisePropertyChanged(nameof(InstanceOverviewFavoriteButtonText));
        RaisePropertyChanged(nameof(HasInstanceOverviewInfoEntries));
    }

    private void RefreshInstanceOverviewSelectionState()
    {
        var overview = _instanceComposition.Overview;
        if (SelectedInstanceOverviewIconIndex == overview.IconIndex)
        {
            InstanceOverviewSelectedIcon = LoadInstanceBitmap(
                overview.IconPath,
                "Images",
                "Blocks",
                InstanceOverviewIconOptions[SelectedInstanceOverviewIconIndex].FilterValue);
        }
        else if (SelectedInstanceOverviewIconIndex == 0)
        {
            InstanceOverviewSelectedIcon = LoadInstanceBitmap(
                overview.IconPath,
                "Images",
                "Blocks",
                "Grass.png");
        }
        else
        {
            var iconName = InstanceOverviewIconOptions[SelectedInstanceOverviewIconIndex].FilterValue;
            InstanceOverviewSelectedIcon = LoadLauncherBitmap("Images", "Blocks", iconName);
        }

        RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
    }

    private string TranslateOverviewIconOption(string label)
    {
        return label switch
        {
            "auto" => SD("instance.overview.icons.auto"),
            "cobblestone" => SD("instance.overview.icons.cobblestone"),
            "command_block" => SD("instance.overview.icons.command_block"),
            "gold_block" => SD("instance.overview.icons.gold_block"),
            "grass_block" => SD("instance.overview.icons.grass_block"),
            "grass_path" => SD("instance.overview.icons.grass_path"),
            "anvil" => SD("instance.overview.icons.anvil"),
            "redstone_block" => SD("instance.overview.icons.redstone_block"),
            "redstone_lamp_on" => SD("instance.overview.icons.redstone_lamp_on"),
            "redstone_lamp_off" => SD("instance.overview.icons.redstone_lamp_off"),
            "egg" => SD("instance.overview.icons.egg"),
            "fabric" => SD("instance.overview.icons.fabric"),
            "quilt" => SD("instance.overview.icons.quilt"),
            "neoforge" => SD("instance.overview.icons.neoforge"),
            "cleanroom" => SD("instance.overview.icons.cleanroom"),
            _ => label
        };
    }

    private string TranslateOverviewCategoryOption(string label)
    {
        return label switch
        {
            "auto" => SD("instance.overview.categories.auto"),
            "hidden" => SD("instance.overview.categories.hidden"),
            "modable_long" => SD("instance.overview.categories.modable_long"),
            "regular" => SD("instance.overview.categories.regular"),
            "rare" => SD("instance.overview.categories.rare"),
            "april_fools" => SD("instance.overview.categories.april_fools"),
            _ => label
        };
    }

    private void ToggleInstanceFavorite()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(
                _isInstanceOverviewStarred
                    ? SD("instance.overview.actions.remove_favorite")
                    : SD("instance.overview.actions.add_favorite"),
                SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        _isInstanceOverviewStarred = !_isInstanceOverviewStarred;
        _shellActionService.PersistInstanceValue(_instanceComposition.Selection.InstanceDirectory, "IsStar", _isInstanceOverviewStarred);
        ReloadInstanceComposition();
        RaisePropertyChanged(nameof(InstanceOverviewFavoriteButtonText));
        AddActivity(
            _isInstanceOverviewStarred
                ? SD("instance.overview.actions.add_favorite")
                : SD("instance.overview.actions.remove_favorite"),
            _instanceComposition.Selection.InstanceName);
    }

    private async Task RenameInstanceAsync()
    {
        var activityTitle = SD("instance.overview.actions.rename");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        var oldName = _instanceComposition.Selection.InstanceName;
        var newName = await _shellActionService.PromptForTextAsync(
            SD("instance.overview.dialogs.rename.title"),
            SD("instance.overview.dialogs.rename.message"),
            oldName,
            SD("instance.overview.dialogs.rename.confirm"),
            SD("instance.overview.dialogs.rename.input_label"));
        if (newName is null)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.rename_canceled"));
            return;
        }

        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            AddActivity(activityTitle, SD("instance.overview.messages.rename_empty"));
            return;
        }

        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            AddActivity(activityTitle, SD("instance.overview.messages.rename_unchanged"));
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || newName.Contains(Path.DirectorySeparatorChar)
            || newName.Contains(Path.AltDirectorySeparatorChar))
        {
            AddActivity(activityTitle, SD("instance.overview.messages.rename_invalid"));
            return;
        }

        var launcherDirectory = _instanceComposition.Selection.LauncherDirectory;
        var oldDirectory = _instanceComposition.Selection.InstanceDirectory;
        var newDirectory = Path.Combine(launcherDirectory, "versions", newName);
        if (Directory.Exists(newDirectory) && !string.Equals(oldDirectory, newDirectory, StringComparison.OrdinalIgnoreCase))
        {
            AddActivity(activityTitle, SD("instance.overview.messages.rename_exists", ("name", newName)));
            return;
        }

        try
        {
            MoveDirectoryPreservingCase(oldDirectory, newDirectory);
            RenamePathPreservingCase(
                Path.Combine(newDirectory, $"{oldName}.json"),
                Path.Combine(newDirectory, $"{newName}.json"));
            RenamePathPreservingCase(
                Path.Combine(newDirectory, $"{oldName}.jar"),
                Path.Combine(newDirectory, $"{newName}.jar"));
            MoveDirectoryPreservingCase(
                Path.Combine(newDirectory, $"{oldName}-natives"),
                Path.Combine(newDirectory, $"{newName}-natives"),
                treatMissingSourceAsSuccess: true);
            ReplacePathText(Path.Combine(newDirectory, "PCL", "config.v1.yml"), oldDirectory, newDirectory);
            RewriteInstanceManifestId(Path.Combine(newDirectory, $"{newName}.json"), newName);
            _shellActionService.PersistLocalValue("LaunchInstanceSelect", newName);
            ReloadInstanceComposition();
            AddActivity(
                activityTitle,
                SD("instance.overview.messages.rename_completed", ("old_name", oldName), ("new_name", newName)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task EditInstanceDescriptionAsync()
    {
        var activityTitle = SD("instance.overview.actions.edit_description");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        var currentValue = _instanceComposition.Setup.CustomInfo;
        var nextValue = await _shellActionService.PromptForTextAsync(
            SD("instance.overview.dialogs.edit_description.title"),
            SD("instance.overview.dialogs.edit_description.message"),
            currentValue,
            SD("instance.overview.dialogs.edit_description.confirm"),
            SD("instance.overview.dialogs.edit_description.input_label"));
        if (nextValue is null)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.description_canceled"));
            return;
        }

        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        var trimmedValue = nextValue.Trim();
        try
        {
            if (string.IsNullOrWhiteSpace(trimmedValue))
            {
                _shellActionService.RemoveInstanceValues(instanceDirectory, ["VersionArgumentInfo", "CustomInfo"]);
            }
            else
            {
                _shellActionService.PersistInstanceValue(instanceDirectory, "VersionArgumentInfo", trimmedValue);
                _shellActionService.PersistInstanceValue(instanceDirectory, "CustomInfo", trimmedValue);
            }

            ReloadInstanceComposition();
            AddActivity(
                activityTitle,
                string.IsNullOrWhiteSpace(trimmedValue)
                    ? SD("instance.overview.messages.description_restored")
                    : trimmedValue);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private void ExportInstanceScript()
    {
        var activityTitle = SD("instance.overview.advanced.export_script");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        try
        {
            var extension = _shellActionService.GetCommandScriptExtension();
            var scriptDirectory = GetInstanceOverviewArtifactDirectory("launch-scripts");
            Directory.CreateDirectory(scriptDirectory);
            var scriptPath = Path.Combine(
                scriptDirectory,
                SD(
                    "instance.overview.artifacts.launch_script_name",
                    ("instance_name", SanitizeArtifactFileName(_instanceComposition.Selection.InstanceName))) + extension);
            var encoding = _launchComposition.SessionStartPlan.CustomCommandPlan.UseUtf8Encoding
                ? new UTF8Encoding(false)
                : Encoding.Default;
            File.WriteAllText(
                scriptPath,
                _launchComposition.SessionStartPlan.CustomCommandPlan.BatchScriptContent,
                encoding);
            _shellActionService.EnsureFileExecutable(scriptPath);
            OpenInstanceTarget(activityTitle, scriptPath, SD("instance.overview.messages.launch_script_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task TestInstanceAsync()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(SD("instance.overview.advanced.test_game"), SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            SD("instance.overview.messages.test_instance_navigated", ("instance_name", _instanceComposition.Selection.InstanceName)));
        await HandleLaunchRequestedAsync();
    }

    private async Task CheckInstanceFilesAsync()
    {
        var activityTitle = SD("instance.overview.advanced.repair_files");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        if (DisableInstanceFileValidation)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.repair_disable_validation_hint"));
            return;
        }

        try
        {
            var result = await RunInstanceRepairAsync(activityTitle, forceCoreRefresh: false);
            AddActivity(
                activityTitle,
                SD("instance.overview.messages.repair_completed", ("downloaded_count", result.DownloadedFiles.Count), ("reused_count", result.ReusedFiles.Count)));
        }
        catch (OperationCanceledException)
        {
            AddActivity(SD("instance.overview.messages.repair_canceled_title"), SD("instance.overview.messages.repair_canceled"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task ResetInstanceAsync()
    {
        var activityTitle = SD("instance.overview.advanced.reset");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            SD("instance.overview.dialogs.reset.title"),
            SD("instance.overview.dialogs.reset.message", ("instance_name", _instanceComposition.Selection.InstanceName)),
            SD("instance.overview.dialogs.reset.confirm"));
        if (!confirmed)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.reset_canceled"));
            return;
        }

        try
        {
            var backupDirectory = BackupInstanceCoreFiles("reset-backups");
            var result = await RunInstanceRepairAsync(activityTitle, forceCoreRefresh: true, backupDirectory);
            AddActivity(
                activityTitle,
                SD("instance.overview.messages.reset_completed", ("downloaded_count", result.DownloadedFiles.Count), ("reused_count", result.ReusedFiles.Count)));
        }
        catch (OperationCanceledException)
        {
            AddActivity(SD("instance.overview.messages.reset_canceled_title"), SD("instance.overview.messages.reset_canceled"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task DeleteInstanceAsync()
    {
        var activityTitle = SD("instance.overview.advanced.delete");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        try
        {
            var outcome = await DeleteInstanceDirectoryAsync(
                _instanceComposition.Selection.InstanceName,
                _instanceComposition.Selection.InstanceDirectory,
                _instanceComposition.Selection.LauncherDirectory,
                _instanceComposition.Selection.IsIndie);
            if (outcome is null)
            {
                return;
            }

            _shellActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
            ReloadInstanceComposition();

            if (outcome.IsPermanentDelete)
            {
                AddActivity(activityTitle, SD("instance.overview.messages.delete_permanent", ("instance_name", outcome.InstanceName)));
                return;
            }

            AddActivity(
                activityTitle,
                SD("instance.overview.messages.delete_recycled", ("instance_name", outcome.InstanceName), ("trash_directory", outcome.TrashDirectory)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task PatchInstanceCoreAsync()
    {
        var activityTitle = SD("instance.overview.advanced.patch_core");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.no_instance_selected"));
            return;
        }

        var confirmed = await _shellActionService.ConfirmAsync(
            SD("instance.overview.dialogs.patch_core.title"),
            SD("instance.overview.dialogs.patch_core.message", ("instance_name", _instanceComposition.Selection.InstanceName)),
            SD("instance.overview.dialogs.patch_core.confirm"));
        if (!confirmed)
        {
            AddActivity(activityTitle, SD("instance.overview.messages.patch_canceled"));
            return;
        }

        string? patchPath;
        try
        {
            patchPath = await _shellActionService.PickOpenFileAsync(
                SD("instance.overview.dialogs.patch_core.file_title"),
                SD("instance.overview.dialogs.patch_core.file_filter"),
                "*.jar",
                "*.zip");
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(patchPath))
        {
            AddActivity(activityTitle, SD("instance.overview.messages.patch_file_canceled"));
            return;
        }

        try
        {
            BackupInstanceCoreFiles("patched-core-backups");
            var corePath = Path.Combine(
                _instanceComposition.Selection.InstanceDirectory,
                $"{_instanceComposition.Selection.InstanceName}.jar");
            PatchCoreArchive(corePath, patchPath);
            _shellActionService.PersistInstanceValue(_instanceComposition.Selection.InstanceDirectory, "VersionAdvanceAssetsV2", true);
            ReloadInstanceComposition();
            OpenInstanceTarget(activityTitle, corePath, SD("instance.overview.messages.patched_core_missing"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private string BackupInstanceCoreFiles(string folderName)
    {
        var backupDirectory = Path.Combine(
            GetInstanceOverviewArtifactDirectory(folderName),
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        var manifestPath = Path.Combine(
            _instanceComposition.Selection.InstanceDirectory,
            $"{_instanceComposition.Selection.InstanceName}.json");
        var jarPath = Path.Combine(
            _instanceComposition.Selection.InstanceDirectory,
            $"{_instanceComposition.Selection.InstanceName}.jar");

        CopyFileIfExists(manifestPath, Path.Combine(backupDirectory, Path.GetFileName(manifestPath)));
        CopyFileIfExists(jarPath, Path.Combine(backupDirectory, Path.GetFileName(jarPath)));
        return backupDirectory;
    }

    private string GetInstanceOverviewArtifactDirectory(string folderName)
    {
        return Path.Combine(
            _shellActionService.RuntimePaths.FrontendArtifactDirectory,
            "instance-overview",
            folderName,
            SanitizePathSegment(_instanceComposition.Selection.InstanceName));
    }

    private async Task<FrontendInstanceRepairResult> RunInstanceRepairAsync(
        string actionTitle,
        bool forceCoreRefresh,
        string? backupDirectory = null)
    {
        var instanceDirectory = _instanceComposition.Selection.InstanceDirectory;
        var manifestPath = Path.Combine(instanceDirectory, $"{_instanceComposition.Selection.InstanceName}.json");
        var jarPath = Path.Combine(instanceDirectory, $"{_instanceComposition.Selection.InstanceName}.jar");

        var result = await ExecuteManagedInstanceRepairAsync(
            SD("instance.overview.messages.repair_task_title", ("action_title", actionTitle), ("instance_name", _instanceComposition.Selection.InstanceName)),
            new FrontendInstanceRepairRequest(
                _instanceComposition.Selection.LauncherDirectory,
                _instanceComposition.Selection.InstanceDirectory,
                _instanceComposition.Selection.InstanceName,
                forceCoreRefresh));

        var summaryLines = new List<string?>
        {
            SD("instance.overview.messages.repair_summary.instance", ("instance_name", _instanceComposition.Selection.InstanceName)),
            SD("instance.overview.messages.repair_summary.instance_directory", ("path", instanceDirectory)),
            SD("instance.overview.messages.repair_summary.core_json", ("status", File.Exists(manifestPath) ? SD("instance.overview.messages.repair_summary.present") : SD("instance.overview.messages.repair_summary.missing")), ("path", manifestPath)),
            SD("instance.overview.messages.repair_summary.core_jar", ("status", File.Exists(jarPath) ? SD("instance.overview.messages.repair_summary.present") : SD("instance.overview.messages.repair_summary.missing")), ("path", jarPath)),
            backupDirectory is null ? null : SD("instance.overview.messages.repair_summary.backup_directory", ("path", backupDirectory)),
            SD("instance.overview.messages.repair_summary.downloaded_count", ("count", result.DownloadedFiles.Count)),
            SD("instance.overview.messages.repair_summary.reused_count", ("count", result.ReusedFiles.Count)),
            string.Empty,
            SD("instance.overview.messages.repair_summary.intro")
        };
        summaryLines.AddRange(result.DownloadedFiles.Take(20).Select(path => SD("instance.overview.messages.repair_summary.downloaded_item", ("path", path))));
        summaryLines.AddRange(result.ReusedFiles.Take(20).Select(path => SD("instance.overview.messages.repair_summary.reused_item", ("path", path))));

        await ShowToolboxConfirmationAsync(
            actionTitle,
            string.Join(
                Environment.NewLine,
                summaryLines.Where(line => !string.IsNullOrWhiteSpace(line)).Cast<string>()));
        return result;
    }

    private static void ReplacePathText(string path, string oldValue, string newValue)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path);
        if (!content.Contains(oldValue, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.WriteAllText(path, content.Replace(oldValue, newValue, StringComparison.OrdinalIgnoreCase), new UTF8Encoding(false));
    }

    private static void RewriteInstanceManifestId(string manifestPath, string instanceName)
    {
        if (!File.Exists(manifestPath))
        {
            return;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath));
        }
        catch
        {
            return;
        }

        if (root is not JsonObject obj)
        {
            return;
        }

        obj["id"] = instanceName;
        File.WriteAllText(
            manifestPath,
            obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
    }

    private static void CopyFileIfExists(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.Copy(sourcePath, targetPath, overwrite: true);
    }

    private static void RenamePathPreservingCase(string sourcePath, string targetPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = $"{sourcePath}.rename-{Guid.NewGuid():N}.tmp";
            File.Move(sourcePath, tempPath, overwrite: true);
            File.Move(tempPath, targetPath, overwrite: true);
            return;
        }

        File.Move(sourcePath, targetPath, overwrite: true);
    }

    private static void MoveDirectoryPreservingCase(string sourcePath, string targetPath, bool treatMissingSourceAsSuccess = false)
    {
        if (!Directory.Exists(sourcePath))
        {
            if (treatMissingSourceAsSuccess)
            {
                return;
            }

            throw new DirectoryNotFoundException($"未找到目录：{sourcePath}");
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            var tempPath = $"{sourcePath}.rename-{Guid.NewGuid():N}";
            Directory.Move(sourcePath, tempPath);
            Directory.Move(tempPath, targetPath);
            return;
        }

        Directory.Move(sourcePath, targetPath);
    }

    private static string GetUniquePath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return path;
        }

        var suffix = 1;
        while (true)
        {
            var candidate = $"{path}-{suffix}";
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (invalidCharacters.Contains(character))
            {
                continue;
            }

            builder.Append(character switch
            {
                ' ' => '-',
                '/' => '-',
                '\\' => '-',
                _ => character
            });
        }

        return builder.Length == 0 ? "instance" : builder.ToString();
    }

    private static string SanitizeArtifactFileName(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (invalidCharacters.Contains(character))
            {
                continue;
            }

            builder.Append(character switch
            {
                '/' => '-',
                '\\' => '-',
                _ => character
            });
        }

        return builder.Length == 0 ? "instance" : builder.ToString().Trim();
    }

    private static void PatchCoreArchive(string corePath, string patchArchivePath)
    {
        if (!File.Exists(corePath))
        {
            throw new FileNotFoundException($"未找到指定文件：{corePath}");
        }

        if (!File.Exists(patchArchivePath))
        {
            throw new FileNotFoundException($"未找到指定文件：{patchArchivePath}");
        }

        using var coreStream = new FileStream(corePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 16384, useAsync: true);
        using var patchStream = new FileStream(patchArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true);
        using var coreArchive = new ZipArchive(coreStream, ZipArchiveMode.Update);
        using var patchArchive = new ZipArchive(patchStream, ZipArchiveMode.Read);
        var filter = patchArchivePath.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ? string.Empty : "MINECRAFT-JAR";

        foreach (var entry in patchArchive.Entries)
        {
            if (!entry.FullName.Contains(filter, StringComparison.Ordinal))
            {
                continue;
            }

            coreArchive.GetEntry(entry.FullName)?.Delete();
            var targetEntry = coreArchive.CreateEntry(entry.FullName);
            using var targetStream = targetEntry.Open();
            using var sourceStream = entry.Open();
            sourceStream.CopyTo(targetStream);
        }

        foreach (var signatureEntry in coreArchive.Entries
                     .Where(entry => entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            signatureEntry.Delete();
        }
    }
}
