using PCL.Frontend.Avalonia.ViewModels;

namespace PCL.Frontend.Avalonia.Models;

internal sealed record FrontendSetupUpdateStatus(
    UpdateSurfaceState SurfaceState,
    string CurrentVersionName,
    string CurrentVersionDescription,
    string AvailableUpdateName,
    string AvailableUpdatePublisher,
    string AvailableUpdateSummary,
    string AvailableUpdateSource,
    string AvailableUpdateSha256,
    string? AvailableUpdateChangelog,
    string? AvailableUpdateReleaseUrl,
    string? AvailableUpdateDownloadUrl);
