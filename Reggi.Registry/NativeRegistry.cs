using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Reggi.Registry;

/// <summary>
/// Native Windows Registry operations not exposed by <see cref="RegistryKey"/>.
/// </summary>
internal static class NativeRegistry
{
    private const int _errorSuccess = 0;
    private const int _errorFileNotFound = 2;
    private const int _errorAccessDenied = 5;
    private const int _errorInvalidParameter = 87;
    private const int _errorCallNotImplemented = 120;
    private const int _errorAlreadyExists = 183;

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

        if (status == _errorSuccess) return;
        throw new InvalidOperationException(Describe(status, subKeyName, newName));
    }

    private static string Describe(int status, string subKeyName, string newName) => status switch
    {
        _errorAccessDenied => "Access denied. Run in an elevated terminal to rename this key.",
        _errorAlreadyExists => $"A key named '{newName}' already exists here.",
        _errorFileNotFound => $"'{subKeyName}' no longer exists.",
        _errorInvalidParameter => $"'{newName}' is not a valid key name.",
        _errorCallNotImplemented => "This version of Windows does not support renaming keys.",
        _ => new Win32Exception(status).Message
    };
}
