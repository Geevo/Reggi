using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RegTerm.Registry;

/// <summary>
/// Native Windows Registry operations not exposed by <see cref="RegistryKey"/>.
/// </summary>
internal static class NativeRegistry
{
    private const int ErrorSuccess = 0;
    private const int ErrorFileNotFound = 2;
    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidParameter = 87;
    private const int ErrorCallNotImplemented = 120;
    private const int ErrorAlreadyExists = 183;

    /// <summary>
    /// LSTATUS RegRenameKey(HKEY hKey, LPCWSTR lpSubKeyName, LPCWSTR lpNewKeyName).
    /// </summary>
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegRenameKey(IntPtr hKey, string lpSubKeyName, string lpNewKeyName);

    /// <summary>
    /// Renames <paramref name="subKeyName"/> beneath <paramref name="parent"/>.
    /// </summary>
    public static void RenameSubKey(RegistryKey parent, string subKeyName, string newName)
    {
        var status = RegRenameKey(parent.Handle.DangerousGetHandle(), subKeyName, newName);

        // The RegistryKey must outlive the raw handle handed to the API.
        GC.KeepAlive(parent);

        if (status == ErrorSuccess) return;
        throw new InvalidOperationException(Describe(status, subKeyName, newName));
    }

    private static string Describe(int status, string subKeyName, string newName) => status switch
    {
        ErrorAccessDenied => "Access denied. Run in an elevated terminal to rename this key.",
        ErrorAlreadyExists => $"A key named '{newName}' already exists here.",
        ErrorFileNotFound => $"'{subKeyName}' no longer exists.",
        ErrorInvalidParameter => $"'{newName}' is not a valid key name.",
        ErrorCallNotImplemented => "This version of Windows does not support renaming keys.",
        _ => new Win32Exception(status).Message
    };
}
