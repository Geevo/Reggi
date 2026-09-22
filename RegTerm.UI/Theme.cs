using RegTerm.Core;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace RegTerm.UI;

/// <summary>
/// Application colour palette. Call <see cref="Apply"/> after Application.Init.
/// </summary>
public static class Theme
{
    private const Color _surface = Color.Black;
    private const Color _text = Color.Gray;
    private const Color _muted = Color.DarkGray;
    private const Color _accent = Color.BrightCyan;

    public static ColorScheme Pane { get; private set; } = null!;
    public static ColorScheme Frame { get; private set; } = null!;
    public static ColorScheme PathBar { get; private set; } = null!;
    public static ColorScheme Status { get; private set; } = null!;
    public static ColorScheme Dialog { get; private set; } = null!;
    public static ColorScheme Danger { get; private set; } = null!;
    public static ColorScheme ScrollBar { get; private set; } = null!;

    public static Attribute Normal { get; private set; }
    public static Attribute Dimmed { get; private set; }
    public static Attribute Highlight { get; private set; }
    public static Attribute Selected { get; private set; }
    public static Attribute SelectedBlurred { get; private set; }
    public static Attribute Error { get; private set; }
    public static Attribute Heading { get; private set; }

    private static Attribute _stringValue;
    private static Attribute _numericValue;
    private static Attribute _binaryValue;

    public static void Apply()
    {
        Normal = Attribute.Make(_text, _surface);
        Dimmed = Attribute.Make(_muted, _surface);
        Highlight = Attribute.Make(_accent, _surface);
        Selected = Attribute.Make(_surface, _accent);
        SelectedBlurred = Attribute.Make(Color.White, Color.DarkGray);
        Error = Attribute.Make(Color.BrightRed, _surface);
        Heading = Attribute.Make(Color.White, _surface);

        _stringValue = Attribute.Make(Color.BrightGreen, _surface);
        _numericValue = Attribute.Make(Color.BrightYellow, _surface);
        _binaryValue = Attribute.Make(Color.BrightMagenta, _surface);

        Pane = new ColorScheme
        {
            Normal = Normal,
            Focus = Selected,
            HotNormal = Highlight,
            HotFocus = Selected,
            Disabled = Dimmed
        };

        Frame = new ColorScheme
        {
            Normal = Dimmed,
            Focus = Highlight,
            HotNormal = Highlight,
            HotFocus = Highlight,
            Disabled = Dimmed
        };

        PathBar = new ColorScheme
        {
            Normal = Normal,
            Focus = Normal,
            HotNormal = Highlight,
            HotFocus = Highlight,
            Disabled = Dimmed
        };

        Status = new ColorScheme
        {
            Normal = Dimmed,
            Focus = Selected,
            HotNormal = Highlight,
            HotFocus = Selected,
            Disabled = Dimmed
        };

        Dialog = new ColorScheme
        {
            Normal = Attribute.Make(_text, Color.Black),
            Focus = Selected,
            HotNormal = Highlight,
            HotFocus = Selected,
            Disabled = Dimmed
        };

        Danger = new ColorScheme
        {
            Normal = Error,
            Focus = Attribute.Make(Color.Black, Color.BrightRed),
            HotNormal = Error,
            HotFocus = Attribute.Make(Color.Black, Color.BrightRed),
            Disabled = Dimmed
        };

        ScrollBar = new ColorScheme
        {
            Normal = Dimmed,
            Focus = Highlight,
            HotNormal = Highlight,
            HotFocus = Highlight,
            Disabled = Dimmed
        };

        Colors.Base = Pane;
        Colors.Dialog = Dialog;
        Colors.Menu = Status;
        Colors.Error = Danger;
        Colors.TopLevel = Pane;
    }

    /// <summary>Colour used for a value's data, chosen by its type.</summary>
    public static Attribute ForValueKind(RegistryValueType kind) => kind switch
    {
        RegistryValueType.DWord or RegistryValueType.QWord => _numericValue,
        RegistryValueType.Binary or RegistryValueType.None => _binaryValue,
        _ => _stringValue
    };
}
