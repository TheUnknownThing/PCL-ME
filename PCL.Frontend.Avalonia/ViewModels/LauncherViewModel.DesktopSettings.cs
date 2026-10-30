using System.Globalization;
using PCL.Frontend.Avalonia.Models;
using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed partial class LauncherViewModel
{
    private bool _keepRunningInTray;
    private bool _desktopPetEnabled;
    private bool _desktopPetTopmost = true;
    private double _desktopPetScale = 1;

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

    internal FrontendDesktopSettings DesktopSettings => new(KeepRunningInTray, DesktopPetEnabled, DesktopPetTopmost, DesktopPetScale);

    private void InitializeDesktopSettings()
    {
        var settings = _setupComposition.LauncherMisc.Desktop;
        _keepRunningInTray = settings.KeepRunningInTray;
        _desktopPetEnabled = settings.PetEnabled;
        _desktopPetTopmost = settings.PetTopmost;
        _desktopPetScale = settings.PetScale;
        RaisePropertyChanged(nameof(KeepRunningInTray));
        RaisePropertyChanged(nameof(DesktopPetEnabled));
        RaisePropertyChanged(nameof(DesktopPetTopmost));
        RaisePropertyChanged(nameof(DesktopPetScale));
        RaisePropertyChanged(nameof(DesktopPetScaleText));
    }
}
