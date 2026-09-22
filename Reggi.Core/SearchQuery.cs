namespace Reggi.Core;

/// <summary>What to look for. At least one of the three scopes must be enabled.</summary>
public sealed record SearchQuery(
    string Text,
    bool MatchKeys = true,
    bool MatchValueNames = true,
    bool MatchData = true,
    bool WholeStringOnly = false)
{
    public bool IsUsable => Text.Length > 0 && (MatchKeys || MatchValueNames || MatchData);

    /// <summary>
    /// True when numeric values should include their hexadecimal representation.
    /// </summary>
    public bool LooksHexadecimal => Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

    /// <summary>Matches using ordinal, case-insensitive comparison.</summary>
    public bool Matches(string candidate) => WholeStringOnly
        ? string.Equals(candidate, Text, StringComparison.OrdinalIgnoreCase)
        : candidate.Contains(Text, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Where a match was found. <see cref="ValueName"/> is null when the key name itself matched.
/// </summary>
public sealed record SearchHit(RegistryPath Path, string? ValueName)
{
    public bool IsKeyMatch => ValueName is null;

    public string Describe() => IsKeyMatch
        ? Path.ToString()
        : $"{Path}  →  {(ValueName!.Length == 0 ? "(Default)" : ValueName)}";
}
