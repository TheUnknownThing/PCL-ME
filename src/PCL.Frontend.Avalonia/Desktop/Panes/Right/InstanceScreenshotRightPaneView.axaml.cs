using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

internal sealed partial class InstanceScreenshotRightPaneView : UserControl
{
    private const double PreferredTileWidth = 250d;
    private const double MinTileWidth = 190d;
    private const double MaxTileWidth = 360d;
    private const double TileGap = 8d;
    private const double TileAspectRatio = 250d / 170d; // width / height
    private WrapPanel? _screenshotWrapPanel;

    public InstanceScreenshotRightPaneView()
    {
        InitializeComponent();
        SizeChanged += OnLayoutChanged;
        ScreenshotItemsControl.SizeChanged += OnLayoutChanged;
        AttachedToVisualTree += (_, _) => QueueUpdateScreenshotTileLayout();
    }

    private void OnLayoutChanged(object? sender, SizeChangedEventArgs e)
    {
        QueueUpdateScreenshotTileLayout();
    }

    private void QueueUpdateScreenshotTileLayout()
    {
        Dispatcher.UIThread.Post(UpdateScreenshotTileLayout, DispatcherPriority.Loaded);
    }

    private void UpdateScreenshotTileLayout()
    {
        var availableWidth = ScreenshotItemsControl.Bounds.Width;
        if (availableWidth <= 0)
        {
            return;
        }

        var wrapPanel = ResolveScreenshotWrapPanel();
        if (wrapPanel is null)
        {
            return;
        }

        var columns = Math.Max(
            1,
            (int)Math.Floor((availableWidth + TileGap) / (PreferredTileWidth + TileGap)));
        while (columns > 1)
        {
            var horizontalGapCount = Math.Max(0, columns - 1);
            var candidateWidth = (availableWidth - horizontalGapCount * TileGap) / columns;
            if (candidateWidth >= MinTileWidth)
            {
                break;
            }

            columns--;
        }

        while (true)
        {
            var horizontalGapCount = Math.Max(0, columns - 1);
            var candidateWidth = (availableWidth - horizontalGapCount * TileGap) / columns;
            if (candidateWidth <= MaxTileWidth)
            {
                break;
            }

            columns++;
        }

        var gapCount = Math.Max(0, columns - 1);
        var tileWidth = Math.Clamp(
            (availableWidth - gapCount * TileGap) / columns,
            MinTileWidth,
            MaxTileWidth);
        var tileHeight = Math.Max(100d, tileWidth / TileAspectRatio);

        if (Math.Abs(wrapPanel.ItemWidth - tileWidth) < 0.5d
            && Math.Abs(wrapPanel.ItemHeight - tileHeight) < 0.5d)
        {
            UpdateTileMargins(wrapPanel, columns);
            return;
        }

        wrapPanel.ItemWidth = tileWidth;
        wrapPanel.ItemHeight = tileHeight;
        UpdateTileMargins(wrapPanel, columns);
    }

    private WrapPanel? ResolveScreenshotWrapPanel()
    {
        if (_screenshotWrapPanel is not null)
        {
            return _screenshotWrapPanel;
        }

        _screenshotWrapPanel = ScreenshotItemsControl
            .GetVisualDescendants()
            .OfType<WrapPanel>()
            .FirstOrDefault();
        return _screenshotWrapPanel;
    }

    private static void UpdateTileMargins(WrapPanel wrapPanel, int columns)
    {
        var children = wrapPanel.Children.OfType<Control>().ToArray();
        if (children.Length == 0)
        {
            return;
        }

        for (var i = 0; i < children.Length; i++)
        {
            var isLastInRow = columns > 0 && (i + 1) % columns == 0;
            var isLastItem = i == children.Length - 1;
            var right = isLastInRow || isLastItem ? 0d : TileGap;
            var next = new Thickness(0, 0, right, TileGap);
            if (children[i].Margin != next)
            {
                children[i].Margin = next;
            }
        }
    }
}
