using Reggi.Core;
using Terminal.Gui;

namespace Reggi.UI;

/// <summary>
/// Displays the current key path.
/// </summary>
public sealed class PathBarView : View
{
    private const string _marker = "▎ ";

    private RegistryPath _path;

    public PathBarView()
    {
        Height = 1;
        CanFocus = false;
    }

    public RegistryPath Path
    {
        get => _path;
        set
        {
            if (_path == value) return;
            _path = value;
            SetNeedsDisplay();
        }
    }

    public override void Redraw(Rect bounds)
    {
        var width = Bounds.Width;
        if (width <= 0) return;

        Move(0, 0);
        Driver.SetAttribute(Theme.Highlight);
        var used = Draw.Clipped(Driver, _marker, width);

        var remaining = width - used;
        if (remaining <= 0) return;

        // Keep the leaf visible when the path is clipped.
        var text = Draw.EllipsizeStart(_path.ToString(), remaining);
        var sep = text.IndexOf('\\');

        if (sep > 0)
        {
            Driver.SetAttribute(Theme.Heading);
            remaining -= Draw.Clipped(Driver, text[..sep], remaining);
            Driver.SetAttribute(Theme.Normal);
            remaining -= Draw.Clipped(Driver, text[sep..], remaining);
        }
        else
        {
            Driver.SetAttribute(Theme.Heading);
            remaining -= Draw.Clipped(Driver, text, remaining);
        }

        Driver.SetAttribute(Theme.Normal);
        Draw.Blank(Driver, remaining);
    }
}
