using Avalonia.Controls;
using PCL.Extensions.Contracts;

namespace PCL.Extensions.Avalonia;

public enum ExtensionPageArea { Download, Settings, Tools }
public enum ExtensionContextActionLocation { Instance, Mod, World, Resource }

public static class ExtensionRegions
{
    public const string DownloadInstallExtras = "download.install.extras";
    public const string DownloadCatalogExtras = "download.catalog.extras";
    public const string DownloadResourceExtras = "download.resources.extras";
    public const string InstanceOverviewCards = "instance.overview.cards";
    public const string InstanceResourceExtras = "instance.resources.extras";
    public const string SettingsSections = "settings.extensions.sections";
}

public sealed record ExtensionSurfaceContext
{
    public string? InstanceId { get; init; }
    public string? MinecraftVersion { get; init; }
    public string? Loader { get; init; }
    public string? WorldPath { get; init; }
    public ExtensionResourceQuery? ResourceQuery { get; init; }
    public CancellationToken Lifetime { get; init; }
}

public sealed record ExtensionPage
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public ExtensionPageArea Area { get; init; }
    public int Order { get; init; }
}

public sealed record ExtensionSection
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public required string Region { get; init; }
    public int Order { get; init; }
}

public sealed record ExtensionContextAction
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? TitleKey { get; init; }
    public ExtensionContextActionLocation Location { get; init; }
    public int Order { get; init; }
}

public sealed record ExtensionActionContext
{
    public required string InstanceId { get; init; }
    public string? ResourcePath { get; init; }
}

public interface IExtensionContributions
{
    IDisposable RegisterPage(ExtensionPage page, Func<ExtensionSurfaceContext, Control> create);
    ValueTask ShowPageAsync(string pageId, CancellationToken cancellationToken = default);
    IDisposable RegisterSection(ExtensionSection section, Func<ExtensionSurfaceContext, Control> create);
    IDisposable RegisterAction(ExtensionContextAction action,
        Func<ExtensionActionContext, CancellationToken, ValueTask> execute,
        Func<ExtensionActionContext, bool>? canExecute = null);
}
