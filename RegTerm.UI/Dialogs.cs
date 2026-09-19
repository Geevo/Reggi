using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

/// <summary>
/// Modal input and confirmation dialogs.
/// </summary>
public static class Dialogs
{
    public static void Error(string title, string message) =>
        MessageBox.ErrorQuery(Math.Min(72, message.Length + 12), 8, title, message, "OK");

    public static bool Confirm(string title, string message, string acceptLabel) =>
        MessageBox.Query(Math.Min(72, message.Length + 12), 8, title, message, acceptLabel, "Cancel") == 0;

    /// <summary>Edits an existing value. Returns the parsed replacement, or null if cancelled.</summary>
    public static object? EditValue(RegistryValueItem value)
    {
        var numeric = RegistryValueCodec.IsNumeric(value.Kind);

        var startingBase = numeric ? NumberBase.Hexadecimal : NumberBase.Decimal;

        var field = new TextField(RegistryValueCodec.ToEditable(value.Kind, value.RawValue, startingBase))
        {
            X = 1,
            Y = 6,
            Width = numeric ? Dim.Sized(46) : Dim.Fill(2)
        };

        NumberBaseSelector? numberBase = null;
        if (numeric)
        {
            numberBase = new NumberBaseSelector(50, 6, startingBase);
            numberBase.Rewrites(field, () => value.Kind);
        }

        object? result = null;
        var save = new Button("Save", is_default: true);
        var cancel = new Button("Cancel");

        save.Clicked += () =>
        {
            var chosen = numberBase?.Current ?? NumberBase.Decimal;
            if (!TryParse(value.Kind, field.Text.ToString() ?? string.Empty, chosen, out var parsed)) return;
            result = parsed;
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        var dialog = Build($"Edit  {value.DisplayName}", 74, 15, save, cancel);
        dialog.Add(
            Caption(1, 1, "Name"),
            Value(9, 1, value.DisplayName),
            Caption(1, 2, "Type"),
            Value(9, 2, value.TypeName),
            Caption(1, 4, "Data"),
            field,
            Hint(1, 9, HintFor(value.Kind)));

        if (numberBase is not null)
        {
            dialog.Add(Caption(50, 4, "Base"), numberBase.View);
        }

        field.SetFocus();
        Application.Run(dialog);
        return result;
    }

