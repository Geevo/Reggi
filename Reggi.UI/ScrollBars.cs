using Terminal.Gui;

namespace Reggi.UI;

/// <summary>
/// Attaches a vertical scroll indicator to a list pane.
/// </summary>
internal static class ScrollBars
{
    public static ScrollBarView AttachVertical(ListView list)
    {
        var bar = new ScrollBarView(list, isVertical: true, showBothScrollIndicator: false)
        {
            AutoHideScrollBars = true,      // nothing drawn when the content fits
            ColorScheme = Theme.ScrollBar
        };

        bar.ChangedPosition += () =>
        {
            list.TopItem = bar.Position;

            // Mirror ListView's clamped position.
            if (list.TopItem != bar.Position) bar.Position = list.TopItem;

            list.SetNeedsDisplay();
        };

        // Synchronize changes initiated outside the scroll bar.
        list.DrawContent += _ =>
        {
            bar.Size = list.Source?.Count ?? 0;
            bar.Position = list.TopItem;
            bar.Refresh();
        };

        return bar;
    }
}
