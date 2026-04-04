using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceOverviewName = "Modern Fabric Demo";
    private string _instanceOverviewSubtitle = "Fabric 0.16.9 / Minecraft 1.21.1 / 独立实例";
    private Bitmap? _instanceOverviewSelectedIcon;
    private int _selectedInstanceOverviewIconIndex;
    private int _selectedInstanceOverviewCategoryIndex;
    private bool _isInstanceOverviewStarred = true;
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewIconOptions =
    [
        new DownloadResourceFilterOptionViewModel("自动", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("圆石", "CobbleStone.png"),
        new DownloadResourceFilterOptionViewModel("命令方块", "CommandBlock.png"),
        new DownloadResourceFilterOptionViewModel("金块", "GoldBlock.png"),
        new DownloadResourceFilterOptionViewModel("草方块", "Grass.png"),
        new DownloadResourceFilterOptionViewModel("土径", "GrassPath.png"),
        new DownloadResourceFilterOptionViewModel("铁砧", "Anvil.png"),
        new DownloadResourceFilterOptionViewModel("红石块", "RedstoneBlock.png"),
        new DownloadResourceFilterOptionViewModel("红石灯（开）", "RedstoneLampOn.png"),
        new DownloadResourceFilterOptionViewModel("红石灯（关）", "RedstoneLampOff.png"),
        new DownloadResourceFilterOptionViewModel("鸡蛋", "Egg.png"),
        new DownloadResourceFilterOptionViewModel("布料（Fabric）", "Fabric.png"),
        new DownloadResourceFilterOptionViewModel("方格（Quilt）", "Quilt.png"),
        new DownloadResourceFilterOptionViewModel("狐狸（NeoForge）", "NeoForge.png"),
        new DownloadResourceFilterOptionViewModel("Cleanroom", "Cleanroom.png")
    ];
    private readonly IReadOnlyList<DownloadResourceFilterOptionViewModel> _instanceOverviewCategoryOptions =
    [
        new DownloadResourceFilterOptionViewModel("自动", "自动"),
        new DownloadResourceFilterOptionViewModel("从实例列表中隐藏", "从实例列表中隐藏"),
        new DownloadResourceFilterOptionViewModel("可安装 Mod 的实例", "可安装 Mod 的实例"),
        new DownloadResourceFilterOptionViewModel("常规实例", "常规实例"),
        new DownloadResourceFilterOptionViewModel("不常用实例", "不常用实例"),
        new DownloadResourceFilterOptionViewModel("愚人节版本", "愚人节版本")
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

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewIconOptions => _instanceOverviewIconOptions;

    public IReadOnlyList<DownloadResourceFilterOptionViewModel> InstanceOverviewCategoryOptions => _instanceOverviewCategoryOptions;

    public int SelectedInstanceOverviewIconIndex
    {
        get => _selectedInstanceOverviewIconIndex;
        set
        {
            var nextValue = Math.Clamp(value, 0, InstanceOverviewIconOptions.Count - 1);
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
            var nextValue = Math.Clamp(value, 0, InstanceOverviewCategoryOptions.Count - 1);
            if (SetProperty(ref _selectedInstanceOverviewCategoryIndex, nextValue))
            {
                RaisePropertyChanged(nameof(InstanceOverviewCategoryLabel));
            }
        }
    }

    public string InstanceOverviewCategoryLabel => InstanceOverviewCategoryOptions[SelectedInstanceOverviewCategoryIndex].Label;

    public string InstanceOverviewFavoriteButtonText => _isInstanceOverviewStarred ? "移出收藏夹" : "加入收藏夹";

    public bool HasInstanceOverviewInfoEntries => InstanceOverviewInfoEntries.Count > 0;

    public ActionCommand RenameInstanceCommand => new(() => AddActivity("修改实例名", "Would open the rename-instance dialog."));

    public ActionCommand EditInstanceDescriptionCommand => new(() => AddActivity("修改实例描述", "Would open the instance description dialog."));

    public ActionCommand ToggleInstanceFavoriteCommand => new(ToggleInstanceFavorite);

    public ActionCommand OpenInstanceFolderCommand => new(() =>
        OpenInstanceTarget("实例文件夹", _instanceComposition.Selection.InstanceDirectory, "当前未选择实例。"));

    public ActionCommand OpenInstanceSavesFolderCommand => new(() =>
        OpenInstanceTarget(
            "存档文件夹",
            _instanceComposition.Selection.HasSelection ? Path.Combine(_instanceComposition.Selection.IndieDirectory, "saves") : string.Empty,
            "当前实例没有存档目录。"));

    public ActionCommand OpenInstanceModsFolderCommand => new(() =>
        OpenInstanceTarget(
            "Mod 文件夹",
            ResolveCurrentInstanceResourceDirectory("mods"),
            "当前实例没有 Mod 目录。"));

    public ActionCommand ExportInstanceScriptCommand => new(() => AddActivity("导出启动脚本", "Would export a launcher script for the current instance."));

    public ActionCommand TestInstanceCommand => new(() => AddActivity("测试游戏", "Would start a dry-run style launch for the selected instance."));

    public ActionCommand CheckInstanceFilesCommand => new(() => AddActivity("补全文件", "Would verify assets, libraries, and missing files for the instance."));

    public ActionCommand RestoreInstanceCommand => new(() => AddActivity("重置实例", "Would rebuild the instance from its install metadata without deleting saves."));

    public ActionCommand DeleteInstanceCommand => new(() => AddActivity("删除实例", "Would open the delete-instance confirmation flow."));

    public ActionCommand PatchInstanceCoreCommand => new(() => AddActivity("修补核心", "Would patch the instance core files for compatibility or repair."));

    private void InitializeInstanceOverviewSurface()
    {
        var overview = _instanceComposition.Overview;
        InstanceOverviewName = overview.Name;
        InstanceOverviewSubtitle = overview.Subtitle;
        _selectedInstanceOverviewIconIndex = Math.Clamp(overview.IconIndex, 0, InstanceOverviewIconOptions.Count - 1);
        _selectedInstanceOverviewCategoryIndex = Math.Clamp(overview.CategoryIndex, 0, InstanceOverviewCategoryOptions.Count - 1);
        _isInstanceOverviewStarred = overview.IsStarred;

        ReplaceItems(InstanceOverviewDisplayTags, overview.DisplayTags);
        ReplaceItems(
            InstanceOverviewInfoEntries,
            overview.InfoEntries.Select(entry => new KeyValueEntryViewModel(entry.Label, entry.Value)));

        InstanceOverviewSelectedIcon = LoadInstanceBitmap(
            overview.IconPath,
            "Images",
            "Blocks",
            InstanceOverviewIconOptions[_selectedInstanceOverviewIconIndex].FilterValue);
    }

    private void RefreshInstanceOverviewSurface()
    {
        if (!IsInstanceOverviewSurface)
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

    private void ToggleInstanceFavorite()
    {
        if (!_instanceComposition.Selection.HasSelection)
        {
            AddActivity("收藏实例", "当前未选择实例。");
            return;
        }

        _isInstanceOverviewStarred = !_isInstanceOverviewStarred;
        _shellActionService.PersistInstanceValue(_instanceComposition.Selection.InstanceDirectory, "IsStar", _isInstanceOverviewStarred);
        ReloadInstanceComposition();
        RaisePropertyChanged(nameof(InstanceOverviewFavoriteButtonText));
        AddActivity(_isInstanceOverviewStarred ? "加入收藏夹" : "移出收藏夹", _instanceComposition.Selection.InstanceName);
    }
}
