using PCL.Frontend.Avalonia.Workflows;

namespace PCL.Frontend.Avalonia.ViewModels;

internal sealed class LaunchProfileEntryViewModel(
    string title,
    string info,
    bool isSelected,
    ActionCommand command,
    string accessoryIconData,
    bool accessoryIsSpinning,
    string accessoryToolTip,
    ActionCommand? accessoryCommand,
    string secondaryAccessoryIconData,
    string secondaryAccessoryToolTip,
    ActionCommand? secondaryAccessoryCommand)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public bool IsSelected { get; } = isSelected;

    public ActionCommand Command { get; } = command;

    public string AccessoryIconData { get; } = accessoryIconData;

    public bool AccessoryIsSpinning { get; } = accessoryIsSpinning;

    public string AccessoryToolTip { get; } = accessoryToolTip;

    public ActionCommand? AccessoryCommand { get; } = accessoryCommand;

    public string SecondaryAccessoryIconData { get; } = secondaryAccessoryIconData;

    public string SecondaryAccessoryToolTip { get; } = secondaryAccessoryToolTip;

    public ActionCommand? SecondaryAccessoryCommand { get; } = secondaryAccessoryCommand;
}

internal enum LaunchProfileSurfaceKind
{
    Auto = 0,
    Summary = 1,
    Chooser = 2,
    Selection = 3,
    OfflineEditor = 4,
    MicrosoftEditor = 5,
    AuthlibEditor = 6
}

internal sealed partial class FrontendShellViewModel
{
    private sealed class LaunchProfileRequestException(string url, int statusCode, string responseBody)
        : Exception($"HTTP {statusCode}: {url}")
    {
        public int StatusCode { get; } = statusCode;

        public string ResponseBody { get; } = responseBody;
    }

    private sealed record LaunchProfileRefreshResult(
        bool WasChecked,
        string Message,
        bool ShouldInvalidateAvatarCache = false);
}
