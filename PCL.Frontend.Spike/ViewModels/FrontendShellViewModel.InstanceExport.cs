using System.Collections.ObjectModel;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private string _instanceExportName = "Modern Fabric Demo";
    private string _instanceExportVersion = "1.0.0";
    private bool _instanceExportIncludeResources;
    private bool _instanceExportModrinthMode;

    public ObservableCollection<ExportOptionGroupViewModel> InstanceExportOptionGroups { get; } = [];

    public string InstanceExportName
    {
        get => _instanceExportName;
        set => SetProperty(ref _instanceExportName, value);
    }

    public string InstanceExportVersion
    {
        get => _instanceExportVersion;
        set => SetProperty(ref _instanceExportVersion, value);
    }

    public bool InstanceExportIncludeResources
    {
        get => _instanceExportIncludeResources;
        set
        {
            if (SetProperty(ref _instanceExportIncludeResources, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
            }
        }
    }

    public bool InstanceExportModrinthMode
    {
        get => _instanceExportModrinthMode;
        set
        {
            if (SetProperty(ref _instanceExportModrinthMode, value))
            {
                RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
            }
        }
    }

    public bool ShowInstanceExportIncludeWarning => InstanceExportIncludeResources;

    public bool ShowInstanceExportOptiFineWarning => InstanceExportModrinthMode;

    public bool HasInstanceExportOptionGroups => InstanceExportOptionGroups.Count > 0;

    private void InitializeInstanceExportSurface()
    {
        _instanceExportName = "Modern Fabric Demo";
        _instanceExportVersion = "1.0.0";
        _instanceExportIncludeResources = false;
        _instanceExportModrinthMode = false;

        ReplaceItems(InstanceExportOptionGroups,
        [
            CreateExportOptionGroup("游戏本体", string.Empty, true,
            [
                CreateExportOption("游戏本体设置", "键位、音量、视频设置等", true),
                CreateExportOption("游戏本体个人信息", "命令历史、已保存的快捷栏", false),
                CreateExportOption("OptiFine 设置", string.Empty, true)
            ]),
            CreateExportOptionGroup("Mod", "模组", true,
            [
                CreateExportOption("已禁用的 Mod", string.Empty, false),
                CreateExportOption("整合包重要数据", "脚本文件、内置资源包、数据包等", true),
                CreateExportOption("Mod 设置", string.Empty, true),
                CreateExportOption("已绘制的地图", "地图类 Mod 的地图、路标点等", false),
                CreateExportOption("JEI 个人信息", "物品收藏夹等", false),
                CreateExportOption("EMI 个人信息", "物品收藏夹、默认配方、历史记录等", false),
                CreateExportOption("帕秋莉手册个人信息", "教程书的已读记录、书签等", false)
            ]),
            CreateExportOptionGroup("资源包", "纹理包 / 材质包", true,
            [
                CreateExportOption("Faithful 32x", "经典风格资源包", true),
                CreateExportOption("Fresh Animations", "实体动作增强资源包", false)
            ]),
            CreateExportOptionGroup("光影包", string.Empty, true,
            [
                CreateExportOption("Complementary Reimagined", "当前演示实例中的主光影", true),
                CreateExportOption("BSL Shaders", "额外收藏的兼容光影", false)
            ]),
            CreateExportOptionGroup("截图", string.Empty, false, []),
            CreateExportOptionGroup("导出的结构", "schematics 文件夹", false, []),
            CreateExportOptionGroup("录像回放", "Replay Mod 的录像文件", false, []),
            CreateExportOptionGroup("单人游戏存档", "世界 / 地图", false,
            [
                CreateExportOption("Demo Survival World", "当前常用的演示存档", false),
                CreateExportOption("Creative Test Flat", "测试结构与建材展示", false)
            ]),
            CreateExportOptionGroup("多人游戏服务器列表", string.Empty, false, []),
            CreateExportOptionGroup("PCL 启动器程序", "打包社区版 PCL，以便没有启动器的玩家安装整合包", true,
            [
                CreateExportOption("PCL 个性化内容", "功能隐藏设置、主页、背景音乐和图片等", true)
            ])
        ]);
    }

    private void RefreshInstanceExportSurface()
    {
        if (!IsInstanceExportSurface)
        {
            return;
        }

        RaisePropertyChanged(nameof(InstanceExportName));
        RaisePropertyChanged(nameof(InstanceExportVersion));
        RaisePropertyChanged(nameof(InstanceExportIncludeResources));
        RaisePropertyChanged(nameof(InstanceExportModrinthMode));
        RaisePropertyChanged(nameof(ShowInstanceExportIncludeWarning));
        RaisePropertyChanged(nameof(ShowInstanceExportOptiFineWarning));
        RaisePropertyChanged(nameof(HasInstanceExportOptionGroups));
    }

    private void ResetInstanceExportOptions()
    {
        InitializeInstanceExportSurface();
        RefreshInstanceExportSurface();
        AddActivity("重置导出选项", "实例导出页已恢复到默认演示配置。");
    }

    private void StartInstanceExport()
    {
        AddActivity("开始导出", $"{InstanceExportName} {InstanceExportVersion}");
    }

    private static ExportOptionEntryViewModel CreateExportOption(string title, string description, bool isChecked)
    {
        return new ExportOptionEntryViewModel(title, description, isChecked);
    }

    private static ExportOptionGroupViewModel CreateExportOptionGroup(
        string title,
        string description,
        bool isChecked,
        IReadOnlyList<ExportOptionEntryViewModel> children)
    {
        return new ExportOptionGroupViewModel(
            new ExportOptionEntryViewModel(title, description, isChecked),
            children);
    }
}
