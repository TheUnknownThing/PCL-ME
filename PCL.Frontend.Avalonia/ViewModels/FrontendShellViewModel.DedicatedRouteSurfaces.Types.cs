using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Essentials;
using PCL.Core.App.Tasks;
using PCL.Frontend.Avalonia.Desktop.Controls;
using PCL.Frontend.Avalonia.Icons;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed class InstanceSelectEntryViewModel(
    string title,
    string subtitle,
    string detail,
    IReadOnlyList<string> tags,
    bool isSelected,
    bool isFavorite,
    Bitmap? icon,
    string selectText,
    string favoriteToolTip,
    string openFolderToolTip,
    string deleteToolTip,
    string settingsToolTip,
    ActionCommand selectCommand,
    ActionCommand openSettingsCommand,
    ActionCommand toggleFavoriteCommand,
    ActionCommand openFolderCommand,
    ActionCommand deleteCommand)
{
    private static readonly FrontendIcon NavigationSettingsIcon = FrontendIconCatalog.GetNavigationIcon("settings");

    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public string Detail { get; } = detail;

    public IReadOnlyList<string> Tags { get; } = tags;

    public bool HasTags => Tags.Count > 0;

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public bool HasMetadataContent => HasTags || HasSubtitle;

    public bool IsSelected { get; } = isSelected;

    public bool IsFavorite { get; } = isFavorite;

    public Bitmap? Icon { get; } = icon;

    public string SelectText { get; } = selectText;

    public string FavoriteToolTip { get; } = favoriteToolTip;

    public string FavoriteIconData => IsFavorite
        ? FrontendIconCatalog.FavoriteFilled.Data
        : FrontendIconCatalog.FavoriteOutline.Data;

    public IBrush FavoriteIconBrush => FrontendThemeResourceResolver.GetBrush("ColorBrush3", "#1370F3");

    public string OpenFolderIconData => FrontendIconCatalog.FolderOutline.Data;

    public string OpenFolderToolTip { get; } = openFolderToolTip;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string DeleteToolTip { get; } = deleteToolTip;

    public string SettingsIconData => NavigationSettingsIcon.Data;

    public double SettingsIconScale => NavigationSettingsIcon.Scale;

    public string SettingsToolTip { get; } = settingsToolTip;

    public ActionCommand SelectCommand { get; } = selectCommand;

    public ActionCommand OpenSettingsCommand { get; } = openSettingsCommand;

    public ActionCommand ToggleFavoriteCommand { get; } = toggleFavoriteCommand;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public ActionCommand DeleteCommand { get; } = deleteCommand;
}

internal sealed class InstanceSelectionGroupViewModel : ViewModelBase
{
    private bool _isExpanded;

    public InstanceSelectionGroupViewModel(string title, string headerText, IReadOnlyList<InstanceSelectEntryViewModel> entries, bool isExpanded)
    {
        Title = title;
        HeaderText = headerText;
        Entries = entries;
        _isExpanded = isExpanded;
        ToggleExpandCommand = new ActionCommand(() => IsExpanded = !IsExpanded);
    }

    public string Title { get; }

    public string HeaderText { get; }

    public IReadOnlyList<InstanceSelectEntryViewModel> Entries { get; }

    public int EntryCount => Entries.Count;

    public bool IsExpanded
    {
        get => _isExpanded;
        private set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                RaisePropertyChanged(nameof(ChevronIconPath));
            }
        }
    }

    public string ChevronIconPath => IsExpanded
        ? "M256 640L512 384 768 640 704 704 512 512 320 704Z"
        : "M320 384L512 576 704 384 768 448 512 704 256 448Z";

    public ActionCommand ToggleExpandCommand { get; }
}

internal sealed class InstanceSelectionFolderEntryViewModel(
    string title,
    string path,
    bool isSelected,
    string iconPath,
    string openFolderToolTip,
    string deleteToolTip,
    ActionCommand command,
    ActionCommand openFolderCommand,
    ActionCommand? deleteCommand)
{
    public string Title { get; } = title;

    public string Path { get; } = path;

    public bool IsSelected { get; } = isSelected;

    public string IconPath { get; } = iconPath;

    public ActionCommand Command { get; } = command;

    public string OpenFolderIconData => FrontendIconCatalog.OpenFolder.Data;

    public string OpenFolderToolTip { get; } = openFolderToolTip;

    public ActionCommand OpenFolderCommand { get; } = openFolderCommand;

    public string DeleteIconData => FrontendIconCatalog.DeleteOutline.Data;

    public string DeleteToolTip { get; } = deleteToolTip;

    public ActionCommand? DeleteCommand { get; } = deleteCommand;
}

