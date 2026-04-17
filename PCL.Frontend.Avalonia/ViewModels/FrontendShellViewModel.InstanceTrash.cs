using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Input;
using PCL.Core.App.Essentials;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private KeyModifiers _activeKeyboardModifiers;

    internal void UpdateKeyboardModifiers(KeyModifiers modifiers)
    {
        _activeKeyboardModifiers = modifiers;
    }

    private bool IsPermanentInstanceDeleteRequested()
    {
        return (_activeKeyboardModifiers & KeyModifiers.Shift) == KeyModifiers.Shift;
    }

    private string ResolveInstanceVersionTrashDirectory(string launcherDirectory)
    {
        return Path.Combine(launcherDirectory, ".pcl-trash", "versions");
    }

    private void OpenInstanceSelectionTrashDirectory()
    {
        var launcherDirectory = string.IsNullOrWhiteSpace(_instanceSelectionLauncherDirectory)
            ? _instanceComposition.Selection.LauncherDirectory
            : _instanceSelectionLauncherDirectory;
        var targetDirectory = string.IsNullOrWhiteSpace(launcherDirectory)
            ? string.Empty
            : ResolveInstanceVersionTrashDirectory(launcherDirectory);
        OpenInstanceDirectoryTarget(
            T("instance.trash.activities.open_directory"),
            targetDirectory,
            T("instance.trash.messages.launcher_missing"));
    }

    private async Task<InstanceDeleteOutcome?> DeleteInstanceDirectoryAsync(
        string activityTitle,
        string instanceName,
        string instanceDirectory,
        string launcherDirectory,
        bool showIndieWarning)
    {
        var isPermanentDelete = IsPermanentInstanceDeleteRequested();
        var confirmed = await _shellActionService.ConfirmAsync(
            T("instance.trash.dialogs.delete.title"),
            BuildInstanceDeleteConfirmationMessage(instanceName, isPermanentDelete, showIndieWarning),
            isPermanentDelete
                ? T("instance.trash.dialogs.delete.confirm_permanent")
                : T("instance.trash.dialogs.delete.confirm_recycle"),
            isDanger: showIndieWarning || isPermanentDelete);
        if (!confirmed)
        {
            AddActivity(activityTitle, T("instance.trash.messages.delete_canceled"));
            return null;
        }

        if (isPermanentDelete)
        {
            await PermanentlyDeleteInstanceDirectoryAsync(instanceDirectory).ConfigureAwait(false);
            return new InstanceDeleteOutcome(instanceName, true, null);
        }

        var trashDirectory = ResolveInstanceVersionTrashDirectory(launcherDirectory);
        Directory.CreateDirectory(trashDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var targetDirectory = GetUniquePath(Path.Combine(trashDirectory, $"{instanceName}-{timestamp}"));
        Directory.Move(instanceDirectory, targetDirectory);

        var metadataPath = Path.Combine(targetDirectory, ".pcl-trash.json");
        File.WriteAllText(
            metadataPath,
            JsonSerializer.Serialize(new
            {
                instanceName,
                originalPath = instanceDirectory,
                deletedAt = DateTimeOffset.Now
            }, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        return new InstanceDeleteOutcome(instanceName, false, trashDirectory);
    }

    private void HandleDeletedInstance(
        string deletedInstanceName,
        string deletedInstanceDirectory,
        string activityTitle)
    {
        var deletedSelectedInstance = _instanceComposition.Selection.HasSelection
            && (string.Equals(_instanceComposition.Selection.InstanceDirectory, deletedInstanceDirectory, GetPathComparison())
                || string.Equals(_instanceComposition.Selection.InstanceName, deletedInstanceName, StringComparison.OrdinalIgnoreCase));

        if (deletedSelectedInstance)
        {
            _shellActionService.PersistLocalValue("LaunchInstanceSelect", string.Empty);
        }

        RefreshLaunchState();
        RefreshInstanceSelectionSurface();
        RefreshInstanceSelectionRouteMetadata();

        if (deletedSelectedInstance && _currentRoute.Page == LauncherFrontendPageKey.InstanceSetup)
        {
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                activityTitle,
                RouteNavigationBehavior.Reset);
        }
    }

    private string BuildInstanceDeleteConfirmationMessage(
        string instanceName,
        bool isPermanentDelete,
        bool showIndieWarning)
    {
        var builder = new StringBuilder()
            .Append(
                isPermanentDelete
                    ? T(
                        "instance.trash.messages.confirm_permanent",
                        ("instance_name", instanceName))
                    : T(
                        "instance.trash.messages.confirm_recycle",
                        ("instance_name", instanceName)));

        if (showIndieWarning)
        {
            builder
                .AppendLine()
                .Append(T("instance.trash.messages.confirm_indie_warning"));
        }

        if (!isPermanentDelete)
        {
            builder
                .AppendLine()
                .Append(T("instance.trash.messages.confirm_recycle_note"));
        }

        return builder.ToString();
    }

    private static async Task PermanentlyDeleteInstanceDirectoryAsync(string instanceDirectory)
    {
        if (string.IsNullOrWhiteSpace(instanceDirectory) || !Directory.Exists(instanceDirectory))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(instanceDirectory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                Directory.Delete(instanceDirectory, recursive: true);
                return;
            }
            catch when (attempt == 0)
            {
                await Task.Delay(300).ConfigureAwait(false);
            }
        }
    }

    private sealed record InstanceDeleteOutcome(
        string InstanceName,
        bool IsPermanentDelete,
        string? TrashDirectory);
}
