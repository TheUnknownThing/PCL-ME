using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace PCL.Frontend.Avalonia.Desktop.Panes.Right;

/// <summary>
/// A vertical items panel that stacks its children and lets any child whose
/// <see cref="Layoutable.VerticalAlignment"/> is <see cref="VerticalAlignment.Stretch"/>
/// fill the remaining height, while the others keep their desired (natural) size.
///
/// This is used by the Minecraft version catalog so that the single expanded
/// section card fills the catalog area (and therefore the displayed window) while
/// the other, collapsed cards stay compact at the top. The expanded card is given a
/// bounded height, which is what lets the list inside it scroll independently of the
/// outer page scroll viewer.
///
/// The panel only stretches to fill a bounded height; when it is measured with an
/// unbounded height it simply reports the summed natural height of its children, so
/// it behaves like a plain <see cref="StackPanel"/> in a scroll view.
/// </summary>
internal sealed class CatalogSectionPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsPositiveInfinity(availableSize.Width) ? 0 : availableSize.Width;

        var desiredWidth = 0.0;
        var naturalHeight = 0.0;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            // Measure every child with the available width and an unbounded height so
            // its natural (desired) size is reported regardless of whether it will
            // end up filling the remaining space.
            var margin = child.Margin;
            child.Measure(new Size(
                Math.Max(0, width - margin.Left - margin.Right),
                double.PositiveInfinity));
            desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width + margin.Left + margin.Right);
            naturalHeight += child.DesiredSize.Height + margin.Top + margin.Bottom;
        }

        // When the host gives us a bounded height we are expected to fill it, so report
        // that height (the stretch children absorb the difference). When unbounded we
        // report the natural size so the surrounding scroll view sizes us correctly.
        var height = double.IsPositiveInfinity(availableSize.Height)
            ? naturalHeight
            : Math.Min(availableSize.Height, Math.Max(naturalHeight, 0));

        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var height = finalSize.Height;

        var naturalTotal = 0.0;
        var stretchCount = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var margin = child.Margin;
            if (child.VerticalAlignment == VerticalAlignment.Stretch)
            {
                stretchCount++;
            }
            else
            {
                naturalTotal += child.DesiredSize.Height + margin.Top + margin.Bottom;
            }
        }

        // Space left for the stretch children once the natural children are placed.
        var remaining = Math.Max(0, height - naturalTotal);
        var share = stretchCount > 0 ? remaining / stretchCount : 0;

        var cursor = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var margin = child.Margin;
            double childHeight;
            double slotHeight;
            if (child.VerticalAlignment == VerticalAlignment.Stretch)
            {
                slotHeight = share;
                childHeight = Math.Max(0, share - margin.Top - margin.Bottom);
            }
            else
            {
                slotHeight = child.DesiredSize.Height + margin.Top + margin.Bottom;
                childHeight = child.DesiredSize.Height;
            }

            child.Arrange(new Rect(
                margin.Left,
                cursor + margin.Top,
                Math.Max(0, width - margin.Left - margin.Right),
                childHeight));
            cursor += slotHeight;
        }

        return finalSize;
    }
}
