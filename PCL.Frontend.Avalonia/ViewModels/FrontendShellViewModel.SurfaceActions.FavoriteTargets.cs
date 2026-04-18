using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.App.Configuration.Storage;
using PCL.Frontend.Avalonia.Desktop.Dialogs;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
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
                await DeleteDownloadFavoriteTargetAsync(root, currentTarget, selectedIndex);
                return;
            default:
                AddFailureActivity(T("download.favorites.targets.manage.failed"), T("download.favorites.targets.manage.unknown_action", ("action_id", actionId)));
                return;
        }
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
        ReapplySelectedDownloadFavoriteTargetIndex(selectedIndex);
    }

    private void ReapplySelectedDownloadFavoriteTargetIndex(int selectedIndex)
    {
        var nextIndex = Math.Clamp(selectedIndex, 0, Math.Max(DownloadFavoriteTargetOptions.Count - 1, 0));

        // Force Avalonia to re-apply the effective selection after the ComboBox clears its index during item updates.
        _selectedDownloadFavoriteTargetIndex = -1;
        RaisePropertyChanged(nameof(SelectedDownloadFavoriteTargetIndex));
        SelectedDownloadFavoriteTargetIndex = nextIndex;
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

    private async Task DeleteDownloadFavoriteTargetAsync(JsonArray root, JsonObject target, int selectedIndex)
    {
        var targets = root.OfType<JsonObject>().ToArray();
        if (targets.Length <= 1)
        {
            var message = T("download.favorites.targets.delete.blocked_last");
            AddActivity(T("download.favorites.targets.delete.activity"), message);
            AvaloniaHintBus.Show(message, AvaloniaHintTheme.Info);
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

        var nextSelectedIndex = Math.Clamp(selectedIndex, 0, targets.Length - 2);
        root.Remove(target);
        PersistDownloadFavoriteTargetRoot(root, nextSelectedIndex);
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
}
