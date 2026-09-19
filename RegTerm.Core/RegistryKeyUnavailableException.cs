namespace RegTerm.Core;

/// <summary>
/// A key was enumerated but could not be opened.
/// </summary>
public sealed class RegistryKeyUnavailableException(RegistryPath path)
    : Exception($"'{path}' could not be opened. It may have been removed, or access to it is restricted.")
{
    public RegistryPath Path { get; } = path;
}
