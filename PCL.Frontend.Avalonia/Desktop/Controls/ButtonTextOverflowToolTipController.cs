using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace PCL.Frontend.Avalonia.Desktop.Controls;

internal sealed class ButtonTextOverflowToolTipController
{
    private const double OverflowTolerance = 0.5;

    private readonly Control _host;
    private readonly IReadOnlyList<TextBlock> _textBlocks;
    private bool _refreshQueued;

    public ButtonTextOverflowToolTipController(Control host, params TextBlock[] textBlocks)
    {
        _host = host;
        _textBlocks = textBlocks;

        _host.AttachedToVisualTree += OnVisualStateChanged;
        _host.SizeChanged += OnSizeChanged;

        foreach (var textBlock in _textBlocks)
        {
            textBlock.AttachedToVisualTree += OnVisualStateChanged;
            textBlock.SizeChanged += OnSizeChanged;
            textBlock.PropertyChanged += OnTextBlockPropertyChanged;
        }

        QueueRefresh();
    }

    private void OnVisualStateChanged(object? sender, VisualTreeAttachmentEventArgs e)
    {
        QueueRefresh();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        QueueRefresh();
    }

    private void OnTextBlockPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Render);
    }

    private void Refresh()
    {
        _refreshQueued = false;

        var lines = new List<string>();
        foreach (var textBlock in _textBlocks)
        {
            if (!IsOverflowing(textBlock))
            {
                continue;
            }

            var text = textBlock.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || lines.Contains(text))
            {
                continue;
            }

            lines.Add(text);
        }

        ToolTip.SetTip(_host, lines.Count == 0 ? null : string.Join(Environment.NewLine, lines));
    }

    private static bool IsOverflowing(TextBlock textBlock)
    {
        if (!textBlock.IsVisible ||
            string.IsNullOrWhiteSpace(textBlock.Text) ||
            textBlock.Bounds.Width <= 0 ||
            textBlock.Bounds.Height <= 0)
        {
            return false;
        }

        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desiredSize = textBlock.DesiredSize;

        return desiredSize.Width > textBlock.Bounds.Width + OverflowTolerance ||
               desiredSize.Height > textBlock.Bounds.Height + OverflowTolerance;
    }
}
