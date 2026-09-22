using System.Security;

namespace Reggi.Core;

/// <summary>
/// Formats registry errors for display.
/// </summary>
public static class RegistryErrors
{
    public static bool IsAccessProblem(Exception ex) =>
        ex is UnauthorizedAccessException or SecurityException or RegistryKeyUnavailableException;

    public static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException or SecurityException =>
            "Access denied. Run in an elevated terminal to work with this key.",
        RegistryKeyUnavailableException unavailable => unavailable.Message,
        IOException io => io.Message,
        _ => ex.Message
    };
}
