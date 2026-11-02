using PCL.Extensions.Avalonia;
using PCL.Extensions.Contracts;
using PCL.UI.Icons;

namespace PCL.Extensions.GlobalSearch;

public sealed class GlobalSearchExtension : IExtension
{
    private IExtensionContext? _context;
    private IExtensionUi? _ui;
    private IExtensionOverlay? _overlay;
    private SearchPalette? _palette;

    public ValueTask ActivateAsync(IExtensionContext context, CancellationToken cancellationToken)
    {
        _context = context;
        _ui = context.GetService<IExtensionUi>();
        var command = context.Manifest.Commands.Single();
        context.Commands.Register(command, _ => { Toggle(); return ValueTask.CompletedTask; });
        _ui.RegisterAction(new("pcl.global-search.open", command.Title, command.Id, ExtensionActionLocation.Toolbar, command.TitleKey, PclIcons.Search.Data));
        context.Events.Subscribe("launcher.locale.changed", _ => _ui.Dispatch(() => _palette?.RefreshText()));
        return ValueTask.CompletedTask;
    }

    private void Toggle()
    {
        if (_overlay is { IsOpen: true }) { _overlay.Dispose(); return; }
        _palette = new SearchPalette(_context!);
        _palette.CloseRequested += () => _overlay?.Dispose();
        _overlay = _ui!.ShowOverlay(_palette, new() { MaxWidth = 680, Margin = new(24, 64, 24, 24) });
        _overlay.Closed += () => { _overlay = null; _palette = null; };
    }

    public ValueTask DisposeAsync()
    {
        _overlay?.Dispose();
        _overlay = null; _palette = null; _context = null; _ui = null;
        return ValueTask.CompletedTask;
    }
}
