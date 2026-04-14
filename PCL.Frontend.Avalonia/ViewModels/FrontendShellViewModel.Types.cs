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
    int enterDelay,
    IReadOnlyList<SidebarListItemViewModel> items)
{
    public string Title { get; } = title;

    public bool HasTitle { get; } = hasTitle;

    public int EnterDelay { get; } = enterDelay;

    public IReadOnlyList<SidebarListItemViewModel> Items { get; } = items;
}

internal sealed class SidebarListItemViewModel(
    string title,
    string summary,
    bool isSelected,
    string iconPath,
    double iconScale,
    int enterDelay,
    ActionCommand command,
    string accessoryToolTip,
    string accessoryIconPath,
    ActionCommand? accessoryCommand) : ViewModelBase
{
    private bool _isSelected = isSelected;

    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string IconPath { get; } = iconPath;

    public double IconScale { get; } = iconScale;

    public int EnterDelay { get; } = enterDelay;

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

    public string InlineDescription => HasDescription ? $"   {Description}" : string.Empty;

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
    IBrush foregroundBrush,
    Thickness margin)
{
    public string Text { get; } = text;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IBrush BorderBrush { get; } = borderBrush;

    public IBrush ForegroundBrush { get; } = foregroundBrush;

    public Thickness Margin { get; } = margin;
}

internal sealed class DownloadInstallMinecraftSectionViewModel(
    string title,
    IReadOnlyList<DownloadInstallMinecraftChoiceViewModel> choices,
    bool isExpanded,
    bool canCollapse,
    ActionCommand toggleCommand) : ViewModelBase
{
    private bool _isExpanded = isExpanded;

    public string Title { get; } = title;

    public IReadOnlyList<DownloadInstallMinecraftChoiceViewModel> Choices { get; } = choices;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                RaisePropertyChanged(nameof(ChevronAngle));
            }
        }
    }

    public bool CanCollapse { get; } = canCollapse;

    public double ChevronAngle => IsExpanded ? 180 : 0;

    public ActionCommand ToggleCommand { get; } = toggleCommand;
}

internal sealed class DownloadInstallMinecraftChoiceViewModel(
    string title,
    string summary,
    Bitmap? icon,
    ActionCommand selectCommand)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public Bitmap? Icon { get; } = icon;

    public ActionCommand SelectCommand { get; } = selectCommand;
}

internal sealed class DownloadInstallChoiceItemViewModel(
    string title,
    string summary,
    Bitmap? icon,
    bool isSelected,
    ActionCommand selectCommand)
{
    public string Title { get; } = title;

    public string Summary { get; } = summary;

    public Bitmap? Icon { get; } = icon;

    public bool IsSelected { get; } = isSelected;

    public IBrush BackgroundBrush { get; } = isSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySelectedBackground", "#EAF2FE")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushGray8", "#F7F9FC");

    public IBrush BorderBrush { get; } = isSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#BFD9FF")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushGray7", "#E1E7EF");

    public IBrush ForegroundBrush { get; } = isSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : FrontendThemeResourceResolver.GetBrush("ColorBrush1", "#343D4A");

    public IBrush SummaryBrush { get; } = isSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondarySelected", "#4B78C2")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondaryIdle", "#7D8897");

    public ActionCommand SelectCommand { get; } = selectCommand;
}

