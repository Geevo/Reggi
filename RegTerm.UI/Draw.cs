using Terminal.Gui;
using Rune = System.Rune;

namespace RegTerm.UI;

/// <summary>Small helpers for writing clipped, padded text straight to the driver.</summary>
internal static class Draw
{
    /// <summary>Writes text clipped to <paramref name="width"/>, space-padding any remainder.</summary>
    public static void Cell(ConsoleDriver driver, string text, int width)
    {
        if (width <= 0) return;
        Blank(driver, width - Clipped(driver, text, width));
    }

    /// <summary>
    /// Writes text clipped by terminal column width and returns the columns used.
    /// </summary>
    public static int Clipped(ConsoleDriver driver, string text, int width)
    {
        if (width <= 0) return 0;

        var used = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var rune = NextRune(text, ref i);

        // Tabs and newlines would move the cursor.
            if (rune == '\t' || rune == '\n' || rune == '\r') rune = ' ';

            var cells = Rune.ColumnWidth(rune);
            if (cells <= 0) cells = 1;          // combining marks must still advance
            if (used + cells > width) break;    // never spill past the column

            driver.AddRune(rune);
            used += cells;
        }
        return used;
    }

    /// <summary>Reads one rune, combining a surrogate pair and replacing a lone half.</summary>
    private static Rune NextRune(string text, ref int index)
    {
        var ch = text[index];

        if (char.IsHighSurrogate(ch) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            var pair = char.ConvertToUtf32(ch, text[index + 1]);
            index++;
            return new Rune((uint)pair);
        }

        return char.IsSurrogate(ch) ? new Rune(0xFFFD) : ch;
    }

    public static void Blank(ConsoleDriver driver, int count)
    {
        for (var i = 0; i < count; i++) driver.AddRune(' ');
    }

    /// <summary>Trims from the left, prefixing an ellipsis, so the tail stays visible.</summary>
    public static string EllipsizeStart(string text, int width)
    {
        if (width <= 0) return string.Empty;
        if (text.Length <= width) return text;
        return width <= 1 ? "…" : string.Concat("…", text.AsSpan(text.Length - (width - 1)));
    }
}

/// <summary>
    /// Column widths shared by the values header and rows.
/// </summary>
internal readonly record struct ValueColumns(int Name, int Type, int Data)
{
    public const int Gap = 2;

    public static ValueColumns For(int width)
    {
        var name = Math.Clamp(width * 32 / 100, 8, 34);
        var type = 14;
        var data = width - name - type - Gap * 2;

        if (data < 10)
        {
            name = Math.Max(6, name + data - 10);
            data = width - name - type - Gap * 2;
        }
        if (data < 6)
        {
            type = Math.Max(6, type + data - 6);
            data = width - name - type - Gap * 2;
        }

        return new ValueColumns(Math.Max(0, name), Math.Max(0, type), Math.Max(0, data));
    }
}
