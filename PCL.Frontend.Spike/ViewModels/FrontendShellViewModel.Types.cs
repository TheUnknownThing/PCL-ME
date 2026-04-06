using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Frontend.Spike.Desktop.Controls;

namespace PCL.Frontend.Spike.ViewModels;

internal sealed class NavigationEntryViewModel(
    string title,
    string summary,
    string meta,
    bool isSelected,
    string iconPath,
    double iconScale,
    NavigationPalette palette,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Meta { get; } = meta;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public IBrush BackgroundBrush { get; } = palette.Background;

    public IBrush BorderBrush { get; } = palette.Border;

    public IBrush ForegroundBrush { get; } = palette.Foreground;

    public IBrush AccentBrush { get; } = palette.Accent;

    public ActionCommand Command { get; } = command;
}

internal sealed class SidebarSectionViewModel(
    string title,
    bool hasTitle,
    IReadOnlyList<SidebarListItemViewModel> items)
{
    public string Title { get; } = title;

    public bool HasTitle { get; } = hasTitle;

    public IReadOnlyList<SidebarListItemViewModel> Items { get; } = items;
}

internal sealed class SidebarListItemViewModel(
    string title,
    string summary,
    bool isSelected,
    string iconPath,
    double iconScale,
    ActionCommand command,
    string accessoryToolTip,
    string accessoryIconPath,
    ActionCommand? accessoryCommand)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public ActionCommand Command { get; } = command;

    public string AccessoryToolTip { get; } = accessoryToolTip;

    public string AccessoryIconPath { get; } = accessoryIconPath;

    public ActionCommand? AccessoryCommand { get; } = accessoryCommand;
}

