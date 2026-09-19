namespace RegTerm.Core;

/// <summary>A single value under a registry key, as read from disk.</summary>
public sealed class RegistryValueItem
{
    public required string Name { get; init; }
    public required RegistryValueType Kind { get; init; }
    public object? RawValue { get; init; }

    /// <summary>True for the placeholder shown when a key has no (Default) value written.</summary>
    public bool IsUnset { get; init; }

    /// <summary>Display label for the unnamed default value.</summary>
    public const string DefaultLabel = "(Default)";

    public bool IsDefault => Name.Length == 0;

    /// <summary>
    /// Display name, with a literal "(Default)" quoted to distinguish it from the unnamed value.
    /// </summary>
    public string DisplayName =>
        IsDefault ? DefaultLabel
        : Name == DefaultLabel ? $"\"{DefaultLabel}\""
        : Name;

    public string TypeName => RegistryValueCodec.TypeName(Kind);

    public string DataText => IsUnset
        ? "(value not set)"
        : RegistryValueCodec.Format(Kind, RawValue);

    public static RegistryValueItem UnsetDefault() => new()
    {
        Name = string.Empty,
        Kind = RegistryValueType.String,
        RawValue = null,
        IsUnset = true
    };
}