    /// <summary>
    /// Prompts for a new name for a key or value. Returns null if cancelled or unchanged.
    /// </summary>
    public static string? Rename(string what, string currentName, RegistryPath location)
    {
        var field = new TextField(currentName) { X = 1, Y = 5, Width = Dim.Fill(2) };

        string? result = null;
        var rename = new Button("Rename", is_default: true);
        var cancel = new Button("Cancel");

        rename.Clicked += () =>
        {
            var name = (field.Text.ToString() ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                Error("Name required", $"Enter a new name for the {what}.");
                return;
            }
            result = name;
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        var dialog = Build($"Rename {what}", 66, 13, rename, cancel);
        dialog.Add(
            Caption(1, 1, "In"),
            Value(1, 2, location.ToString()),
            Caption(1, 4, $"New {what} name"),
            field,
            Hint(1, 7, what == "key"
                ? "No backslashes; names cannot differ only by letter case."
                : "Names cannot differ only by letter case."));

        field.SetFocus();
        Application.Run(dialog);

        return string.Equals(result, currentName, StringComparison.Ordinal) ? null : result;
    }

    /// <summary>Prompts for a new subkey name. Returns null if cancelled.</summary>
    public static string? NewKeyName(RegistryPath parent)
    {
        var field = new TextField(string.Empty) { X = 1, Y = 5, Width = Dim.Fill(2) };

        string? result = null;
        var create = new Button("Create", is_default: true);
        var cancel = new Button("Cancel");

        create.Clicked += () =>
        {
            var name = (field.Text.ToString() ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                Error("Name required", "Enter a name for the new key.");
                return;
            }
            if (name.Contains('\\'))
            {
                Error("Invalid name", "A key name cannot contain a backslash.");
                return;
            }
            result = name;
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        var dialog = Build("New Key", 64, 12, create, cancel);
        dialog.Add(
            Caption(1, 1, "Create under"),
            Value(1, 2, parent.ToString()),
            Caption(1, 4, "Name"),
            field);

        field.SetFocus();
        Application.Run(dialog);
        return result;
    }

    /// <summary>Collects a new value. Returns null if cancelled.</summary>
    public static NewValueRequest? NewValue(RegistryPath parent)
    {
        var kinds = RegistryValueCodec.CreatableKinds;

        var nameField = new TextField(string.Empty) { X = 1, Y = 4, Width = 28 };
        var kindGroup = new RadioGroup(kinds.Select(k => (NStack.ustring)RegistryValueCodec.TypeName(k)).ToArray())
        {
            X = 1,
            Y = 7,
            Width = 28,
            Height = kinds.Length,
            // Prevent underscores in registry type names from becoming hotkeys.
            HotKeySpecifier = new System.Rune(0x0001)
        };
        var dataField = new TextField(string.Empty) { X = 32, Y = 4, Width = Dim.Fill(2) };
        var hint = Hint(32, 6, HintFor(kinds[0]));

        RegistryValueType SelectedKind() => kinds[Math.Clamp(kindGroup.SelectedItem, 0, kinds.Length - 1)];

        var numberBase = new NumberBaseSelector(32, 9);
        numberBase.Rewrites(dataField, SelectedKind);
        var baseCaption = Caption(32, 8, "Base");

        // Keep the layout stable when switching value types.
        void SyncBaseAvailability() => numberBase.Enabled = RegistryValueCodec.IsNumeric(SelectedKind());
        SyncBaseAvailability();

        kindGroup.SelectedItemChanged += args =>
        {
            var index = Math.Clamp(args.SelectedItem, 0, kinds.Length - 1);
            hint.Text = HintFor(kinds[index]);
            SyncBaseAvailability();
        };

        NewValueRequest? result = null;
        var create = new Button("Create", is_default: true);
        var cancel = new Button("Cancel");

        create.Clicked += () =>
        {
            var kind = SelectedKind();
            var name = (nameField.Text.ToString() ?? string.Empty).Trim();

            if (!TryParse(kind, dataField.Text.ToString() ?? string.Empty, numberBase.Current, out var parsed))
                return;

            result = new NewValueRequest(name, kind, parsed!);
            Application.RequestStop();
        };
        cancel.Clicked += () => Application.RequestStop();

        var dialog = Build("New Value", 78, 20, create, cancel);
        dialog.Add(
            Caption(1, 1, "Create under"),
            Value(1, 2, parent.ToString()),
            Caption(1, 3, "Name"),
            nameField,
            Caption(1, 6, "Type"),
            kindGroup,
            Caption(32, 3, "Data"),
            dataField,
            hint,
            baseCaption,
            numberBase.View,
            Hint(1, 5, "leave blank for (Default)"));

        nameField.SetFocus();
        Application.Run(dialog);
        return result;
    }

    public static void Help()
    {
        var close = new Button("Close", is_default: true);
        close.Clicked += () => Application.RequestStop();

        var dialog = Build("Keyboard Shortcuts", 66, 23, close);

        var rows = new (string Keys, string Action)[]
        {
            ("Tab", "Switch between the Keys and Values panes"),
            ("Up / Down", "Move through the current pane"),
            ("Enter", "Keys: expand or step in.  Values: edit"),
            ("Right", "Expand key, or step into its first child"),
            ("Left", "Collapse key, or jump to its parent"),
            ("Space", "Toggle expand and collapse"),
            ("Backspace", "Go up one key"),
            ("A-Z", "Type-ahead jump in the Keys pane"),
            ("Ctrl+F", "Find keys, value names or data"),
            ("F3", "Find the next match"),
            ("F2", "Rename the selected key or value"),
            ("F5", "Reload the current key"),
            ("F6", "New value"),
            ("F7", "New key"),
            ("Del  (or F8)", "Delete the selected key or value"),
            ("Ctrl+Q", "Quit")
        };

        for (var i = 0; i < rows.Length; i++)
        {
            dialog.Add(Caption(2, i + 1, rows[i].Keys));
            dialog.Add(Value(17, i + 1, rows[i].Action));
        }

        Application.Run(dialog);
    }

    private static bool TryParse(RegistryValueType kind, string text, NumberBase numberBase, out object? parsed)
    {
        try
        {
            parsed = RegistryValueCodec.Parse(kind, text, numberBase);
            return true;
        }
        catch (FormatException ex)
        {
            Error("Invalid data", ex.Message);
            parsed = null;
            return false;
        }
    }

    private static string HintFor(RegistryValueType kind) => kind switch
    {
        RegistryValueType.DWord or RegistryValueType.QWord => "a 0x prefix always means hex",
        RegistryValueType.Binary => "hex bytes, e.g. 4A 00 FF",
        RegistryValueType.MultiString => @"separate with ;  (escape as \; or \\)",
        RegistryValueType.ExpandString => "%VARIABLES% expand on read",
        _ => " "
    };

    internal static Dialog Build(string title, int width, int height, params Button[] buttons) =>
        new(title, width, height, buttons) { ColorScheme = Theme.Dialog };

    internal static Label Caption(int x, int y, string text) =>
        new(x, y, text) { ColorScheme = LabelScheme(Theme.Dimmed) };

    internal static Label Value(int x, int y, string text) =>
        new(x, y, text) { ColorScheme = LabelScheme(Theme.Heading) };

    internal static Label Hint(int x, int y, string text) =>
        new(x, y, text) { ColorScheme = LabelScheme(Theme.Dimmed), Width = Dim.Fill(2) };

    internal static ColorScheme LabelScheme(Terminal.Gui.Attribute attribute) => new()
    {
        Normal = attribute,
        Focus = attribute,
        HotNormal = attribute,
        HotFocus = attribute,
        Disabled = Theme.Dimmed
    };
}

/// <summary>A validated new-value request, ready to hand to the registry.</summary>
public sealed record NewValueRequest(string Name, RegistryValueType Kind, object Value);
