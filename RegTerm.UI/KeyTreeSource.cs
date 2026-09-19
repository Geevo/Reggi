using System.Collections;
using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

/// <summary>Renders <see cref="KeyTree"/> rows with indentation, chevrons and per-row colour.</summary>
public sealed class KeyTreeSource(KeyTree tree) : IListDataSource
{
    private const int IndentPerLevel = 2;

    public int Count => tree.Count;

    public int Length => tree.Nodes.Count == 0
        ? 0
        : tree.Nodes.Max(n => n.Depth * IndentPerLevel + 2 + n.Name.Length);

    public void Render(ListView container, ConsoleDriver driver, bool selected, int item,
                       int col, int line, int width, int start = 0)
    {
        // IListDataSource coordinates are relative to the ListView.
        container.Move(col, line);

        var node = tree[item];
        if (node is null)
        {
            Draw.Blank(driver, width);
            return;
        }

        var remaining = width;

        var indent = Math.Min(node.Depth * IndentPerLevel, remaining);
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
    public IList ToList() => tree.Nodes.Select(n => n.Name).ToList();
}
