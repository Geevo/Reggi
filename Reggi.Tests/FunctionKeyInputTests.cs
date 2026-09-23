using Reggi.UI;
using Terminal.Gui;
using Xunit;

namespace Reggi.Tests;

public sealed class FunctionKeyInputTests
{
    [Fact]
    public void EncodedFunctionKeysMapToF1ThroughF12()
    {
        for (uint offset = 0; offset < 12; offset++)
        {
            var input = new KeyEvent { Key = (Key)(0xFFBE + offset) };

            Assert.False(FunctionKeyInput.Normalize(input));
            Assert.Equal((Key)((uint)Key.F1 + offset), input.Key);
        }
    }

    [Fact]
    public void OtherKeysPassThrough()
    {
        foreach (var key in new[] { (Key)0xFFBD, (Key)0xFFCA, Key.F1, Key.A })
        {
            var input = new KeyEvent { Key = key };

            Assert.False(FunctionKeyInput.Normalize(input));
            Assert.Equal(key, input.Key);
        }
    }

    [Fact]
    public void ModifierBitsArePreserved()
    {
        var input = new KeyEvent { Key = (Key)0xFFC0 | Key.ShiftMask };

        Assert.False(FunctionKeyInput.Normalize(input));
        Assert.Equal(Key.F3 | Key.ShiftMask, input.Key);
    }
}
