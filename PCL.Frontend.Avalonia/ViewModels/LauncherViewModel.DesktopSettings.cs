using System.Globalization;
using Avalonia.Media.Imaging;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private bool _keepRunningInTray;
    private bool _desktopPetEnabled;
    private bool _desktopPetTopmost = true;
    private double _desktopPetScale = 1;
    private string _desktopPetImagePath = string.Empty;
    private string _desktopPetPreviewPath = string.Empty;
    private Bitmap? _desktopPetImagePreview;
    private bool _isChoosingDesktopPetImage;
    private ActionCommand? _chooseDesktopPetImageCommand;
    private ActionCommand? _resetDesktopPetImageCommand;
    private int _selectedDesktopPetAppearanceIndex;

    public IReadOnlyList<string> DesktopPetAppearanceOptions => SetupText.LauncherMisc.DesktopPetAppearanceOptions;

    public int SelectedDesktopPetAppearanceIndex
    {
        get => HasDesktopPetImage ? FrontendDesktopSettingsService.BuiltinAppearances.Length : _selectedDesktopPetAppearanceIndex;
        set
        {
            if (!TryNormalizeSelectionIndex(value, DesktopPetAppearanceOptions.Count, out var index)) return;
            if (index == FrontendDesktopSettingsService.BuiltinAppearances.Length)
            {
                if (!HasDesktopPetImage) _ = ChooseDesktopPetImageAsync();
                return;
            }
            SetProperty(ref _selectedDesktopPetAppearanceIndex, index);
            DesktopPetImagePath = string.Empty;
        }
    }

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

    public ActionCommand ChooseDesktopPetImageCommand => _chooseDesktopPetImageCommand ??= new(
        () => _ = ChooseDesktopPetImageAsync(), () => !_isChoosingDesktopPetImage);

    public ActionCommand ResetDesktopPetImageCommand => _resetDesktopPetImageCommand ??= new(
        () => DesktopPetImagePath = string.Empty, () => HasDesktopPetImage && !_isChoosingDesktopPetImage);

    internal FrontendDesktopSettings DesktopSettings => new(
        KeepRunningInTray, DesktopPetEnabled, DesktopPetTopmost, DesktopPetScale, DesktopPetImagePath,
        FrontendDesktopSettingsService.BuiltinAppearances[_selectedDesktopPetAppearanceIndex]);

    private async Task ChooseDesktopPetImageAsync()
    {
        if (_isChoosingDesktopPetImage) return;
        _isChoosingDesktopPetImage = true;
        NotifyDesktopPetImageCommands();
        try
        {
            var source = await _launcherActionService.PickOpenFileAsync(
                T("desktop.settings.choose_image"), T("desktop.settings.image_filter"),
                "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp");
            if (string.IsNullOrWhiteSpace(source)) return;
            var imported = await Task.Run(() => FrontendDesktopPetImageService.Import(
                _launcherActionService.RuntimePaths.DataDirectory, source));
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
            NotifyDesktopPetImageCommands();
            RaisePropertyChanged(nameof(SelectedDesktopPetAppearanceIndex));
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
        RaisePropertyChanged(nameof(SelectedDesktopPetAppearanceIndex));
        NotifyDesktopPetImageCommands();
    }

    private void NotifyDesktopPetImageCommands()
    {
        _chooseDesktopPetImageCommand?.NotifyCanExecuteChanged();
        _resetDesktopPetImageCommand?.NotifyCanExecuteChanged();
    }

    private void InitializeDesktopSettings()
    {
        var settings = _setupComposition.LauncherMisc.Desktop;
        _keepRunningInTray = settings.KeepRunningInTray;
        _desktopPetEnabled = settings.PetEnabled;
        _desktopPetTopmost = settings.PetTopmost;
        _desktopPetScale = settings.PetScale;
        _desktopPetImagePath = settings.PetImagePath;
        _selectedDesktopPetAppearanceIndex = Array.IndexOf(FrontendDesktopSettingsService.BuiltinAppearances,
            FrontendDesktopSettingsService.NormalizeAppearance(settings.PetAppearance));
        RaisePropertyChanged(nameof(KeepRunningInTray));
        RaisePropertyChanged(nameof(DesktopPetEnabled));
        RaisePropertyChanged(nameof(DesktopPetTopmost));
        RaisePropertyChanged(nameof(DesktopPetScale));
        RaisePropertyChanged(nameof(DesktopPetScaleText));
        RaisePropertyChanged(nameof(DesktopPetImagePath));
        RefreshDesktopPetImagePreview();
    }
}
