using System.IO;
using System.Text;
using System.Text.Json;
using Avalonia.Input;

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
        OpenInstanceDirectoryTarget("实例回收区", targetDirectory, "当前未找到启动目录。");
    }

    private async Task<InstanceDeleteOutcome?> DeleteInstanceDirectoryAsync(
        string instanceName,
        string instanceDirectory,
        string launcherDirectory,
        bool showIndieWarning)
    {
        var isPermanentDelete = IsPermanentInstanceDeleteRequested();
        var confirmed = await _shellActionService.ConfirmAsync(
            "实例删除确认",
            BuildInstanceDeleteConfirmationMessage(instanceName, isPermanentDelete, showIndieWarning),
            isPermanentDelete ? "永久删除" : "移入回收区",
            isDanger: showIndieWarning || isPermanentDelete);
        if (!confirmed)
        {
            AddActivity("删除实例", "已取消删除。");
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

    private static string BuildInstanceDeleteConfirmationMessage(
        string instanceName,
        bool isPermanentDelete,
        bool showIndieWarning)
    {
        var builder = new StringBuilder()
            .Append("你确定要")
            .Append(isPermanentDelete ? "永久" : string.Empty)
            .Append("删除实例 ")
            .Append(instanceName)
            .Append(" 吗？");

        if (showIndieWarning)
        {
            builder
                .AppendLine()
                .Append("由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！");
        }

        if (!isPermanentDelete)
        {
            builder
                .AppendLine()
                .Append("实例会先移入实例回收区，便于你手动清理或恢复。");
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
