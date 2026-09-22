namespace Reggi.Core;

/// <summary>
/// A location in the registry: a hive name plus a backslash-separated subkey path.
/// An empty <see cref="SubPath"/> means the hive root.
/// </summary>
public readonly record struct RegistryPath(string Hive, string SubPath)
{
    public static RegistryPath Root(string hive) => new(hive, string.Empty);

    public bool IsHiveRoot => SubPath.Length == 0;

    public string LeafName => IsHiveRoot ? Hive : SubPath[(SubPath.LastIndexOf('\\') + 1)..];

    public RegistryPath Child(string name) =>
        new(Hive, IsHiveRoot ? name : string.Concat(SubPath, "\\", name));

    public RegistryPath? Parent()
    {
        if (IsHiveRoot) return null;
        var sep = SubPath.LastIndexOf('\\');
        return new RegistryPath(Hive, sep < 0 ? string.Empty : SubPath[..sep]);
    }

    public override string ToString() => IsHiveRoot ? Hive : string.Concat(Hive, "\\", SubPath);
}
