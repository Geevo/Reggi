using Terminal.Gui;

namespace Reggi.UI;

/// <summary>
/// Column headings for the values pane.
/// </summary>
public sealed class ValueHeaderView : View
{
    public ValueHeaderView()
    {
        Height = 1;
        CanFocus = false;
    }

    public override void Redraw(Rect bounds)
    {
        var width = Bounds.Width;
        if (width <= 0) return;

        var cols = ValueColumns.For(width);

        Move(0, 0);
        Driver.SetAttribute(Theme.Heading);
        Draw.Cell(Driver, "Name", cols.Name);
        Draw.Blank(Driver, ValueColumns.Gap);
        Draw.Cell(Driver, "Type", cols.Type);
        Draw.Blank(Driver, ValueColumns.Gap);
        Draw.Cell(Driver, "Data", cols.Data);
    }
}