internal sealed class InstanceSelectionShortcutEntryViewModel(
    string title,
    string description,
    string iconPath,
    ActionCommand command)
{
    public string Title { get; } = title;

    public string Description { get; } = description;

    public string IconPath { get; } = iconPath;

    public ActionCommand Command { get; } = command;
}

internal sealed class TaskManagerEntryViewModel(
    FrontendShellViewModel owner,
    ActionCommand primaryActionCommand,
    ActionCommand pauseCommand) : ViewModelBase
{
    private readonly FrontendShellViewModel _owner = owner;
    private string _title = string.Empty;
    private TaskState _taskState;
    private string _state = string.Empty;
    private string _summary = string.Empty;
    private string _activityText = string.Empty;
    private string _progressText = string.Empty;
    private double _progressValue;
    private bool _hasProgress;
    private string _speedText = string.Empty;
    private string _remainingFilesText = string.Empty;
    private string _progressLabel = string.Empty;
    private string _speedSummaryText = string.Empty;
    private string _remainingFilesSummaryText = string.Empty;
    private int _childCount;
    private IReadOnlyList<TaskManagerStageEntryViewModel> _stageEntries = [];
    private bool _hasPrimaryAction;
    private bool _canPause;

    public string Title => _title;

    public TaskState TaskState => _taskState;

    public string State => _state;

    public string Summary => _summary;

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);

    public bool ShowSummary => HasSummary && !HasStageEntries;

    public string ActivityText => _activityText;

    public bool HasActivityText => !string.IsNullOrWhiteSpace(ActivityText);

    public string ProgressText => _progressText;

    public double ProgressValue => _progressValue;

    public bool HasProgress => _hasProgress;

    public string SpeedText => _speedText;

    public string RemainingFilesText => _remainingFilesText;

    public string ProgressLabel => _progressLabel;

    public string SpeedSummaryText => _speedSummaryText;

    public string RemainingFilesSummaryText => _remainingFilesSummaryText;

    public int ChildCount => _childCount;

    public bool HasChildren => ChildCount > 0;

    public string ChildrenText => _owner.LT("shell.task_manager.entries.children", ("count", ChildCount));

    public IReadOnlyList<TaskManagerStageEntryViewModel> StageEntries => _stageEntries;

    public bool HasStageEntries => StageEntries.Count > 0;

    public bool HasPrimaryAction => _hasPrimaryAction;

    public bool CanPause => _canPause;

    public ActionCommand PrimaryActionCommand { get; } = primaryActionCommand;

    public ActionCommand PauseCommand { get; } = pauseCommand;

    public IBrush StateBadgeBackgroundBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessBackground", "#EAF7F4"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorBackground", "#FFF0F0"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBackground", "#F4F7FB"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBackground", "#F4F7FB"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBackground", "#EDF5FF")
    };

    public IBrush StateBadgeBorderBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessBorder", "#C8E6DF"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorBorder", "#F1C8C8"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBorder", "#DAE3F0"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralBorder", "#DAE3F0"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoBorder", "#CFE0FA")
    };

    public IBrush StateBadgeForegroundBrush => TaskState switch
    {
        TaskState.Success => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessForeground", "#24534E"),
        TaskState.Failed => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorForeground", "#D05B5B"),
        TaskState.Canceled => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
        TaskState.Waiting => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
        _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoForeground", "#5B87DA")
    };

    public void Update(
        string title,
        TaskState taskState,
        string state,
        string summary,
        string activityText,
        string progressText,
        double progressValue,
        bool hasProgress,
        string speedText,
        string remainingFilesText,
        int childCount,
        IReadOnlyList<TaskManagerStageEntryViewModel> stageEntries,
        string progressLabel,
        string speedSummaryText,
        string remainingFilesSummaryText,
        bool hasPrimaryAction,
        bool canPause)
    {
        SetProperty(ref _title, title, nameof(Title));

        if (SetProperty(ref _taskState, taskState, nameof(TaskState)))
        {
            RaisePropertyChanged(nameof(StateBadgeBackgroundBrush));
            RaisePropertyChanged(nameof(StateBadgeBorderBrush));
            RaisePropertyChanged(nameof(StateBadgeForegroundBrush));
        }

        SetProperty(ref _state, state, nameof(State));

        if (SetProperty(ref _summary, summary, nameof(Summary)))
        {
            RaisePropertyChanged(nameof(HasSummary));
            RaisePropertyChanged(nameof(ShowSummary));
        }

        if (SetProperty(ref _activityText, activityText, nameof(ActivityText)))
        {
            RaisePropertyChanged(nameof(HasActivityText));
        }

        SetProperty(ref _progressText, progressText, nameof(ProgressText));
        SetProperty(ref _progressValue, Math.Clamp(progressValue, 0d, 1d) * 100d, nameof(ProgressValue));
        SetProperty(ref _hasProgress, hasProgress, nameof(HasProgress));
        SetProperty(ref _speedText, speedText, nameof(SpeedText));
        SetProperty(ref _remainingFilesText, remainingFilesText, nameof(RemainingFilesText));
        SetProperty(ref _progressLabel, progressLabel, nameof(ProgressLabel));
        SetProperty(ref _speedSummaryText, speedSummaryText, nameof(SpeedSummaryText));
        SetProperty(ref _remainingFilesSummaryText, remainingFilesSummaryText, nameof(RemainingFilesSummaryText));

        if (SetProperty(ref _childCount, childCount, nameof(ChildCount)))
        {
            RaisePropertyChanged(nameof(HasChildren));
            RaisePropertyChanged(nameof(ChildrenText));
        }

        if (SetProperty(ref _stageEntries, stageEntries, nameof(StageEntries)))
        {
            RaisePropertyChanged(nameof(HasStageEntries));
            RaisePropertyChanged(nameof(ShowSummary));
        }

        SetProperty(ref _hasPrimaryAction, hasPrimaryAction, nameof(HasPrimaryAction));
        SetProperty(ref _canPause, canPause, nameof(CanPause));

        PrimaryActionCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
    }
}

