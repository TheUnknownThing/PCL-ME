using System.Globalization;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private static readonly PetAction[] ClickActionOrder =
    [
        PetAction.PlayAnimation,
        PetAction.ShowLauncher,
        PetAction.HidePet,
        PetAction.LaunchSelected,
        PetAction.None
    ];

    private static readonly WindowCloseAction[] CloseActionOrder =
    [
        WindowCloseAction.MinimizeToTray,
        WindowCloseAction.Exit
    ];

    private static readonly PetAction[] DoubleClickActionOrder =
    [
        PetAction.ShowLauncher,
        PetAction.PlayAnimation,
        PetAction.HidePet,
        PetAction.LaunchSelected,
        PetAction.None
    ];

    private bool _keepRunningInTray;
    private bool _desktopPetEnabled;
    private bool _desktopPetTopmost = true;
    private double _desktopPetScale = 1;
    private bool _desktopPetUseCustom;
    private int _selectedDesktopPetPresetIndex;
    private int _selectedDesktopPetClickActionIndex;
    private int _selectedDesktopPetDoubleClickActionIndex;
    private int _selectedWindowCloseActionIndex;
    private bool _closeActionExplicit;
    private string _desktopPetImagePath = string.Empty;
    private string _desktopPetPreviewPath = string.Empty;
    private Bitmap? _desktopPetImagePreview;
    private bool _isChoosingDesktopPetImage;
    private ActionCommand? _chooseDesktopPetImageCommand;

    public IReadOnlyList<string> DesktopPetPresetOptions => SetupText.LauncherMisc.DesktopPetPresetOptions;
    public IReadOnlyList<string> DesktopPetClickActionOptions => SetupText.LauncherMisc.DesktopPetClickActionOptions;
    public IReadOnlyList<string> DesktopPetDoubleClickActionOptions => SetupText.LauncherMisc.DesktopPetDoubleClickActionOptions;
    public IReadOnlyList<string> CloseActionOptions => SetupText.LauncherMisc.CloseActionOptions;

    public int SelectedDesktopPetPresetIndex
    {
        get => _selectedDesktopPetPresetIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, DesktopPetPresetOptions.Count, out var index)) return;
            if (!SetProperty(ref _selectedDesktopPetPresetIndex, index)) return;
            DesktopPetUseCustom = false;
        }
    }

    public bool DesktopPetUseCustom
    {
        get => _desktopPetUseCustom;
        set
        {
            if (!SetProperty(ref _desktopPetUseCustom, value)) return;
            RaisePropertyChanged(nameof(IsDesktopPetPresetSelectionEnabled));
            RaisePropertyChanged(nameof(IsDesktopPetCustomSelectionEnabled));
        }
    }

    public bool IsDesktopPetPresetSelectionEnabled => !DesktopPetUseCustom;
    public bool IsDesktopPetCustomSelectionEnabled => DesktopPetUseCustom;

    public int SelectedDesktopPetClickActionIndex
    {
        get => _selectedDesktopPetClickActionIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, DesktopPetClickActionOptions.Count, out var index)) return;
            SetProperty(ref _selectedDesktopPetClickActionIndex, index);
        }
    }

    public int SelectedDesktopPetDoubleClickActionIndex
    {
        get => _selectedDesktopPetDoubleClickActionIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, DesktopPetDoubleClickActionOptions.Count, out var index)) return;
            SetProperty(ref _selectedDesktopPetDoubleClickActionIndex, index);
        }
    }

    public int SelectedCloseActionIndex
    {
        get => _selectedWindowCloseActionIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, CloseActionOptions.Count, out var index)) return;
            if (!SetProperty(ref _selectedWindowCloseActionIndex, index)) return;
            _closeActionExplicit = true;
        }
    }

    public ActionCommand ChooseDesktopPetImageCommand =>
        _chooseDesktopPetImageCommand ??= new ActionCommand(() => _ = ChooseDesktopPetImageAsync());

    public bool KeepRunningInTray
    {
        get => _keepRunningInTray;
        set => SetProperty(ref _keepRunningInTray, value);
    }

    public bool DesktopPetEnabled
    {
        get => _desktopPetEnabled;
        set => SetProperty(ref _desktopPetEnabled, value);
    }

    public bool DesktopPetTopmost
    {
        get => _desktopPetTopmost;
        set => SetProperty(ref _desktopPetTopmost, value);
    }

    public double DesktopPetScale
    {
        get => _desktopPetScale;
        set
        {
            if (SetProperty(ref _desktopPetScale, FrontendDesktopSettingsService.NormalizeScale(value)))
            {
                RaisePropertyChanged(nameof(DesktopPetScaleText));
            }
        }
    }

    public string DesktopPetScaleText => DesktopPetScale.ToString("P0", CultureInfo.CurrentCulture);

    public string DesktopPetImagePath
    {
        get => _desktopPetImagePath;
        private set
        {
            if (SetProperty(ref _desktopPetImagePath, value)) RefreshDesktopPetImagePreview();
        }
    }

    public bool HasDesktopPetImage => !string.IsNullOrWhiteSpace(DesktopPetImagePath);
    public Bitmap? DesktopPetImagePreview => _desktopPetImagePreview;

    internal FrontendDesktopSettings DesktopSettings => new(
        KeepRunningInTray,
        DesktopPetEnabled,
        DesktopPetTopmost,
        DesktopPetScale,
        DesktopPetUseCustom,
        PetPresetCatalog.All[_selectedDesktopPetPresetIndex].Id,
        DesktopPetImagePath,
        ClickActionOrder[_selectedDesktopPetClickActionIndex],
        DoubleClickActionOrder[_selectedDesktopPetDoubleClickActionIndex],
        _closeActionExplicit ? CloseActionOrder[_selectedWindowCloseActionIndex] : null);

    private async Task ChooseDesktopPetImageAsync()
    {
        if (_isChoosingDesktopPetImage) return;
        _isChoosingDesktopPetImage = true;
        try
        {
            var source = await _launcherActionService.PickOpenFileAsync(
                T("desktop.settings.choose_image"), T("desktop.settings.image_filter"),
                "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp");
            if (string.IsNullOrWhiteSpace(source)) return;
            var imported = await Task.Run(() => FrontendDesktopPetImageService.Import(
                _launcherActionService.RuntimePaths.DataDirectory, source, allowAnimation: false));
            if (string.Equals(imported, DesktopPetImagePath, StringComparison.Ordinal))
            {
                RefreshDesktopPetImagePreview(force: true);
                RaisePropertyChanged(nameof(DesktopPetImagePath));
            }
            else
            {
                DesktopPetImagePath = imported;
            }
        }
        catch (Exception)
        {
            await ShowInAppAlertAsync(T("desktop.settings.image_error_title"),
                T("desktop.settings.image_error_message"), T("common.actions.confirm"));
        }
        finally
        {
            _isChoosingDesktopPetImage = false;
            RaisePropertyChanged(nameof(SelectedDesktopPetPresetIndex));
        }
    }

    private void RefreshDesktopPetImagePreview(bool force = false)
    {
        if (force || !string.Equals(_desktopPetPreviewPath, DesktopPetImagePath, StringComparison.Ordinal))
        {
            var previous = _desktopPetImagePreview;
            _desktopPetImagePreview = FrontendDesktopPetImageService.TryLoad(DesktopPetImagePath);
            _desktopPetPreviewPath = DesktopPetImagePath;
            RaisePropertyChanged(nameof(DesktopPetImagePreview));
            previous?.Dispose();
        }
        RaisePropertyChanged(nameof(HasDesktopPetImage));
    }

    private void InitializeDesktopSettings()
    {
        var settings = _setupComposition.LauncherMisc.Desktop;
        _keepRunningInTray = settings.KeepRunningInTray;
        _desktopPetEnabled = settings.PetEnabled;
        _desktopPetTopmost = settings.PetTopmost;
        _desktopPetScale = settings.PetScale;
        _desktopPetUseCustom = settings.PetUseCustom;
        _desktopPetImagePath = settings.PetImagePath;
        _selectedDesktopPetPresetIndex = FindPetPresetIndex(settings.PetPresetId);
        _selectedDesktopPetClickActionIndex = Math.Max(0, Array.IndexOf(ClickActionOrder, settings.PetClickAction));
        _selectedDesktopPetDoubleClickActionIndex = Math.Max(0, Array.IndexOf(DoubleClickActionOrder, settings.PetDoubleClickAction));
        var closeAction = settings.CloseAction
                          ?? (settings.KeepRunningInTray ? WindowCloseAction.MinimizeToTray : WindowCloseAction.Exit);
        _selectedWindowCloseActionIndex = Math.Max(0, Array.IndexOf(CloseActionOrder, closeAction));
        _closeActionExplicit = settings.CloseAction.HasValue;
        RaisePropertyChanged(nameof(KeepRunningInTray));
        RaisePropertyChanged(nameof(DesktopPetEnabled));
        RaisePropertyChanged(nameof(DesktopPetTopmost));
        RaisePropertyChanged(nameof(DesktopPetScale));
        RaisePropertyChanged(nameof(DesktopPetScaleText));
        RaisePropertyChanged(nameof(DesktopPetUseCustom));
        RaisePropertyChanged(nameof(IsDesktopPetPresetSelectionEnabled));
        RaisePropertyChanged(nameof(IsDesktopPetCustomSelectionEnabled));
        RaisePropertyChanged(nameof(SelectedDesktopPetPresetIndex));
        RaisePropertyChanged(nameof(SelectedDesktopPetClickActionIndex));
        RaisePropertyChanged(nameof(SelectedDesktopPetDoubleClickActionIndex));
        RaisePropertyChanged(nameof(SelectedCloseActionIndex));
        RaisePropertyChanged(nameof(DesktopPetImagePath));
        RefreshDesktopPetImagePreview();
    }

    private static int FindPetPresetIndex(string? presetId)
    {
        for (var i = 0; i < PetPresetCatalog.All.Count; i++)
        {
            if (string.Equals(PetPresetCatalog.All[i].Id, presetId, StringComparison.Ordinal)) return i;
        }
        return 0;
    }
}
