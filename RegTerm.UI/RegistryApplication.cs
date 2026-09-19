using RegTerm.Core;
using Terminal.Gui;

namespace RegTerm.UI;

/// <summary>
/// Terminal UI controller.
/// </summary>
public sealed class RegistryApplication(IRegistryService registry)
{
    private const int TypeAheadTimeoutMs = 800;

    private readonly KeyTree tree = new(registry);
    private readonly ValueListSource valueSource = new();

    private KeyTreeSource treeSource = null!;
    private PathBarView pathBar = null!;
    private FrameView keysFrame = null!;
    private FrameView valuesFrame = null!;
    private ListView keysList = null!;
    private ListView valuesList = null!;

    private RegistryPath current;
    private SearchQuery? lastQuery;
    private RegistrySearch? activeSearch;
    private string typeAheadBuffer = string.Empty;
    private DateTime typeAheadLast = DateTime.MinValue;

    /// <summary>Set while rebuilding a list, so selection events do not re-enter.</summary>
    private bool suppressSelectionEvents;

    public void Run()
    {
        Application.Init();
        try
        {
            Theme.Apply();
            BuildLayout();

            tree.LoadHives();
            treeSource = new KeyTreeSource(tree);
            SyncTree(0);

            keysList.SetFocus();
            Application.Run();
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private void BuildLayout()
    {
        var top = Application.Top;
        top.ColorScheme = Theme.Pane;

        pathBar = new PathBarView { X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.PathBar };

        keysFrame = new FrameView("Keys")
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(38),
            Height = Dim.Fill(1),
            ColorScheme = Theme.Frame
        };

        valuesFrame = new FrameView("Values")
        {
            X = Pos.Right(keysFrame),
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ColorScheme = Theme.Frame
        };

        keysList = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            ColorScheme = Theme.Pane
        };
        keysFrame.Add(keysList);

        var header = new ValueHeaderView { X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.Pane };
        valuesList = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowsMarking = false,
            ColorScheme = Theme.Pane
        };
        valuesFrame.Add(header, valuesList);

        ScrollBars.AttachVertical(keysList);
        ScrollBars.AttachVertical(valuesList);

        WireKeysPane();
        WireValuesPane();
        WireGlobalShortcuts(top);

