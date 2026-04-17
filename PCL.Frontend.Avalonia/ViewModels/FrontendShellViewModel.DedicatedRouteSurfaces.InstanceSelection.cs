using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private void RefreshInstanceSelectionSurface()
    {
        var runtimePaths = _shellActionService.RuntimePaths;
        var localConfig = runtimePaths.OpenLocalConfigProvider();
        var sharedConfig = runtimePaths.OpenSharedConfigProvider();
        var launcherDirectory = ResolveLauncherFolder(
            ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
            runtimePaths);
        var selectedInstance = ReadRememberedLaunchInstanceName(localConfig);
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        _instanceSelectionLauncherDirectory = launcherDirectory;

        ReplaceItems(
            InstanceSelectionFolderEntries,
            BuildInstanceSelectionFolderSnapshots(sharedConfig, localConfig, runtimePaths, launcherDirectory)
                .Select(CreateInstanceSelectionFolderEntry));

        ReplaceItems(
            InstanceSelectionShortcutEntries,
            [
                CreateInstanceSelectionShortcutEntry(
                    LT("shell.instance_select.shortcuts.add_folder.title"),
                    LT("shell.instance_select.shortcuts.add_folder.description"),
                    "F1 m 12 7 a 1 1 0 0 0 -1 1 v 8 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 8 A 1 1 0 0 0 12 7 Z m -4 4 a 1 1 0 0 0 -1 1 a 1 1 0 0 0 1 1 h 8 a 1 1 0 0 0 1 -1 a 1 1 0 0 0 -1 -1 z M 12 1 C 5.93671 1 1 5.93671 1 12 C 1 18.0633 5.93671 23 12 23 C 18.0633 23 23 18.0633 23 12 C 23 5.93671 18.0633 1 12 1 Z m 0 2 c 4.98241 0 9 4.01759 9 9 c 0 4.98241 -4.01759 9 -9 9 C 7.01759 21 3 16.9824 3 12 C 3 7.01759 7.01759 3 12 3 Z",
                    _addInstanceSelectionFolderCommand),
                CreateInstanceSelectionShortcutEntry(
                    LT("shell.instance_select.shortcuts.import_pack.title"),
                    LT("shell.instance_select.shortcuts.import_pack.description"),
                    "F1 m 11.293 11.293 l -3 3 a 1 1 0 0 0 0 1.41406 a 1 1 0 0 0 1.41406 0 L 12 13.4141 l 2.29297 2.29297 a 1 1 0 0 0 1.41406 0 a 1 1 0 0 0 0 -1.41406 l -3 -3 a 1.0001 1.0001 0 0 0 -1.41406 0 z M 12 11 a 1 1 0 0 0 -1 1 v 6 a 1 1 0 0 0 1 1 a 1 1 0 0 0 1 -1 V 12 A 1 1 0 0 0 12 11 Z M 14 1 a 1 1 0 0 0 -1 1 v 5 c 0 1.09272 0.907275 2 2 2 h 5 A 1 1 0 0 0 21 8 A 1 1 0 0 0 20 7 H 15 V 2 A 1 1 0 0 0 14 1 Z M 6 1 C 4.35499 1 3 2.35499 3 4 v 16 c 0 1.64501 1.35499 3 3 3 h 12 c 1.64501 0 3 -1.35499 3 -3 V 8.00195 V 8 C 21.001 7.09394 20.6387 6.22279 19.9961 5.58398 L 16.4121 2 L 16.4101 1.99805 C 15.7718 1.35838 14.9038 0.999054 14 1 Z m 0 2 h 8 a 1.0001 1.0001 0 0 0 0.002 0 c 0.373356 -0.0006051 0.730614 0.147632 0.994141 0.412109 a 1.0001 1.0001 0 0 0 0 0.00195 l 3.58789 3.58789 a 1.0001 1.0001 0 0 0 0.0039 0.00195 C 18.8531 7.26753 19.0006 7.62412 19 7.99805 A 1.0001 1.0001 0 0 0 19 8 v 12 c 0 0.564129 -0.435871 1 -1 1 H 6 C 5.43587 21 5 20.5641 5 20 V 4 C 5 3.43587 5.43587 3 6 3 Z",
                    _importInstanceSelectionPackCommand),
                CreateInstanceSelectionShortcutEntry(
                    LT("shell.instance_select.shortcuts.trash.title"),
                    LT("shell.instance_select.shortcuts.trash.description"),
                    FrontendIconCatalog.FolderOutline.Data,
                    new ActionCommand(OpenInstanceSelectionTrashDirectory))
            ]);

        var allEntries = Directory.Exists(versionsDirectory)
            ? Directory.EnumerateDirectories(versionsDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(directory => BuildInstanceSelectionSnapshot(directory, selectedInstance))
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.IsSelected)
                .ThenByDescending(snapshot => snapshot.IsStarred)
                .ThenBy(snapshot => snapshot.IsBroken)
                .ThenBy(snapshot => snapshot.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
            : [];
        _instanceSelectionTotalCount = allEntries.Length;

        var query = InstanceSelectionSearchQuery.Trim();
        var filteredEntries = string.IsNullOrWhiteSpace(query)
            ? allEntries
            : allEntries.Where(entry => MatchesInstanceSelectionQuery(entry, query)).ToArray();

        var groupExpansionStates = InstanceSelectionGroups.ToDictionary(
            group => group.Title,
            group => group.IsExpanded,
            StringComparer.CurrentCultureIgnoreCase);

        ReplaceItems(
            InstanceSelectionEntries,
            filteredEntries.Select(entry => CreateInstanceSelectionEntry(entry)));

        ReplaceItems(
            InstanceSelectionGroups,
            BuildInstanceSelectionGroups(filteredEntries, groupExpansionStates));

        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectory));
        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectoryLabel));
        RaisePropertyChanged(nameof(InstanceSelectionLauncherDirectoryPath));
        RaisePropertyChanged(nameof(HasInstanceSelectionFolders));
        RaisePropertyChanged(nameof(HasInstanceSelectionSearchBox));
        RaisePropertyChanged(nameof(InstanceSelectionSearchToolTip));
        RaisePropertyChanged(nameof(InstanceSelectionSearchWatermark));
        RaisePropertyChanged(nameof(HasInstanceSelectionEntries));
        RaisePropertyChanged(nameof(HasNoInstanceSelectionEntries));
        RaisePropertyChanged(nameof(InstanceSelectionResultSummary));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyTitle));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyDescription));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyDownloadAction));
        RaisePropertyChanged(nameof(ShowInstanceSelectionEmptyClearAction));
        RaisePropertyChanged(nameof(InstanceSelectionEmptyDownloadButtonText));
        RaisePropertyChanged(nameof(InstanceSelectionFoldersHeader));
        RaisePropertyChanged(nameof(InstanceSelectionShortcutsHeader));
    }

    private void SelectInstanceAndCloseSelection(InstanceSelectionSnapshot entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return;
        }

        if (!_instanceComposition.Selection.HasSelection ||
            !string.Equals(_instanceComposition.Selection.InstanceName, entry.Name, System.StringComparison.OrdinalIgnoreCase))
        {
            RefreshSelectedInstanceSmoothly(entry.Name);
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
            LT("shell.instance_select.navigation.launch", ("name", entry.Name)));
    }

    private async Task AddInstanceSelectionFolderAsync()
    {
        try
        {
            var pickedFolderPath = await _shellActionService.PickFolderAsync(LT("shell.instance_select.shortcuts.add_folder.pick_title"));
            if (string.IsNullOrWhiteSpace(pickedFolderPath))
            {
                AddActivity(
                    LT("shell.instance_select.shortcuts.add_folder.title"),
                    LT("shell.instance_select.shortcuts.add_folder.cancelled"));
                return;
            }

            var runtimePaths = _shellActionService.RuntimePaths;
            var resolvedFolderPath = ResolvePickedLauncherFolderPath(pickedFolderPath);
            var localConfig = runtimePaths.OpenLocalConfigProvider();
            var sharedConfig = runtimePaths.OpenSharedConfigProvider();
            var currentFolderPath = ResolveLauncherFolder(
                ReadValue(localConfig, "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
                runtimePaths);
            var configuredFolders = BuildInstanceSelectionFolderSnapshots(sharedConfig, localConfig, runtimePaths, currentFolderPath)
                .ToList();
            var existingFolder = configuredFolders.FirstOrDefault(folder =>
                string.Equals(folder.Directory, resolvedFolderPath, GetPathComparison()));
            var addedToList = existingFolder is null;

            if (existingFolder is null)
            {
                configuredFolders.Add(new InstanceSelectionFolderSnapshot(
                    GetInstanceSelectionDirectoryLabel(resolvedFolderPath),
                    resolvedFolderPath,
                    StoreLauncherFolderPath(resolvedFolderPath, runtimePaths),
                    IsPersisted: true));
                PersistInstanceSelectionFolders(configuredFolders, runtimePaths);
            }

            RefreshSelectedLauncherFolderSmoothly(
                StoreLauncherFolderPath(resolvedFolderPath, runtimePaths),
                resolvedFolderPath,
                addedToList
                ? LT("shell.instance_select.shortcuts.add_folder.added_and_switched", ("path", resolvedFolderPath))
                : LT("shell.instance_select.shortcuts.add_folder.switched", ("path", resolvedFolderPath)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.shortcuts.add_folder.failure"), ex.Message);
        }
    }

    private async Task ImportInstanceSelectionPackAsync()
    {
        try
        {
            var sourcePath = await _shellActionService.PickOpenFileAsync(
                LT("shell.instance_select.shortcuts.import_pack.pick_title"),
                LT("shell.instance_select.shortcuts.import_pack.pick_filter"),
                "*.zip",
                "*.mrpack",
                "*.rar");
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                AddActivity(
                    LT("shell.instance_select.shortcuts.import_pack.title"),
                    LT("shell.instance_select.shortcuts.import_pack.cancelled"));
                return;
            }

            await StartInstanceSelectionPackInstallAsync(sourcePath);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.failure"), ex.Message);
        }
    }

    private async Task StartInstanceSelectionPackInstallAsync(string sourcePath)
    {
        var launcherDirectory = string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory)
            ? ResolveLauncherFolder(
                ReadValue(_shellActionService.RuntimePaths.OpenLocalConfigProvider(), "LaunchFolderSelect", FrontendLauncherPathService.DefaultLauncherFolderRaw),
                _shellActionService.RuntimePaths)
            : _instanceSelectionLauncherDirectory;
        var versionsDirectory = Path.Combine(launcherDirectory, "versions");
        Directory.CreateDirectory(versionsDirectory);

        string? instanceName;
        try
        {
            var suggestion = SanitizeInstallDirectoryName(FrontendModpackInstallWorkflowService.SuggestInstanceName(sourcePath));
            instanceName = await PromptForCommunityProjectInstanceNameAsync(versionsDirectory, suggestion);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.name_failure"), ex.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(instanceName))
        {
            AddActivity(
                LT("shell.instance_select.shortcuts.import_pack.title"),
                LT("shell.instance_select.shortcuts.import_pack.no_name"));
            return;
        }

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".zip";
        }

        var normalizedExtension = extension.ToLowerInvariant();
        var targetDirectory = Path.Combine(versionsDirectory, instanceName);
        var archivePath = Path.Combine(targetDirectory, $"original-modpack{normalizedExtension}");
        var taskTitle = LT("shell.instance_select.tasks.install_modpack", ("name", instanceName));

        TaskCenter.Register(new FrontendManagedModpackInstallTask(
            taskTitle,
            new FrontendModpackInstallRequest(
                SourceUrl: null,
                SourceArchivePath: sourcePath,
                ArchivePath: archivePath,
                LauncherDirectory: launcherDirectory,
                DownloadSourceIndex: SelectedDownloadSourceIndex,
                InstanceName: instanceName,
                TargetDirectory: targetDirectory,
                ProjectId: null,
                ProjectSource: null,
                IconPath: null,
                ProjectDescription: null,
                CommunitySourcePreference: SelectedCommunityDownloadSourceIndex),
            ResolveDownloadRequestTimeout(),
            _shellActionService.GetDownloadTransferOptions(),
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
                    AddFailureActivity(LT("shell.instance_select.shortcuts.import_pack.failure"), message);
                });
            }));
        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.TaskManager),
            LT("shell.instance_select.shortcuts.import_pack.task_queued", ("task", taskTitle)));
    }

    private void OpenInstanceSelectionFolder(InstanceSelectionFolderSnapshot folder)
    {
        if (string.IsNullOrWhiteSpace(folder.Directory))
        {
            AddFailureActivity(
                LT("shell.instance_select.open_folder.failure"),
                LT("shell.instance_select.open_folder.missing_path"));
            return;
        }

        if (!Directory.Exists(folder.Directory))
        {
            AddFailureActivity(
                LT("shell.instance_select.open_folder.failure"),
                LT("shell.instance_select.open_folder.not_found", ("path", folder.Directory)));
            return;
        }

        if (_shellActionService.TryRevealExternalTarget(folder.Directory, out var error))
        {
            AddActivity(LT("shell.instance_select.open_folder.activity"), folder.Directory);
            return;
        }

        AddFailureActivity(LT("shell.instance_select.open_folder.failure"), error ?? folder.Directory);
    }

    private void OpenInstanceSelectionEntry(InstanceSelectionSnapshot entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            return;
        }

        _shellActionService.PersistLocalValue("LaunchInstanceSelect", entry.Name);
        if (!_instanceComposition.Selection.HasSelection
            || !string.Equals(
                _instanceComposition.Selection.InstanceName,
                entry.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            ApplyOptimisticInstanceSelection(entry.Name);
            SetOptimisticLaunchInstanceName(entry.Name);
            var refreshVersion = System.Threading.Interlocked.Increment(ref _instanceSelectionRefreshVersion);
            QueueSelectedInstanceStateRefresh(refreshVersion);
        }

        NavigateTo(
            new LauncherFrontendRoute(LauncherFrontendPageKey.InstanceSetup, LauncherFrontendSubpageKey.VersionOverall),
            LT("shell.instance_select.navigation.overview", ("name", entry.Name)));
    }

    private void ToggleInstanceSelectionFavorite(InstanceSelectionSnapshot entry)
    {
        try
        {
            var nextIsFavorite = !entry.IsStarred;
            _shellActionService.PersistInstanceValue(entry.Directory, "IsStar", nextIsFavorite);
            RefreshInstanceSelectionSurface();
            AddActivity(
                nextIsFavorite
                    ? LT("shell.instance_select.favorite.added_activity")
                    : LT("shell.instance_select.favorite.removed_activity"),
                entry.Name);
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.favorite.failure"), ex.Message);
        }
    }

    private async Task DeleteInstanceSelectionEntryAsync(InstanceSelectionSnapshot entry)
    {
        var activityTitle = LT("shell.instance_select.delete.activity");
        try
        {
            var showIndieWarning = _instanceComposition.Selection.HasSelection
                && string.Equals(_instanceComposition.Selection.InstanceDirectory, entry.Directory, GetPathComparison())
                && _instanceComposition.Selection.IsIndie;
            var outcome = await DeleteInstanceDirectoryAsync(
                activityTitle,
                entry.Name,
                entry.Directory,
                _instanceSelectionLauncherDirectory,
                showIndieWarning);
            if (outcome is null)
            {
                return;
            }

            HandleDeletedInstance(
                outcome.InstanceName,
                entry.Directory,
                activityTitle);
            if (outcome.IsPermanentDelete)
            {
                AddActivity(
                    activityTitle,
                    LT("shell.instance_select.delete.permanently_deleted", ("name", outcome.InstanceName)));
                return;
            }

            AddActivity(
                activityTitle,
                LT("shell.instance_select.delete.moved_to_trash", ("name", outcome.InstanceName), ("path", outcome.TrashDirectory)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(LT("shell.instance_select.delete.failure"), ex.Message);
        }
    }

    private InstanceSelectionSnapshot? BuildInstanceSelectionSnapshot(string directory, string selectedInstance)
    {
        if (!FrontendRuntimePaths.IsRecognizedInstanceDirectory(directory))
        {
            return null;
        }

        var name = Path.GetFileName(directory);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var instanceConfig = FrontendRuntimePaths.OpenInstanceConfigProvider(directory, createDirectoryIfMissing: false);
        var manifestPath = Path.Combine(directory, $"{name}.json");
        var manifest = ParseInstanceManifest(manifestPath);
        var tags = new List<string>();
        if (ReadValue(instanceConfig, "IsStar", false))
        {
            tags.Add(LT("shell.instance_select.tags.favorite"));
        }

        var category = MapInstanceCategory(ReadValue(instanceConfig, "DisplayType", 0));
        if (!string.IsNullOrWhiteSpace(category))
        {
            tags.Add(category);
        }

        if (manifest.LoaderLabel is not null)
        {
            tags.Add(manifest.LoaderLabel);
        }

        if (manifest.IsBroken)
        {
            tags.Add(LT("shell.instance_select.tags.broken"));
        }

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifest.VersionLabel))
        {
            subtitleParts.Add(manifest.VersionLabel);
        }

        if (!string.IsNullOrWhiteSpace(manifest.LoaderLabel))
        {
            subtitleParts.Add(manifest.LoaderLabel);
        }

        var customInfo = ReadValue(
            instanceConfig,
            "VersionArgumentInfo",
            ReadValue(instanceConfig, "CustomInfo", string.Empty));
        if (!string.IsNullOrWhiteSpace(customInfo))
        {
            subtitleParts.Add(customInfo.Trim());
        }

        var subtitle = subtitleParts.Count == 0
            ? LT("shell.instance_select.subtitle.unknown_version")
            : string.Join(" • ", subtitleParts);
        var detail = LT(
            "shell.instance_select.detail.last_modified",
            ("path", directory),
            ("time", Directory.GetLastWriteTime(directory).ToString("yyyy-MM-dd HH:mm")));

        return new InstanceSelectionSnapshot(
            name,
            subtitle,
            detail,
            tags,
            string.Equals(name, selectedInstance, StringComparison.OrdinalIgnoreCase),
            ReadValue(instanceConfig, "IsStar", false),
            manifest.IsBroken,
            directory,
            ReadValue(instanceConfig, "DisplayType", 0),
            manifest.VersionLabel,
            manifest.LoaderLabel,
            string.IsNullOrWhiteSpace(customInfo) ? null : customInfo.Trim(),
            ReadValue(instanceConfig, "LogoCustom", false),
            ReadValue(instanceConfig, "Logo", string.Empty));
    }

    private static bool MatchesInstanceSelectionQuery(InstanceSelectionSnapshot entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Detail.Contains(query, StringComparison.CurrentCultureIgnoreCase)
               || entry.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private InstanceSelectEntryViewModel CreateInstanceSelectionEntry(InstanceSelectionSnapshot entry)
    {
        var icon = ResolveInstanceSelectionBitmap(entry);
        var displayTags = BuildInstanceSelectionDisplayTags(entry);
        var subtitle = BuildInstanceSelectionSubtitle(entry, displayTags);

        return new InstanceSelectEntryViewModel(
            entry.Name,
            subtitle,
            entry.Detail,
            displayTags,
            entry.IsSelected,
            entry.IsStarred,
            icon,
            entry.IsSelected
                ? LT("shell.instance_select.entry.current")
                : LT("shell.instance_select.entry.set_as_launch"),
            LT("shell.instance_select.entry.favorite_tooltip"),
            LT("shell.instance_select.entry.open_folder_tooltip"),
            LT("shell.instance_select.entry.delete_tooltip"),
            LT("shell.instance_select.entry.open_settings_tooltip"),
            new ActionCommand(() => SelectInstanceAndCloseSelection(entry)),
            new ActionCommand(() => OpenInstanceSelectionEntry(entry)),
            new ActionCommand(() => ToggleInstanceSelectionFavorite(entry)),
            new ActionCommand(() =>
            {
                if (_shellActionService.TryOpenExternalTarget(entry.Directory, out var error))
                {
                    AddActivity(LT("shell.instance_select.entry.open_instance_directory"), entry.Directory);
                }
                else
                {
                    AddFailureActivity(LT("shell.instance_select.entry.open_instance_directory_failure"), error ?? entry.Directory);
                }
            }),
            new ActionCommand(() => _ = DeleteInstanceSelectionEntryAsync(entry)));
    }

    private IReadOnlyList<InstanceSelectionGroupViewModel> BuildInstanceSelectionGroups(
        IReadOnlyList<InstanceSelectionSnapshot> entries,
        IReadOnlyDictionary<string, bool> groupExpansionStates)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var groups = new List<InstanceSelectionGroupViewModel>();
        var favorites = entries.Where(entry => entry.IsStarred).ToArray();
        if (favorites.Length > 0)
        {
            groups.Add(CreateInstanceSelectionGroup(
                LT("shell.instance_select.groups.favorites"),
                favorites,
                groupExpansionStates,
                isExpandedByDefault: true,
                isCountSuppressed: true));
        }

        foreach (var bucket in entries.GroupBy(GetInstanceSelectionBaseGroupKey))
        {
            var orderedEntries = bucket
                .OrderByDescending(entry => entry.IsSelected)
                .ThenByDescending(entry => entry.IsStarred)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            groups.Add(CreateInstanceSelectionGroup(
                ResolveInstanceSelectionGroupTitle(bucket.Key, orderedEntries),
                orderedEntries,
                groupExpansionStates,
                isExpandedByDefault: bucket.Key is not "error" and not "rarely-used" and not "hidden"));
        }

        return groups
            .OrderBy(group => GetInstanceSelectionGroupPriority(group.Title))
            .ThenBy(group => group.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private InstanceSelectionGroupViewModel CreateInstanceSelectionGroup(
        string title,
        IReadOnlyList<InstanceSelectionSnapshot> entries,
        IReadOnlyDictionary<string, bool> groupExpansionStates,
        bool isExpandedByDefault,
        bool isCountSuppressed = false)
    {
        return new InstanceSelectionGroupViewModel(
            title,
            isCountSuppressed ? title : LT("shell.instance_select.groups.header", ("title", title), ("count", entries.Count)),
            entries.Select(CreateInstanceSelectionEntry).ToArray(),
            groupExpansionStates.TryGetValue(title, out var isExpanded)
                ? isExpanded
                : isExpandedByDefault);
    }

    private IReadOnlyList<string> BuildInstanceSelectionDisplayTags(InstanceSelectionSnapshot entry)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.VersionLabel))
        {
            tags.Add(entry.VersionLabel);
        }

        if (!string.IsNullOrWhiteSpace(entry.LoaderLabel))
        {
            tags.Add(entry.LoaderLabel);
        }

        if (entry.IsBroken)
        {
            tags.Add(LT("shell.instance_select.tags.broken"));
        }
        else if (entry.DisplayType == 4)
        {
            tags.Add(LT("shell.instance_select.tags.rarely_used"));
        }

        return tags;
    }

    private static string BuildInstanceSelectionSubtitle(InstanceSelectionSnapshot entry, IReadOnlyList<string> displayTags)
    {
        if (!string.IsNullOrWhiteSpace(entry.CustomInfo))
        {
            return entry.CustomInfo!;
        }

        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            var subtitle = entry.Subtitle
                .Replace("Minecraft ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("•", " ", StringComparison.Ordinal)
                .Trim();
            foreach (var tag in displayTags)
            {
                subtitle = subtitle.Replace(tag, string.Empty, StringComparison.CurrentCultureIgnoreCase).Trim();
            }

            return string.Join(" ", subtitle.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return string.Empty;
    }

    private static Bitmap? ResolveInstanceSelectionBitmap(InstanceSelectionSnapshot entry)
    {
        if (entry.IsCustomLogo)
        {
            var customPath = Path.Combine(entry.Directory, "PCL", "Logo.png");
            if (File.Exists(customPath))
            {
                return new Bitmap(customPath);
            }
        }

        var mappedLogo = MapStoredLogoPath(entry.RawLogoPath);
        if (!string.IsNullOrWhiteSpace(mappedLogo) && File.Exists(mappedLogo))
        {
            return new Bitmap(mappedLogo);
        }

        return LoadLauncherBitmap("Images", "Blocks", DetermineInstanceSelectionIconName(entry));
    }

    private static string? MapStoredLogoPath(string rawLogoPath)
    {
        if (string.IsNullOrWhiteSpace(rawLogoPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(rawLogoPath.Replace("pack://application:,,,/images/Blocks/", string.Empty, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.Combine(LauncherRootDirectory, "Images", "Blocks", fileName);
    }

    private static string DetermineInstanceSelectionIconName(InstanceSelectionSnapshot entry)
    {
        if (entry.IsBroken)
        {
            return "RedstoneBlock.png";
        }

        return entry.LoaderLabel switch
        {
            "Forge" => "Anvil.png",
            "NeoForge" => "NeoForge.png",
            "Cleanroom" => "Cleanroom.png",
            "Fabric" or "Legacy Fabric" => "Fabric.png",
            "Quilt" => "Quilt.png",
            "LiteLoader" => "GrassPath.png",
            "LabyMod" => "LabyMod.png",
            _ => "Grass.png"
        };
    }

    private static string GetInstanceSelectionBaseGroupKey(InstanceSelectionSnapshot entry)
    {
        if (entry.IsBroken)
        {
            return "error";
        }

        return entry.DisplayType switch
        {
            3 => "hidden",
            4 => "rarely-used",
            _ when !string.IsNullOrWhiteSpace(entry.LoaderLabel) => $"loader:{NormalizeInstanceSelectionLoader(entry.LoaderLabel!)}",
            2 => "api",
            _ => "normal"
        };
    }

    private string ResolveInstanceSelectionGroupTitle(string key, IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        if (key.StartsWith("loader:", StringComparison.Ordinal))
        {
            return ResolveSingleLoaderInstanceGroupTitle(key["loader:".Length..]);
        }

        return key switch
        {
            "api" => ResolveApiInstanceGroupTitle(entries),
            "error" => LT("shell.instance_select.groups.error"),
            "hidden" => LT("shell.instance_select.groups.hidden"),
            "rarely-used" => LT("shell.instance_select.groups.rarely_used"),
            _ => LT("shell.instance_select.groups.regular")
        };
    }

    private string ResolveSingleLoaderInstanceGroupTitle(string loaderKey)
    {
        return loaderKey switch
        {
            "forge" => LT("shell.instance_select.groups.loaders.forge"),
            "neoforge" => LT("shell.instance_select.groups.loaders.neoforge"),
            "cleanroom" => LT("shell.instance_select.groups.loaders.cleanroom"),
            "labymod" => LT("shell.instance_select.groups.loaders.labymod"),
            "liteloader" => LT("shell.instance_select.groups.loaders.liteloader"),
            "quilt" => LT("shell.instance_select.groups.loaders.quilt"),
            "legacy-fabric" => LT("shell.instance_select.groups.loaders.legacy_fabric"),
            _ => LT("shell.instance_select.groups.loaders.fabric")
        };
    }

    private static string NormalizeInstanceSelectionLoader(string loaderLabel)
    {
        return loaderLabel.Trim() switch
        {
            "Forge" => "forge",
            "NeoForge" => "neoforge",
            "Cleanroom" => "cleanroom",
            "LabyMod" => "labymod",
            "LiteLoader" => "liteloader",
            "Quilt" => "quilt",
            "Legacy Fabric" => "legacy-fabric",
            _ => "fabric"
        };
    }

    private string ResolveApiInstanceGroupTitle(IReadOnlyList<InstanceSelectionSnapshot> entries)
    {
        var loaderLabels = entries
            .Select(entry => entry.LoaderLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (loaderLabels.Length > 1)
        {
            return LT("shell.instance_select.groups.api_installable");
        }

        if (loaderLabels.Length == 1)
        {
            return loaderLabels[0] switch
            {
                "Forge" => LT("shell.instance_select.groups.loaders.forge"),
                "NeoForge" => LT("shell.instance_select.groups.loaders.neoforge"),
                "Cleanroom" => LT("shell.instance_select.groups.loaders.cleanroom"),
                "LabyMod" => LT("shell.instance_select.groups.loaders.labymod"),
                "LiteLoader" => LT("shell.instance_select.groups.loaders.liteloader"),
                "Quilt" => LT("shell.instance_select.groups.loaders.quilt"),
                _ => LT("shell.instance_select.groups.loaders.fabric")
            };
        }

        return LT("shell.instance_select.groups.api_installable");
    }

    private int GetInstanceSelectionGroupPriority(string title)
    {
        return title switch
        {
            var value when value == LT("shell.instance_select.groups.favorites") => 0,
            var value when value == LT("shell.instance_select.groups.regular") => 1,
            var value when value == LT("shell.instance_select.groups.loaders.fabric") => 2,
            var value when value == LT("shell.instance_select.groups.loaders.forge") => 3,
            var value when value == LT("shell.instance_select.groups.loaders.neoforge") => 4,
            var value when value == LT("shell.instance_select.groups.loaders.quilt") => 5,
            var value when value == LT("shell.instance_select.groups.loaders.liteloader") => 6,
            var value when value == LT("shell.instance_select.groups.loaders.cleanroom") => 7,
            var value when value == LT("shell.instance_select.groups.loaders.labymod") => 8,
            var value when value == LT("shell.instance_select.groups.api_installable") => 9,
            var value when value == LT("shell.instance_select.groups.rarely_used") => 10,
            var value when value == LT("shell.instance_select.groups.error") => 11,
            var value when value == LT("shell.instance_select.groups.hidden") => 12,
            _ => 99
        };
    }

    private InstanceSelectionFolderEntryViewModel CreateInstanceSelectionFolderEntry(InstanceSelectionFolderSnapshot folder)
    {
        return new InstanceSelectionFolderEntryViewModel(
            folder.Label,
            folder.Directory,
            string.Equals(folder.Directory, _instanceSelectionLauncherDirectory, GetPathComparison()),
            FrontendIconCatalog.Folder.Data,
            LT("shell.instance_select.folder.open_tooltip"),
            LT("shell.instance_select.folder.remove_tooltip"),
            new ActionCommand(() =>
            {
                if (string.Equals(folder.Directory, _instanceSelectionLauncherDirectory, GetPathComparison()))
                {
                    AddActivity(LT("shell.instance_select.folder.current_activity"), folder.Directory);
                    return;
                }

                RefreshSelectedLauncherFolderSmoothly(
                    folder.StoredPath,
                    folder.Directory,
                    LT("shell.instance_select.folder.switched", ("directory", folder.Directory)));
            }),
            new ActionCommand(() => OpenInstanceSelectionFolder(folder)),
            folder.IsPersisted ? new ActionCommand(() => _ = DeleteInstanceSelectionFolderAsync(folder)) : null);
    }

    private static InstanceSelectionShortcutEntryViewModel CreateInstanceSelectionShortcutEntry(
        string title,
        string description,
        string iconPath,
        ActionCommand command)
    {
        return new InstanceSelectionShortcutEntryViewModel(title, description, iconPath, command);
    }

    private static InstanceManifestSnapshot ParseInstanceManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return new InstanceManifestSnapshot(string.Empty, null, true);
        }

        var profile = FrontendVersionManifestInspector.ReadProfileFromManifestPath(manifestPath);
        return new InstanceManifestSnapshot(
            profile.VanillaVersion,
            profile.PrimaryLoaderName,
            !profile.IsManifestValid);
    }

    private string MapInstanceCategory(int displayType)
    {
        return displayType switch
        {
            1 => LT("shell.instance_select.tags.favorite"),
            2 => LT("shell.instance_select.tags.api"),
            3 => LT("shell.instance_select.tags.hidden"),
            4 => LT("shell.instance_select.tags.rarely_used"),
            _ => string.Empty
        };
    }

}