internal sealed class SurfaceFactViewModel(
    string label,
    string value,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Label { get; } = label;

    public string Value { get; } = value;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceSectionViewModel(
    string eyebrow,
    string title,
    IReadOnlyList<SurfaceLineViewModel> lines,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Eyebrow { get; } = eyebrow;

    public string Title { get; } = title;

    public IReadOnlyList<SurfaceLineViewModel> Lines { get; } = lines;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class SurfaceLineViewModel(
    string text,
    IBrush accentBrush,
    IBrush foregroundBrush)
{
    public string Text { get; } = text;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class ActivityItemViewModel(string time, string title, string body)
{
    public string Time { get; } = time;

    public string Title { get; } = title;

    public string Body { get; } = body;
}

internal sealed class AboutEntryViewModel(
    string title,
    string info,
    Bitmap? avatar,
    string? actionText,
    ActionCommand? actionCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public Bitmap? Avatar { get; } = avatar;

    public string ActionText { get; } = actionText ?? string.Empty;

    public ActionCommand? ActionCommand { get; } = actionCommand;

    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionText);
}

internal sealed class SimpleListEntryViewModel(string title, string info, ActionCommand command)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public ActionCommand Command { get; } = command;
}

internal sealed class KeyValueEntryViewModel(string label, string value)
{
    public string Label { get; } = label;

    public string Value { get; } = value;
}

internal sealed class ExportOptionEntryViewModel(
    string title,
    string description,
    bool isChecked) : ViewModelBase
{
    private bool _isChecked = isChecked;

    public string Title { get; } = title;

    public string Description { get; } = description;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

internal sealed class ExportOptionGroupViewModel(
    ExportOptionEntryViewModel header,
    IReadOnlyList<ExportOptionEntryViewModel> children)
{
    public ExportOptionEntryViewModel Header { get; } = header;

    public IReadOnlyList<ExportOptionEntryViewModel> Children { get; } = children;

    public bool HasChildren => Children.Count > 0;
}

internal sealed class ToolboxActionViewModel(
    string title,
    string toolTip,
    double minWidth,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string ToolTip { get; } = toolTip;

    public double MinWidth { get; } = minWidth;

    public PclButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

internal sealed class SurfaceNoticeViewModel(
    string text,
    IBrush backgroundBrush,
    IBrush borderBrush,
    IBrush foregroundBrush)
{
    public string Text { get; } = text;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;
}

internal sealed class DownloadInstallOptionViewModel(
    string title,
    string selection,
    Bitmap? icon,
    string detailText,
    string selectText,
    bool canSelect,
    ActionCommand selectCommand,
    bool canClear,
    ActionCommand clearCommand)
{
    public string Title { get; } = title;

    public string Selection { get; } = selection;

    public Bitmap? Icon { get; } = icon;

    public string DetailText { get; } = detailText;

    public string SelectText { get; } = selectText;

    public bool CanSelect { get; } = canSelect;

    public ActionCommand SelectCommand { get; } = selectCommand;

    public bool CanClear { get; } = canClear;

    public ActionCommand ClearCommand { get; } = clearCommand;
}

internal sealed class DownloadCatalogActionViewModel(
    string text,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Text { get; } = text;

    public PclButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

internal sealed class DownloadCatalogSectionViewModel(
    string title,
    IReadOnlyList<DownloadCatalogEntryViewModel> items)
{
    public string Title { get; } = title;

    public IReadOnlyList<DownloadCatalogEntryViewModel> Items { get; } = items;
}

internal sealed class DownloadCatalogEntryViewModel(
    string title,
    string info,
    string meta,
    string actionText,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Meta { get; } = meta;

    public string ActionText { get; } = actionText;

    public ActionCommand Command { get; } = command;

    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);
}

internal sealed class DownloadResourceFilterOptionViewModel(
    string label,
    string filterValue,
    bool isHeader = false)
{
    public string Label { get; } = label;

    public string FilterValue { get; } = filterValue;

    public bool IsHeader { get; } = isHeader;
}

internal sealed class DownloadResourceEntryViewModel(
    Bitmap? icon,
    string title,
    string info,
    string source,
    string version,
    string loader,
    IReadOnlyList<string> tags,
    int downloadCount,
    int followCount,
    int releaseRank,
    int updateRank,
    string actionText,
    ActionCommand command)
{
    public Bitmap? Icon { get; } = icon;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Source { get; } = source;

    public string Version { get; } = version;

    public string Loader { get; } = loader;

    public IReadOnlyList<string> Tags { get; } = tags;

    public int DownloadCount { get; } = downloadCount;

    public int FollowCount { get; } = followCount;

    public int ReleaseRank { get; } = releaseRank;

    public int UpdateRank { get; } = updateRank;

    public string ActionText { get; } = actionText;

    public ActionCommand Command { get; } = command;

    public bool HasIcon => Icon is not null;

    public string Meta
    {
        get
        {
            var parts = new List<string> { Source };

            if (!string.IsNullOrWhiteSpace(Loader))
            {
                parts.Add(Loader);
            }

            if (!string.IsNullOrWhiteSpace(Version))
            {
                parts.Add(Version);
            }

            return string.Join(" • ", parts);
        }
    }

    public string SearchText => string.Join(" ", new[]
    {
        Title,
        Info,
        Source,
        Version,
        Loader,
        string.Join(" ", Tags)
    });
}

internal sealed class InstanceScreenshotEntryViewModel(
    Bitmap? image,
    string title,
    string info,
    ActionCommand openCommand)
{
    public Bitmap? Image { get; } = image;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public ActionCommand OpenCommand { get; } = openCommand;
}

internal sealed class InstanceServerEntryViewModel(
    string title,
    string info,
    string status,
    ActionCommand actionCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Status { get; } = status;

    public ActionCommand ActionCommand { get; } = actionCommand;
}

internal sealed class InstanceResourceEntryViewModel(
    Bitmap? icon,
    string title,
    string info,
    string meta,
    ActionCommand actionCommand)
{
    public Bitmap? Icon { get; } = icon;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Meta { get; } = meta;

    public ActionCommand ActionCommand { get; } = actionCommand;
}

internal sealed class HelpTopicViewModel(
    string groupTitle,
    string title,
    string summary,
    string keywords,
    ActionCommand command)
{
    public string GroupTitle { get; } = groupTitle;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public string Keywords { get; } = keywords;

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
    ActionCommand toggleEnabledCommand) : ViewModelBase
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

    public string ToggleButtonText => IsEnabled ? "禁用" : "启用";

    public double TitleOpacity => IsEnabled ? 1.0 : 0.48;

    public IBrush TitleForeground => IsEnabled ? Brush.Parse("#343D4A") : Brush.Parse("#7D8897");
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

internal sealed class HelpTopicGroupViewModel(string title, IReadOnlyList<HelpTopicViewModel> items)
{
    public string Title { get; } = title;

    public IReadOnlyList<HelpTopicViewModel> Items { get; } = items;
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

internal sealed class PromptLaneViewModel(
    SpikePromptLaneKind kind,
    string title,
    string summary,
    ActionCommand command) : ViewModelBase
{
    private int _count;
    private bool _isSelected;

    public SpikePromptLaneKind Kind { get; } = kind;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public ActionCommand Command { get; } = command;

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(BackgroundBrush));
                RaisePropertyChanged(nameof(BorderBrush));
                RaisePropertyChanged(nameof(ForegroundBrush));
            }
        }
    }

    public IBrush BackgroundBrush => IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#01EAF2FE");

    public IBrush BorderBrush => IsSelected ? Brush.Parse("#1370F3") : Brush.Parse("#D5E6FD");

    public IBrush ForegroundBrush => IsSelected ? Brushes.White : Brush.Parse("#404040");
}

internal sealed class PromptCardViewModel(
    SpikePromptLaneKind lane,
    string id,
    string title,
    string message,
    string source,
    string severity,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IReadOnlyList<PromptOptionViewModel> options)
{
    public SpikePromptLaneKind Lane { get; } = lane;

    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Source { get; } = source;

    public string Severity { get; } = severity;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IReadOnlyList<PromptOptionViewModel> Options { get; } = options;
}

internal sealed class PromptOptionViewModel(
    string label,
    string detail,
    ActionCommand command)
{
    public string Label { get; } = label;

    public string Detail { get; } = detail;

    public ActionCommand Command { get; } = command;
}

internal sealed record NavigationPalette(
    IBrush Background,
    IBrush Border,
    IBrush Foreground,
    IBrush Accent);

internal sealed record SurfacePalette(
    IBrush Background,
    IBrush Border,
    IBrush Accent,
    IBrush Foreground);

internal enum NavigationVisualStyle
{
    TopLevel = 0,
    Sidebar = 1,
    Utility = 2
}

internal enum SpikePromptLaneKind
{
    Startup = 0,
    Launch = 1,
    Crash = 2
}

internal enum UpdateSurfaceState
{
    Checking = 0,
    Available = 1,
    Latest = 2,
    Error = 3
}
