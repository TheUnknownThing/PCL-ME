using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;

namespace PCL.Frontend.Avalonia.ViewModels;
internal sealed class HelpTopicViewModel(
    string groupTitle,
    string title,
    string summary,
    string keywords,
    Bitmap? icon,
    ActionCommand command)
{
    public string GroupTitle { get; } = groupTitle;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Keywords { get; } = keywords;

    public Bitmap? Icon { get; } = icon;

    public ActionCommand Command { get; } = command;
}

internal sealed class JavaRuntimeEntryViewModel(
    string key,
    string title,
    string info,
    IReadOnlyList<string> tags,
    bool isEnabled,
    ActionCommand selectCommand,
    ActionCommand openCommand,
    ActionCommand infoCommand,
    ActionCommand toggleEnabledCommand,
    string enableText = "Enable",
    string disableText = "Disable") : ViewModelBase
{
    private bool _isSelected;
    private bool _isEnabled = isEnabled;

    public string Key { get; } = key;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public IReadOnlyList<string> Tags { get; } = tags;

    public ActionCommand SelectCommand { get; } = selectCommand;

    public ActionCommand OpenCommand { get; } = openCommand;

    public ActionCommand InfoCommand { get; } = infoCommand;

    public ActionCommand ToggleEnabledCommand { get; } = toggleEnabledCommand;

    public string EnableText { get; } = enableText;

    public string DisableText { get; } = disableText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                RaisePropertyChanged(nameof(ToggleButtonText));
                RaisePropertyChanged(nameof(TitleOpacity));
                RaisePropertyChanged(nameof(TitleForeground));
            }
        }
    }

    public string ToggleButtonText => IsEnabled ? DisableText : EnableText;

    public double TitleOpacity => IsEnabled ? 1.0 : 0.48;

    public IBrush TitleForeground => IsEnabled
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush1", "#343D4A")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondaryIdle", "#7D8897");
}

internal sealed class UiFeatureToggleGroupViewModel(string title, IReadOnlyList<UiFeatureToggleItemViewModel> items)
{
    public string Title { get; } = title;

    public IReadOnlyList<UiFeatureToggleItemViewModel> Items { get; } = items;
}

internal sealed class UiFeatureToggleItemViewModel(
    string title,
    bool isChecked,
    Action<bool>? changed = null) : ViewModelBase
{
    private bool _isChecked = isChecked;

    public string Title { get; } = title;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
            {
                changed?.Invoke(value);
            }
        }
    }
}

internal sealed class HelpTopicGroupViewModel(
    string title,
    IReadOnlyList<HelpTopicViewModel> items,
    bool isExpanded) : ViewModelBase
{
    private bool _isExpanded = isExpanded;

    public string Title { get; } = title;

    public IReadOnlyList<HelpTopicViewModel> Items { get; } = items;

    public ActionCommand ToggleCommand => new(() => IsExpanded = !IsExpanded);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

internal sealed class FeedbackSectionViewModel(
    string title,
    IReadOnlyList<SimpleListEntryViewModel> items,
    bool isExpanded) : ViewModelBase
{
    private bool _isExpanded = isExpanded;

    public string Title { get; } = title;

    public IReadOnlyList<SimpleListEntryViewModel> Items { get; } = items;

    public ActionCommand ToggleCommand => new(() => IsExpanded = !IsExpanded);

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
