using System.Collections.ObjectModel;
using System.Linq;

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
        var exportState = _instanceComposition.Export;
        _instanceExportName = exportState.Name;
        _instanceExportVersion = exportState.Version;
        _instanceExportIncludeResources = exportState.IncludeResources;
        _instanceExportModrinthMode = exportState.ModrinthMode;

        ReplaceItems(
            InstanceExportOptionGroups,
            exportState.OptionGroups.Select(group => CreateExportOptionGroup(
                group.Title,
                group.Description,
                group.IsChecked,
                group.Children.Select(child => CreateExportOption(child.Title, child.Description, child.IsChecked)).ToArray())));
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
        ReloadInstanceComposition();
        RefreshInstanceExportSurface();
        AddActivity("重置导出选项", "实例导出页已恢复到当前实例扫描结果。");
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
