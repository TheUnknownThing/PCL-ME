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

internal sealed partial class FrontendShellViewModel
{

    private IReadOnlyList<CommunityProjectActionButtonViewModel> BuildCommunityProjectActionButtons()
    {
        var isFavorite = IsCommunityProjectFavorite();
        var favoriteIcon = isFavorite
            ? FrontendIconCatalog.FavoriteFilled
            : FrontendIconCatalog.FavoriteOutline;
        var buttons = new List<CommunityProjectActionButtonViewModel>();
        if (!string.IsNullOrWhiteSpace(CommunityProjectWebsite))
        {
            buttons.Add(new CommunityProjectActionButtonViewModel(
                CommunityProjectSource,
                CommunityProjectLinkIcon.Data,
                CommunityProjectLinkIcon.Scale,
                PclIconTextButtonColorState.Normal,
                CreateOpenTargetCommand(T("resource_detail.activities.open_project_homepage", ("project_title", CommunityProjectTitle)), CommunityProjectWebsite, CommunityProjectWebsite)));
        }
        else
        {
            buttons.Add(new CommunityProjectActionButtonViewModel(
                CommunityProjectSource,
                CommunityProjectLinkIcon.Data,
                CommunityProjectLinkIcon.Scale,
                PclIconTextButtonColorState.Normal,
                CreateIntentCommand(T("resource_detail.activities.view_source", ("project_title", CommunityProjectTitle)), CommunityProjectSource)));
        }

        buttons.Add(new CommunityProjectActionButtonViewModel(
            T("resource_detail.actions.mc_wiki"),
            CommunityProjectLinkIcon.Data,
            CommunityProjectLinkIcon.Scale,
            PclIconTextButtonColorState.Normal,
            CreateOpenTargetCommand(
                T("resource_detail.activities.open_mc_wiki", ("project_title", CommunityProjectTitle)),
                BuildCommunityProjectEncyclopediaUrl(CommunityProjectTitle),
                CommunityProjectTitle)));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            T("resource_detail.actions.copy_name"),
            CommunityProjectCopyIcon.Data,
            CommunityProjectCopyIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                T("resource_detail.actions.copy_name"),
                CommunityProjectTitle,
                T("resource_detail.copy_name.empty")))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            T("resource_detail.actions.copy_link"),
            CommunityProjectCopyIcon.Data,
            CommunityProjectCopyIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                T("resource_detail.actions.copy_link"),
                string.IsNullOrWhiteSpace(CommunityProjectWebsite)
                    ? FrontendCommunityProjectService.CreateCompDetailTarget(_communityProjectState.ProjectId)
                    : CommunityProjectWebsite,
                T("resource_detail.copy_link.empty")))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            T("resource_detail.actions.translate_description"),
            CommunityProjectTranslateIcon.Data,
            CommunityProjectTranslateIcon.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = CopyCommunityProjectTextAsync(
                T("resource_detail.actions.translate_description"),
                BuildCommunityProjectDescriptionCopyText(),
                T("resource_detail.translate_description.empty")))));
        buttons.Add(new CommunityProjectActionButtonViewModel(
            T("download.favorites.actions.favorite_to"),
            FrontendIconCatalog.FolderAdd.Data,
            FrontendIconCatalog.FolderAdd.Scale,
            PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = FavoriteCurrentCommunityProjectToTargetAsync())));
        buttons.Add(CreateCommunityProjectFavoriteActionButton());
        return buttons;
    }

    private CommunityProjectActionButtonViewModel CreateCommunityProjectFavoriteActionButton()
    {
        var isFavorite = IsCommunityProjectFavorite();
        var icon = isFavorite ? CommunityProjectFavoriteFilledIcon : CommunityProjectFavoriteOutlineIcon;
        return new CommunityProjectActionButtonViewModel(
            T("download.favorites.actions.favorite"),
            icon.Data,
            icon.Scale,
            isFavorite ? PclIconTextButtonColorState.Highlight : PclIconTextButtonColorState.Normal,
            new ActionCommand(() => _ = ToggleCommunityProjectFavoriteAsync()));
    }

    private ActionCommand CreateProjectSectionCommand(FrontendDownloadCatalogEntry entry)
    {
        if (FrontendCommunityProjectService.TryParseCompDetailTarget(entry.Target, out var projectId))
        {
            return new ActionCommand(() => OpenCommunityProjectDetail(projectId, entry.Title));
        }

        return string.IsNullOrWhiteSpace(entry.Target)
            ? CreateIntentCommand(entry.Title, entry.Info)
            : CreateOpenTargetCommand(T("resource_detail.activities.open_project_content", ("entry_title", entry.Title)), entry.Target, entry.Target);
    }

    private async Task CopyCommunityProjectTextAsync(string title, string text, string emptyMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            AddActivity(title, emptyMessage);
            return;
        }

        try
        {
            await _shellActionService.SetClipboardTextAsync(text);
            AddActivity(title, T("resource_detail.copy.messages.completed"));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("resource_detail.copy.messages.failed", ("title", title)), ex.Message);
        }
    }

    private string BuildCommunityProjectDescriptionCopyText()
    {
        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[] { CommunityProjectSummary, CommunityProjectDescription }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private bool IsCommunityProjectFavorite()
    {
        if (string.IsNullOrWhiteSpace(_communityProjectState.ProjectId))
        {
            return false;
        }

        try
        {
            var provider = _shellActionService.RuntimePaths.OpenSharedConfigProvider();
            var raw = provider.Exists("CompFavorites")
                ? SafeReadSharedValue(provider, "CompFavorites", "[]")
                : "[]";
            var root = ParseCommunityProjectFavoriteTargets(raw);
            var target = GetSelectedDownloadFavoriteTarget(root);
            return EnsureCommunityProjectFavoriteArray(target)
                .Select(GetCommunityProjectFavoriteId)
                .Any(value => string.Equals(value, _communityProjectState.ProjectId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private JsonArray ParseCommunityProjectFavoriteTargets(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonNode.Parse(raw);
            if (parsed is JsonArray rootArray)
            {
                if (rootArray.Any(node => node is JsonObject))
                {
                    return rootArray;
                }

                // Migrate the old flat string-array format into the default target format.
                return
                [
                    new JsonObject
                    {
                        ["Name"] = T("download.favorites.targets.default_name"),
                        ["Id"] = "default",
                        ["Favs"] = new JsonArray(rootArray.Select(node => node?.DeepClone()).ToArray()),
                        ["Notes"] = new JsonObject()
                    }
                ];
            }
        }
        catch
        {
            // Ignore malformed favorite payloads and rebuild a default structure.
        }

        return [];
    }

    private JsonObject EnsureCommunityProjectFavoriteTarget(JsonArray root)
    {
        var existing = root.OfType<JsonObject>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new JsonObject
        {
            ["Name"] = T("download.favorites.targets.default_name"),
            ["Id"] = "default",
            ["Favs"] = new JsonArray(),
            ["Notes"] = new JsonObject()
        };
        root.Add(created);
        return created;
    }

    private static JsonArray EnsureCommunityProjectFavoriteArray(JsonObject target)
    {
        if (target["Favs"] is JsonArray favorites)
        {
            return favorites;
        }

        if (target["Favorites"] is JsonArray legacyFavorites)
        {
            target["Favs"] = legacyFavorites;
            target.Remove("Favorites");
            return legacyFavorites;
        }

        var created = new JsonArray();
        target["Favs"] = created;
        return created;
    }

    private static string SafeReadSharedValue(JsonFileProvider provider, string key, string fallback)
    {
        try
        {
            return provider.Get<string>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static string? GetCommunityProjectFavoriteId(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCommunityProjectEncyclopediaUrl(string title)
    {
        return $"https://www.mcmod.cn/s?key={Uri.EscapeDataString(title)}";
    }

}
