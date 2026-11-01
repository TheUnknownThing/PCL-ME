namespace PCL.Frontend.Avalonia.Models;

internal enum FrontendInstallSelectionKind
{
    NotInstalled = 0,
    Available = 1,
    Installed = 2,
    Versioned = 3
}

internal sealed record FrontendInstallSelectionState(
    FrontendInstallSelectionKind Kind,
    string DisplayText)
{
    public bool HasSelection => Kind is FrontendInstallSelectionKind.Installed or FrontendInstallSelectionKind.Versioned;

    public bool CanResolveToChoice => Kind == FrontendInstallSelectionKind.Versioned;

    public static FrontendInstallSelectionState NotInstalled(string displayText)
        => new(FrontendInstallSelectionKind.NotInstalled, displayText);

    public static FrontendInstallSelectionState Available(string displayText)
        => new(FrontendInstallSelectionKind.Available, displayText);

    public static FrontendInstallSelectionState Installed(string displayText)
        => new(FrontendInstallSelectionKind.Installed, displayText);

    public static FrontendInstallSelectionState Versioned(string displayText)
        => new(FrontendInstallSelectionKind.Versioned, displayText);
}
