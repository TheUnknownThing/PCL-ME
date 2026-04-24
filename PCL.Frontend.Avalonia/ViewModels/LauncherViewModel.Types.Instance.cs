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
    int sourceIndex,
    string title,
    string address,
    Bitmap? backgroundImage,
    Bitmap? logo,
    ActionCommand refreshCommand,
    ActionCommand editAddressCommand,
    ActionCommand copyCommand,
    ActionCommand connectCommand,
    ActionCommand inspectCommand) : ViewModelBase
{
    private Bitmap? _logo = logo;
    private string _statusText = "Saved server";
    private IBrush _statusBrush = Brushes.White;
    private string _playerCount = "-/-";
    private string _latency = string.Empty;
    private IBrush _latencyBrush = Brushes.White;
    private string? _playerTooltip;
    private IReadOnlyList<MinecraftServerQueryMotdLineViewModel> _motdLines = [];
    private bool _hasMotd;
    private bool _hasLatency;

    public int SourceIndex { get; } = sourceIndex;

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

    public ActionCommand EditAddressCommand { get; } = editAddressCommand;

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
        string actionToolTip = "View",
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
        string infoToolTip = "Details",
        string websiteToolTip = "Open website",
        string openToolTip = "Open file location",
        string enableToolTip = "Enable",
        string disableToolTip = "Disable",
        string deleteToolTip = "Delete",
        string disabledTagText = "Disabled")
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
