using Avalonia.Controls;
using Avalonia.Input;
using PCL.Frontend.Spike.ViewModels;

namespace PCL.Frontend.Spike.Desktop.ShellViews.Right;

internal sealed partial class DownloadResourceShellRightPaneView : UserControl
{
    private readonly TextBox _searchTextBox;

    public DownloadResourceShellRightPaneView()
    {
        InitializeComponent();
        _searchTextBox = this.FindControl<TextBox>("DownloadResourceSearchTextBox")
            ?? throw new InvalidOperationException("下载资源页面未找到搜索文本框。");
        _searchTextBox.KeyDown += SearchTextBoxOnKeyDown;
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
}