        top.Add(pathBar, keysFrame, valuesFrame, BuildStatusBar());
    }

    private StatusBar BuildStatusBar() => new(
    [
        new StatusItem(Key.F1, "~F1~ Help", Dialogs.Help),
        new StatusItem(Key.CtrlMask | Key.F, "~^F~ Find", StartSearch),
        new StatusItem(Key.F3, "~F3~ Next", FindNextMatch),
        new StatusItem(Key.F2, "~F2~ Rename", RenameSelected),
        new StatusItem(Key.F5, "~F5~ Reload", ReloadCurrent),
        new StatusItem(Key.F6, "~F6~ New Value", CreateValue),
        new StatusItem(Key.F7, "~F7~ New Key", CreateKey),
        new StatusItem(Key.DeleteChar, "~Del~ Delete", DeleteSelected),
        new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop())
    ])
    { ColorScheme = Theme.Status };

    private void WireKeysPane()
    {
        keysList.SelectedItemChanged += _ =>
        {
            if (suppressSelectionEvents) return;
            UpdateCurrentFromTree();
        };

        keysList.OpenSelectedItem += _ => StepInto();
        keysList.Enter += _ => RefreshFrameTitles();

        keysList.KeyPress += e =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.CursorRight:
                    StepInto();
                    break;
                case Key.Enter:
                    StepInto();
                    break;
                case Key.CursorLeft:
                    StepOut();
                    break;
                case Key.Space:
                    ToggleSelected();
                    break;
                case Key.Tab:
                    valuesList.SetFocus();
                    break;
                case Key.CursorUp:
                case Key.CursorDown:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Home:
                case Key.End:
                    return;
                default:
                    if (!HandleTypeAhead(e.KeyEvent.Key)) return;
                    break;
            }
            e.Handled = true;
        };

        keysList.MouseClick += _ => keysList.SetFocus();
    }

    private void WireValuesPane()
    {
        valuesList.OpenSelectedItem += _ => EditSelectedValue();
        valuesList.Enter += _ => RefreshFrameTitles();

        valuesList.KeyPress += e =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.Tab:
                    keysList.SetFocus();
                    break;
                case Key.Backspace:
                    keysList.SetFocus();
                    StepOut();
                    break;
                case Key.Enter:
                case Key.CursorRight:
                    EditSelectedValue();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        };

        valuesList.MouseClick += _ => valuesList.SetFocus();
    }

    private void WireGlobalShortcuts(Toplevel top)
    {
        top.KeyPress += e =>
        {
            var key = e.KeyEvent.Key;

            if (key == Key.F1) Dialogs.Help();
            else if (IsCtrl(key, 'f')) StartSearch();
            else if (key == Key.F3) FindNextMatch();
            else if (key == Key.F2) RenameSelected();
            else if (key == Key.F5) ReloadCurrent();
            else if (key == Key.F6) CreateValue();
            else if (key == Key.F7) CreateKey();
            // F8 supports terminals that do not report Delete reliably.
            else if (key == Key.DeleteChar || key == Key.Delete || key == Key.F8) DeleteSelected();
            else if (key == Key.Backspace) StepOut();
            else if (IsCtrl(key, 'q')) Application.RequestStop();
            else return;

            e.Handled = true;
        };
    }

    /// <summary>
    /// Matches Ctrl+letter across terminal driver key encodings.
    /// </summary>
    private static bool IsCtrl(Key key, char letter)
    {
        if ((key & Key.CtrlMask) == 0) return false;

        var code = (int)(key & ~(Key.CtrlMask | Key.ShiftMask | Key.AltMask));
        var upper = char.ToUpperInvariant(letter);

        return code == upper || code == char.ToLowerInvariant(letter) || code == upper - 'A' + 1;
    }

    private void StepInto()
    {
        var index = keysList.SelectedItem;
        var node = tree[index];
        if (node is null) return;

        if (!node.HasChildren) return;

        if (!node.Expanded)
        {
            tree.Expand(index);
            SyncTree(index);
            return;
        }

        var child = tree.FirstChildIndex(index);
        if (child >= 0) SyncTree(child);
    }

    private void StepOut()
    {
        var index = keysList.SelectedItem;
        var node = tree[index];
        if (node is null) return;

        if (node.Expanded)
        {
            tree.Collapse(index);
            SyncTree(index);
            return;
        }

        var parent = tree.ParentIndex(index);
        if (parent.HasValue) SyncTree(parent.Value);
    }

    private void ToggleSelected()
    {
        var index = keysList.SelectedItem;
        if (tree.Toggle(index)) SyncTree(index);
    }

    private void ReloadCurrent()
    {
        var index = keysList.SelectedItem;
        tree.Reload(index);
        SyncTree(index);
    }

    /// <summary>Rebinds the tree list after a structural change and restores the selection.</summary>
    private void SyncTree(int selectIndex)
    {
        var target = tree.Count == 0 ? 0 : Math.Clamp(selectIndex, 0, tree.Count - 1);

        suppressSelectionEvents = true;
        keysList.Source = treeSource;           // resets selection and scroll offset
        if (tree.Count > 0) keysList.SelectedItem = target;
        suppressSelectionEvents = false;

        ScrollTreeIntoView(target);
        UpdateCurrentFromTree();
    }

    private void ScrollTreeIntoView(int index)
    {
        var height = keysList.Bounds.Height;
        if (height <= 0 || tree.Count == 0) return;

        var half = Math.Max(0, height / 2 - 1);
        var topItem = Math.Clamp(index - half, 0, Math.Max(0, tree.Count - height));
        keysList.TopItem = topItem;
        keysList.SetNeedsDisplay();
    }

    private void UpdateCurrentFromTree()
    {
        var node = tree[keysList.SelectedItem];
        if (node is null) return;

        current = node.Path;
        pathBar.Path = current;
        LoadValues(0);
    }

    private void LoadValues(int? preferredSelection)
    {
        var desired = preferredSelection ?? valuesList.SelectedItem;
        var listing = registry.GetValues(current);

        if (listing.Denied)
            valueSource.SetNotice(listing.AccessError!, isError: true);
        else if (listing.Values.Count == 0)
            valueSource.SetNotice("(this key has no values)", isError: false);
        else
            valueSource.SetValues(listing.Values);

        suppressSelectionEvents = true;
        valuesList.Source = valueSource;
        if (valueSource.Count > 0)
            valuesList.SelectedItem = Math.Clamp(desired, 0, valueSource.Count - 1);
        suppressSelectionEvents = false;

        valuesList.SetNeedsDisplay();
        RefreshFrameTitles();
    }

    /// <summary>Shows counts in the frame titles and marks which pane has focus.</summary>
    private void RefreshFrameTitles()
    {
        keysFrame.Title = keysList.HasFocus ? "Keys ●" : "Keys";
        valuesFrame.Title = valueSource.ShowingNotice
            ? (valuesList.HasFocus ? "Values ●" : "Values")
            : $"Values ({valueSource.Count})" + (valuesList.HasFocus ? " ●" : string.Empty);
    }

    private RegistryValueItem? SelectedValue() =>
        valueSource.ShowingNotice ? null : valueSource.At(valuesList.SelectedItem);

    private void EditSelectedValue()
    {
        if (!valuesList.HasFocus && valueSource.ShowingNotice)
        {
            Dialogs.Error("Nothing to edit", "The selected key has no values to edit.");
            return;
        }

        if (!valuesList.HasFocus)
        {
            valuesList.SetFocus();
            if (valuesList.SelectedItem < 0) valuesList.SelectedItem = 0;
        }

        var value = SelectedValue();
        if (value is null) return;

        if (value.Kind == RegistryValueType.Unknown)
        {
            Dialogs.Error("Cannot edit value",
                "This registry data type is not supported. It is shown read-only to avoid changing its type or data.");
            return;
        }

        var parsed = Dialogs.EditValue(value);
        if (parsed is null) return;

        var selection = valuesList.SelectedItem;
        if (TryWrite(() => registry.SetValue(current, value.Name, parsed, value.Kind), "Cannot save value"))
            LoadValues(selection);
    }

    private void CreateValue()
    {
        var request = Dialogs.NewValue(current);
        if (request is null) return;

        if (TryWrite(() => registry.CreateValue(current, request.Name, request.Value, request.Kind),
                     "Cannot create value"))
            LoadValues(null);
    }

    private void CreateKey()
    {
        var name = Dialogs.NewKeyName(current);
        if (name is null) return;

        if (!TryWrite(() => registry.CreateSubKey(current, name), "Cannot create key")) return;

        var index = tree.IndexOf(current);
        if (index < 0) return;

        tree.Reload(index);
        var created = tree.IndexOf(current.Child(name));
        SyncTree(created >= 0 ? created : index);
    }

    private void DeleteSelected()
    {
        if (keysList.HasFocus) DeleteSelectedKey();
        else DeleteSelectedValue();
    }

    private void DeleteSelectedKey()
    {
        var index = keysList.SelectedItem;
        var node = tree[index];
        if (node is null) return;

        if (node.IsHive)
        {
            Dialogs.Error("Cannot delete", "Root hives cannot be deleted.");
            return;
        }

        var parentIndex = tree.ParentIndex(index);
        if (parentIndex is null) return;
        var parent = tree[parentIndex.Value];
        if (parent is null) return;

        if (!Dialogs.Confirm("Delete Key",
                $"Delete '{node.Name}' and everything under it?", "Delete"))
            return;

        if (!TryWrite(() => registry.DeleteSubKeyTree(parent.Path, node.Name), "Cannot delete key")) return;

        tree.Reload(parentIndex.Value);
        SyncTree(parentIndex.Value);
    }

    private void DeleteSelectedValue()
    {
        var value = SelectedValue();
        if (value is null) return;

        if (value.IsUnset)
        {
            Dialogs.Error("Cannot delete", "The (Default) value is not set.");
            return;
        }

        if (!Dialogs.Confirm("Delete Value", $"Delete value '{value.DisplayName}'?", "Delete")) return;

        var selection = valuesList.SelectedItem;
        if (TryWrite(() => registry.DeleteValue(current, value.Name), "Cannot delete value"))
            LoadValues(selection);
    }

    /// <summary>F2. Renames whichever pane has focus.</summary>
    private void RenameSelected()
    {
        if (keysList.HasFocus) RenameSelectedKey();
        else RenameSelectedValue();
    }

    private void RenameSelectedKey()
    {
        var index = keysList.SelectedItem;
        var node = tree[index];
        if (node is null) return;

        if (node.IsHive)
        {
            Dialogs.Error("Cannot rename", "Hives cannot be renamed.");
            return;
        }

        var parentIndex = tree.ParentIndex(index);
        var parentPath = node.Path.Parent();
        if (parentIndex is null || parentPath is null) return;

        var newName = Dialogs.Rename("key", node.Name, parentPath.Value);
        if (newName is null) return;

        if (!TryWrite(() => registry.RenameSubKey(node.Path, newName), "Cannot rename key")) return;

        tree.Reload(parentIndex.Value);
        var renamed = tree.IndexOf(parentPath.Value.Child(newName));
        SyncTree(renamed >= 0 ? renamed : parentIndex.Value);
        keysList.SetFocus();
    }

    private void RenameSelectedValue()
    {
        var value = SelectedValue();
        if (value is null) return;

        if (value.IsDefault)
        {
            Dialogs.Error("Cannot rename", "The (Default) value cannot be renamed.");
            return;
        }

        var newName = Dialogs.Rename("value", value.Name, current);
        if (newName is null) return;

        if (!TryWrite(() => registry.RenameValue(current, value.Name, newName), "Cannot rename value")) return;

        LoadValues(null);
        SelectValueNamed(newName);
    }

    private void StartSearch()
    {
        var query = SearchDialogs.AskForQuery(lastQuery);
        if (query is null) return;

        lastQuery = query;
        activeSearch = new RegistrySearch(registry, query, current);
        RunSearch(firstAttempt: true);
    }

    /// <summary>F3. Continues the live search, or opens the form if there is not one.</summary>
    private void FindNextMatch()
    {
        if (activeSearch is null)
        {
            StartSearch();
            return;
        }
        RunSearch(firstAttempt: false);
    }

    private void RunSearch(bool firstAttempt)
    {
        var result = SearchDialogs.Run(activeSearch!);

        switch (result.Outcome)
        {
            case SearchOutcome.Found:
                RevealHit(result.Hit!);
                break;

            case SearchOutcome.NotFound:
                activeSearch = null;
                Dialogs.Error("Find",
                    firstAttempt
                        ? $"'{lastQuery!.Text}' was not found."
                        : $"No more matches for '{lastQuery!.Text}'.");
                break;

            case SearchOutcome.Failed:
                activeSearch = null;
                Dialogs.Error("Find", result.Error ?? "The search could not be completed.");
                break;

            case SearchOutcome.Cancelled:
                break;
        }
    }

    private void RevealHit(SearchHit hit)
    {
        var index = tree.Reveal(hit.Path);
        if (index < 0)
        {
            Dialogs.Error("Find", $"Matched in {hit.Path}, but that key is no longer in the tree.");
            return;
        }

        SyncTree(index);                       // also reloads the values pane for the hit

        if (hit.ValueName is null)
        {
            keysList.SetFocus();
            return;
        }

        if (!SelectValueNamed(hit.ValueName)) keysList.SetFocus();
    }

    private bool SelectValueNamed(string name)
    {
        for (var i = 0; i < valueSource.Count; i++)
        {
            if (!string.Equals(valueSource.At(i)?.Name, name, StringComparison.OrdinalIgnoreCase)) continue;

            valuesList.SelectedItem = i;
            valuesList.SetFocus();
            RefreshFrameTitles();
            return true;
        }
        return false;
    }

    /// <summary>Runs a registry write, reporting any failure as a dialog. True if it succeeded.</summary>
    private static bool TryWrite(Action write, string title)
    {
        try
        {
            write();
            return true;
        }
        catch (Exception ex)
        {
            Dialogs.Error(title, RegistryErrors.Describe(ex));
            return false;
        }
    }

    private bool HandleTypeAhead(Key key)
    {
        var code = (int)key;
        if ((key & (Key.CtrlMask | Key.AltMask)) != 0) return false;
        if (code is < 32 or > 126) return false;

        var now = DateTime.UtcNow;
        if ((now - typeAheadLast).TotalMilliseconds > TypeAheadTimeoutMs)
            typeAheadBuffer = string.Empty;
        typeAheadLast = now;
        typeAheadBuffer += (char)code;

        var startAfter = typeAheadBuffer.Length > 1 ? keysList.SelectedItem - 1 : keysList.SelectedItem;
        var found = tree.FindByPrefix(typeAheadBuffer, startAfter);
        if (found >= 0) SyncTree(found);

        return true;
    }
}
