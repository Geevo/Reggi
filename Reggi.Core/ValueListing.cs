namespace Reggi.Core;

/// <summary>
/// Values read from a key, or an access error.
/// </summary>
public sealed class ValueListing
{
    public required IReadOnlyList<RegistryValueItem> Values { get; init; }
    public string? AccessError { get; init; }

    public bool Denied => AccessError is not null;

    public static ValueListing Ok(IReadOnlyList<RegistryValueItem> values) =>
        new() { Values = values };

    public static ValueListing Inaccessible(string message) =>
        new() { Values = [], AccessError = message };
}
