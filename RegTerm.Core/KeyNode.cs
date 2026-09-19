namespace RegTerm.Core;

/// <summary>One visible row of the key tree.</summary>
public sealed class KeyNode
{
    public required RegistryPath Path { get; init; }
    public required string Name { get; init; }
    public required int Depth { get; init; }

    public bool Expanded { get; set; }
    public bool HasChildren { get; set; }

    public bool IsHive => Depth == 0;
}
