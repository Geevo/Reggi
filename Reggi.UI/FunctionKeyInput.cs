using Terminal.Gui;

namespace Reggi.UI;

/// <summary>Normalizes function keys reported as Unicode characters by some console paths.</summary>
internal static class FunctionKeyInput
{
    private const uint FirstEncodedKey = 0xFFBE;
    private const uint LastEncodedKey = 0xFFC9;
    private const Key ModifierMask = Key.CtrlMask | Key.ShiftMask | Key.AltMask;

    /// <summary>Runs before Terminal.Gui sends a key to the focused view.</summary>
    public static bool Normalize(KeyEvent keyEvent)
    {
        var encodedKey = (uint)(keyEvent.Key & ~ModifierMask);
        if (encodedKey >= FirstEncodedKey && encodedKey <= LastEncodedKey)
        {
            var modifiers = keyEvent.Key & ModifierMask;
            keyEvent.Key = (Key)((uint)Key.F1 + encodedKey - FirstEncodedKey) | modifiers;
        }

        return false; // Let Terminal.Gui dispatch the normalized key.
    }
}
