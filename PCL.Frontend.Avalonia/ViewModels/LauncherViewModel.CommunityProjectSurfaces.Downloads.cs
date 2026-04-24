using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{

    private ActionCommand CreateCommunityProjectReleaseDownloadCommand(FrontendCommunityProjectReleaseEntry entry)
    {
        return new ActionCommand(() => _ = DownloadCommunityProjectReleaseAsync(entry));
    }

    private async Task DownloadCommunityProjectReleaseAsync(FrontendCommunityProjectReleaseEntry entry)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(entry.Target))
            {
                AddActivity(T("resource_detail.activities.download_file", ("entry_title", entry.Title)), T("resource_detail.download.errors.missing_url"));
                return;
            }

            if (ShouldAutoInstallCommunityProjectRelease(entry))
            {
                await DownloadAndInstallCommunityProjectReleaseAsync(entry);
                return;
            }

            var suggestedFileName = ResolveCommunityProjectReleaseFileName(entry, CommunityProjectTitle);
            var extension = Path.GetExtension(suggestedFileName);
            var patterns = string.IsNullOrWhiteSpace(extension) ? Array.Empty<string>() : [$"*{extension}"];
            var suggestedStartFolder = ResolveCommunityProjectDownloadStartDirectory();

            string? targetPath;
            try
            {
                targetPath = await _launcherActionService.PickSaveFileAsync(
                    T("resource_detail.download.dialogs.pick_save_path.title"),
                    suggestedFileName,
                    T("resource_detail.download.dialogs.pick_save_path.file_type"),
                    suggestedStartFolder,
                    patterns);
            }
            catch (Exception ex)
            {
                AddFailureActivity(T("resource_detail.activities.pick_save_path_failed", ("entry_title", entry.Title)), ex.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                AddActivity(T("resource_detail.activities.download_canceled", ("entry_title", entry.Title)), T("resource_detail.download.messages.no_save_path"));
                return;
            }

            TaskCenter.Register(new FrontendManagedFileDownloadTask(
                T("resource_detail.download.task_title", ("file_name", Path.GetFileNameWithoutExtension(targetPath))),
                entry.Target,
                targetPath,
                ResolveDownloadRequestTimeout(),
                _launcherActionService.GetDownloadTransferOptions(),
                onStarted: filePath => AvaloniaHintBus.Show(T("resource_detail.download.hints.started", ("file_name", Path.GetFileName(filePath))), AvaloniaHintTheme.Info),
                onCompleted: filePath => AvaloniaHintBus.Show(T("resource_detail.download.hints.completed", ("file_name", Path.GetFileName(filePath))), AvaloniaHintTheme.Success),
                onFailed: message => AvaloniaHintBus.Show(message, AvaloniaHintTheme.Error)));
            AddActivity(T("resource_detail.activities.download_started", ("entry_title", entry.Title)), targetPath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.activities.download_failed", ("entry_title", entry.Title)), ex.Message);
        }
    }

    private bool CanShowCommunityProjectInstallSuggestionCard()
    {
        if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadPack
            || _selectedCommunityProjectOriginSubpage is null)
        {
            return false;
        }

        if (_selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack)
        {
            return ResolveCurrentDatapackInstallSelection() is not null;
        }

        return _instanceComposition.Selection.HasSelection;
    }

    private bool CanInstallCommunityProjectToCurrentInstance()
    {
        if (!CanShowCommunityProjectInstallSuggestionCard())
        {
            return false;
        }

        return TryGetCommunityProjectInstallRelease(out _);
    }

    private FrontendInstalledCommunityResourceMatch? GetCurrentCommunityProjectInstalledModMatch()
    {
        if (_selectedCommunityProjectOriginSubpage != LauncherFrontendSubpageKey.DownloadMod
            || !_instanceComposition.Selection.HasSelection
            || string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            return null;
        }

        var release = GetSuggestedCommunityProjectInstallRelease();
        return FrontendInstanceDownloadedResourceIndexService.FindInstalledMod(
            _instanceComposition,
            _communityProjectState,
            release);
    }

    private async Task SetCommunityProjectInstalledModEnabledAsync(bool isEnabled)
    {
        var match = GetCurrentCommunityProjectInstalledModMatch();
        if (match is null)
        {
            AddActivity(
                T(isEnabled ? "resource_detail.installed_mod.activities.enable" : "resource_detail.installed_mod.activities.disable"),
                T("resource_detail.installed_mod.messages.not_found"));
            return;
        }

        await SetInstanceResourceEntriesEnabledAsync(
            new[] { (Title: match.Entry.Title, Path: match.Entry.Path, IsEnabledState: match.Entry.IsEnabled) },
            isEnabled,
            T("resource_detail.installed_mod.messages.not_found"));
        RaiseCommunityProjectInstalledModProperties();
    }

    private async Task DeleteCommunityProjectInstalledModAsync()
    {
        var match = GetCurrentCommunityProjectInstalledModMatch();
        if (match is null)
        {
            AddActivity(
                T("resource_detail.installed_mod.activities.uninstall"),
                T("resource_detail.installed_mod.messages.not_found"));
            return;
        }

        await DeleteInstanceResourcesAsync(
            new[] { (Title: match.Entry.Title, Path: match.Entry.Path) },
            T("resource_detail.installed_mod.messages.not_found"));
        RaiseCommunityProjectInstalledModProperties();
    }

    private void RaiseCommunityProjectInstalledModProperties()
    {
        RaisePropertyChanged(nameof(ShowCommunityProjectInstalledModCard));
        RaisePropertyChanged(nameof(CommunityProjectInstalledModTitle));
        RaisePropertyChanged(nameof(CommunityProjectInstalledModSummary));
        RaisePropertyChanged(nameof(CanEnableCommunityProjectInstalledMod));
        RaisePropertyChanged(nameof(CanDisableCommunityProjectInstalledMod));
        RaisePropertyChanged(nameof(CanUninstallCommunityProjectInstalledMod));
    }

    private FrontendVersionSaveSelectionState? ResolveCurrentDatapackInstallSelection()
    {
        return _versionSavesComposition.Selection.HasSelection
            ? _versionSavesComposition.Selection
            : null;
    }

    private string GetCommunityProjectInstallActivityTitle()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadDataPack
            ? T("resource_detail.activities.install_current_save")
            : T("resource_detail.activities.install_current_instance");
    }

    private FrontendCommunityProjectReleaseEntry? GetSuggestedCommunityProjectInstallRelease()
    {
        return TryGetCommunityProjectInstallRelease(out var release) ? release : null;
    }

    private async Task InstallCommunityProjectToCurrentInstanceAsync()
    {
        var activityTitle = GetCommunityProjectInstallActivityTitle();
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.current_instance.none_selected"));
            return;
        }

        if (!TryGetCommunityProjectInstallRelease(out var entry))
        {
            AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.errors.no_filtered_release"));
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.download.errors.missing_url"));
            return;
        }

        try
        {
            var includeDependencies = ShouldInstallCommunityProjectMissingDependencies();
            var route = _selectedCommunityProjectOriginSubpage;
            if (route is null)
            {
                AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.errors.unknown_resource_type"));
                return;
            }

            var datapackSaveSelection = route == LauncherFrontendSubpageKey.DownloadDataPack
                ? ResolveCurrentDatapackInstallSelection()
                : null;
            if (route == LauncherFrontendSubpageKey.DownloadDataPack && datapackSaveSelection is null)
            {
                AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.errors.no_datapack_save_selected"));
                return;
            }

            AvaloniaHintBus.Show(T("resource_detail.install.hints.analyzing"), AvaloniaHintTheme.Info);
            AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.messages.analyzing", ("instance_name", CommunityProjectCurrentInstanceName), ("project_title", CommunityProjectTitle)));
            var result = await Task.Run(() => BuildCommunityProjectInstallBuildResult(
                [
                    new CommunityProjectInstallRootRequest(
                        _selectedCommunityProjectId,
                        CommunityProjectTitle,
                        route.Value,
                        _communityProjectState,
                        entry)
                ],
                _instanceComposition,
                includeDependencies,
                datapackSaveSelection));

            if (includeDependencies)
            {
                var confirmed = await ConfirmCommunityProjectInstallWithDependenciesAsync(result);
                if (!confirmed)
                {
                    AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.messages.canceled_with_dependencies"));
                    return;
                }
            }

            if (result.Plans.Count == 0)
            {
                AddActivity(T("resource_detail.activities.install_current_instance"), result.Skipped.Count == 0
                    ? T("resource_detail.install.messages.no_tasks")
                    : string.Join("；", result.Skipped.Take(3)));
                return;
            }

            AvaloniaHintBus.Show(T("resource_detail.install.hints.started"), AvaloniaHintTheme.Info);
            foreach (var plan in result.Plans)
            {
                RegisterCommunityProjectInstallTask(plan, T("resource_detail.activities.install_current_instance"));
            }

            var summaryParts = new List<string> { T("resource_detail.install.summary.tasks", ("count", result.Plans.Count)) };
            if (includeDependencies)
            {
                var dependencyCount = result.Plans.Count(plan => plan.IsDependency);
                if (dependencyCount > 0)
                {
                    summaryParts.Add(T("resource_detail.install.summary.dependencies", ("count", dependencyCount)));
                }
            }

            if (result.Skipped.Count > 0)
            {
                summaryParts.Add(T("resource_detail.install.summary.skipped", ("count", result.Skipped.Count)));
            }

            AddActivity(T("resource_detail.activities.install_current_instance"), T("resource_detail.install.summary.completed", ("instance_name", CommunityProjectCurrentInstanceName), ("summary", string.Join(T("common.punctuation.comma"), summaryParts))));
            foreach (var skipped in result.Skipped.Take(5))
            {
                AddActivity(T("resource_detail.activities.install_current_instance"), skipped);
            }
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.activities.install_current_instance_failed"), ex.Message);
        }
    }

    private async Task<bool> ConfirmCommunityProjectInstallWithDependenciesAsync(CommunityProjectInstallBuildResult result)
    {
        try
        {
            return await _launcherActionService.ConfirmAsync(
                T("resource_detail.install.dialogs.confirm_dependencies.title"),
                BuildCommunityProjectInstallConfirmationMessage(CommunityProjectCurrentInstanceName, result),
                T("resource_detail.install.dialogs.confirm_dependencies.confirm"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.install.dialogs.confirm_dependencies.failed"), ex.Message);
            return false;
        }
    }

    private bool ShouldAutoInstallCommunityProjectRelease(FrontendCommunityProjectReleaseEntry entry)
    {
        if (!entry.IsDirectDownload || !IsCommunityProjectModpack())
        {
            return false;
        }

        var extension = ResolveCommunityProjectReleaseExtension(entry);
        return extension is ".mrpack" or ".zip";
    }

    private bool IsCommunityProjectModpack()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadPack
               || _communityProjectState.Website.Contains("/modpacks/", StringComparison.OrdinalIgnoreCase)
               || _communityProjectState.Website.Contains("/modpack/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadAndInstallCommunityProjectReleaseAsync(FrontendCommunityProjectReleaseEntry entry)
    {
        var launcherDirectory = ResolveDownloadLauncherFolder();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        Directory.CreateDirectory(versionsDirectory);

        string? instanceName;
        try
        {
            instanceName = await PromptForCommunityProjectInstanceNameAsync(versionsDirectory);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.modpack.activities.prompt_instance_name_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            AddActivity(T("resource_detail.modpack.activities.install_canceled", ("entry_title", entry.Title)), T("resource_detail.modpack.messages.missing_instance_name"));
            return;
        }

        var extension = ResolveCommunityProjectReleaseExtension(entry);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mrpack";
        }

        var targetDirectory = Path.Combine(versionsDirectory, instanceName);
        var downloadedPath = Path.Combine(targetDirectory, T("resource_detail.modpack.archive_name", ("extension", extension)));
        var description = string.IsNullOrWhiteSpace(_communityProjectState.Summary)
            ? _communityProjectState.Description
            : _communityProjectState.Summary;
        var taskTitle = T("resource_detail.modpack.task_title", ("source_name", _communityProjectState.Source), ("instance_name", instanceName));

        TaskCenter.Register(new FrontendManagedModpackInstallTask(
            taskTitle,
            new FrontendModpackInstallRequest(
                entry.Target!,
                null,
                downloadedPath,
                launcherDirectory,
                _selectedDownloadSourceIndex,
                instanceName,
                targetDirectory,
                _selectedCommunityProjectId,
                _communityProjectState.Source,
                _communityProjectState.IconPath,
                description,
                _selectedCommunityDownloadSourceIndex),
            ResolveDownloadRequestTimeout(),
            _launcherActionService.GetDownloadTransferOptions(),
            onStarted: filePath =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AvaloniaHintBus.Show(T("resource_detail.modpack.hints.download_and_install_started", ("file_name", Path.GetFileName(filePath))), AvaloniaHintTheme.Info);
                });
            },
            onCompleted: result =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    HandleCommunityProjectModpackInstalled(result);
                });
            },
            onFailed: message =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    AddFailureActivity(T("resource_detail.modpack.activities.install_failed", ("entry_title", entry.Title)), message);
                });
            },
            i18n: _i18n));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            T("resource_detail.install.messages.queued_in_task_center", ("task_title", taskTitle)));
    }

    private async Task SelectCommunityProjectInstanceAsync()
    {
        var instances = LoadAvailableDownloadTargetInstances();
        if (instances.Count == 0)
        {
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSelect),
                T("resource_detail.instance_selection.messages.no_instances"));
            return;
        }

        string? selectedId;
        try
        {
            selectedId = await _launcherActionService.PromptForChoiceAsync(
                T("resource_detail.instance_selection.dialog.title"),
                T("resource_detail.instance_selection.dialog.message"),
                instances.Select(entry => new PclChoiceDialogOption(
                    entry.Name,
                    entry.Name,
                    entry.Subtitle)).ToArray(),
                _instanceComposition.Selection.HasSelection ? _instanceComposition.Selection.InstanceName : null,
                T("download.resource.current_instance.actions.switch"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.instance_selection.activities.select_failed"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        if (_instanceComposition.Selection.HasSelection
            && string.Equals(_instanceComposition.Selection.InstanceName, selectedId, StringComparison.OrdinalIgnoreCase))
        {
            AddActivity(T("resource_detail.instance_selection.activities.select"), T("resource_detail.instance_selection.messages.already_selected", ("instance_name", selectedId)));
            return;
        }

        RefreshSelectedInstanceSmoothly(selectedId);
    }

    private async Task<string?> PromptForCommunityProjectInstanceNameAsync(string versionsDirectory, string? suggestion = null)
    {
        suggestion ??= BuildCommunityProjectInstanceNameSuggestion();
        while (true)
        {
            var input = await _launcherActionService.PromptForTextAsync(
                T("resource_detail.modpack.dialogs.instance_name.title"),
                T("resource_detail.modpack.dialogs.instance_name.message"),
                suggestion,
                T("download.install.actions.start"));
            if (input is null)
            {
                return null;
            }

            var trimmed = input.Trim();
            var validationError = ValidateCommunityProjectInstanceName(trimmed, versionsDirectory);
            if (string.IsNullOrWhiteSpace(validationError))
            {
                return trimmed;
            }

            var retry = await _launcherActionService.ConfirmAsync(
                T("resource_detail.modpack.dialogs.invalid_name.title"),
                validationError,
                T("resource_detail.modpack.dialogs.invalid_name.confirm"));
            if (!retry)
            {
                return null;
            }

            suggestion = trimmed;
        }
    }

    private string BuildCommunityProjectInstanceNameSuggestion()
    {
        var title = _communityProjectState.Title
            .Replace(".zip", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".rar", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".mrpack", string.Empty, StringComparison.OrdinalIgnoreCase);
        return SanitizeInstallDirectoryName(title);
    }

    private string? ValidateCommunityProjectInstanceName(string value, string versionsDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return T("resource_detail.modpack.validation.empty");
        }

        if (value.StartsWith(' ') || value.EndsWith(' '))
        {
            return T("resource_detail.modpack.validation.trim_spaces");
        }

        if (value.Length > 100)
        {
            return T("resource_detail.modpack.validation.too_long");
        }

        if (value.EndsWith(".", StringComparison.Ordinal))
        {
            return T("resource_detail.modpack.validation.trailing_dot");
        }

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('!') || value.Contains(';'))
        {
            return T("resource_detail.modpack.validation.invalid_characters");
        }

        if (CommunityProjectReservedInstanceNames.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return T("resource_detail.modpack.validation.reserved_name");
        }

        if (Regex.IsMatch(value, ".{2,}~\\d", RegexOptions.CultureInvariant))
        {
            return T("resource_detail.modpack.validation.short_name_pattern");
        }

        if (Directory.Exists(versionsDirectory) && Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase)))
        {
            return T("resource_detail.modpack.validation.duplicate");
        }

        return null;
    }

    private void HandleCommunityProjectModpackInstalled(FrontendModpackInstallResult result)
    {
        if (AutoSelectNewInstance)
        {
            _launcherActionService.PersistLocalValue("LaunchInstanceSelect", result.InstanceName);
        }

        RefreshLaunchState();
        ReloadDownloadComposition();
        AddActivity(T("resource_detail.modpack.activities.install_completed"), T("resource_detail.modpack.messages.install_completed", ("instance_name", result.InstanceName), ("target_directory", result.TargetDirectory)));
        AvaloniaHintBus.Show(T("resource_detail.modpack.hints.install_completed", ("instance_name", result.InstanceName)), AvaloniaHintTheme.Success);
    }

    private string ResolveCommunityProjectReleaseExtension(FrontendCommunityProjectReleaseEntry entry)
    {
        var suggestedFileName = ResolveCommunityProjectReleaseFileName(entry, _communityProjectState.Title);
        var extension = Path.GetExtension(suggestedFileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        if (Uri.TryCreate(entry.Target, UriKind.Absolute, out var uri))
        {
            extension = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension.ToLowerInvariant();
            }
        }

        return string.Empty;
    }

    private string? ResolveSelectedInstanceLoaderLabel()
    {
        return ResolvePreferredInstanceLoaderLabel(_instanceComposition, _selectedCommunityProjectOriginSubpage);
    }

    private static string? ResolvePreferredInstanceLoaderLabel(
        FrontendInstanceComposition composition,
        LauncherFrontendSubpageKey? route)
    {
        return route == LauncherFrontendSubpageKey.DownloadShader
            ? ResolveInstanceShaderLoaderLabel(composition)
            : ResolveInstancePrimaryLoaderLabel(composition);
    }

    private static string? ResolveInstancePrimaryLoaderLabel(FrontendInstanceComposition composition)
    {
        if (!composition.Selection.HasSelection)
        {
            return null;
        }

        foreach (var optionTitle in new[]
                 {
                     "NeoForge",
                     "Cleanroom",
                     "Fabric",
                     "Legacy Fabric",
                     "Quilt",
                     "Forge",
                     "OptiFine",
                     "LiteLoader",
                     "LabyMod"
                 })
        {
            if (HasInstalledManagedOption(composition, optionTitle))
            {
                return optionTitle;
            }
        }

        return null;
    }

    private static string? ResolveInstanceShaderLoaderLabel(FrontendInstanceComposition composition)
    {
        if (!composition.Selection.HasSelection)
        {
            return null;
        }

        if (HasInstalledManagedOption(composition, "OptiFine"))
        {
            return "OptiFine";
        }

        return HasInstalledManagedAddonMod(composition.Mods.Entries, "iris", "irisshaders")
            ? "Iris"
            : null;
    }

    private static bool HasInstalledManagedOption(FrontendInstanceComposition composition, string optionTitle)
    {
        return composition.Install.Options.Any(option =>
            string.Equals(option.Title, optionTitle, StringComparison.Ordinal)
            && option.SelectionState.HasSelection);
    }

    private static bool HasInstalledManagedAddonMod(
        IEnumerable<FrontendInstanceResourceEntry> entries,
        params string[] identifiers)
    {
        return entries.Any(entry =>
            MatchesManagedAddonIdentity(entry.Identity, identifiers)
            || MatchesManagedAddonIdentity(entry.Title, identifiers)
            || identifiers.Any(identifier => NormalizeManagedAddonIdentity(Path.GetFileNameWithoutExtension(entry.Path))
                .StartsWith(NormalizeManagedAddonIdentity(identifier), StringComparison.Ordinal)));
    }

    private static bool MatchesManagedAddonIdentity(string? value, IEnumerable<string> identifiers)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeManagedAddonIdentity(value);
        return identifiers.Any(identifier => string.Equals(
            normalized,
            NormalizeManagedAddonIdentity(identifier),
            StringComparison.Ordinal));
    }

    private static string NormalizeManagedAddonIdentity(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private IReadOnlyList<InstanceSelectionSnapshot> LoadAvailableDownloadTargetInstances()
    {
        var runtimePaths = _launcherActionService.RuntimePaths;
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstance = ReadValue(localConfig, "LaunchInstanceSelect", string.Empty).Trim();
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(directory => BuildInstanceSelectionSnapshot(directory, selectedInstance))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .OrderByDescending(snapshot => snapshot.IsSelected)
            .ThenByDescending(snapshot => snapshot.IsStarred)
            .ThenBy(snapshot => snapshot.IsBroken)
            .ThenBy(snapshot => snapshot.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private bool TryGetCommunityProjectInstallRelease(out FrontendCommunityProjectReleaseEntry entry)
    {
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        var preferredLoader = ResolveSelectedInstanceLoaderLabel();
        var versionGrouping = DetermineCommunityProjectVersionGrouping(_communityProjectState.Releases);
        entry = SelectPreferredCommunityProjectRelease(
            GetVisibleCommunityProjectReleases(versionGrouping)
                .Where(release => release.IsDirectDownload
                                  && !string.IsNullOrWhiteSpace(release.Target)
                                  && IsCompatibleCommunityProjectInstallRelease(
                                      release,
                                      preferredVersion,
                                      preferredLoader,
                                      _selectedCommunityProjectOriginSubpage)))!;
        return entry is not null;
    }

    private FrontendCommunityProjectReleaseEntry? SelectPreferredCommunityProjectRelease(
        IEnumerable<FrontendCommunityProjectReleaseEntry> releases)
    {
        var preferredVersion = NormalizeMinecraftVersion(_instanceComposition.Selection.VanillaVersion);
        var preferredLoader = ResolveSelectedInstanceLoaderLabel();

        return releases
            .OrderByDescending(release => ReleaseMatchesExactInstanceVersion(release, preferredVersion))
            .ThenByDescending(release => ReleaseMatchesExactInstanceLoader(release, preferredLoader))
            .ThenByDescending(release => release.PublishedUnixTime)
            .ThenBy(release => release.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    private static bool ReleaseMatchesExactInstanceVersion(FrontendCommunityProjectReleaseEntry release, string? preferredVersion)
    {
        if (string.IsNullOrWhiteSpace(preferredVersion))
        {
            return false;
        }

        return release.GameVersions.Any(version =>
            string.Equals(NormalizeMinecraftVersion(version) ?? version, preferredVersion, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReleaseMatchesExactInstanceLoader(FrontendCommunityProjectReleaseEntry release, string? preferredLoader)
    {
        if (string.IsNullOrWhiteSpace(preferredLoader))
        {
            return false;
        }

        return release.Loaders.Any(loader => string.Equals(loader, preferredLoader, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCompatibleCommunityProjectInstallRelease(
        FrontendCommunityProjectReleaseEntry release,
        string? preferredVersion,
        string? preferredLoader,
        LauncherFrontendSubpageKey? originSubpage)
    {
        if (!ReleaseMatchesExactInstanceVersion(release, NormalizeMinecraftVersion(preferredVersion)))
        {
            return false;
        }

        if (!RequiresCommunityProjectInstallLoader(originSubpage))
        {
            return true;
        }

        return ReleaseMatchesExactInstanceLoader(release, preferredLoader);
    }

    private static bool RequiresCommunityProjectInstallLoader(LauncherFrontendSubpageKey? originSubpage)
    {
        return originSubpage is LauncherFrontendSubpageKey.DownloadMod
            or LauncherFrontendSubpageKey.DownloadShader;
    }

    private bool ShouldInstallCommunityProjectMissingDependencies()
    {
        return _selectedCommunityProjectOriginSubpage == LauncherFrontendSubpageKey.DownloadMod
               && string.Equals(
                   SelectedCommunityProjectInstallModeOption?.FilterValue,
                   CommunityProjectInstallModeWithDependenciesValue,
                   StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveCommunityProjectReleaseFileName(FrontendCommunityProjectReleaseEntry entry, string? projectTitle = null)
    {
        var fileName = FrontendGameManagementService.ResolveCommunityResourceFileName(
            projectTitle,
            entry.SuggestedFileName,
            entry.Title,
            SelectedFileNameFormatIndex);
        return NormalizeCommunityProjectInstallArtifactFileName(_selectedCommunityProjectOriginSubpage, fileName);
    }

    private static string NormalizeCommunityProjectInstallArtifactFileName(
        LauncherFrontendSubpageKey? route,
        string fileName)
    {
        if (route != LauncherFrontendSubpageKey.DownloadDataPack)
        {
            return fileName;
        }

        var extension = Path.GetExtension(fileName);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return $"{fileName}.zip";
        }

        if (string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(fileName, ".zip");
        }

        return fileName;
    }

    private static string FinalizeCommunityProjectInstalledArtifact(
        LauncherFrontendSubpageKey? originSubpage,
        string downloadedPath,
        string? replacedPath = null)
    {
        if (originSubpage == LauncherFrontendSubpageKey.DownloadWorld)
        {
            return FrontendWorldArchiveInstallService.ExtractInstalledWorldArchive(downloadedPath);
        }

        return downloadedPath;
    }

    private string? ResolveCommunityProjectDownloadStartDirectory()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            return null;
        }

        var directory = _selectedCommunityProjectOriginSubpage switch
        {
            LauncherFrontendSubpageKey.DownloadResourcePack => ResolveCurrentInstanceResourceDirectory("resourcepacks"),
            LauncherFrontendSubpageKey.DownloadShader => ResolveCurrentInstanceResourceDirectory("shaderpacks"),
            LauncherFrontendSubpageKey.DownloadWorld => Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves"),
            LauncherFrontendSubpageKey.DownloadDataPack => _versionSavesComposition.Selection.HasSelection
                ? _versionSavesComposition.Selection.DatapackDirectory
                : Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves"),
            LauncherFrontendSubpageKey.DownloadPack => _instanceComposition.Selection.InstanceDirectory,
            _ => ResolveCurrentInstanceResourceDirectory("mods")
        };

        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        Directory.CreateDirectory(directory);
        return directory;
    }

    private TimeSpan ResolveDownloadRequestTimeout()
    {
        var seconds = Math.Clamp((int)Math.Round(DownloadTimeoutSeconds), 1, 60);
        return TimeSpan.FromSeconds(seconds);
    }

}
