using System.Collections;
using Reggi.Core;
using Terminal.Gui;

namespace Reggi.UI;

/// <summary>
/// Renders registry values and empty or inaccessible notices.
/// </summary>
public sealed class ValueListSource : IListDataSource
{
    private IReadOnlyList<RegistryValueItem> _items = [];
    private string? _notice;
    private bool _noticeIsError;

    public bool ShowingNotice => _notice is not null;
    public int Count => _notice is not null ? 1 : _items.Count;
    public int Length => 120;

    public void SetValues(IReadOnlyList<RegistryValueItem> values)
    {
        _items = values;
        _notice = null;
        _noticeIsError = false;
    }

    public void SetNotice(string text, bool isError)
    {
        _items = [];
        _notice = text;
        _noticeIsError = isError;
    }

    /// <summary>The value at a row, or null if this row is a notice.</summary>
    public RegistryValueItem? At(int index) =>
        _notice is null && index >= 0 && index < _items.Count ? _items[index] : null;

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item,
                       int col, int line, int width, int start = 0)
    {
        // IListDataSource coordinates are relative to the ListView.
        container.Move(col, line);

        if (_notice is not null)
        {
            if (!selected) driver.SetAttribute(_noticeIsError ? Theme.Error : Theme.Dimmed);
            Draw.Cell(driver, _notice, width);
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
    public IList ToList() => _notice is not null
        ? new List<string> { _notice }
        : _items.Select(v => v.DisplayName).ToList();
}
