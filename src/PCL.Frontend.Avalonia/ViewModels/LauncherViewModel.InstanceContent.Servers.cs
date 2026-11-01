using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media.Imaging;
using fNbt;
using PCL.Core.App.Essentials;
using PCL.Core.Minecraft;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.Panes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private void RefreshInstanceServerEntries()
    {
        ReplaceItems(
            InstanceServerEntries,
            _instanceComposition.Server.Entries
                .Select(CreateInstanceServerEntry));
    }

    private InstanceServerEntryViewModel CreateInstanceServerEntry(FrontendInstanceServerEntry entry)
    {
        InstanceServerEntryViewModel? viewModel = null;
        viewModel = new InstanceServerEntryViewModel(
            entry.Index,
            entry.Title,
            entry.Address,
            LoadLauncherBitmap("Images", "Backgrounds", "server_bg.png"),
            LoadLauncherBitmap("Images", "Icons", "DefaultServer.png"),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = RefreshInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = EditInstanceServerAddressAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = CopyInstanceServerAddressAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    _ = ConnectInstanceServerAsync(viewModel);
                }
            }),
            new ActionCommand(() =>
            {
                if (viewModel is not null)
                {
                    ViewInstanceServer(viewModel);
                }
            }));
        ApplyInstanceServerIdleState(viewModel, entry.Status);
        return viewModel;
    }

    private async Task RefreshAllInstanceServersAsync()
    {
        var activityTitle = SD("instance.content.server.actions.refresh_all");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        ReloadInstanceComposition();
        var entries = InstanceServerEntries.ToArray();
        if (entries.Length == 0)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_saved_servers"));
            return;
        }

        await Task.WhenAll(entries.Select(entry => RefreshInstanceServerAsync(entry, addActivity: false)));
        AddActivity(activityTitle, SD("instance.content.server.messages.refreshed_count", ("count", entries.Length)));
    }

    private async Task RefreshInstanceServerAsync(InstanceServerEntryViewModel entry, bool addActivity = true)
    {
        var activityTitle = SD("instance.content.server.actions.refresh");
        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            ApplyInstanceServerErrorState(entry, SD("instance.content.server.messages.address_empty"));
            if (addActivity)
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), BuildActivityDetail(entry.Title, SD("instance.content.server.messages.address_empty")));
            }

            return;
        }

        ApplyInstanceServerLoadingState(entry);

        try
        {
            var reachableAddress = await ResolveMinecraftServerQueryEndpointAsync(address, CancellationToken.None);
            using var queryService = global::PCL.Core.Link.McPing.McPingServiceFactory.CreateService(reachableAddress.Ip, reachableAddress.Port);
            var result = await queryService.PingAsync(CancellationToken.None);
            if (result is null)
            {
                throw new InvalidOperationException(SD("instance.content.server.messages.no_server_info"));
            }

            ApplyInstanceServerSuccessState(entry, result);
            if (addActivity)
            {
                AddActivity(activityTitle, BuildActivityDetail(entry.Title, $"{result.Players.Online}/{result.Players.Max}", $"{result.Latency}ms"));
            }
        }
        catch (Exception ex)
        {
            ApplyInstanceServerErrorState(entry, ex.Message);
            if (addActivity)
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), BuildActivityDetail(entry.Title, ex.Message));
            }
        }
    }

    private void ApplyInstanceServerIdleState(InstanceServerEntryViewModel entry, string status)
    {
        entry.StatusText = string.IsNullOrWhiteSpace(status) ? SD("instance.content.server.status.saved") : status;
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = "-/-";
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.click_to_refresh"));
    }

    private void ApplyInstanceServerLoadingState(InstanceServerEntryViewModel entry)
    {
        entry.StatusText = SD("instance.content.server.status.connecting");
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = SD("instance.content.server.status.connecting_short");
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.connecting"));
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyInstanceServerSuccessState(InstanceServerEntryViewModel entry, global::PCL.Core.Link.McPing.Model.McPingResult result)
    {
        entry.StatusText = SD("instance.content.server.status.online");
        entry.StatusBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerCount = $"{result.Players.Online}/{result.Players.Max}";
        entry.Latency = $"{result.Latency}ms";
        entry.LatencyBrush = GetMinecraftServerQueryLatencyBrush(result.Latency);
        entry.PlayerTooltip = result.Players.Samples?.Any() == true
            ? string.Join(Environment.NewLine, result.Players.Samples.Select(sample => sample.Name))
            : null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(result.Description);
        entry.Logo = DecodeMinecraftServerQueryLogo(result.Favicon)
            ?? LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private void ApplyInstanceServerErrorState(InstanceServerEntryViewModel entry, string message)
    {
        entry.StatusText = SD("instance.content.server.status.connection_failed", ("message", message));
        entry.StatusBrush = global::Avalonia.Media.Brushes.Red;
        entry.PlayerCount = SD("instance.content.server.status.offline_short");
        entry.Latency = string.Empty;
        entry.LatencyBrush = global::Avalonia.Media.Brushes.White;
        entry.PlayerTooltip = null;
        entry.MotdLines = BuildMinecraftServerQueryMotdLines(SD("instance.content.server.motd.offline"));
        entry.Logo = LoadLauncherBitmap("Images", "Icons", "DefaultServer.png");
    }

    private async Task CopyInstanceServerAddressAsync(InstanceServerEntryViewModel entry)
    {
        var activityTitle = SD("instance.content.server.actions.copy_address");
        var address = (entry.Address ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_copyable_address", ("entry_title", entry.Title)));
            return;
        }

        try
        {
            await _launcherActionService.SetClipboardTextAsync(address);
            AddActivity(activityTitle, SD("instance.content.server.messages.copied_address", ("entry_title", entry.Title), ("address", address)));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task ConnectInstanceServerAsync(InstanceServerEntryViewModel entry)
    {
        var activityTitle = SD("instance.content.server.actions.connect");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        var address = (entry.Address ?? string.Empty).Trim().Replace("：", ":");
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_connectable_address", ("entry_title", entry.Title)));
            return;
        }

        try
        {
            InstanceServerAutoJoin = address;
            RefreshLaunchState();
            NavigateTo(
                new LauncherFrontendRoute(LauncherFrontendPageKey.Launch),
                SD("instance.content.server.messages.launch_route_prepared", ("entry_title", entry.Title)));
            await HandleLaunchRequestedAsync();
            AddActivity(activityTitle, BuildActivityDetail(entry.Title, address));
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
        }
    }

    private async Task EditInstanceServerAddressAsync(InstanceServerEntryViewModel entry)
    {
        var activityTitle = SD("instance.content.server.actions.edit_address");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        var currentAddress = (entry.Address ?? string.Empty).Trim();
        string? resolvedAddress;
        try
        {
            resolvedAddress = await _launcherActionService.PromptForTextAsync(
                SD("instance.content.server.dialogs.edit.title"),
                SD("instance.content.server.dialogs.edit.address_prompt"),
                currentAddress,
                activityTitle);
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (resolvedAddress is null)
        {
            return;
        }

        var address = resolvedAddress.Trim();
        if (string.IsNullOrWhiteSpace(address))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.address_required"));
            return;
        }

        if (string.Equals(currentAddress, address, StringComparison.Ordinal))
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.address_unchanged", ("entry_title", entry.Title)));
            return;
        }

        var serversPath = Path.Combine(_instanceComposition.Selection.IndieDirectory, "servers.dat");
        NbtList? serverList;
        try
        {
            serverList = await Task.Run(() => File.Exists(serversPath)
                ? LoadInstanceServerList(serversPath)
                : null);
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.read_list_failed", ("error", ex.Message)));
            return;
        }

        if (serverList is null
            || entry.SourceIndex < 0
            || entry.SourceIndex >= serverList.Count
            || serverList[entry.SourceIndex] is not NbtCompound server)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.server_not_found"));
            return;
        }

        try
        {
            var addressTag = server.Get<NbtString>("ip");
            if (addressTag is null)
            {
                server.Add(new NbtString("ip", address));
            }
            else
            {
                addressTag.Value = address;
            }
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.update_list_failed", ("error", ex.Message)));
            return;
        }

        try
        {
            if (!await Task.Run(() => TryWriteInstanceServerList(serversPath, (NbtList)serverList.Clone())))
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
                return;
            }
        }
        catch
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
            return;
        }

        entry.Address = address;
        AddActivity(activityTitle, SD("instance.content.server.messages.address_updated", ("entry_title", entry.Title), ("address", address)));
    }

    private async Task AddInstanceServerAsync()
    {
        var activityTitle = SD("instance.content.server.actions.add");
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity(activityTitle, SD("instance.content.server.messages.no_instance_selected"));
            return;
        }

        var serverInfo = default((bool Success, string Name, string Address, string? Activity));
        try
        {
            serverInfo = await PromptForNewInstanceServerAsync();
        }
        catch (Exception ex)
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), ex.Message);
            return;
        }

        if (!serverInfo.Success)
        {
            if (!string.IsNullOrWhiteSpace(serverInfo.Activity))
            {
                AddActivity(activityTitle, serverInfo.Activity);
            }
            return;
        }

        var name = serverInfo.Name;
        var address = serverInfo.Address;

        var serversPath = Path.Combine(_instanceComposition.Selection.IndieDirectory, "servers.dat");
        NbtList serverList;
        try
        {
            serverList = File.Exists(serversPath)
                ? LoadInstanceServerList(serversPath) ?? new NbtList("servers", NbtTagType.Compound)
                : new NbtList("servers", NbtTagType.Compound);
        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.read_list_failed", ("error", ex.Message)));
            return;
        }

        try
        {
            if (serverList.ListType == NbtTagType.Unknown)
            {
                serverList.ListType = NbtTagType.Compound;
            }

            serverList.Add(new NbtCompound
            {
                new NbtString("name", name),
                new NbtString("ip", address)
            });

        }
        catch (Exception ex)
        {
            AddFailureActivity(
                T("common.activities.failed", ("title", activityTitle)),
                SD("instance.content.server.messages.update_list_failed", ("error", ex.Message)));
            return;
        }

        try
        {
            var clonedServerList = (NbtList)serverList.Clone();
            if (!TryWriteInstanceServerList(serversPath, clonedServerList))
            {
                AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
                return;
            }
        }
        catch
        {
            AddFailureActivity(T("common.activities.failed", ("title", activityTitle)), SD("instance.content.server.messages.write_list_failed"));
            return;
        }

        ReloadInstanceComposition();
        AddActivity(activityTitle, SD("instance.content.server.messages.added", ("name", name), ("address", address)));
    }

    private async Task<(bool Success, string Name, string Address, string? Activity)> PromptForNewInstanceServerAsync()
    {
        var resolvedName = await _launcherActionService.PromptForTextAsync(
            SD("instance.content.server.dialogs.edit.title"),
            SD("instance.content.server.dialogs.edit.name_prompt"),
            SD("instance.content.server.dialogs.edit.name_default"));
        if (resolvedName is null)
        {
            return (false, string.Empty, string.Empty, null);
        }

        var resolvedAddress = await _launcherActionService.PromptForTextAsync(
            SD("instance.content.server.dialogs.edit.title"),
            SD("instance.content.server.dialogs.edit.address_prompt"));
        if (string.IsNullOrWhiteSpace(resolvedAddress))
        {
            return resolvedAddress is null
                ? (false, string.Empty, string.Empty, null)
                : (false, string.Empty, string.Empty, SD("instance.content.server.messages.address_required"));
        }

        return (
            true,
            string.IsNullOrWhiteSpace(resolvedName) ? SD("instance.content.server.dialogs.edit.name_default") : resolvedName.Trim(),
            resolvedAddress.Trim(),
            null);
    }

    private static NbtList? LoadInstanceServerList(string serversPath)
    {
        var file = new NbtFile();
        using var stream = File.OpenRead(serversPath);
        file.LoadFromStream(stream, NbtCompression.AutoDetect);
        return file.RootTag.Get<NbtList>("servers");
    }

    private static bool TryWriteInstanceServerList(string serversPath, NbtList serverList)
    {
        var directoryPath = Path.GetDirectoryName(serversPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        Directory.CreateDirectory(directoryPath);

        var rootTag = new NbtCompound { Name = string.Empty };
        rootTag.Add(serverList);

        var file = new NbtFile(rootTag);
        using var stream = File.Create(serversPath);
        file.SaveToStream(stream, NbtCompression.None);
        return true;
    }

    private void ViewInstanceServer(InstanceServerEntryViewModel entry)
    {
        OpenMinecraftServerInspector(entry.Address);
    }

}
