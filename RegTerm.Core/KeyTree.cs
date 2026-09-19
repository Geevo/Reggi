namespace RegTerm.Core;

/// <summary>
/// Flattened key tree with expand and collapse state.
/// </summary>
public sealed class KeyTree(IRegistryService registry)
{
    private readonly List<KeyNode> nodes = [];

    public IReadOnlyList<KeyNode> Nodes => nodes;
    public int Count => nodes.Count;

    public KeyNode? this[int index] =>
        index >= 0 && index < nodes.Count ? nodes[index] : null;

    public void LoadHives()
    {
        nodes.Clear();
        foreach (var hive in registry.HiveNames)
        {
            var path = RegistryPath.Root(hive);
            nodes.Add(new KeyNode
            {
                Path = path,
                Name = hive,
                Depth = 0,
                HasChildren = registry.HasSubKeys(path)
            });
        }
    }

    public bool Expand(int index)
    {
        var node = this[index];
        if (node is null || !node.HasChildren || node.Expanded) return false;

        nodes.InsertRange(index + 1, ChildrenOf(node));
        node.Expanded = true;
        return true;
    }

    public bool Collapse(int index)
    {
        var node = this[index];
        if (node is null || !node.Expanded) return false;

        var count = DescendantCount(index);
        if (count > 0) nodes.RemoveRange(index + 1, count);
        node.Expanded = false;
        return true;
    }

    public bool Toggle(int index)
    {
        var node = this[index];
        if (node is null || !node.HasChildren) return false;
        return node.Expanded ? Collapse(index) : Expand(index);
    }

    /// <summary>Re-reads a node's children from the registry, keeping it expanded.</summary>
    public void Reload(int index)
    {
        var node = this[index];
        if (node is null) return;

        var wasExpanded = node.Expanded;
        Collapse(index);
        node.HasChildren = registry.HasSubKeys(node.Path);
        if (wasExpanded || node.HasChildren) Expand(index);
    }

    public int IndexOf(RegistryPath path)
    {
        for (var i = 0; i < nodes.Count; i++)
            if (nodes[i].Path == path) return i;
        return -1;
    }

    /// <summary>
    /// Expands whatever is needed to make <paramref name="path"/> a visible row and returns
    /// its index, or the deepest row that could be reached if part of the path is gone.
    /// Returns -1 only if the hive itself is missing.
    /// </summary>
    public int Reveal(RegistryPath path)
    {
        var chain = new List<RegistryPath>();
        RegistryPath? current = path;
        while (current is not null)
        {
            chain.Add(current.Value);
            current = current.Value.Parent();
        }
        chain.Reverse();

        var index = IndexOf(chain[0]);
        if (index < 0) return -1;

        for (var i = 1; i < chain.Count; i++)
        {
            if (!nodes[index].Expanded) Expand(index);

            var next = IndexOf(chain[i]);
            if (next < 0) return index;     // key vanished since the search saw it
            index = next;
        }
        return index;
    }

    public int? ParentIndex(int index)
    {
        var node = this[index];
        if (node is null || node.Depth == 0) return null;

        for (var i = index - 1; i >= 0; i--)
            if (nodes[i].Depth == node.Depth - 1) return i;
        return null;
    }

    /// <summary>Index of the first child row, or -1 if the node has none expanded.</summary>
    public int FirstChildIndex(int index)
    {
        var node = this[index];
        if (node is null || !node.Expanded) return -1;
        var child = index + 1;
        return child < nodes.Count && nodes[child].Depth == node.Depth + 1 ? child : -1;
    }

    /// <summary>Wrapping type-ahead search over visible rows, starting after <paramref name="startAfter"/>.</summary>
    public int FindByPrefix(string prefix, int startAfter)
    {
        if (prefix.Length == 0 || nodes.Count == 0) return -1;

        for (var offset = 1; offset <= nodes.Count; offset++)
        {
            var i = ((startAfter + offset) % nodes.Count + nodes.Count) % nodes.Count;
            if (nodes[i].Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private List<KeyNode> ChildrenOf(KeyNode parent)
    {
        var names = registry.GetSubKeyNames(parent.Path);
        var children = new List<KeyNode>(names.Count);
        foreach (var name in names)
        {
            var path = parent.Path.Child(name);
            children.Add(new KeyNode
            {
                Path = path,
                Name = name,
                Depth = parent.Depth + 1,
                HasChildren = registry.HasSubKeys(path)
            });
        }
        return children;
    }

    private int DescendantCount(int index)
    {
        var depth = nodes[index].Depth;
        var count = 0;
        for (var i = index + 1; i < nodes.Count && nodes[i].Depth > depth; i++) count++;
        return count;
    }
}
