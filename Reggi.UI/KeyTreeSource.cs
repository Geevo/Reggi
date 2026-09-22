using System.Collections;
using Reggi.Core;
using Terminal.Gui;

namespace Reggi.UI;

/// <summary>Renders <see cref="KeyTree"/> rows with indentation, chevrons and per-row colour.</summary>
public sealed class KeyTreeSource(KeyTree tree) : IListDataSource
{
    private const int _indentPerLevel = 2;
    private readonly KeyTree _tree = tree;

    public int Count => _tree.Count;

    public int Length => _tree.Nodes.Count == 0
        ? 0
        : _tree.Nodes.Max(n => n.Depth * _indentPerLevel + 2 + n.Name.Length);

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item,
                       int col, int line, int width, int start = 0)
    {
        // IListDataSource coordinates are relative to the ListView.
        container.Move(col, line);

        var node = _tree[item];
        if (node is null)
        {
            Draw.Blank(driver, width);
            return;
        }

        var remaining = width;

        var indent = Math.Min(node.Depth * _indentPerLevel, remaining);
        Draw.Blank(driver, indent);
        remaining -= indent;

        if (remaining <= 0) return;

        if (!selected && node.HasChildren) driver.SetAttribute(Theme.Highlight);
        remaining -= Draw.Clipped(driver, node.HasChildren ? (node.Expanded ? "▾ " : "▸ ") : "  ", remaining);

        if (!selected) driver.SetAttribute(node.IsHive ? Theme.Heading : Theme.Normal);
        Draw.Cell(driver, node.Name, remaining);
    }

    public bool IsMarked(int item) => false;
    public void SetMark(int item, bool value) { }
    public IList ToList() => _tree.Nodes.Select(n => n.Name).ToList();
}
