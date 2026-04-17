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
    private string _choiceSearchQuery = string.Empty;

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
                RaisePropertyChanged(nameof(ShowChoiceSearchBox));
                RaisePropertyChanged(nameof(ShowFilteredEmptyState));
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
                RaisePropertyChanged(nameof(ShowFilteredEmptyState));
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
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                RaisePropertyChanged(nameof(ShowFilteredEmptyState));
            }
        }
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
        set
        {
            if (SetProperty(ref _choices, value))
            {
                RaisePropertyChanged(nameof(VisibleChoices));
                RaisePropertyChanged(nameof(ShowChoiceSearchBox));
                RaisePropertyChanged(nameof(ShowFilteredEmptyState));
            }
        }
    }

    public bool CanClear
    {
        get => _canClear;
        set => SetProperty(ref _canClear, value);
    }

    public string ChoiceSearchQuery
    {
        get => _choiceSearchQuery;
        set
        {
            if (SetProperty(ref _choiceSearchQuery, value))
            {
                RaisePropertyChanged(nameof(VisibleChoices));
                RaisePropertyChanged(nameof(ShowChoiceSearchBox));
                RaisePropertyChanged(nameof(ShowFilteredEmptyState));
                RaisePropertyChanged(nameof(FilteredEmptyStateText));
            }
        }
    }

    public IReadOnlyList<DownloadInstallChoiceItemViewModel> VisibleChoices => GetVisibleChoices();

    public bool ShowChoiceSearchBox => CanExpand
        && (Choices.Count >= 8 || !string.IsNullOrWhiteSpace(ChoiceSearchQuery));

    public bool ShowFilteredEmptyState => CanExpand
        && IsExpanded
        && !IsLoading
        && Choices.Count > 0
        && VisibleChoices.Count == 0;

    public string FilteredEmptyStateText => $"No versions matched \"{ChoiceSearchQuery.Trim()}\".";

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

    private IReadOnlyList<DownloadInstallChoiceItemViewModel> GetVisibleChoices()
    {
        var query = ChoiceSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Choices;
        }

        return Choices
            .Where(choice => choice.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || choice.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
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
