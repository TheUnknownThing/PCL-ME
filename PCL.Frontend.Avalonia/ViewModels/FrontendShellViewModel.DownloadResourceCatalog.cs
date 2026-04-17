using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Frontend.Avalonia.Desktop.Dialogs;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.ViewModels.ShellPanes;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class FrontendShellViewModel
{
    private const int DownloadResourcePageSize = 40;
    private readonly ActionCommand _resetDownloadResourceFiltersCommand;
    private readonly ActionCommand _installDownloadResourceModPackCommand;
    private readonly ActionCommand _searchDownloadResourceCommand;
    private readonly ActionCommand _firstDownloadResourcePageCommand;
    private readonly ActionCommand _previousDownloadResourcePageCommand;
    private readonly ActionCommand _nextDownloadResourcePageCommand;
    private CancellationTokenSource? _downloadResourceRefreshCts;
    private int _downloadResourceRefreshVersion;
    private string _downloadResourceSearchQuery = string.Empty;
    private string _downloadResourceSurfaceTitle = string.Empty;
    private string _downloadResourceLoadingText = string.Empty;
    private string _downloadResourceEmptyStateText = string.Empty;
    private string _downloadResourceEmptyStateHintText = string.Empty;
    private string _downloadResourceHintText = string.Empty;
    private bool _showDownloadResourceHint;
    private bool _showDownloadResourceInstallModPackAction;
    private int _selectedDownloadResourceSourceIndex;
    private int _selectedDownloadResourceTagIndex;
    private int _selectedDownloadResourceSortIndex;
    private int _selectedDownloadResourceVersionIndex;
    private int _selectedDownloadResourceLoaderIndex;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceSourceOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceTagOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceSortOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceVersionOption;
    private DownloadResourceFilterOptionViewModel? _selectedDownloadResourceLoaderOption;
    private int _downloadResourcePageIndex;
    private int _downloadResourceTotalPages = 1;
    private int _downloadResourceTotalEntryCount;
    private bool _downloadResourceHasMoreEntries;
    private bool _downloadResourceSupportsModrinth = true;
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceSourceOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceTagOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceSortOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceVersionOptions = [];
    private IReadOnlyList<DownloadResourceFilterOptionViewModel> _downloadResourceLoaderOptions = [];
    private IReadOnlyList<DownloadResourceEntryViewModel> _allDownloadResourceEntries = [];

    public ObservableCollection<DownloadResourceEntryViewModel> DownloadResourceEntries { get; } = [];
    public ObservableCollection<DownloadResourcePaginationItemViewModel> DownloadResourcePaginationItems { get; } = [];
}
