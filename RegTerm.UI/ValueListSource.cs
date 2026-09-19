using System.Collections;
using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

/// <summary>
/// Renders registry values and empty or inaccessible notices.
/// </summary>
public sealed class ValueListSource : IListDataSource
{
    private IReadOnlyList<RegistryValueItem> items = [];
    private string? notice;
    private bool noticeIsError;

    public bool ShowingNotice => notice is not null;
    public int Count => notice is not null ? 1 : items.Count;
    public int Length => 120;

    public void SetValues(IReadOnlyList<RegistryValueItem> values)
    {
        items = values;
        notice = null;
        noticeIsError = false;
    }

    public void SetNotice(string text, bool isError)
    {
        items = [];
        notice = text;
        noticeIsError = isError;
    }

    /// <summary>The value at a row, or null if this row is a notice.</summary>
    public RegistryValueItem? At(int index) =>
        notice is null && index >= 0 && index < items.Count ? items[index] : null;

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item,
                       int col, int line, int width, int start = 0)
    {
        // IListDataSource coordinates are relative to the ListView.
        container.Move(col, line);

        if (notice is not null)
        {
            if (!selected) driver.SetAttribute(noticeIsError ? Theme.Error : Theme.Dimmed);
            Draw.Cell(driver, notice, width);
            return;
        }

        var value = At(item);
        if (value is null)
        {
            Draw.Blank(driver, width);
            return;
        }

        var cols = ValueColumns.For(width);

        if (!selected) driver.SetAttribute(value.IsDefault ? Theme.Dimmed : Theme.Normal);
        Draw.Cell(driver, value.DisplayName, cols.Name);
        Draw.Blank(driver, ValueColumns.Gap);

        if (!selected) driver.SetAttribute(Theme.Dimmed);
        Draw.Cell(driver, value.TypeName, cols.Type);
        Draw.Blank(driver, ValueColumns.Gap);

        if (!selected)
            driver.SetAttribute(value.IsUnset ? Theme.Dimmed : Theme.ForValueKind(value.Kind));
        Draw.Cell(driver, value.DataText, cols.Data);
    }

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }

    /// <summary>
    /// Returns one entry per row reported by <see cref="Count"/>.
    /// </summary>
    public IList ToList() => notice is not null
        ? new List<string> { notice }
        : items.Select(v => v.DisplayName).ToList();
}
