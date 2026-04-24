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
internal sealed class DownloadCatalogActionViewModel(
    string text,
    PclButtonColorState colorType,
    ActionCommand command)
{
    public string Text { get; } = text;

    public PclButtonColorState ColorType { get; } = colorType;

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
        string loadingText = "Loading versions")
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
                    "Load failed",
                    ex.Message,
                    string.Empty,
                    "Retry",
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
    IReadOnlyList<string> displayTags,
    IReadOnlyList<string> supportedVersions,
    IReadOnlyList<string> supportedLoaders,
    int downloadCount,
    int followCount,
    int releaseRank,
    int updateRank,
    string versionLabel,
    string downloadCountLabel,
    string updatedLabel,
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

    public IReadOnlyList<string> DisplayTags { get; } = displayTags;

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

    public IReadOnlyList<string> VisibleTags => DisplayTags
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(4)
        .ToArray();

    public bool HasVisibleTags => VisibleTags.Count > 0;

    public string VersionLabel { get; } = versionLabel;

    public string DownloadCountLabel { get; } = downloadCountLabel;

    public string UpdatedLabel { get; } = updatedLabel;

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
        string.Join(" ", DisplayTags)
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
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
