namespace Reggi.Core;

/// <summary>
/// Registry operations used by the application.
/// </summary>
public interface IRegistryService
{
    IReadOnlyList<string> HiveNames { get; }

    /// <summary>Subkey names in ordinal order. Empty if the key cannot be read.</summary>
    IReadOnlyList<string> GetSubKeyNames(RegistryPath path);

    /// <summary>Checks for subkeys without returning their names.</summary>
    bool HasSubKeys(RegistryPath path);

    ValueListing GetValues(RegistryPath path);

    void CreateSubKey(RegistryPath parent, string name);

    /// <summary>Renames a key in place, keeping its subkeys, values and permissions.</summary>
    void RenameSubKey(RegistryPath key, string newName);

    /// <summary>Renames a value by rewriting it under the new name, then removing the old.</summary>
    void RenameValue(RegistryPath path, string oldName, string newName);
    void DeleteSubKeyTree(RegistryPath parent, string name);
    void CreateValue(RegistryPath path, string name, object value, RegistryValueType kind);
    void SetValue(RegistryPath path, string name, object value, RegistryValueType kind);
    void DeleteValue(RegistryPath path, string name);
}