internal sealed class DownloadInstallOptionCardViewModel(
    string title,
    string selectionText,
    Bitmap? icon,
    bool showIcon,
    bool useMutedSelectionStyle,
    bool canExpand,
    bool isExpanded,
    bool isLoading,
    string loadingText,
    bool showEmptyState,
    string emptyStateText,
    IReadOnlyList<DownloadInstallChoiceItemViewModel> choices,
    bool canClear,
    ActionCommand toggleCommand,
    ActionCommand clearCommand) : ViewModelBase
{
    private string _selectionText = selectionText;
    private Bitmap? _icon = icon;
    private bool _showIcon = showIcon;
    private bool _useMutedSelectionStyle = useMutedSelectionStyle;
    private bool _canExpand = canExpand;
    private bool _isExpanded = isExpanded;
    private bool _isLoading = isLoading;
    private string _loadingText = loadingText;
    private bool _showEmptyState = showEmptyState;
    private string _emptyStateText = emptyStateText;
    private IReadOnlyList<DownloadInstallChoiceItemViewModel> _choices = choices;
    private bool _canClear = canClear;

    public string Title { get; } = title;

    public string SelectionText
    {
        get => _selectionText;
        set => SetProperty(ref _selectionText, value);
    }

    public Bitmap? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public bool ShowIcon
    {
        get => _showIcon;
        set => SetProperty(ref _showIcon, value);
    }

    public bool UseMutedSelectionStyle
    {
        get => _useMutedSelectionStyle;
        set
        {
            if (SetProperty(ref _useMutedSelectionStyle, value))
            {
                RaisePropertyChanged(nameof(SelectionForegroundBrush));
            }
        }
    }

    public IBrush SelectionForegroundBrush => UseMutedSelectionStyle
        ? FrontendThemeResourceResolver.GetBrush("ColorBrushGray3", "#8C99A8")
        : FrontendThemeResourceResolver.GetBrush("ColorBrush1", "#343D4A");

    public bool CanExpand
    {
        get => _canExpand;
        set
        {
            if (SetProperty(ref _canExpand, value))
            {
                RaisePropertyChanged(nameof(CardContentMargin));
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                RaisePropertyChanged(nameof(ChevronAngle));
            }
        }
    }

    public double ChevronAngle => IsExpanded ? 180 : 0;

    public Thickness CardContentMargin => CanExpand
        ? new Thickness(20, 40, 18, 15)
        : default;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LoadingText
    {
        get => _loadingText;
        set => SetProperty(ref _loadingText, value);
    }

    public bool ShowEmptyState
    {
        get => _showEmptyState;
        set => SetProperty(ref _showEmptyState, value);
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public IReadOnlyList<DownloadInstallChoiceItemViewModel> Choices
    {
        get => _choices;
        set => SetProperty(ref _choices, value);
    }

    public bool CanClear
    {
        get => _canClear;
        set => SetProperty(ref _canClear, value);
    }

    public ActionCommand ToggleCommand { get; } = toggleCommand;

    public ActionCommand ClearCommand { get; } = clearCommand;

    public void UpdateFrom(DownloadInstallOptionCardViewModel other)
    {
        SelectionText = other.SelectionText;
        Icon = other.Icon;
        ShowIcon = other.ShowIcon;
        UseMutedSelectionStyle = other.UseMutedSelectionStyle;
        CanExpand = other.CanExpand;
        IsExpanded = other.IsExpanded;
        IsLoading = other.IsLoading;
        LoadingText = other.LoadingText;
        ShowEmptyState = other.ShowEmptyState;
        EmptyStateText = other.EmptyStateText;
        Choices = other.Choices;
        CanClear = other.CanClear;
    }
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

internal sealed class CommunityProjectFilterButtonViewModel(
    string text,
    bool isChecked,
    ActionCommand command)
{
    public string Text { get; } = text;

    public bool IsChecked { get; } = isChecked;

    public ActionCommand Command { get; } = command;
}

internal sealed class CommunityProjectActionButtonViewModel(
    string text,
    string iconData,
    double iconScale,
    PclIconTextButtonColorState colorType,
    ActionCommand command)
{
    public string Text { get; } = text;

    public string IconData { get; } = iconData;

    public double IconScale { get; } = iconScale;

    public PclIconTextButtonColorState ColorType { get; } = colorType;

    public ActionCommand Command { get; } = command;
}

internal sealed class DownloadCatalogSectionViewModel : ViewModelBase
{
    private IReadOnlyList<DownloadCatalogEntryViewModel> _allItems;
    private readonly ActionCommand _previousPageCommand;
    private readonly ActionCommand _nextPageCommand;
    private readonly Func<CancellationToken, Task<IReadOnlyList<DownloadCatalogEntryViewModel>>>? _loadEntriesAsync;
    private CancellationTokenSource? _loadEntriesCts;
    private bool _hasLoaded;
    private bool _isExpanded;
    private bool _isLoading;

    public DownloadCatalogSectionViewModel(
        string title,
        IReadOnlyList<DownloadCatalogEntryViewModel> items,
        bool isCollapsible = false,
        bool isExpanded = true,
        Func<CancellationToken, Task<IReadOnlyList<DownloadCatalogEntryViewModel>>>? loadEntriesAsync = null,
        string loadingText = "正在获取版本列表")
    {
        Title = title;
        LoadingText = loadingText;
        _allItems = items;
        IsCollapsible = isCollapsible;
        _isExpanded = isExpanded;
        _loadEntriesAsync = loadEntriesAsync;
        _hasLoaded = loadEntriesAsync is null;
        _previousPageCommand = new ActionCommand(
            () => { },
            () => false);
        _nextPageCommand = new ActionCommand(
            () => { },
            () => false);

        if (_hasLoaded)
        {
            ReplaceVisibleItems(items);
        }
        else if (_isExpanded)
        {
            EnsureEntriesLoaded();
        }
    }

    public string Title { get; }

    public string LoadingText { get; }

    public ObservableCollection<DownloadCatalogEntryViewModel> Items { get; } = [];

    public bool IsCollapsible { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                EnsureEntriesLoaded();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasItems => Items.Count > 0;

    public bool ShowPagination => false;

    public string PageLabel => string.Empty;

    public ActionCommand PreviousPageCommand => _previousPageCommand;

    public ActionCommand NextPageCommand => _nextPageCommand;

    private void ReplaceVisibleItems(IReadOnlyList<DownloadCatalogEntryViewModel> items)
    {
        _allItems = items;
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        RaisePropertyChanged(nameof(HasItems));
        RaisePropertyChanged(nameof(ShowPagination));
        RaisePropertyChanged(nameof(PageLabel));
    }

    private void EnsureEntriesLoaded()
    {
        if (_hasLoaded || IsLoading || _loadEntriesAsync is null)
        {
            return;
        }

        _loadEntriesCts?.Cancel();
        _loadEntriesCts = new CancellationTokenSource();
        IsLoading = true;
        _ = LoadEntriesAsync(_loadEntriesCts.Token);
    }

    private async Task LoadEntriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var items = await _loadEntriesAsync!(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _hasLoaded = true;
            ReplaceVisibleItems(items);
        }
        catch (OperationCanceledException)
        {
            // A newer expand request superseded this load.
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _hasLoaded = true;
            ReplaceVisibleItems(
            [
                new DownloadCatalogEntryViewModel(
                    "加载失败",
                    ex.Message,
                    string.Empty,
                    "重试",
                    new ActionCommand(ReloadEntries))
            ]);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void ReloadEntries()
    {
        _hasLoaded = false;
        EnsureEntriesLoaded();
    }
}

internal sealed class DownloadCatalogEntryViewModel(
    string title,
    string info,
    string meta,
    string actionText,
    ActionCommand command,
    Bitmap? icon = null,
    string? iconUrl = null)
    : INotifyPropertyChanged
{
    private Bitmap? _icon = icon;
    private int _iconLoadStarted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Meta { get; } = meta;

    public string ActionText { get; } = actionText;

    public ActionCommand Command { get; } = command;

    public Bitmap? Icon
    {
        get => _icon;
        private set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    public string? IconUrl { get; } = iconUrl;

    public bool HasIcon => Icon is not null;

    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    public string CombinedInfo => string.Join(" • ", new[] { Info, Meta }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public bool TryBeginIconLoad()
    {
        return !string.IsNullOrWhiteSpace(IconUrl)
               && Interlocked.CompareExchange(ref _iconLoadStarted, 1, 0) == 0;
    }

    public void ApplyIcon(Bitmap? icon)
    {
        if (icon is not null)
        {
            Icon = icon;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class DownloadFavoriteSectionViewModel(
    string title,
    IReadOnlyList<InstanceResourceEntryViewModel> items)
{
    public string Title { get; } = title;

    public IReadOnlyList<InstanceResourceEntryViewModel> Items { get; } = items;
}

internal sealed class CommunityProjectReleaseGroupViewModel(
    string title,
    bool isExpanded,
    IReadOnlyList<DownloadCatalogEntryViewModel> items)
{
    public string Title { get; } = title;

    public bool IsExpanded { get; } = isExpanded;

    public IReadOnlyList<DownloadCatalogEntryViewModel> Items { get; } = items;
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

internal sealed class DownloadResourcePaginationItemViewModel(
    string label,
    ActionCommand? command,
    bool isCurrent,
    bool isEllipsis)
{
    public string Label { get; } = label;

    public ActionCommand? Command { get; } = command;

    public bool IsCurrent { get; } = isCurrent;

    public bool IsEllipsis { get; } = isEllipsis;

    public bool ShowPageButton => !IsEllipsis;

    public bool ShowEllipsis => IsEllipsis;

    public bool IsClickable => !IsEllipsis && !IsCurrent && Command is not null;
}

internal sealed class DownloadResourceEntryViewModel(
    Bitmap? icon,
    string title,
    string info,
    string source,
    string version,
    string loader,
    IReadOnlyList<string> tags,
    IReadOnlyList<string> supportedVersions,
    IReadOnlyList<string> supportedLoaders,
    int downloadCount,
    int followCount,
    int releaseRank,
    int updateRank,
    string actionText,
    ActionCommand command,
    string? iconUrl)
    : INotifyPropertyChanged
{
    private Bitmap? _icon = icon;
    private int _iconLoadStarted;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Bitmap? Icon
    {
        get => _icon;
        private set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Source { get; } = source;

    public string Version { get; } = version;

    public string Loader { get; } = loader;

    public IReadOnlyList<string> Tags { get; } = tags;

    public IReadOnlyList<string> SupportedVersions { get; } = supportedVersions;

    public IReadOnlyList<string> SupportedLoaders { get; } = supportedLoaders;

    public int DownloadCount { get; } = downloadCount;

    public int FollowCount { get; } = followCount;

    public int ReleaseRank { get; } = releaseRank;

    public int UpdateRank { get; } = updateRank;

    public string ActionText { get; } = actionText;

    public ActionCommand Command { get; } = command;

    public string? IconUrl { get; } = iconUrl;

    public bool HasIcon => Icon is not null;

    public bool HasInfo => !string.IsNullOrWhiteSpace(Info);

    public IReadOnlyList<string> VisibleTags => Tags
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(4)
        .ToArray();

    public bool HasVisibleTags => VisibleTags.Count > 0;

    public string VersionLabel => string.IsNullOrWhiteSpace(Version) ? "未标注版本" : Version;

    public string DownloadCountLabel => DownloadCount <= 0 ? "暂无下载" : $"{FormatCompactCount(DownloadCount)} 下载";

    public string UpdatedLabel
    {
        get
        {
            var rank = UpdateRank > 0 ? UpdateRank : ReleaseRank;
            if (rank <= 0)
            {
                return "时间未知";
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(rank).LocalDateTime.ToString("yyyy/MM/dd");
            }
            catch
            {
                return "时间未知";
            }
        }
    }

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
        string.Join(" ", SupportedVersions),
        string.Join(" ", SupportedLoaders),
        string.Join(" ", Tags)
    });

    public bool TryBeginIconLoad()
    {
        return !string.IsNullOrWhiteSpace(IconUrl)
               && Interlocked.CompareExchange(ref _iconLoadStarted, 1, 0) == 0;
    }

    public void ApplyIcon(Bitmap? icon)
    {
        if (icon is not null)
        {
            Icon = icon;
        }
    }

    private static string FormatCompactCount(int value)
    {
        return value switch
        {
            >= 100_000_000 => $"{value / 100_000_000d:0.#}亿",
            >= 10_000 => $"{value / 10_000d:0.#}万",
            _ => value.ToString()
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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
    string address,
    Bitmap? backgroundImage,
    Bitmap? logo,
    ActionCommand refreshCommand,
    ActionCommand copyCommand,
    ActionCommand connectCommand,
    ActionCommand inspectCommand) : ViewModelBase
{
    private Bitmap? _logo = logo;
    private string _statusText = "已保存服务器";
    private IBrush _statusBrush = Brushes.White;
    private string _playerCount = "-/-";
    private string _latency = string.Empty;
    private IBrush _latencyBrush = Brushes.White;
    private string? _playerTooltip;
    private IReadOnlyList<MinecraftServerQueryMotdLineViewModel> _motdLines = [];
    private bool _hasMotd;
    private bool _hasLatency;

    public string Title { get; } = title;

    public string Address { get; } = address;

    public Bitmap? BackgroundImage { get; } = backgroundImage;

    public Bitmap? Logo
    {
        get => _logo;
        set => SetProperty(ref _logo, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public IBrush StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    public string PlayerCount
    {
        get => _playerCount;
        set => SetProperty(ref _playerCount, value);
    }

    public string Latency
    {
        get => _latency;
        set
        {
            if (SetProperty(ref _latency, value))
            {
                HasLatency = !string.IsNullOrWhiteSpace(value);
            }
        }
    }

    public IBrush LatencyBrush
    {
        get => _latencyBrush;
        set => SetProperty(ref _latencyBrush, value);
    }

    public string? PlayerTooltip
    {
        get => _playerTooltip;
        set => SetProperty(ref _playerTooltip, value);
    }

    public IReadOnlyList<MinecraftServerQueryMotdLineViewModel> MotdLines
    {
        get => _motdLines;
        set
        {
            if (SetProperty(ref _motdLines, value))
            {
                HasMotd = value.Count > 0;
            }
        }
    }

    public bool HasMotd
    {
        get => _hasMotd;
        private set => SetProperty(ref _hasMotd, value);
    }

    public bool HasLatency
    {
        get => _hasLatency;
        private set => SetProperty(ref _hasLatency, value);
    }

    public ActionCommand RefreshCommand { get; } = refreshCommand;

    public ActionCommand CopyCommand { get; } = copyCommand;

    public ActionCommand ConnectCommand { get; } = connectCommand;

    public ActionCommand InspectCommand { get; } = inspectCommand;
}

internal sealed class InstanceResourceEntryViewModel : ViewModelBase
{
    private readonly Action<bool>? _selectionChanged;
    private readonly ActionCommand _primaryCommand;
    private Bitmap? _icon;
    private bool _isSelected;
    private bool _isEnabled;
    private readonly string _infoToolTip;
    private readonly string _websiteToolTip;
    private readonly string _openToolTip;
    private readonly string _enableToolTip;
    private readonly string _disableToolTip;
    private readonly string _deleteToolTip;
    private readonly string _disabledTagText;

    public InstanceResourceEntryViewModel(
        Bitmap? icon,
        string title,
        string info,
        string meta,
        string path,
        ActionCommand actionCommand,
        string actionToolTip = "查看",
        bool isEnabled = true,
        string description = "",
        string website = "",
        bool showSelection = false,
        bool isSelected = false,
        Action<bool>? selectionChanged = null,
        ActionCommand? infoCommand = null,
        ActionCommand? websiteCommand = null,
        ActionCommand? openCommand = null,
        ActionCommand? toggleCommand = null,
        ActionCommand? deleteCommand = null,
        string infoToolTip = "详情",
        string websiteToolTip = "打开主页",
        string openToolTip = "打开文件位置",
        string enableToolTip = "启用",
        string disableToolTip = "禁用",
        string deleteToolTip = "删除",
        string disabledTagText = "已禁用")
    {
        _icon = icon;
        Title = title;
        Info = info;
        Meta = meta;
        Path = path;
        ActionCommand = actionCommand;
        ActionToolTip = actionToolTip;
        Description = description;
        Website = website;
        ShowSelection = showSelection;
        _isSelected = isSelected;
        _isEnabled = isEnabled;
        _selectionChanged = selectionChanged;
        _primaryCommand = new ActionCommand(ExecutePrimaryAction);
        InfoCommand = infoCommand;
        WebsiteCommand = websiteCommand;
        OpenCommand = openCommand ?? actionCommand;
        ToggleCommand = toggleCommand;
        DeleteCommand = deleteCommand;
        _infoToolTip = infoToolTip;
        _websiteToolTip = websiteToolTip;
        _openToolTip = openToolTip;
        _enableToolTip = enableToolTip;
        _disableToolTip = disableToolTip;
        _deleteToolTip = deleteToolTip;
        _disabledTagText = disabledTagText;
    }

    public Bitmap? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    public string Title { get; }

    public string Info { get; }

    public string Meta { get; }

    public string Path { get; }

    public string Description { get; }

    public string Website { get; }

    public ActionCommand ActionCommand { get; }

    public ActionCommand PrimaryCommand => _primaryCommand;

    public string ActionToolTip { get; }

    public bool ShowSelection { get; }

    public ActionCommand? InfoCommand { get; }

    public ActionCommand? WebsiteCommand { get; }

    public ActionCommand? OpenCommand { get; }

    public ActionCommand? ToggleCommand { get; }

    public ActionCommand? DeleteCommand { get; }

    public bool HasMeta => !string.IsNullOrWhiteSpace(Meta);

    public bool HasAction => ActionCommand is not null;

    public string ActionIconData => FrontendIconCatalog.FolderOutline.Data;

    public bool HasInfoAction => InfoCommand is not null;

    public bool HasWebsiteAction => WebsiteCommand is not null;

    public bool HasOpenAction => OpenCommand is not null;

    public bool HasToggleAction => ToggleCommand is not null;

    public bool HasDeleteAction => DeleteCommand is not null;

    public bool HasStandardActionStack => HasInfoAction || HasWebsiteAction || HasOpenAction || HasToggleAction || HasDeleteAction;

    public string InfoIconData => FrontendIconCatalog.InfoCircle.Data;

    public double InfoIconScale => FrontendIconCatalog.InfoCircle.Scale;

    public string WebsiteIconData => FrontendIconCatalog.Link.Data;

    public double WebsiteIconScale => FrontendIconCatalog.Link.Scale;

    public string OpenIconData => FrontendIconCatalog.OpenFolder.Data;

    public double OpenIconScale => FrontendIconCatalog.OpenFolder.Scale;

    public string ToggleIconData => IsEnabledState
        ? FrontendIconCatalog.DisableCircle.Data
        : FrontendIconCatalog.EnableCircle.Data;

    public double ToggleIconScale => IsEnabledState
        ? FrontendIconCatalog.DisableCircle.Scale
        : FrontendIconCatalog.EnableCircle.Scale;

    public string ToggleToolTip => IsEnabledState ? _disableToolTip : _enableToolTip;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public double DeleteIconScale => FrontendIconCatalog.DeleteOutline.Scale;

    public string InfoToolTip => _infoToolTip;

    public string WebsiteToolTip => _websiteToolTip;

    public string OpenToolTip => _openToolTip;

    public string DeleteToolTip => _deleteToolTip;

    public IReadOnlyList<string> Tags
    {
        get
        {
            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(Meta))
            {
                foreach (var segment in Meta.Split('•', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    tags.Add(segment);
                }
            }

            if (HasToggleAction && !IsEnabledState)
            {
                tags.Add(_disabledTagText);
            }

            return tags;
        }
    }

    public bool HasTags => Tags.Count > 0;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                RaisePropertyChanged(nameof(TitleForeground));
                _selectionChanged?.Invoke(value);
            }
        }
    }

    public bool IsEnabledState
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                RaisePropertyChanged(nameof(ContentOpacity));
                RaisePropertyChanged(nameof(TitleForeground));
                RaisePropertyChanged(nameof(ToggleIconData));
                RaisePropertyChanged(nameof(ToggleIconScale));
                RaisePropertyChanged(nameof(ToggleToolTip));
                RaisePropertyChanged(nameof(Tags));
                RaisePropertyChanged(nameof(HasTags));
            }
        }
    }

    public double ContentOpacity => IsEnabledState ? 1.0 : 0.56;

    public IBrush TitleForeground => IsSelected && IsEnabledState
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : IsEnabledState
            ? FrontendThemeResourceResolver.GetBrush("ColorBrush1", "#343D4A")
            : FrontendThemeResourceResolver.GetBrush("ColorBrushEntrySecondaryIdle", "#7D8897");

    private void ExecutePrimaryAction()
    {
        if (ShowSelection)
        {
            IsSelected = !IsSelected;
            return;
        }

        if (ActionCommand.CanExecute(null))
        {
            ActionCommand.Execute(null);
        }
    }

    public void ApplyIcon(Bitmap? icon)
    {
        if (icon is not null)
        {
            Icon = icon;
        }
    }
}

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

internal sealed class PromptLaneViewModel(
    AvaloniaPromptLaneKind kind,
    string title,
    string summary,
    ActionCommand command) : ViewModelBase
{
    private int _count;
    private bool _isSelected;

    public AvaloniaPromptLaneKind Kind { get; } = kind;

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

    public IBrush BackgroundBrush => IsSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : FrontendThemeResourceResolver.GetBrush("ColorBrushSemiTransparent", "#01EAF2FE");

    public IBrush BorderBrush => IsSelected
        ? FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3")
        : FrontendThemeResourceResolver.GetBrush("ColorBrush6", "#D5E6FD");

    public IBrush ForegroundBrush => IsSelected
        ? Brushes.White
        : FrontendThemeResourceResolver.GetBrush("ColorBrushGray1", "#404040");
}

internal sealed class PromptCardViewModel(
    AvaloniaPromptLaneKind lane,
    string id,
    string title,
    string message,
    string source,
    string severity,
    IBrush titleBrush,
    IBrush accentBrush,
    IBrush backgroundBrush,
    IReadOnlyList<PromptOptionViewModel> options)
{
    public AvaloniaPromptLaneKind Lane { get; } = lane;

    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public string Source { get; } = source;

    public string Severity { get; } = severity;

    public IBrush TitleBrush { get; } = titleBrush;

    public IBrush AccentBrush { get; } = accentBrush;

    public IBrush BackgroundBrush { get; } = backgroundBrush;

    public IReadOnlyList<PromptOptionViewModel> Options { get; } = options;
}

internal sealed class PromptOptionViewModel(
    string label,
    string detail,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Label { get; } = label;

    public string Detail { get; } = detail;

    public PclButtonColorState ColorType { get; } = colorType;

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

internal enum AvaloniaPromptLaneKind
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
