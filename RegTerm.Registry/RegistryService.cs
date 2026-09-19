using Microsoft.Win32;
using RegTerm.Core;

namespace RegTerm.Registry;

public sealed class RegistryService : IRegistryService
{
    private static readonly string[] Hives =
    [
        "HKEY_CLASSES_ROOT",
        "HKEY_CURRENT_USER",
        "HKEY_LOCAL_MACHINE",
        "HKEY_USERS",
        "HKEY_CURRENT_CONFIG"
    ];

    public IReadOnlyList<string> HiveNames => Hives;

    public IReadOnlyList<string> GetSubKeyNames(RegistryPath path)
    {
        try
        {
            using var key = Open(path, writable: false);
            var names = key.GetSubKeyNames();
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }
        catch (Exception ex) when (IsAccessProblem(ex))
        {
            // Protected keys appear empty while browsing.
            return [];
        }
    }

    public bool HasSubKeys(RegistryPath path)
    {
        try
        {
            using var key = Open(path, writable: false);
            return key.SubKeyCount > 0;
        }
        catch (Exception ex) when (IsAccessProblem(ex))
        {
            return false;
        }
    }

    public ValueListing GetValues(RegistryPath path)
    {
        try
        {
            using var key = Open(path, writable: false);
            var names = key.GetValueNames();
            var items = new List<RegistryValueItem>(names.Length + 1);

            if (!names.Any(string.IsNullOrEmpty))
                items.Add(RegistryValueItem.UnsetDefault());

            foreach (var name in names)
            {
                items.Add(new RegistryValueItem
                {
                    Name = name ?? string.Empty,
                    Kind = ToValueType(key.GetValueKind(name)),
                    // Preserve environment-variable references in REG_EXPAND_SZ values.
                    RawValue = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                });
            }

            return ValueListing.Ok(items);
        }
        catch (Exception ex) when (IsAccessProblem(ex))
        {
            return ValueListing.Inaccessible(RegistryErrors.Describe(ex));
        }
    }

    public void CreateSubKey(RegistryPath parent, string name)
    {
        ValidateKeyName(name);

        using var key = Open(parent, writable: true);
        if (key.GetSubKeyNames().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A key named '{name}' already exists here.");

        using var created = key.CreateSubKey(name);
    }

    public void RenameSubKey(RegistryPath key, string newName)
    {
        if (key.IsHiveRoot)
            throw new InvalidOperationException("Hives cannot be renamed.");

        ValidateKeyName(newName);

        if (string.Equals(key.LeafName, newName, StringComparison.Ordinal)) return;

        // Case-only renames collide in the case-insensitive registry.
        if (string.Equals(key.LeafName, newName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Key names cannot differ only by letter case; the registry is case-insensitive.");

        var parent = key.Parent()
            ?? throw new InvalidOperationException("Hives cannot be renamed.");

        if (GetSubKeyNames(parent).Any(n => string.Equals(n, newName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A key named '{newName}' already exists here.");

        using var parentKey = Open(parent, writable: true);
        NativeRegistry.RenameSubKey(parentKey, key.LeafName, newName);
    }

    public void RenameValue(RegistryPath path, string oldName, string newName)
    {
        if (oldName.Length == 0)
            throw new InvalidOperationException("The (Default) value cannot be renamed.");

        ValidateValueName(newName);

        if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;

        // A case-only rename would overwrite the value before deleting it.
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Value names cannot differ only by letter case; the registry is case-insensitive.");

        using var key = Open(path, writable: true);

        if (key.GetValueNames().Any(n => string.Equals(n, newName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A value named '{newName}' already exists here.");

        var kind = key.GetValueKind(oldName);
        var data = key.GetValue(oldName, null, RegistryValueOptions.DoNotExpandEnvironmentNames)
                   ?? throw new InvalidOperationException($"'{oldName}' could not be read.");

        // Write before deleting to avoid data loss on failure.
        key.SetValue(newName, data, kind);
        key.DeleteValue(oldName);
    }

    /// <summary>A backslash separates path components, so a key name cannot contain one.</summary>
    private static void ValidateKeyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a name for the key.");
        if (name.Contains('\\'))
            throw new InvalidOperationException("A key name cannot contain a backslash.");
    }

    /// <summary>
    /// Value names may contain backslashes. Empty names identify the default value.
    /// </summary>
    private static void ValidateValueName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a name for the value.");
    }

    public void DeleteSubKeyTree(RegistryPath parent, string name)
    {
        using var key = Open(parent, writable: true);
        key.DeleteSubKeyTree(name);
    }

    public void SetValue(RegistryPath path, string name, object value, RegistryValueType kind)
    {
        using var key = Open(path, writable: true);
        key.SetValue(name, value, ToNativeKind(kind));
    }

    public void CreateValue(RegistryPath path, string name, object value, RegistryValueType kind)
    {
        using var key = Open(path, writable: true);
        if (key.GetValueNames().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
        {
            var displayName = name.Length == 0 ? RegistryValueItem.DefaultLabel : name;
            throw new InvalidOperationException($"A value named '{displayName}' already exists here.");
        }

        key.SetValue(name, value, ToNativeKind(kind));
    }

    public void DeleteValue(RegistryPath path, string name)
    {
        using var key = Open(path, writable: true);
        key.DeleteValue(name);
    }

    private static RegistryKey Open(RegistryPath path, bool writable)
    {
        var hive = path.Hive switch
        {
            "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKEY_USERS" => RegistryHive.Users,
            "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => throw new InvalidOperationException($"Unknown hive '{path.Hive}'.")
        };

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        if (path.IsHiveRoot)
            return RegistryKey.OpenBaseKey(hive, RegistryView.Default);

        // A parent can enumerate a subkey that cannot be opened.
        return baseKey.OpenSubKey(path.SubPath, writable)
               ?? throw new RegistryKeyUnavailableException(path);
    }

    private static bool IsAccessProblem(Exception ex) =>
        RegistryErrors.IsAccessProblem(ex) || ex is IOException;

    private static RegistryValueType ToValueType(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String => RegistryValueType.String,
        RegistryValueKind.ExpandString => RegistryValueType.ExpandString,
        RegistryValueKind.Binary => RegistryValueType.Binary,
        RegistryValueKind.DWord => RegistryValueType.DWord,
        RegistryValueKind.MultiString => RegistryValueType.MultiString,
        RegistryValueKind.QWord => RegistryValueType.QWord,
        RegistryValueKind.None => RegistryValueType.None,
        _ => RegistryValueType.Unknown
    };

    private static RegistryValueKind ToNativeKind(RegistryValueType kind) => kind switch
    {
        RegistryValueType.String => RegistryValueKind.String,
        RegistryValueType.ExpandString => RegistryValueKind.ExpandString,
        RegistryValueType.Binary => RegistryValueKind.Binary,
        RegistryValueType.DWord => RegistryValueKind.DWord,
        RegistryValueType.MultiString => RegistryValueKind.MultiString,
        RegistryValueType.QWord => RegistryValueKind.QWord,
        RegistryValueType.None => RegistryValueKind.None,
        _ => throw new InvalidOperationException("Unsupported registry data types cannot be written safely.")
    };
}
