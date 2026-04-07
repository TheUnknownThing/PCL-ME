using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.ShellViews.Right;

internal sealed partial class DownloadResourceShellRightPaneView : UserControl
{
    private readonly TextBox _searchTextBox;
    private readonly ComboBox _sourceComboBox;
    private readonly ComboBox _tagComboBox;
    private readonly ComboBox _sortComboBox;
    private readonly ComboBox _versionComboBox;
    private readonly ComboBox _loaderComboBox;
    private FrontendShellViewModel? _observedShell;

    public DownloadResourceShellRightPaneView()
    {
        InitializeComponent();
        _searchTextBox = this.FindControl<TextBox>("DownloadResourceSearchTextBox")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索文本框。");
        _sourceComboBox = this.FindControl<ComboBox>("DownloadResourceSourceComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到来源筛选框。");
        _tagComboBox = this.FindControl<ComboBox>("DownloadResourceTagComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到标签筛选框。");
        _sortComboBox = this.FindControl<ComboBox>("DownloadResourceSortComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到排序筛选框。");
        _versionComboBox = this.FindControl<ComboBox>("DownloadResourceVersionComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到版本筛选框。");
        _loaderComboBox = this.FindControl<ComboBox>("DownloadResourceLoaderComboBox")
            ?? throw new InvalidOperationException("下载资源页面未找到加载器筛选框。");
        _searchTextBox.KeyDown += SearchTextBoxOnKeyDown;
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => ObserveShell(null);
    }

    private void SearchTextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is not FrontendShellViewModel { SearchDownloadResourceCommand: var searchCommand }
            || !searchCommand.CanExecute(null))
        {
            return;
        }

        e.Handled = true;
        searchCommand.Execute(null);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveShell(DataContext as FrontendShellViewModel);
        ScheduleSelectionRestore();
    }

    private void ObserveShell(FrontendShellViewModel? shell)
    {
        if (ReferenceEquals(_observedShell, shell))
        {
            return;
        }

        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged -= OnShellPropertyChanged;
        }

        _observedShell = shell;
        if (_observedShell is not null)
        {
            _observedShell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(FrontendShellViewModel.DownloadResourceSourceOptions)
            or nameof(FrontendShellViewModel.DownloadResourceTagOptions)
            or nameof(FrontendShellViewModel.DownloadResourceSortOptions)
            or nameof(FrontendShellViewModel.DownloadResourceVersionOptions)
            or nameof(FrontendShellViewModel.DownloadResourceLoaderOptions)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceSourceOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceTagOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceSortOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceVersionOption)
            or nameof(FrontendShellViewModel.SelectedDownloadResourceLoaderOption)))
        {
            return;
        }

        ScheduleSelectionRestore();
    }

    private void ScheduleSelectionRestore()
    {
        Dispatcher.UIThread.Post(RestoreFilterSelections, DispatcherPriority.Background);
    }

    private void RestoreFilterSelections()
    {
        if (_observedShell is null)
        {
            return;
        }

        ApplyComboSelection(_sourceComboBox, _observedShell.SelectedDownloadResourceSourceOption);
        ApplyComboSelection(_tagComboBox, _observedShell.SelectedDownloadResourceTagOption);
        ApplyComboSelection(_sortComboBox, _observedShell.SelectedDownloadResourceSortOption);
        ApplyComboSelection(_versionComboBox, _observedShell.SelectedDownloadResourceVersionOption);
        ApplyComboSelection(_loaderComboBox, _observedShell.SelectedDownloadResourceLoaderOption);
    }

    private static void ApplyComboSelection(ComboBox comboBox, object? selectedItem)
    {
        if (selectedItem is null || ReferenceEquals(comboBox.SelectedItem, selectedItem))
        {
            return;
        }

        comboBox.SelectedItem = selectedItem;
    }
}
