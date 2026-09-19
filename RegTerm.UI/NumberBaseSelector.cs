using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

/// <summary>
/// Hexadecimal/decimal selector for DWORD and QWORD fields.
/// </summary>
internal sealed class NumberBaseSelector
{
    private readonly RadioGroup radio;

    public NumberBaseSelector(int x, int y, NumberBase initial = NumberBase.Hexadecimal)
    {
        radio = new RadioGroup([(NStack.ustring)"Hexadecimal", (NStack.ustring)"Decimal"])
        {
            X = x,
            Y = y,
            Width = 18,
            Height = 2,
            SelectedItem = initial == NumberBase.Decimal ? 1 : 0
        };
    }

    public View View => radio;

    public NumberBase Current => radio.SelectedItem == 1 ? NumberBase.Decimal : NumberBase.Hexadecimal;

    public bool Enabled
    {
        get => radio.Enabled;
        set => radio.Enabled = value;
    }

    /// <summary>
    /// Re-renders <paramref name="field"/> when the selected base changes.
    /// </summary>
    public void Rewrites(TextField field, Func<RegistryValueType> currentKind)
    {
        var previous = Current;

        radio.SelectedItemChanged += _ =>
        {
            var next = Current;
            if (next == previous) return;

            var kind = currentKind();
            var text = field.Text.ToString() ?? string.Empty;

            if (text.Trim().Length > 0 && RegistryValueCodec.IsNumeric(kind))
            {
                try
                {
                    var parsed = RegistryValueCodec.Parse(kind, text, previous);
                    field.Text = RegistryValueCodec.ToEditable(kind, parsed, next);
                }
                catch (FormatException)
                {
                    // Leave incomplete input unchanged.
                }
            }

            previous = next;
        };
    }
}
