using RegTerm.Core;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace RegTerm.UI;

/// <summary>
/// Application colour palette. Call <see cref="Apply"/> after Application.Init.
/// </summary>
public static class Theme
{
    private const Color Surface = Color.Black;
    private const Color Text = Color.Gray;
    private const Color Muted = Color.DarkGray;
    private const Color Accent = Color.BrightCyan;

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

    private static Attribute stringValue;
    private static Attribute numericValue;
    private static Attribute binaryValue;

    public static void Apply()
    {
        Normal = Attribute.Make(Text, Surface);
        Dimmed = Attribute.Make(Muted, Surface);
        Highlight = Attribute.Make(Accent, Surface);
        Selected = Attribute.Make(Surface, Accent);
        SelectedBlurred = Attribute.Make(Color.White, Color.DarkGray);
        Error = Attribute.Make(Color.BrightRed, Surface);
        Heading = Attribute.Make(Color.White, Surface);

        stringValue = Attribute.Make(Color.BrightGreen, Surface);
        numericValue = Attribute.Make(Color.BrightYellow, Surface);
        binaryValue = Attribute.Make(Color.BrightMagenta, Surface);

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
            Normal = Attribute.Make(Text, Color.Black),
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
        RegistryValueType.DWord or RegistryValueType.QWord => numericValue,
        RegistryValueType.Binary or RegistryValueType.None => binaryValue,
        _ => stringValue
    };
}