internal sealed class TaskManagerStageEntryViewModel(
    string indicator,
    string title,
    string message)
{
    public string Indicator { get; } = indicator;

    public string Title { get; } = title;

    public string Message { get; } = message;

    public bool HasMessageDetail => !string.IsNullOrWhiteSpace(Message) && !string.Equals(Message, Title, StringComparison.Ordinal);

    public IBrush IndicatorBrush => ResolveIndicatorBrush();

    public double IndicatorFontSize => Indicator.EndsWith('%') ? 12.5 : 16d;

    private IBrush ResolveIndicatorBrush()
    {
        return Indicator switch
        {
            "✓" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticSuccessForeground", "#24534E"),
            "×" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticErrorForeground", "#D05B5B"),
            "···" => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5"),
            _ when Indicator.EndsWith('%') => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticInfoForeground", "#5B87DA"),
            _ => FrontendThemeResourceResolver.GetBrush("ColorBrushSemanticNeutralForeground", "#7E8FA5")
        };
    }
}

internal sealed record DedicatedGenericRouteMetadata(
    string Eyebrow,
    string Description,
    IReadOnlyList<LauncherFrontendPageFact> Facts);

internal sealed record InstanceSelectionFolderSnapshot(
    string Label,
    string Directory,
    string StoredPath,
    bool IsPersisted);

internal sealed record InstanceSelectionSnapshot(
    string Name,
    string Subtitle,
    string Detail,
    IReadOnlyList<string> Tags,
    bool IsSelected,
    bool IsStarred,
    bool IsBroken,
    string Directory,
    int DisplayType,
    string VersionLabel,
    string? LoaderLabel,
    string? CustomInfo,
    bool IsCustomLogo,
    string RawLogoPath);

internal sealed record InstanceManifestSnapshot(
    string VersionLabel,
    string? LoaderLabel,
    bool IsBroken);
